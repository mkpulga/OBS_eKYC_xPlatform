using Microsoft.Maui.Controls.Shapes;
using OBS.Core;
using OBS.Maui.Controls;

namespace OBS.Maui;

public sealed class PrivacyVerificationView : ContentView, IDisposable
{
    private readonly PrivacyScreenState state;
    private readonly Action<View?> showDialog;
    private readonly Entry citizen = new() { Placeholder = "Citizen ID", MaxLength = 100, ReturnType = ReturnType.Go };
    private readonly Button select = new() { Text = "Select applicant", FontSize = 13, CornerRadius = 22, Padding = new Thickness(18, 8) };
    private readonly Label status = Text("Select an applicant before revealing a section.", 13, muted: true);
    private readonly Label message = Text("", 13, muted: true);
    private readonly VerticalStackLayout root = new() { Spacing = 14 };
    private readonly Grid columns = new() { ColumnSpacing = 16, RowSpacing = 14 };
    private readonly Dictionary<string, View> anchors = [];
    private readonly List<Action> updates = [];
    private Grid? overlay;
    private Entry? otp;
    private Label? otpMessage;
    private Button? request, verify;
    private VerticalStackLayout? detailFields;
    private PrivacySection? detailSection;
    private bool disposed, showingOtp;
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? keyRoot;
    private Microsoft.UI.Xaml.Controls.Control? previousFocus;
    private readonly Microsoft.UI.Xaml.Input.KeyEventHandler keyHandler;
#endif
    public PrivacyVerificationView(PrivacyScreenState state, Action<View?> showDialog)
    {
        this.state = state; this.showDialog = showDialog;
#if WINDOWS
        keyHandler = OnNativeKeyDown;
#endif
        var title = Text("Privacy Verification", 24, true); SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        var intro = Text("Enter a citizen ID to view an applicant’s protected details.", 14, muted: true);
        var header = new VerticalStackLayout { Spacing = 12 };
        header.Add(title); header.Add(intro);
        SemanticProperties.SetDescription(citizen, "Citizen ID");
        citizen.TextChanged += (_, args) => { if (!disposed) state.SetCitizenId(args.NewTextValue ?? ""); };
        citizen.Completed += (_, _) => state.SelectApplicant(); select.Clicked += (_, _) => state.SelectApplicant();
        select.BackgroundColor = Color.FromArgb("#485E98"); select.TextColor = Colors.White;
        var selector = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        selector.Add(citizen); selector.Add(select, 1); header.Add(selector); header.Add(status); header.Add(message);
        var headerCard = Card(header); root.Add(headerCard);
        updates.Add(() => { title.IsVisible = intro.IsVisible = !state.Selected; headerCard.MinimumHeightRequest = state.Selected ? 0 : 380; header.VerticalOptions = state.Selected ? LayoutOptions.Start : LayoutOptions.Center; });
        columns.Add(Profile());
        var personal = new VerticalStackLayout { Spacing = 12 };
        var bio = new VerticalStackLayout { Spacing = 12 };
        bio.Add(Heading("Bio Details", "identity", PrivacySection.Bio));
        bio.Add(Field("Birth full name", PrivateValue(PrivacySection.Bio, () => Value(PrivacySection.Bio, "Full name")), true));
        bio.Add(Field("Birthday / Gender", PrivateValue(PrivacySection.Bio, () => Value(PrivacySection.Bio, "Date of birth") + " · " + Value(PrivacySection.Bio, "Gender"))));
        bio.Add(Field("Birthplace", PrivateValue(PrivacySection.Bio, () => Value(PrivacySection.Bio, "Birthplace"))));
        bio.Add(Field("Blood type", PrivateValue(PrivacySection.Bio, () => Value(PrivacySection.Bio, "Blood type"))));
        anchors["bio"] = Card(bio); personal.Add(anchors["bio"]);
        var emergency = new VerticalStackLayout { Spacing = 14 }; emergency.Add(Heading("Emergency Contact Information", "phone", PrivacySection.Emergency));
        var contacts = new VerticalStackLayout { Spacing = 12 }; emergency.Add(contacts);
        updates.Add(() => UpdateContacts(contacts));
        anchors["emergency"] = Card(emergency); personal.Add(anchors["emergency"]);
        var education = new VerticalStackLayout { Spacing = 12 }; education.Add(Heading("Educational Information", "education", PrivacySection.Education));
        var schooling = new VerticalStackLayout { Spacing = 12 }; education.Add(schooling);
        updates.Add(() => UpdateEducation(schooling));
        anchors["education"] = Card(education); personal.Add(anchors["education"]);
        columns.Add(personal); columns.Add(Files()); columns.Add(Sidebar());
        root.Add(columns); Content = root;
        SizeChanged += (_, _) => ArrangeColumns();
        state.Changed += Changed; Render();
    }
    private static Label Text(string value, double size = 15, bool bold = false, bool muted = false)
    {
        var label = new Label { Text = value, FontSize = size, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, LineBreakMode = LineBreakMode.WordWrap };
        label.SetDynamicResource(Label.TextColorProperty, muted ? "DashMuted" : "DashText"); return label;
    }
    private static Border Card(View content, Thickness? padding = null)
    {
        var card = new Border { Content = content, Padding = padding ?? new Thickness(17), StrokeShape = new RoundRectangle { CornerRadius = 21 }, StrokeThickness = 1, VerticalOptions = LayoutOptions.Start };
        card.SetDynamicResource(BackgroundColorProperty, "DashPanel"); card.SetDynamicResource(Border.StrokeProperty, "DashLine"); return card;
    }
    private static DashboardIcon Icon(string kind, double size = 18) => new() { Kind = kind, Tint = Color.FromArgb("#485E98"), WidthRequest = size, HeightRequest = size, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
    private Grid IconButton(string kind, string title, Action? click, Func<bool>? enabled = null, PrivacySection? section = null)
    {
        var container = new Grid { WidthRequest = 30, HeightRequest = 32, VerticalOptions = LayoutOptions.Center };
        var button = new Button { BackgroundColor = Colors.Transparent, Padding = 0, MinimumWidthRequest = 30, MinimumHeightRequest = 30, IsEnabled = click is not null };
        var icon = Icon(kind); button.Clicked += (_, _) => click?.Invoke(); container.Add(button); container.Add(icon);
        SemanticProperties.SetDescription(button, title); ToolTipProperties.SetText(button, click is null ? ApplicationPresentation.EditUnavailable : title);
        if (click is not null) updates.Add(() =>
        {
            button.IsEnabled = enabled?.Invoke() ?? (state.Selected && !state.IsBusy);
            if (section is { } s) { var visible = state.IsVisible(s); icon.Kind = visible ? "eye-off" : "eye"; var label = (visible ? "Hide " : "Reveal ") + title; SemanticProperties.SetDescription(button, label); ToolTipProperties.SetText(button, label); }
        });
        return container;
    }
    private View Eye(PrivacySection section, string title) => IconButton("eye", title, () => _ = state.ShowOrHideAsync(section), section: section);
    private View Heading(string title, string icon, PrivacySection section)
    {
        var row = new Grid { ColumnDefinitions = [new(34), new(GridLength.Star), new(30), new(30)], ColumnSpacing = 10 };
        row.Add(new Border { Content = Icon(icon), BackgroundColor = Color.FromArgb("#18485E98"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 11 }, HeightRequest = 34 });
        var label = Text(title, 15, true); label.VerticalOptions = LayoutOptions.Center; row.Add(label, 1);
        row.Add(IconButton("list", "View " + title + " details", () => OpenDetails(section), () => state.IsVisible(section)), 2); row.Add(Eye(section, title), 3); return row;
    }
    private static View Field(string title, View value, bool edit = false)
    {
        var stack = new VerticalStackLayout { Spacing = 5 }; stack.Add(Text(title, 11, muted: true));
        if (!edit) stack.Add(value);
        else
        {
            var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(30)], ColumnSpacing = 3 }; row.Add(value);
            var editContainer = new Grid { WidthRequest = 30, HeightRequest = 32 };
            var editButton = new Button { IsEnabled = false, BackgroundColor = Colors.Transparent, Padding = 0, MinimumWidthRequest = 30, MinimumHeightRequest = 30 };
            SemanticProperties.SetDescription(editButton, "Edit " + title); ToolTipProperties.SetText(editButton, ApplicationPresentation.EditUnavailable); editContainer.Add(editButton); editContainer.Add(Icon("pencil")); row.Add(editContainer, 1); stack.Add(row);
        }
        return stack;
    }
    private string Value(PrivacySection section, string label) => state.Fields(section).FirstOrDefault(f => f.Label == label)?.Value ?? "Not provided";
    private View PrivateValue(PrivacySection section, Func<string> value, double size = 15, bool bold = false, bool muted = false)
    {
        var label = Text("", size, bold, muted); var mask = Mask(size);
        var grid = new Grid(); grid.Add(label); grid.Add(mask);
        updates.Add(() => { var shown = state.IsVisible(section); label.Text = shown ? value() : ""; label.IsVisible = shown; mask.IsVisible = !shown; });
        return grid;
    }
    private static View Mask(double size = 15)
    {
        var view = new GraphicsView { WidthRequest = 128, HeightRequest = size * 1.5, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
        var tint = Text("", size, muted: true); tint.IsVisible = false;
        view.Drawable = new HiddenValue(() => tint.TextColor);
        tint.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(Label.TextColor)) view.Invalidate(); };
        SemanticProperties.SetDescription(view, "Protected details are hidden");
        var host = new Grid { InputTransparent = true }; host.Add(tint); host.Add(view); return host;
    }
    private sealed class HiddenValue(Func<Color> tint) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var heights = new[] { 7f, 10f, 8f, 6f, 9f, 7f, 10f, 8f };
            for (var spread = 6; spread >= 0; spread--)
            {
                canvas.FillColor = tint().WithAlpha(.045f);
                for (var i = 0; i < heights.Length; i++) canvas.FillRoundedRectangle(7 + i * 14 - spread * .65f, (rect.Height - heights[i]) / 2 - spread * .65f, 11 + spread * 1.3f, heights[i] + spread * 1.3f, 3 + spread * .65f);
            }
        }
    }
    private View Profile()
    {
        var body = new VerticalStackLayout { Spacing = 0 };
        body.Add(new Border { HeightRequest = 132, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 14 }, Background = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb("#F48500"), 0), new(Color.FromArgb("#FF662D"), .53f), new(Color.FromArgb("#1D51BC"), 1) }, new Point(0, 0), new Point(1, .3)) });
        var avatar = new Grid { WidthRequest = 88, HeightRequest = 88, Margin = new Thickness(14, -38, 0, 12), HorizontalOptions = LayoutOptions.Start };
        var face = new Border { Content = Icon("user", 38), StrokeThickness = 4, StrokeShape = new RoundRectangle { CornerRadius = 44 }, Shadow = new Shadow { Brush = Colors.Black, Opacity = .12f, Offset = new Point(0, 4), Radius = 8 } };
        face.SetDynamicResource(BackgroundColorProperty, "DashPill"); face.SetDynamicResource(Border.StrokeProperty, "DashSidebar"); avatar.Add(face); avatar.Add(IconButton("camera", "Change applicant photo", null)); body.Add(avatar);
        var name = new Grid { ColumnDefinitions = [new(GridLength.Star), new(30), new(30)], ColumnSpacing = 6, Margin = new Thickness(0, 2, 0, 10) };
        var label = Text("Applicant", 22, true); updates.Add(() => label.Text = state.IsVisible(PrivacySection.Bio) ? Value(PrivacySection.Bio, "Full name") : "Applicant");
        name.Add(label); name.Add(IconButton("pencil", "Edit applicant name", null), 1); name.Add(Eye(PrivacySection.Bio, "applicant profile"), 2); body.Add(name);
        foreach (var (icon, caption) in new[] { ("hash", "Citizen ID: "), ("mail", "Email: Not provided"), ("phone", "Phone: Not provided") })
        {
            var row = new Grid { ColumnDefinitions = [new(15), new(GridLength.Star)], ColumnSpacing = 8, Margin = new Thickness(0, 4) };
            var value = Text(caption, 12, muted: true); if (icon == "hash") updates.Add(() => value.Text = caption + state.CitizenId);
            row.Add(Icon(icon, 15)); row.Add(value, 1); body.Add(row);
        }
        var badges = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8, Margin = new Thickness(0, 14, 0, 16) };
        for (var i = 0; i < 2; i++)
        {
            var labels = new VerticalStackLayout { Spacing = 4 }; labels.Add(Text(i == 0 ? "Account" : "Card", 10, muted: true));
            var value = Text(i == 0 ? "HIDDEN" : "NOT PROVIDED", 12, true); value.TextColor = Color.FromArgb(i == 0 ? "#008865" : "#AC6800"); labels.Add(value);
            if (i == 0) updates.Add(() => value.Text = state.IsVisible(PrivacySection.Bio) ? Value(PrivacySection.Bio, "Account status").ToUpperInvariant() : "HIDDEN");
            var badge = Card(labels, 10); badge.Stroke = new SolidColorBrush(Color.FromArgb(i == 0 ? "#91D1C5" : "#E6BC8C")); badge.BackgroundColor = Color.FromArgb(i == 0 ? "#1842BA9B" : "#18D4A968"); badges.Add(badge, i);
        }
        body.Add(badges);
        var line = new BoxView { HeightRequest = 1, Margin = new Thickness(0, 0, 0, 10) }; line.SetDynamicResource(BoxView.ColorProperty, "DashPill"); body.Add(line);
        var heading = new Grid { ColumnDefinitions = [new(GridLength.Star), new(30), new(30)], Margin = new Thickness(0, 0, 0, 6) }; heading.Add(Text("Address", 13, true)); heading.Add(IconButton("pencil", "Edit address", null), 1); heading.Add(Eye(PrivacySection.Address, "Address"), 2); body.Add(heading);
        var addresses = new VerticalStackLayout { Spacing = 8, Margin = new Thickness(0, 0, 0, 12) }; anchors["address"] = addresses; body.Add(addresses); updates.Add(() =>
        {
            ClearLabels(addresses);
            if (!state.IsVisible(PrivacySection.Address)) { addresses.Add(Mask(12)); return; }
            var fields = state.Fields(PrivacySection.Address); if (fields.Count == 0) addresses.Add(Text("No details returned for this section.", 12, muted: true));
            for (var i = 0; i < fields.Count; i += 10)
            {
                var row = fields.Skip(i).Take(10).ToArray(); addresses.Add(Text(row[0].Label.Split(" · Specific address")[0], 10, muted: true));
                addresses.Add(Text(string.Join(", ", row.Select(f => f.Value).Where(v => v != "Not provided")), 12));
            }
        });
        var map = new Grid { HeightRequest = 112, IsClippedToBounds = true }; map.SetDynamicResource(BackgroundColorProperty, "DashPill");
        foreach (var rotation in new[] { -16d, 11d, 55d, -64d }) { var road = new BoxView { HeightRequest = 7, Rotation = rotation, VerticalOptions = LayoutOptions.Center, InputTransparent = true }; road.SetDynamicResource(BoxView.ColorProperty, "DashSidebar"); map.Add(road); }
        map.Add(new BoxView { Color = Color.FromArgb("#B6D7A3"), WidthRequest = 38, HeightRequest = 25, CornerRadius = 5, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = 8 });
        var mapButton = new Button { Text = "View Map", IsEnabled = false, FontSize = 13, CornerRadius = 20, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        mapButton.SetDynamicResource(BackgroundColorProperty, "DashSidebar"); ToolTipProperties.SetText(mapButton, "Map preview is unavailable in this screen."); map.Add(mapButton);
        body.Add(new Border { Content = map, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 } });
        var card = Card(body, new Thickness(15, 15, 15, 42)); anchors["account"] = card; return card;
    }
    private void UpdateContacts(VerticalStackLayout host)
    {
        ClearLabels(host); var visible = state.IsVisible(PrivacySection.Emergency); var fields = state.Fields(PrivacySection.Emergency);
        if (visible && fields.Count == 0) { host.Add(Text("No details returned for this section.", 12, muted: true)); return; }
        for (var i = 0; i < (visible ? fields.Count : 3); i += 3)
        {
            var row = new Grid { ColumnDefinitions = [new(54), new(GridLength.Star)], ColumnSpacing = 14 };
            var avatar = new Border { Content = Icon("user", 29), HeightRequest = 68, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 } }; avatar.SetDynamicResource(BackgroundColorProperty, "DashBackground"); row.Add(avatar);
            var text = new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center };
            text.Add(visible ? Text(fields[i].Value, 13, true) : Mask(13));
            text.Add(visible ? Text(fields[i + 2].Value + " · " + fields[i + 1].Value, 11) : Mask(11)); text.Add(Text("Address not provided", 11, muted: true)); row.Add(text, 1);
            var card = Card(row, 14); card.BackgroundColor = Color.FromArgb("#28485E98"); host.Add(card);
        }
    }
    private void UpdateEducation(VerticalStackLayout host)
    {
        ClearLabels(host); var visible = state.IsVisible(PrivacySection.Education); var fields = state.Fields(PrivacySection.Education);
        if (visible && fields.Count == 0) { host.Add(Text("No details returned for this section.", 12, muted: true)); return; }
        for (var i = 0; i < (visible ? fields.Count : 8); i += 8)
        {
            host.Add(Field("Educational attainment", visible ? Text(fields[i].Value + " · " + fields[i + 2].Value) : Mask(), true));
            host.Add(Field("School / Year graduated", visible ? Text(fields[i + 1].Value + " · Year not provided") : Mask()));
            host.Add(Field("Address", Text("Not provided", 12, muted: true)));
        }
    }
    private View Files()
    {
        var body = new VerticalStackLayout { Spacing = 16 }; body.Add(Text("Biometrics and Documents", 15, true));
        var capture = new VerticalStackLayout { Spacing = 10 }; capture.Add(Text("Biometrics", 13));
        var panel = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(8, 20) };
        var button = new Button { Text = "Capture", IsEnabled = false, FontSize = 13, CornerRadius = 20, Padding = new Thickness(16, 7), BackgroundColor = Color.FromArgb("#485E98"), TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center };
        panel.Add(button); var reason = Text("Capture is not available in this screen.", 10, muted: true); reason.HorizontalTextAlignment = TextAlignment.Center; panel.Add(reason);
        var surface = Card(panel, 8); surface.SetDynamicResource(BackgroundColorProperty, "DashPill"); capture.Add(surface); anchors["biometrics"] = capture; body.Add(capture);
        var documents = new VerticalStackLayout { Spacing = 8 };
        var heading = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] }; heading.Add(Text("Documents", 13));
        var add = new Button { Text = "Add Attachment", IsEnabled = false, FontSize = 12, BackgroundColor = Colors.Transparent, Padding = 0 }; ToolTipProperties.SetText(add, "Documents are not available in this screen."); heading.Add(add, 1);
        documents.Add(heading); documents.Add(Text("Documents are not available in this screen.", 12, muted: true)); anchors["documents"] = documents; body.Add(documents); return Card(body);
    }
    private View Sidebar()
    {
        var side = new VerticalStackLayout { Spacing = 12 }; var actions = new VerticalStackLayout { Spacing = 12 }; actions.Add(Text("Actions", 15, true));
        var picker = new Picker { Title = "Select an action", FontSize = 13 }; picker.Items.Add("Hide protected details"); picker.Items.Add("Select another applicant"); picker.SetDynamicResource(Picker.TextColorProperty, "DashText");
        picker.SelectedIndexChanged += (_, _) => { var choice = picker.SelectedIndex; if (choice < 0) return; picker.SelectedIndex = -1; detailSection = null; if (choice == 0) state.CancelDialog(); else state.SetCitizenId(""); };
        actions.Add(Card(picker, new Thickness(8, 0))); side.Add(Card(actions));
        var links = new VerticalStackLayout { Spacing = 2 }; links.Add(Text("Quick Links", 15, true));
        foreach (var (title, id, icon) in new[] { ("Account", "account", "user"), ("Bio Details", "bio", "identity"), ("Emergency Contact", "emergency", "phone"), ("Educational Info", "education", "education"), ("Biometrics", "biometrics", "capture"), ("Documents", "documents", "document") })
        {
            var row = new Grid { ColumnDefinitions = [new(22), new(GridLength.Star)], ColumnSpacing = 16, HeightRequest = 47 }; row.Add(Icon(icon));
            var button = new Button { Text = title, FontSize = 13, BackgroundColor = Colors.Transparent, Padding = new Thickness(16, 7), HorizontalOptions = LayoutOptions.Start }; button.SetDynamicResource(Button.TextColorProperty, "DashText");
            button.Clicked += async (_, _) => await ScrollTo(id); row.Add(button, 1); links.Add(row);
        }
        side.Add(Card(links)); return side;
    }
    private async Task ScrollTo(string id)
    {
        if (!anchors.TryGetValue(id, out var target)) return;
        var parent = Parent; while (parent is not null && parent is not ScrollView) parent = parent.Parent;
        if (parent is ScrollView scroll) await scroll.ScrollToAsync(target, ScrollToPosition.Center, true);
    }
    private void ArrangeColumns()
    {
        var count = Width >= 1200 ? 4 : Width >= 650 ? 2 : 1;
        columns.ColumnDefinitions.Clear(); columns.RowDefinitions.Clear();
        for (var i = 0; i < count; i++) columns.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < 4 / count; i++) columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var i = 0; i < columns.Children.Count; i++) { Grid.SetColumn((BindableObject)columns.Children[i], i % count); Grid.SetRow((BindableObject)columns.Children[i], i / count); }
    }
    private void Changed() => MainThread.BeginInvokeOnMainThread(() => { if (!disposed) Render(); });
    private void Render()
    {
        if (disposed) return;
        if (citizen.Text != state.CitizenId) citizen.Text = state.CitizenId;
        select.IsEnabled = !state.IsBusy; columns.IsVisible = state.Selected;
        status.Text = state.Selected ? state.Status : "Select an applicant before revealing a section.";
        message.Text = state.Message; message.IsVisible = !string.IsNullOrEmpty(message.Text);
        foreach (var update in updates) update();
        ArrangeColumns(); RenderDialog();
    }
    private void OpenDetails(PrivacySection section)
    {
        if (disposed || !state.IsVisible(section)) return;
        ClearDialog(); detailSection = section; RenderDialog();
    }
    private void RenderDetails(PrivacySection section)
    {
        if (overlay is null)
        {
            overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
            var body = new VerticalStackLayout { Spacing = 14 }; body.Add(Text(PrivacyPresentation.Sections.Single(s => s.Section == section).Title, 20, true));
            detailFields = new VerticalStackLayout { Spacing = 12 }; body.Add(detailFields);
            var close = new Button { Text = "Close", HorizontalOptions = LayoutOptions.End }; close.Clicked += (_, _) => { detailSection = null; ClearDialog(); }; body.Add(close);
            var card = Card(new ScrollView { Content = body }); card.SetDynamicResource(BackgroundColorProperty, "DashSidebar"); card.HorizontalOptions = card.VerticalOptions = LayoutOptions.Center;
            overlay.Add(card); overlay.SizeChanged += (_, _) => { if (overlay is not null) { card.WidthRequest = Math.Max(0, Math.Min(680, overlay.Width - 32)); card.MaximumHeightRequest = Math.Max(0, overlay.Height - 32); } };
            overlay.Loaded += (_, _) =>
            {
#if WINDOWS
                HookKeyboard();
#endif
                close.Focus();
            };
            showDialog(overlay);
        }
        ClearLabels(detailFields!);
        foreach (var field in state.Fields(section)) { detailFields!.Add(Text(field.Label, 11, muted: true)); detailFields.Add(Text(field.Value)); }
    }
    private void RenderDialog()
    {
        if (detailSection is { } shown && !state.IsVisible(shown)) detailSection = null;
        if (!state.DialogOpen)
        {
            if (detailSection is { } details) { RenderDetails(details); return; }
            ClearDialog(); return;
        }
        if (!showingOtp) ClearDialog();
        showingOtp = true;
        if (overlay is null)
        {
            overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
            var body = new VerticalStackLayout { Spacing = 14 };
            var title = Text("Verify protected access", 20, true); SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
            body.Add(title); body.Add(Text("Request a six-digit code sent to your staff email."));
            request = new Button { Text = "Request code" }; request.Clicked += async (_, _) => await state.RequestCodeAsync(); body.Add(request);
            body.Add(Text("Email code", 12));
            otp = new Entry { Placeholder = "Six-digit email code", Keyboard = Keyboard.Numeric, MaxLength = 6, IsPassword = true, ReturnType = ReturnType.Done };
            SemanticProperties.SetDescription(otp, "Six-digit email code");
            otp.TextChanged += (_, args) => state.SetOtp(args.NewTextValue ?? ""); otp.Completed += async (_, _) => await state.VerifyCodeAsync(); body.Add(otp);
            otpMessage = Text("", muted: true); body.Add(otpMessage);
            var actions = new HorizontalStackLayout { Spacing = 12 };
            verify = new Button { Text = "Verify", BackgroundColor = Color.FromArgb("#485E98"), TextColor = Colors.White };
            verify.Clicked += async (_, _) => await state.VerifyCodeAsync();
            var cancel = new Button { Text = "Cancel" }; cancel.Clicked += (_, _) => state.CancelDialog(); actions.Add(verify); actions.Add(cancel); body.Add(actions);
            var card = Card(new ScrollView { Content = body });
            card.SetDynamicResource(BackgroundColorProperty, "DashSidebar"); card.HorizontalOptions = card.VerticalOptions = LayoutOptions.Center;
            overlay.Add(card);
            overlay.SizeChanged += (_, _) => { if (overlay is not null) { card.WidthRequest = Math.Max(0, Math.Min(440, overlay.Width - 32)); card.MaximumHeightRequest = Math.Max(0, overlay.Height - 32); } };
            overlay.Loaded += (_, _) =>
            {
#if WINDOWS
                HookKeyboard();
#endif
                request?.Focus();
            };
            showDialog(overlay);
        }
        request!.IsEnabled = !state.IsBusy; request.Text = state.IsBusy ? "Please wait…" : "Request code";
        verify!.IsEnabled = state.CanVerify; otp!.IsEnabled = state.CanVerify;
        if (otp.Text != state.Otp) otp.Text = state.Otp;
        otpMessage!.Text = state.Message; otpMessage.IsVisible = !string.IsNullOrEmpty(otpMessage.Text);
    }
    public bool CancelDialogIfOpen()
    {
        if (disposed) return false;
        if (state.DialogOpen) { state.CancelDialog(); return true; }
        if (detailSection is not null) { detailSection = null; ClearDialog(); return true; }
        return false;
    }
    private void ClearDialog()
    {
        if (overlay is null) return;
        if (otp is not null) otp.Text = "";
        if (otpMessage is not null) otpMessage.Text = "";
        if (detailFields is not null) { ClearLabels(detailFields); detailFields = null; }
#if WINDOWS
        keyRoot?.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, keyHandler); keyRoot = null;
