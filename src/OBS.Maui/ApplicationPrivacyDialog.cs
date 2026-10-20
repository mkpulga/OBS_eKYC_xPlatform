using Microsoft.Maui.Controls.Shapes;
using OBS.Core;

namespace OBS.Maui;

// Uses the existing privacy interaction/controller. Protected detail dialogs render
// fresh fields and are erased on hide, revocation, expiry and screen disposal.
internal sealed class ApplicationPrivacyDialog : IDisposable
{
    public PrivacyScreenState State { get; }
    private readonly Action<View?> show;
    private readonly VisualElement owner;
    private Grid? overlay;
    private VerticalStackLayout? details;
    private Entry? otp;
    private Label? message;
    private Button? request, verify;
    private PrivacySection? section;
    private bool disposed, showingOtp;
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? keyRoot;
    private Microsoft.UI.Xaml.Controls.Control? previousFocus;
    private readonly Microsoft.UI.Xaml.Input.KeyEventHandler keyHandler;
#endif
    public ApplicationPrivacyDialog(PrivacyScreenState state, Action<View?> show, VisualElement owner)
    {
        State = state; this.show = show; this.owner = owner;
#if WINDOWS
        keyHandler = (_, args) => { if (args.Key == Windows.System.VirtualKey.Escape && Cancel()) args.Handled = true; };
#endif
        State.Changed += Changed;
    }
    private void Changed() => MainThread.BeginInvokeOnMainThread(() => { if (!disposed) Render(); });
    public void ShowDetails(PrivacySection selected)
    {
        if (disposed || !State.IsVisible(selected)) return;
        Clear(); section = selected; Render();
    }
    public bool Cancel()
    {
        if (disposed || overlay is null) return false;
        section = null;
        if (State.DialogOpen) State.CancelDialog();
        Clear(); return true;
    }
    private static Label Text(string value, double size = 14) { var label = new Label { Text = value, FontSize = size }; label.SetDynamicResource(Label.TextColorProperty, "DashText"); return label; }
    private void Create(VerticalStackLayout body, double width, VisualElement focus)
    {
        overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
        var card = new Border { Content = new ScrollView { Content = body }, Padding = 24, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 21 }, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        card.SetDynamicResource(VisualElement.BackgroundColorProperty, "DashSidebar"); overlay.Add(card);
        overlay.SizeChanged += (_, _) => { if (overlay is not null) { card.WidthRequest = Math.Max(0, Math.Min(width, overlay.Width - 32)); card.MaximumHeightRequest = Math.Max(0, overlay.Height - 32); } };
        overlay.Loaded += (_, _) =>
        {
#if WINDOWS
            if (owner.Window?.Handler?.PlatformView is Microsoft.Maui.MauiWinUIWindow native && native.Content is { XamlRoot: not null } root)
            {
                keyRoot = root; previousFocus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root.XamlRoot) as Microsoft.UI.Xaml.Controls.Control;
                root.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, keyHandler, true);
                if (overlay?.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement scope) scope.TabFocusNavigation = Microsoft.UI.Xaml.Input.KeyboardNavigationMode.Cycle;
            }
#endif
            focus.Focus();
        };
        show(overlay);
    }
    private void Render()
    {
        if (section is { } selected && !State.IsVisible(selected)) { section = null; Clear(); }
        if (State.DialogOpen)
        {
            if (!showingOtp) Clear(); showingOtp = true;
            if (overlay is null)
            {
                var body = new VerticalStackLayout { Spacing = 14 };
                body.Add(Text("Verify protected access", 20)); body.Add(Text("Request a six-digit code sent to your staff email."));
                request = new Button { Text = "Request code" }; request.Clicked += async (_, _) => await State.RequestCodeAsync(); body.Add(request);
                body.Add(Text("Email code", 12));
                otp = new Entry { IsPassword = true, Keyboard = Keyboard.Numeric, MaxLength = 6, ReturnType = ReturnType.Done, Placeholder = "Six-digit email code" };
                SemanticProperties.SetDescription(otp, "Six-digit email code");
                otp.TextChanged += (_, args) => State.SetOtp(args.NewTextValue ?? ""); otp.Completed += async (_, _) => await State.VerifyCodeAsync(); body.Add(otp);
                message = Text(""); body.Add(message);
                verify = new Button { Text = "Verify", BackgroundColor = Color.FromArgb("#485E98"), TextColor = Colors.White };
                verify.Clicked += async (_, _) => await State.VerifyCodeAsync();
                var cancel = new Button { Text = "Cancel" }; cancel.Clicked += (_, _) => { State.CancelDialog(); Clear(); };
                body.Add(new HorizontalStackLayout { Spacing = 12, Children = { verify, cancel } }); Create(body, 440, request);
            }
            request!.IsEnabled = !State.IsBusy; request.Text = State.IsBusy ? "Please wait…" : "Request code";
            verify!.IsEnabled = otp!.IsEnabled = State.CanVerify;
            if (otp.Text != State.Otp) otp.Text = State.Otp;
            message!.Text = State.Message; return;
        }
        if (showingOtp) Clear();
        if (section is { } shown && State.IsVisible(shown))
        {
            if (overlay is null)
            {
                var body = new VerticalStackLayout { Spacing = 14 }; body.Add(Text(PrivacyPresentation.Sections.Single(s => s.Section == shown).Title, 20));
                details = new VerticalStackLayout { Spacing = 12 }; body.Add(details);
                var close = new Button { Text = "Close" }; close.Clicked += (_, _) => { section = null; Clear(); }; body.Add(close); Create(body, 680, close);
            }
            Erase(details); details!.Clear();
            var fields = State.Fields(shown, CitizenSearchPresentation.Missing);
            if (fields.Count == 0) details.Add(Text(CitizenSearchPresentation.Missing));
            foreach (var field in fields) { details.Add(Text(field.Label, 11)); details.Add(Text(field.Value)); }
        }
        else Clear();
    }
    private static void Erase(View? view)
    {
        if (view is Label label) label.Text = "";
        else if (view is Entry entry) entry.Text = "";
        else if (view is Layout layout) foreach (var child in layout.Children.OfType<View>()) Erase(child);
        else if (view is Border { Content: View body }) Erase(body);
        else if (view is ScrollView { Content: View content }) Erase(content);
    }
    private void Clear()
    {
        if (overlay is null) return;
        Erase(overlay); show(null); overlay = null; details = null; otp = null; message = null; request = verify = null; showingOtp = false;
#if WINDOWS
        keyRoot?.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, keyHandler); keyRoot = null;
        previousFocus?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); previousFocus = null;
#endif
    }
    public void Dispose() { if (disposed) return; disposed = true; State.Changed -= Changed; section = null; Clear(); }
}
