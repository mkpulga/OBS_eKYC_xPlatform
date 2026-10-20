using Microsoft.Maui.Controls.Shapes;
using OBS.Core;

namespace OBS.Maui.Controls;

#if WINDOWS
public enum BiometricFinger { R1, R2, R3, R4, R5, L1, L2, L3, L4, L5 }

// Live preview stays in memory and never marks a finger as captured.
public sealed class BiometricCaptureOverlay : ContentView
{
    private static readonly Color Accent = Color.FromArgb("#485E98");
    private readonly Dictionary<BiometricFinger, byte[]> captures = [];
    private readonly Dictionary<BiometricFinger, (Border Border, Image Image, Label Label, Border Badge, Button Button)> tiles = [];
    private readonly Grid columns = new() { ColumnSpacing = 20, RowSpacing = 20 };
    private readonly Label heading = Text("Right Thumb", 16, true);
    private readonly Label status = Text("Select a finger, then press Start.", 13, muted: true);
    private readonly Label service = Text("Live preview only — no validation or saving.", 11, muted: true);
    private readonly Image preview = new() { Aspect = Aspect.AspectFit, IsVisible = false };
    private readonly DashboardIcon placeholder = Icon("fingerprint", 48, muted: true);
    private readonly Button start, stop, reset, close;
    private readonly Border card;
    private readonly ScrollView scroll;
    private readonly FingerprintScanSession scan;
    private FingerprintFrame? displayedFrame;
    private int updatePending;
    private bool scanBelongsToOpen, livePreview, failureShown;
    private BiometricFinger selectedFinger;
    private bool closing;
    private Page? owner;
    private Microsoft.UI.Xaml.UIElement? keyRoot;
    private Microsoft.UI.Xaml.Controls.Control? previousFocus;
    private readonly Microsoft.UI.Xaml.Input.KeyEventHandler keyHandler;

    public BiometricFinger SelectedFinger => selectedFinger;
    public bool HasCaptures => captures.Count > 0;
    public bool StartEnabled { get => start.IsEnabled; set => start.IsEnabled = value; }
    public bool StopEnabled { get => stop.IsEnabled; set => stop.IsEnabled = value; }
    public bool ResetEnabled { get => reset.IsEnabled; set => reset.IsEnabled = value; }
    public string StatusMessage { get => status.Text; set => status.Text = value; }
    public string ServiceMessage { get => service.Text; set => service.Text = value; }

    public BiometricCaptureOverlay(IFingerprintScanner? scanner = null)
    {
        IsVisible = false; ZIndex = 100;
        scan = new FingerprintScanSession(scanner ?? new Platforms.Windows.FutronicScanner());
        scan.Changed += QueueScanUpdate;
        keyHandler = OnNativeKeyDown;
        var root = new Grid();
        var backdrop = new BoxView { Color = Color.FromArgb("#70000000") };
        var dismiss = new TapGestureRecognizer(); dismiss.Tapped += async (_, _) => await RequestCloseAsync(); backdrop.GestureRecognizers.Add(dismiss);
        root.Add(backdrop);

        var header = new Grid { ColumnDefinitions = [new(GridLength.Star), new(32)], ColumnSpacing = 12 };
        var title = Text("Biometric capture", 20, true); title.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1); header.Add(title);
        close = Button("×", "Close biometric capture"); close.FontSize = 24; close.Padding = 0;
        close.SetDynamicResource(Microsoft.Maui.Controls.Button.TextColorProperty, "DashMuted");
        close.Clicked += async (_, _) => await RequestCloseAsync(); header.Add(close, 1);

        var hands = new VerticalStackLayout { Spacing = 20 };
        hands.Add(Hand("Right hand", [BiometricFinger.R1, BiometricFinger.R2, BiometricFinger.R3, BiometricFinger.R4, BiometricFinger.R5]));
        hands.Add(Hand("Left hand", [BiometricFinger.L1, BiometricFinger.L2, BiometricFinger.L3, BiometricFinger.L4, BiometricFinger.L5]));
        columns.Add(hands);