#endif
        showDialog(null); overlay = null; otp = null; otpMessage = null; request = verify = null; showingOtp = false;
#if WINDOWS
        previousFocus?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); previousFocus = null;
#endif
    }
    private static void Erase(View view)
    {
        if (view is Label label) label.Text = "";
        else if (view is Layout layout) foreach (var child in layout.Children.OfType<View>()) Erase(child);
        else if (view is Border { Content: View body }) Erase(body);
        else if (view is ContentView { Content: View content }) Erase(content);
    }
    private static void ClearLabels(VerticalStackLayout host) { foreach (var child in host.Children.OfType<View>()) Erase(child); host.Clear(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; detailSection = null; state.Changed -= Changed; state.Dispose(); ClearDialog(); citizen.Text = ""; message.Text = status.Text = "";
        Erase(root); updates.Clear(); anchors.Clear(); columns.Clear();
    }
#if WINDOWS
    private void HookKeyboard()
    {
        if (keyRoot is not null || Window?.Handler?.PlatformView is not Microsoft.Maui.MauiWinUIWindow native || native.Content is not { XamlRoot: not null } root) return;
        keyRoot = root; previousFocus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root.XamlRoot) as Microsoft.UI.Xaml.Controls.Control;
        keyRoot.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, keyHandler, true);
        if (overlay?.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement scope) scope.TabFocusNavigation = Microsoft.UI.Xaml.Input.KeyboardNavigationMode.Cycle;
    }
    private void OnNativeKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    { if (args.Key == Windows.System.VirtualKey.Escape && CancelDialogIfOpen()) args.Handled = true; }
#endif
}
