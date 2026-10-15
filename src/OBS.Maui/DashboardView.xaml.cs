using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using OBS.Core;
using OBS.Maui.Controls;

namespace OBS.Maui;

public partial class DashboardView : ContentView
{
    private readonly DashboardActivityDrawable activity = new();
    private readonly DashboardReleaseDrawable release = new();
    private readonly List<Label> navigationLabels = [];
    private readonly List<Label> navigationHeadings = [];
    private readonly List<(Button Button, NavigationEntry? Entry)> navigationButtons = [];
    private IReadOnlyList<NavigationEntry> modules = [];
    private NavigationEntry? selected;
    private bool dark, collapsed, mobileOpen, canChangePassword;
    private string sessionMessage = "";
    private SessionController? session;
    private ICardholderVerification cardholders = new UnavailableCardholderVerification();
    private CancellationTokenSource? cardholderLoad;
    public event EventHandler? LogoutRequested;
    public event EventHandler? PasswordChangeRequested;

    public DashboardView()
    {
        InitializeComponent();
        SearchType.SelectedIndex = 0;
        SearchType.HandlerChanged += OnSearchHandlerChanged;
        SearchEntry.HandlerChanged += OnSearchHandlerChanged;
        HandlerChanged += OnSearchHandlerChanged;
        ActivityChart.Drawable = activity;
        ReleaseChart.Drawable = release;
        ReleasePercentLabel.Text = $"{DashboardSample.ReleasePercent}%";
        SemanticProperties.SetDescription(ReleaseChart, $"Sample release progress: {DashboardSample.ReleasePercent}% of requested cards.");
        foreach (var metric in DashboardSample.Portfolio) PortfolioGrid.Add(CreateMetric(metric), PortfolioGrid.Children.Count);
        foreach (var metric in DashboardSample.Queues) QueueCards.Add(CreateQueue(metric));
        foreach (var period in DashboardSample.Periods)
        {
            var button = new Button { Text = DashboardSample.Label(period), CommandParameter = period, FontSize = 10, MinimumHeightRequest = 28, HeightRequest = 28, Padding = new Thickness(10, 0), CornerRadius = 16 };
            SemanticProperties.SetDescription(button, DashboardSample.Description(period));
            button.Clicked += OnPeriodClicked;
            PeriodButtons.Add(button);
        }
        UpdatePeriod();
        RefreshClock();
    }

    public void Configure(ThemeOptions theme, SessionController session, ICardholderVerification cardholders)
    {
        this.session = session;
        this.cardholders = cardholders;
        ToolbarCityLabel.Text = theme.CityName;
        SubtitleLabel.Text = $"{theme.CityName} · Sample data";
    }

    public void SetStaff(StaffSession? staff)
    {
        OperatorLabel.Text = staff?.Email ?? "";
        InitialsLabel.Text = DashboardPresentation.Initials(staff?.Email ?? "");
        modules = staff is null ? [] : OBS.Core.Navigation.For(staff).ToArray();
        canChangePassword = staff?.Allows("CPW") == true;
        if (staff is null)
        {
            ClearCardholders();
            selected = null;
            SearchEntry.Text = "";
            NoticeCard.IsVisible = false;
            sessionMessage = "";
        }
        if (selected is not null && !modules.Contains(selected)) selected = null;
        RebuildNavigation();
        ShowSelectedPage();
    }

    public void SetSessionMessage(string message)
    {
        if (message == sessionMessage) return;
        sessionMessage = message;
        NoticeLabel.Text = message;
        NoticeCard.IsVisible = !string.IsNullOrEmpty(message);
    }