        var details = new VerticalStackLayout { Spacing = 6 };
        details.Add(heading); details.Add(status); details.Add(service);
        var picture = new Grid(); picture.Add(placeholder); picture.Add(preview);
        var box = Card(picture, 10); box.WidthRequest = 165; box.HeightRequest = 195;
        box.HorizontalOptions = LayoutOptions.Center; box.Margin = new Thickness(0, 6, 0, 6);
        box.SetDynamicResource(BackgroundColorProperty, "DashPill"); details.Add(box);
        var buttons = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
        buttons.Add(ActionButton("Start", "capture", out start, true));
        buttons.Add(ActionButton("Stop", "stop", out stop, muted: true), 1);
        buttons.Add(ActionButton("Reset", "reset", out reset), 2);
        start.Clicked += async (_, _) => await OnStartAsync(); stop.Clicked += async (_, _) => await OnStop(); reset.Clicked += (_, _) => OnReset();
        StopEnabled = false; details.Add(buttons); columns.Add(details, 1);

        scroll = new ScrollView { Content = columns, Orientation = ScrollOrientation.Vertical };
        var content = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], RowSpacing = 16, Padding = 20 };
        content.SetDynamicResource(BackgroundColorProperty, "DashPanel"); content.Add(header); content.Add(scroll, 0, 1);
        card = Card(content, 16); card.WidthRequest = 920;
        card.HorizontalOptions = card.VerticalOptions = LayoutOptions.Center;
        // DashPanel is translucent in the dashboard; give it an opaque themed base here.
        card.SetDynamicResource(BackgroundColorProperty, "DashBackground");
        card.Shadow = new Shadow { Brush = Colors.Black, Opacity = .16f, Offset = new Point(0, 6), Radius = 18 };
        root.Add(card); Content = root;
        SizeChanged += (_, _) => ArrangeColumns();
        Loaded += (_, _) => { if (IsVisible) HookKeyboard(); };
        Unloaded += (_, _) => Dismiss();
        Select(BiometricFinger.R1); ArrangeColumns();
    }

    public void Show(Page page)
    {
        if (IsVisible) return;
        owner = page; closing = false; scanBelongsToOpen = livePreview = failureShown = false; displayedFrame = null;
        ClearAll(); Select(BiometricFinger.R1);
        StatusMessage = "Select a finger, then press Start.";
        ServiceMessage = "Live preview only — no validation or saving.";
        StartEnabled = !scan.Snapshot.IsRunning; ResetEnabled = true; StopEnabled = false;
        IsVisible = true; ArrangeColumns(); HookKeyboard();
        Dispatcher.Dispatch(() => { if (IsVisible) close.Focus(); });
    }

    public async Task RequestCloseAsync()
    {
        if (!IsVisible || closing) return;
        closing = true;
        try
        {
            if (HasCaptures && (owner is null || !await owner.DisplayAlert("Discard captured fingerprints?", "The captured fingerprints will be discarded. Close biometric capture?", "Yes", "No"))) return;
            Dismiss();
            await scan.StopAsync();
        }
        finally { closing = false; if (IsVisible) QueueScanUpdate(); }
    }

    // Used when the owning page is shut down or protected work is no longer available.
    public void Dismiss()
    {
        var wasVisible = IsVisible;
        scanBelongsToOpen = livePreview = false; displayedFrame = null;
        // Request cancellation now; only the worker closes the device, after its native call returns.
        _ = scan.StopAsync(); scan.ClearFrame();
        IsVisible = false; UnhookKeyboard(); ClearAll(); owner = null;
        if (wasVisible) previousFocus?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        previousFocus = null;
    }

    public void SetCaptured(BiometricFinger finger, byte[] image)
    {
        Validate(finger); ArgumentNullException.ThrowIfNull(image);
        if (image.Length == 0) throw new ArgumentException("A captured image cannot be empty.", nameof(image));
        captures[finger] = image.ToArray(); Refresh(finger);
    }
    public void ClearCaptured(BiometricFinger finger) { Validate(finger); captures.Remove(finger); Refresh(finger); }
    public void ClearAll() { captures.Clear(); foreach (var finger in tiles.Keys) Refresh(finger); }

    private View Hand(string title, BiometricFinger[] fingers)
    {
        var stack = new VerticalStackLayout { Spacing = 6 }; stack.Add(Text(title, 12, muted: true));
        var row = new Grid { ColumnSpacing = 8 };
        foreach (var finger in fingers)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var image = new Image { Aspect = Aspect.AspectFill, IsVisible = false };
            var label = Text(finger.ToString(), 14, muted: true); label.HorizontalOptions = label.VerticalOptions = LayoutOptions.Center;
            var check = Text("✓", 10, true); check.TextColor = Colors.White; check.HorizontalTextAlignment = TextAlignment.Center;
            var badge = Card(check, 9); badge.BackgroundColor = Accent; badge.StrokeThickness = 0; badge.Padding = 1;
            badge.WidthRequest = badge.HeightRequest = 18; badge.HorizontalOptions = LayoutOptions.End; badge.VerticalOptions = LayoutOptions.End; badge.Margin = 5; badge.IsVisible = false;
            var visual = new Grid { InputTransparent = true }; visual.Add(image); visual.Add(label); visual.Add(badge);
            var button = Button("", FingerName(finger)); button.Clicked += (_, _) => Select(finger);
            var tileContent = new Grid(); tileContent.Add(button); tileContent.Add(visual);
            var tile = Card(tileContent, 8); tile.HeightRequest = 126;
            tile.SetDynamicResource(BackgroundColorProperty, "DashPill");
            tiles[finger] = (tile, image, label, badge, button); row.Add(tile, row.Children.Count);
        }
        stack.Add(row); return stack;
    }
    private void Select(BiometricFinger finger)
    {
        if (!scan.Snapshot.IsRunning) livePreview = false;
        selectedFinger = finger; heading.Text = FingerName(finger);
        foreach (var key in tiles.Keys) Refresh(key);
    }
    private void Refresh(BiometricFinger finger)
    {
        var tile = tiles[finger]; var captured = captures.TryGetValue(finger, out var bytes);
        tile.Image.Source = captured ? Source(bytes!) : null; tile.Image.IsVisible = captured;
        tile.Label.IsVisible = !captured; tile.Badge.IsVisible = captured;
        if (finger == selectedFinger) { tile.Border.RemoveDynamicResource(Border.StrokeProperty); tile.Border.Stroke = new SolidColorBrush(Accent); }
        else { tile.Border.ClearValue(Border.StrokeProperty); tile.Border.SetDynamicResource(Border.StrokeProperty, "DashLine"); }
        tile.Border.StrokeThickness = finger == selectedFinger ? 1.5 : 1;
        SemanticProperties.SetDescription(tile.Button, FingerName(finger) + (finger == selectedFinger ? ", selected" : "") + (captured ? ", captured" : ""));
        if (finger != selectedFinger || (scanBelongsToOpen && scan.Snapshot.IsRunning)) return;
        preview.Source = captured ? Source(bytes!) : null; preview.IsVisible = captured; placeholder.IsVisible = !captured;
    }
    private static ImageSource Source(byte[] bytes) => ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
    private static void Validate(BiometricFinger finger)
    {
        if (!Enum.IsDefined(finger)) throw new ArgumentOutOfRangeException(nameof(finger));
    }
    private static string FingerName(BiometricFinger finger) => ((int)finger < 5 ? "Right " : "Left ") + (((int)finger % 5) switch { 0 => "Thumb", 1 => "Index", 2 => "Middle", 3 => "Ring", _ => "Little" });

    private void ArrangeColumns()
    {
        var width = Math.Max(0, Math.Min(920, Width - 48));
        card.WidthRequest = width;
        scroll.MaximumHeightRequest = Math.Max(1, Height - 48 - 40 - 34 - 16);
        var stacked = width < 760;
        columns.ColumnDefinitions.Clear(); columns.RowDefinitions.Clear();
        columns.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        if (!stacked) columns.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (stacked) columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetColumn((BindableObject)columns.Children[1], stacked ? 0 : 1); Grid.SetRow((BindableObject)columns.Children[1], stacked ? 1 : 0);
    }
    private void HookKeyboard()
    {
        if (keyRoot is not null || Window?.Handler?.PlatformView is not Microsoft.Maui.MauiWinUIWindow native || native.Content is not { XamlRoot: not null } root) return;
        keyRoot = root;
        previousFocus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(keyRoot.XamlRoot) as Microsoft.UI.Xaml.Controls.Control;
        keyRoot.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, keyHandler, true);
        if (Handler?.PlatformView is Microsoft.UI.Xaml.UIElement scope) scope.TabFocusNavigation = Microsoft.UI.Xaml.Input.KeyboardNavigationMode.Cycle;
    }
    private void UnhookKeyboard()
    {
        keyRoot?.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, keyHandler); keyRoot = null;
    }
    private async void OnNativeKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (!IsVisible || args.Key != Windows.System.VirtualKey.Escape) return;
        args.Handled = true; await RequestCloseAsync();
    }

    private Task OnStartAsync()
    {
        if (!IsVisible || closing || !scan.Start()) return Task.CompletedTask;
        scanBelongsToOpen = livePreview = true; failureShown = false; displayedFrame = null;
        StartEnabled = false; StopEnabled = true;
        StatusMessage = "Scanning… place a finger on the sensor.";
        QueueScanUpdate();
        return Task.CompletedTask;
    }
    private async Task OnStop()
    {
        StopEnabled = false;
        await scan.StopAsync();
        await UpdateScanUiAsync();
    }
    private void QueueScanUpdate()
    {
        if (Interlocked.CompareExchange(ref updatePending, 1, 0) != 0) return;
        // This callback captures no frame. Slow UI dispatch drops older frames instead of queuing them.
        if (!Dispatcher.Dispatch(() =>
        {
            Interlocked.Exchange(ref updatePending, 0);
            _ = UpdateScanUiAsync();
        })) Interlocked.Exchange(ref updatePending, 0);
    }
    private async Task UpdateScanUiAsync()
    {
        if (!IsVisible || closing) return;
        var current = scan.Snapshot;
        StartEnabled = !current.IsRunning; StopEnabled = scanBelongsToOpen && current.IsRunning;
        if (!scanBelongsToOpen) return;
        if (livePreview && current.Frame is { } frame && !ReferenceEquals(frame, displayedFrame))
        {
            var bitmap = GrayscaleBitmap.Encode(frame);
            displayedFrame = frame; preview.Source = Source(bitmap); preview.IsVisible = true; placeholder.IsVisible = false;
        }
        StatusMessage = current.State switch
        {
            FingerprintScanState.Scanning => "Scanning… place a finger on the sensor.",
            FingerprintScanState.Faulted => current.ErrorMessage!,
            _ => "Scan stopped."
        };
        if (current.State != FingerprintScanState.Faulted || failureShown || owner is not { } page) return;
        failureShown = true;
        await page.DisplayAlert("Fingerprint scanner", current.ErrorMessage!, "Close");
    }
    private void OnReset()
    {
        // TODO: function to be provided
    }

    private static Label Text(string value, double size = 15, bool bold = false, bool muted = false)
    {
        var label = new Label { Style = new Style(typeof(Label)), Text = value, FontSize = size, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, LineBreakMode = LineBreakMode.WordWrap };
        label.SetDynamicResource(Label.TextColorProperty, muted ? "DashMuted" : "DashText"); return label;
    }
    private static Border Card(View content, double radius)
    {
        var border = new Border { Content = content, Padding = 0, StrokeShape = new RoundRectangle { CornerRadius = radius }, StrokeThickness = 1 };
        border.SetDynamicResource(BackgroundColorProperty, "DashPanel"); border.SetDynamicResource(Border.StrokeProperty, "DashLine"); return border;
    }
    private static DashboardIcon Icon(string kind, double size = 16, bool muted = false)
    {
        var icon = new DashboardIcon { Kind = kind, WidthRequest = size, HeightRequest = size, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        if (muted) icon.SetDynamicResource(DashboardIcon.TintProperty, "DashMuted"); else icon.Tint = Accent; return icon;
    }
    private static Button Button(string title, string description)
    {
        var button = new Button { Style = new Style(typeof(Button)), Text = title, FontSize = 13, CornerRadius = 18, Padding = 0, MinimumWidthRequest = 0, MinimumHeightRequest = 32, BackgroundColor = Colors.Transparent, BorderWidth = 0 };
        SemanticProperties.SetDescription(button, description); ToolTipProperties.SetText(button, description); return button;
    }
    private static Grid ActionButton(string title, string kind, out Button button, bool primary = false, bool muted = false)
    {
        button = Button("", title); button.BackgroundColor = primary ? Accent : Colors.Transparent; button.BorderWidth = primary ? 0 : 1;
        button.SetDynamicResource(Microsoft.Maui.Controls.Button.BorderColorProperty, "DashLine");
        var row = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
        var icon = Icon(kind, 14, muted); var label = Text(title, 13, primary, muted);
        if (primary) { icon.Tint = Colors.White; label.TextColor = Colors.White; }
        else if (!muted) label.TextColor = Accent;
        row.Add(icon); row.Add(label);
        var control = button;
        button.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(IsEnabled)) row.Opacity = control.IsEnabled ? 1 : .4; };
        var grid = new Grid(); grid.Add(button); grid.Add(row); return grid;
    }
}
#endif
