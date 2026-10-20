using OBS.Core;

namespace OBS.Maui;

// Layout and styles are in MainPage.xaml. This file selects the visible screen and forwards actions.
public sealed partial class MainPage : ContentPage
{
    private readonly SessionController session;
    private readonly ClientStatus status;
    private readonly CancellationTokenSource stop = new();
    private bool busy, forgot;
    private SignInStage renderedStage = (SignInStage)(-1);
    private bool renderedProtected;
#if WINDOWS
    private readonly Controls.BiometricCaptureOverlay biometricCapture;
#endif

    public MainPage(SessionController session, ClientStatus status, ThemeOptions theme, ICardholderVerification cardholders, IApplicationRecords applications, PrivacyWorkspace privacy)
    {
        InitializeComponent();
        this.session = session;
        this.status = status;
        Resources["ThemeBackground"] = Color.FromArgb(theme.Background);
        Resources["ThemeNavigation"] = Color.FromArgb(theme.Navigation);
        Resources["ThemeAction"] = Color.FromArgb(theme.Action);
        Resources["ThemeText"] = Color.FromArgb(theme.Text);
        ProductNameLabel.Text = theme.ProductName;
        CityNameLabel.Text = theme.CityName;
        DashboardPanel.Configure(theme, session, cardholders, applications, privacy);
        DashboardPanel.LogoutRequested += OnLogoutClicked;
        DashboardPanel.PasswordChangeRequested += OnOpenPasswordChangeClicked;
#if WINDOWS
        biometricCapture = new Controls.BiometricCaptureOverlay();
        biometricCapture.Resources = DashboardPanel.Resources;
        RootGrid.Add(biometricCapture);
        DashboardPanel.BiometricCaptureRequested += OnBiometricCaptureRequested;
#endif

        session.Changed += Update;
        session.RefreshChecked += TraceRefresh;
        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (stop.IsCancellationRequested) return false;
            UpdateResendButton();
            if (DashboardPanel.IsVisible) DashboardPanel.RefreshClock();
            return true;
        });
        _ = RunChecks();
        Render();
    }

    private async Task RunChecks()
    {
        try { await session.RunChecksAsync(stop.Token); }
        catch (OperationCanceledException) { }
    }

    private static void TraceRefresh(RefreshDiagnostic diagnostic)
    {
        System.Diagnostics.Debug.WriteLine($"OBS session refresh: HTTP={diagnostic.HttpStatus?.ToString() ?? "none"}, business={diagnostic.BusinessCode?.ToString() ?? "none"}, failure={diagnostic.Failure}, reason={diagnostic.Reason}, dataKind={diagnostic.Shape?.Data}, itemKind={diagnostic.Shape?.FirstItem}, accessKind={diagnostic.Shape?.AccessToken}, tokenKind={diagnostic.Shape?.TokenContainer}");
    }

    private void Update() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (stop.IsCancellationRequested) return;
        if (session.Stage != renderedStage || session.ProtectedWorkAllowed != renderedProtected) Render();
        UpdateMessage();
    });

    private void Render()
    {
#if WINDOWS
        if (!session.ProtectedWorkAllowed) biometricCapture.Dismiss();
#endif
        ClearFields();
        if (session.Current is null) DashboardPanel.SetStaff(null);
        renderedStage = session.Stage;
        renderedProtected = session.ProtectedWorkAllowed;

        foreach (var panel in ScreenPanels()) panel.IsVisible = false;
        AuthenticationScroll.IsVisible = true;
        ConfigurationMessageLabel.Text = "";
        if (status.ConfigurationError is not null || !session.ContractAvailable)
        {
            ConfigurationPanel.IsVisible = true;
            ConfigurationMessageLabel.Text = status.ConfigurationError ?? "This API contract is unavailable. Configure Refactored.";
        }
        else
        {
            switch (session.Stage)
            {
                case SignInStage.Login:
                    LoginPanel.IsVisible = !forgot;
                    ForgotPasswordPanel.IsVisible = forgot;
                    break;
                case SignInStage.Otp:
                    OtpPanel.IsVisible = true;
                    break;
                case SignInStage.PasswordChange:
                    PasswordChangePanel.IsVisible = true;
                    break;
                case SignInStage.Protected when !session.ProtectedWorkAllowed:
                    SessionPausedPanel.IsVisible = true;
                    break;
                case SignInStage.Protected when session.Current is { } staff:
                    DashboardPanel.IsVisible = true;
                    AuthenticationScroll.IsVisible = false;
                    DashboardPanel.SetStaff(staff);
                    break;
            }
        }
        UpdateResendButton();
        UpdateMessage();
    }

    private View[] ScreenPanels() => [ConfigurationPanel, LoginPanel, ForgotPasswordPanel, OtpPanel,
        PasswordChangePanel, SessionPausedPanel, DashboardPanel];

    private void UpdateResendButton()
    {
        var seconds = session.ResendSeconds;
        ResendButton.Text = seconds > 0 ? $"Resend OTP ({seconds}s)" : "Resend OTP";
        ResendButton.IsEnabled = !busy && session.Stage == SignInStage.Otp && seconds == 0;
    }

    private void UpdateMessage()
    {
        MessageLabel.Text = session.Message;
        MessageLabel.IsVisible = !string.IsNullOrEmpty(MessageLabel.Text);
        DashboardPanel.SetSessionMessage(session.Message);
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (busy || stop.IsCancellationRequested) return;
        busy = true;
        foreach (var panel in ScreenPanels()) panel.IsEnabled = false;
        BusyIndicator.IsVisible = BusyIndicator.IsRunning = true;
        try { await action(); }
        finally
        {
            busy = false;
            foreach (var panel in ScreenPanels()) panel.IsEnabled = true;
            BusyIndicator.IsVisible = BusyIndicator.IsRunning = false;
            ClearSecrets();
            UpdateResendButton();
            Update();
        }
    }

    private async void OnSignInClicked(object? sender, EventArgs e) =>
        await ExecuteAsync(() => session.SignInAsync(LoginEmailEntry.Text ?? "", LoginPasswordEntry.Text ?? ""));
    private async void OnRecoveryClicked(object? sender, EventArgs e) =>
        await ExecuteAsync(() => session.ForgotPasswordAsync(RecoveryEmailEntry.Text ?? ""));
    private async void OnVerifyClicked(object? sender, EventArgs e) =>
        await ExecuteAsync(() => session.VerifyAsync(OtpEntry.Text ?? ""));
    private async void OnResendClicked(object? sender, EventArgs e) => await ExecuteAsync(session.ResendAsync);
    private async void OnChangePasswordClicked(object? sender, EventArgs e) =>
        await ExecuteAsync(() => session.ChangePasswordAsync(CurrentPasswordEntry.Text ?? "", NewPasswordEntry.Text ?? "", ConfirmationEntry.Text ?? ""));

    private void OnForgotPasswordClicked(object? sender, EventArgs e)
    {
        if (busy || session.Stage != SignInStage.Login) return;
        forgot = true;
        Render();
    }

    private void OnBackToLoginClicked(object? sender, EventArgs e)
    {
        if (busy) return;
        forgot = false;
        session.BackToLogin();
        Render();
    }

    private void OnOpenPasswordChangeClicked(object? sender, EventArgs e)
    {
        if (!busy && session.ProtectedWorkAllowed) session.BeginPasswordChange();
    }

    private async void OnLogoutClicked(object? sender, EventArgs e) => await ExecuteAsync(async () =>
    {
        if (await DisplayAlert("Log out", "Are you sure you want to log out?", "Yes", "No")) await session.LogoutAsync();
    });

    private void ClearSecrets()
    {
        LoginPasswordEntry.Text = OtpEntry.Text = CurrentPasswordEntry.Text = NewPasswordEntry.Text = ConfirmationEntry.Text = "";
    }

    private void ClearFields()
    {
        ClearSecrets();
        LoginEmailEntry.Text = RecoveryEmailEntry.Text = "";
    }

    public void Shutdown()
    {
#if WINDOWS
        biometricCapture.Dismiss();
        DashboardPanel.BiometricCaptureRequested -= OnBiometricCaptureRequested;
#endif
        stop.Cancel();
        session.Changed -= Update;
        session.RefreshChecked -= TraceRefresh;
        ClearFields();
        MessageLabel.Text = "";
        DashboardPanel.SetStaff(null);
        DashboardPanel.LogoutRequested -= OnLogoutClicked;
        DashboardPanel.PasswordChangeRequested -= OnOpenPasswordChangeClicked;
        foreach (var panel in ScreenPanels()) panel.IsVisible = false;
    }
    protected override bool OnBackButtonPressed() => DashboardPanel.CancelPrivacyDialog() || base.OnBackButtonPressed();
#if WINDOWS
    private void OnBiometricCaptureRequested()
    {
        if (session.ProtectedWorkAllowed && DashboardPanel.IsVisible) biometricCapture.Show(this);
    }
#endif
}