    public void RefreshClock()
    {
        var now = DashboardPresentation.LocalTime;
        DateLabel.Text = now.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);
        TimeLabel.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private void OnSearchHandlerChanged(object? sender, EventArgs e)
    {
#if WINDOWS
        // Keep Windows' native fields inside the shared rounded search surface.
        if (SearchType.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ComboBox picker)
        {
            picker.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            picker.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
        if (SearchEntry.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox entry)
            entry.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
        if (Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement root)
            root.RequestedTheme = dark ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light;
#endif
    }

    private static (Color Foreground, Color Background) Accent(string accent) => accent switch
    {
        "mint" => (Color.FromArgb("#16CFA8"), Color.FromArgb("#2039D6B7")),
        "blue" => (Color.FromArgb("#579AFF"), Color.FromArgb("#20579AFF")),
        "orange" => (Color.FromArgb("#FF9058"), Color.FromArgb("#20FF9058")),
        _ => (Color.FromArgb("#8C55FF"), Color.FromArgb("#268C55FF"))
    };
    private static Border IconTile(DashboardMetric metric, int size = 38)
    {
        var (foreground, background) = Accent(metric.Accent);
        return new Border { WidthRequest = size, HeightRequest = size, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 11 }, BackgroundColor = background,
            Content = new DashboardIcon { Kind = metric.Icon, Tint = foreground, WidthRequest = 18, HeightRequest = 18, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } };
    }
    private Label Caption(string text) => new() { Text = text, Style = (Style)Resources["Caption"] };
    private Border CreateMetric(DashboardMetric metric)
    {
        var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(38), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 12 };
        grid.Add(IconTile(metric));
        var labels = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        labels.Add(Caption(metric.Label));
        labels.Add(new Label { Text = DashboardSample.Number(metric.Value), FontSize = 24, FontAttributes = FontAttributes.Bold });
        grid.Add(labels, 1);
        var card = new Border { StrokeShape = new RoundRectangle { CornerRadius = 17 }, Padding = new Thickness(13, 14), Content = grid };
        card.SetDynamicResource(Border.StrokeProperty, "DashLine");
        return card;
    }
    private Border CreateQueue(DashboardMetric metric)
    {
        var title = new Grid { ColumnDefinitions = [new ColumnDefinition(34), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 10 };
        title.Add(IconTile(metric, 34));
        title.Add(new Label { Text = metric.Label, FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, 1);
        var content = new VerticalStackLayout { Spacing = 14 };
        content.Add(title);
        content.Add(new Label { Text = DashboardSample.Number(metric.Value), FontSize = 29, FontAttributes = FontAttributes.Bold });
        return new Border { Style = (Style)Resources["Card"], Padding = 15, Content = content, MinimumHeightRequest = 122 };
    }
    private void RebuildNavigation()
    {
        ModuleList.Clear(); SidebarActions.Clear(); navigationLabels.Clear(); navigationHeadings.Clear(); navigationButtons.Clear();
        AddHeading("OVERVIEW");
        ModuleList.Add(NavigationRow("Dashboard", "dashboard", OnOverview));
        foreach (var group in modules.Select(DashboardPresentation.Module).GroupBy(item => item.Group))
        {
            AddHeading(group.Key);
            foreach (var module in group)
            {
                var row = NavigationRow(module.Label, module.Icon, (_, _) =>
                {
                    if (!modules.Contains(module.Entry)) return;
                    selected = module.Entry; mobileOpen = false; ShowSelectedPage(); ApplyLayout();
                    if (CardholderVerificationPresentation.IsQueue(selected)) _ = LoadCardholdersAsync();
                }, module.Entry, group.Key == "RECORDS" ? "#64B276" : "#519CFF");
                ModuleList.Add(row);
            }
        }
        if (canChangePassword) SidebarActions.Add(NavigationRow("Change password", "lock", (_, _) => PasswordChangeRequested?.Invoke(this, EventArgs.Empty), compact: true));
        SidebarActions.Add(NavigationRow("Log out", "logout", (_, _) => LogoutRequested?.Invoke(this, EventArgs.Empty), compact: true));
        SidebarActions.Add(NavigationRow(dark ? "Light mode" : "Dark mode", dark ? "sun" : "moon", OnToggleTheme, compact: true));
        ApplyLayout();
    }
    private void AddHeading(string title)
    {
        var label = Caption(title);
        label.CharacterSpacing = .9;
        label.Margin = new Thickness(16, 22, 0, 8);
        navigationHeadings.Add(label);
        ModuleList.Add(label);
    }
    private Grid NavigationRow(string text, string icon, EventHandler action, NavigationEntry? entry = null, string? tint = null, bool compact = false)
    {
        var grid = new Grid { HeightRequest = compact ? 38 : 46 };
        var button = new Button { CornerRadius = 24, Padding = 0, MinimumHeightRequest = compact ? 38 : 46 };
        SemanticProperties.SetDescription(button, text);
        ToolTipProperties.SetText(button, text);
        button.Clicked += action;
        grid.Add(button);
        var content = new Grid { ColumnDefinitions = [new ColumnDefinition(20), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 16, Padding = new Thickness(14, 0), InputTransparent = true };
        var glyph = new DashboardIcon { Kind = icon, VerticalOptions = LayoutOptions.Center };
        if (tint is not null) glyph.Tint = Color.FromArgb(tint); else glyph.SetDynamicResource(DashboardIcon.TintProperty, "DashMuted");
        content.Add(glyph);
        var label = new Label { Text = text, FontSize = compact ? 12 : 16, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
        content.Add(label, 1);
        navigationLabels.Add(label);
        grid.Add(content);
        if (!compact) navigationButtons.Add((button, entry));
        return grid;
    }
    private void ShowSelectedPage()
    {
        DashboardColumns.IsVisible = selected is null;
        WorkflowCard.IsVisible = selected is not null;
        WorkflowTitle.Text = selected?.Title ?? "";
        CardholderPanel.IsVisible = CardholderVerificationPresentation.IsQueue(selected);
        WorkflowPlaceholder.IsVisible = !CardholderPanel.IsVisible;
        foreach (var (button, entry) in navigationButtons)
            if (entry == selected) button.SetDynamicResource(BackgroundColorProperty, "DashSelected");
            else { button.RemoveDynamicResource(BackgroundColorProperty); button.BackgroundColor = Colors.Transparent; }
    }
    private void OnRefreshCardholders(object? sender, EventArgs e) => _ = LoadCardholdersAsync();
    private async Task LoadCardholdersAsync()
    {
        if (session?.Current is not { } staff || !session.ProtectedWorkAllowed) return;
        cardholderLoad?.Cancel();
        var current = cardholderLoad = new CancellationTokenSource();
        SetCardholderLoading(true);
        try
        {
            var reply = await cardholders.GetPendingAsync(staff, current.Token);
            // Ignore replies superseded by a newer refresh or issued for a session that has since ended.
            // Compare by email: a token refresh replaces the StaffSession instance for the same user.
            if (current.IsCancellationRequested || session.Current?.Email != staff.Email || !session.ProtectedWorkAllowed) return;
            ShowCardholders(reply);
        }
        finally { if (cardholderLoad == current) SetCardholderLoading(false); }
    }
    private void SetCardholderLoading(bool loading)
    {
        CardholderLoading.IsRunning = CardholderLoading.IsVisible = loading;
        CardholderRefresh.IsEnabled = !loading;
        CardholderRefresh.Text = loading ? "Loading…" : "Refresh";
        if (loading && CardholderRows.Children.Count == 0) { CardholderStatus.Text = "Loading pending cardholders…"; CardholderStatus.IsVisible = true; }
    }
    private void ShowCardholders(PendingReply reply)
    {
        CardholderRows.Clear();
        var status = CardholderVerificationPresentation.Status(reply);
        CardholderStatus.Text = status ?? "";
        CardholderStatus.IsVisible = status is not null;
        CardholderSummary.Text = "Pending card holder verification, oldest first" +
            (reply.Outcome == PendingOutcome.Success ? $" · {CardholderVerificationPresentation.Count(reply)}" : "");
        if (reply.Outcome != PendingOutcome.Success) return;
        foreach (var item in reply.Items) CardholderRows.Add(CreateCardholderRow(item));
    }
    private void ClearCardholders()
    {
        cardholderLoad?.Cancel(); cardholderLoad = null;
        CardholderRows.Clear();
        CardholderStatus.Text = ""; CardholderStatus.IsVisible = false;
        CardholderSummary.Text = "Pending card holder verification, oldest first";
        SetCardholderLoading(false);
    }
    // One card per cardholder: readable at phone width and on desktop without a wide table.
    private Border CreateCardholderRow(PendingCardholder item)
    {
        Label Caption(string text) => new() { Text = text, Style = (Style)Resources["Caption"] };
        var name = new Label { Text = $"{item.No}. {CardholderVerificationPresentation.Name(item)}", FontAttributes = FontAttributes.Bold };
        var status = new Label { Text = CardholderVerificationPresentation.Text(item.status), FontSize = 12, HorizontalOptions = LayoutOptions.End };
        var header = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        header.Add(name); header.Add(status, 1);
        var content = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                header,
                Caption($"Ref. {CardholderVerificationPresentation.Text(item.referencenumber)} · Mobile {CardholderVerificationPresentation.Text(item.msisdn)}"),
                Caption(CardholderVerificationPresentation.Text(CardholderVerificationPresentation.Address(item))),
                Caption($"Created {CardholderVerificationPresentation.Text(item.datecreated)}")
            }
        };
        var row = new Border { Content = content, Padding = new Thickness(14, 10), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 14 } };
        row.SetDynamicResource(BackgroundColorProperty, "DashSidebar");
        row.SetDynamicResource(Border.StrokeProperty, "DashLine");
        SemanticProperties.SetDescription(row, $"{name.Text}, status {status.Text}");
        return row;
    }
    private void OnOverview(object? sender, EventArgs e)
    {
        selected = null; mobileOpen = false; NoticeCard.IsVisible = false; ShowSelectedPage(); ApplyLayout();
    }
    private void OnToggleSidebar(object? sender, EventArgs e)
    {
        if (Width < 900) mobileOpen = !mobileOpen; else collapsed = !collapsed;
        ApplyLayout();
    }
    private void OnDashboardSizeChanged(object? sender, EventArgs e) => ApplyLayout();
    private void ApplyLayout()
    {
        if (Width <= 0) return;
        var compact = Width < 900 || collapsed;
        var expanded = !compact || mobileOpen;
        SidebarColumn.Width = compact ? 64 : 280;
        Sidebar.Padding = compact && !mobileOpen ? new Thickness(8) : new Thickness(12, 8, 12, 12);
        Grid.SetColumnSpan(Sidebar, compact && mobileOpen ? 2 : 1);
        Sidebar.WidthRequest = compact && mobileOpen ? 260 : -1;
        Sidebar.HorizontalOptions = compact && mobileOpen ? LayoutOptions.Start : LayoutOptions.Fill;
        ProfileCard.IsVisible = expanded;
        foreach (var label in navigationLabels) label.IsVisible = expanded;
        foreach (var heading in navigationHeadings) heading.IsVisible = expanded;
        var available = Width - (compact ? 64 : 280);
        var mobile = available < 600;
        var padding = mobile ? 12 : 36;
        MainContent.Padding = new Thickness(padding, 0, padding, 12);
        Toolbar.Padding = new Thickness(padding, 12, padding, 20);
        DateBlock.IsVisible = available > 1050;
        Toolbar.ColumnDefinitions[0].Width = DateBlock.IsVisible ? 184 : 0;
        Toolbar.ColumnSpacing = mobile ? 0 : 16;
        Toolbar.RowDefinitions.Clear();
        Toolbar.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Toolbar.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(SearchBorder, mobile ? 1 : 0);
        Grid.SetColumn(SearchBorder, mobile ? 0 : 1);
        Grid.SetColumnSpan(SearchBorder, mobile ? 3 : 1);
        SearchBorder.Margin = mobile ? new Thickness(0, 6, 0, 0) : new Thickness(0);
        var stacked = available < 1000;
        DashboardColumns.ColumnDefinitions[1].Width = stacked ? 0 : 286;
        DashboardColumns.RowDefinitions.Clear();
        DashboardColumns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        DashboardColumns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetColumn(SecondaryColumn, stacked ? 0 : 1);
        Grid.SetRow(SecondaryColumn, stacked ? 1 : 0);
        var stackMetrics = available < 700;
        PortfolioGrid.ColumnDefinitions.Clear();
        PortfolioGrid.RowDefinitions.Clear();
        for (var i = 0; i < (stackMetrics ? 1 : 3); i++) PortfolioGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < (stackMetrics ? 3 : 1); i++) PortfolioGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var i = 0; i < PortfolioGrid.Children.Count; i++) { Grid.SetColumn((BindableObject)PortfolioGrid.Children[i], stackMetrics ? 0 : i); Grid.SetRow((BindableObject)PortfolioGrid.Children[i], stackMetrics ? i : 0); }
        ActivityHeading.RowDefinitions.Clear();
        ActivityHeading.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ActivityHeading.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(PeriodBorder, mobile ? 1 : 0);
        Grid.SetColumn(PeriodBorder, mobile ? 0 : 1);
        PeriodBorder.HorizontalOptions = mobile ? LayoutOptions.Start : LayoutOptions.End;
        ActivityChart.HeightRequest = mobile ? 290 : Math.Max(360, Height - 475);
    }
    private void OnPeriodClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: DashboardPeriod period }) { activity.Period = period; UpdatePeriod(); }
    }
    private void UpdatePeriod()
    {
        foreach (var button in PeriodButtons.Children.OfType<Button>())
        {
            var active = Equals(button.CommandParameter, activity.Period);
            button.BackgroundColor = active ? Color.FromArgb("#485E98") : Colors.Transparent;
            button.TextColor = active ? Colors.White : (Color)Resources["DashMuted"];
            SemanticProperties.SetDescription(button, $"{DashboardSample.Description((DashboardPeriod)button.CommandParameter)}{(active ? ", selected" : "")}");
        }
        SemanticProperties.SetDescription(ActivityChart, $"Sample activity, {DashboardSample.Description(activity.Period)}. " + string.Join(". ", DashboardSample.Activity(activity.Period).Select(item => $"{item.Label}: {item.Completed} completed, {item.InProgress} in progress")));
        ActivityChart.Invalidate();
    }
    private void OnToggleTheme(object? sender, EventArgs e)
    {
        dark = !dark;
        Resources["DashBackground"] = Color.FromArgb(dark ? "#191B2A" : "#F4F3FB");
        Resources["DashSidebar"] = Color.FromArgb(dark ? "#202233" : "#F8F7FD");
        Resources["DashPanel"] = Color.FromArgb(dark ? "#EB222537" : "#48FFFFFF");
        Resources["DashLine"] = Color.FromArgb(dark ? "#36394F" : "#B8FFFFFF");
        Resources["DashText"] = Color.FromArgb(dark ? "#F1F0F8" : "#202127");
        Resources["DashMuted"] = Color.FromArgb(dark ? "#B9BDCE" : "#51535F");
        Resources["DashSelected"] = Color.FromArgb(dark ? "#343E61" : "#DCE1FA");
        Resources["DashPill"] = Color.FromArgb(dark ? "#303349" : "#E6E5EE");
        HeaderGradient.Opacity = dark ? .22 : 1;
        ThemeIcon.Kind = dark ? "sun" : "moon";
        OnSearchHandlerChanged(this, EventArgs.Empty);
        SemanticProperties.SetDescription(ThemeButton, dark ? "Switch to light mode" : "Switch to dark mode");
        activity.Dark = release.Dark = dark;
        UpdatePeriod(); ReleaseChart.Invalidate();
        RebuildNavigation(); ShowSelectedPage();
    }
    private void Notice(string message) { NoticeLabel.Text = string.IsNullOrEmpty(sessionMessage) ? message : sessionMessage; NoticeCard.IsVisible = true; }
    private void OnDismissNotice(object? sender, EventArgs e) { if (string.IsNullOrEmpty(sessionMessage)) NoticeCard.IsVisible = false; }
    private void OnSearch(object? sender, EventArgs e) => Notice(string.IsNullOrWhiteSpace(SearchEntry.Text) ? "Enter a value to search." : "Record search is not available yet. This dashboard displays sample data.");
    private void OnScan(object? sender, EventArgs e) => Notice("QR scanning is not available on this dashboard yet.");
    private void OnNotifications(object? sender, EventArgs e) => Notice("No notifications to display. This dashboard displays sample data.");
}
