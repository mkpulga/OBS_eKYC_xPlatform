using Microsoft.Maui.Controls.Shapes;
using OBS.Core;
using OBS.Maui.Controls;

namespace OBS.Maui;

// Shares its record service and display rules with Blazor; inherits the dashboard's theme resources.
public sealed class ViewApplicationView : ContentView
{
    private readonly Grid columns = new() { ColumnSpacing = 16, RowSpacing = 14 };
    private readonly Dictionary<string, View> sections = [];
    private readonly List<string> previewFiles = [];
    private readonly ApplicationRevealState reveal = new();
    private readonly Dictionary<ApplicationGroup, List<Action>> groupUpdates = [];
    private SessionController? session;
    private IApplicationRecords records = new UnavailableApplicationRecords();
    private CancellationTokenSource? load;
    private string number = "";
    private ApplicationReply? reply;
    public event Action<NavigationEntry>? WorkflowRequested;
#if WINDOWS
    public event Action? BiometricCaptureRequested;
#endif
    public ViewApplicationView() { SizeChanged += (_, _) => ArrangeColumns(); ShowState("Search by application number to view an applicant’s complete record."); }
    public void Configure(SessionController controller, IApplicationRecords service) { session = controller; records = service; }

    public async Task LoadAsync(string applicationNumber)
    {
        ResetReveal();
        if (session?.Current is not { } staff || !session.ProtectedWorkAllowed) return;
        Clear(); number = applicationNumber.Trim();
        if (number.Length == 0) { ShowState("Search by application number to view an applicant’s complete record."); return; }
        var current = load = new CancellationTokenSource();
        ShowState("Loading application…", true);
        try
        {
            var result = await records.FindAsync(staff, number, current.Token);
            if (current.IsCancellationRequested || session.Current?.Email != staff.Email || !session.ProtectedWorkAllowed) return;
            reply = result;
            if (result.Record is { } record) Render(record);
            else ShowState(ApplicationPresentation.Status(result.Outcome), retry: result.Outcome is ApplicationOutcome.ConnectionFailure or ApplicationOutcome.InvalidResponse or ApplicationOutcome.Rejected);
        }
        finally { if (load == current) load = null; current.Dispose(); }
    }
    public void Clear()
    {
        ResetReveal(); groupUpdates.Clear();
        load?.Cancel(); load = null; reply = null; sections.Clear(); columns.Clear();
        foreach (var path in previewFiles) { try { File.Delete(path); } catch (IOException) { } }
        previewFiles.Clear();
        ShowState("Search by application number to view an applicant’s complete record.");
    }
    private static Label Text(string value, double size = 15, bool bold = false, bool muted = false)
    {
        var label = new Label { Text = value, FontSize = size, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, LineBreakMode = LineBreakMode.WordWrap };
        label.SetDynamicResource(Label.TextColorProperty, muted ? "DashMuted" : "DashText");
        return label;
    }
    private static Border Card(View content, Thickness? padding = null)
    {
        var border = new Border { Content = content, Padding = padding ?? new Thickness(17), StrokeShape = new RoundRectangle { CornerRadius = 21 }, StrokeThickness = 1, VerticalOptions = LayoutOptions.Start };
        border.SetDynamicResource(BackgroundColorProperty, "DashPanel"); border.SetDynamicResource(Border.StrokeProperty, "DashLine");
        return border;
    }
    private static DashboardIcon Icon(string kind, double size = 18) => new() { Kind = kind, Tint = Color.FromArgb("#485E98"), WidthRequest = size, HeightRequest = size, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
    private Grid IconButton(string icon, string label, Action? action, string? unavailable = null, ApplicationGroup? group = null, bool toggle = false)
    {
        var grid = new Grid { WidthRequest = 30, HeightRequest = 32, VerticalOptions = LayoutOptions.Center };
        var button = new Button { BackgroundColor = Colors.Transparent, Padding = 0, MinimumWidthRequest = 30, MinimumHeightRequest = 30, IsEnabled = action is not null };
        SemanticProperties.SetDescription(button, label); ToolTipProperties.SetText(button, unavailable ?? label);
        button.Clicked += (_, _) => action?.Invoke(); grid.Add(button); var glyph = Icon(icon); grid.Add(glyph);
        if (group is { } g) Watch(g, () =>
        {
            var shown = reveal.IsVisible(g);
            var description = toggle ? (shown ? "Hide " : "Show ") + label : "View " + label;
            button.IsEnabled = toggle || reveal.DetailsAllowed(g);
            glyph.Kind = toggle ? (shown ? "eye-off" : "eye") : icon;
            SemanticProperties.SetDescription(button, description);
            ToolTipProperties.SetText(button, !toggle && !shown ? "Show " + label + " first" : description);
        });
        return grid;
    }
    private void Watch(ApplicationGroup group, Action update)
    {
        if (!groupUpdates.TryGetValue(group, out var updates)) groupUpdates[group] = updates = [];
        updates.Add(update); update();
    }
    private void ResetReveal()
    {
        reveal.Reset();
        foreach (var updates in groupUpdates.Values) foreach (var update in updates) update();
    }
    private void Toggle(ApplicationGroup group, string title)
    {
        reveal.Toggle(group);
        foreach (var update in groupUpdates[group]) update();
        SemanticScreenReader.Default.Announce(title + (reveal.IsVisible(group) ? " shown" : " hidden"));
    }
    private View PrivateText(ApplicationGroup group, string value, double size = 15, bool bold = false, bool muted = false, string prefix = "")
    {
        var label = Text("", size, bold);
        var blur = new GraphicsView { WidthRequest = 128, HeightRequest = size * 1.5, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
        blur.Drawable = new HiddenValue(() => label.TextColor);
        SemanticProperties.SetDescription(blur, "Hidden");
        label.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(Label.TextColor)) blur.Invalidate(); };
        var valueArea = new Grid(); valueArea.Add(label); valueArea.Add(blur);
        Watch(group, () =>
        {
            var shown = reveal.IsVisible(group);
            label.Text = reveal.Display(group, value);
            label.IsVisible = shown; blur.IsVisible = !shown;
            label.SetDynamicResource(Label.TextColorProperty, !shown || muted ? "DashMuted" : "DashText");
            SemanticProperties.SetDescription(label, shown ? null : "Hidden");
        });
        if (prefix.Length == 0) return valueArea;
        var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        row.Add(Text(prefix, size, bold, muted)); row.Add(valueArea, 1); return row;
    }
    // Soft, text-like shapes contain no applicant data and never vary with its length.
    private sealed class HiddenValue(Func<Color> tint) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var color = tint();
            var heights = new[] { 7f, 10f, 8f, 6f, 9f, 7f, 10f, 8f };
            for (var spread = 6; spread >= 0; spread--)
            {
                canvas.FillColor = color.WithAlpha(.045f);
                for (var i = 0; i < heights.Length; i++)
                {
                    var height = heights[i];
                    canvas.FillRoundedRectangle(7 + i * 14 - spread * .65f, (dirtyRect.Height - height) / 2 - spread * .65f,
                        11 + spread * 1.3f, height + spread * 1.3f, 3 + spread * .65f);
                }
            }
        }
    }
    private static Button Button(string title, Action action, bool primary = false)
    {
        var button = new Button { Text = title, FontSize = 13, CornerRadius = 20, Padding = new Thickness(16, 7), MinimumHeightRequest = 34, HorizontalOptions = LayoutOptions.Start };
        button.BackgroundColor = primary ? Color.FromArgb("#485E98") : Colors.Transparent;
        if (primary) button.TextColor = Colors.White; else button.SetDynamicResource(Microsoft.Maui.Controls.Button.TextColorProperty, "DashText");
        button.Clicked += (_, _) => action(); return button;
    }
    private void ShowState(string message, bool loading = false, bool retry = false)
    {
        var content = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(12, 72), HorizontalOptions = LayoutOptions.Fill };
        content.Add(Icon("identity", 32));
        var heading = Text("View Application", 24, true); heading.HorizontalTextAlignment = TextAlignment.Center; content.Add(heading);
        var status = Text(message, 14, muted: true); status.HorizontalTextAlignment = TextAlignment.Center; content.Add(status);
        if (loading) content.Add(new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#485E98"), HeightRequest = 24 });
        if (retry) { var button = Button("Try again", () => _ = LoadAsync(number), true); button.HorizontalOptions = LayoutOptions.Center; content.Add(button); }
        Content = Card(content);
    }
    private View Heading(string title, string icon, ApplicationGroup group, Action view)
    {
        var grid = new Grid { ColumnDefinitions = [new(34), new(GridLength.Star), new(30), new(30)], ColumnSpacing = 10 };
        var tile = new Border { Content = Icon(icon), BackgroundColor = Color.FromArgb("#18485E98"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 11 }, HeightRequest = 34 };
        grid.Add(tile); var label = Text(title, 15, true); label.VerticalOptions = LayoutOptions.Center; grid.Add(label, 1);
        grid.Add(IconButton("list", title, view, group: group), 2);
        grid.Add(IconButton("eye", title, () => Toggle(group, title), group: group, toggle: true), 3); return grid;
    }
    private View Field(ApplicationGroup group, string label, string value, bool edit = false)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        stack.Add(Text(label, 11, muted: true));
        if (!edit) stack.Add(PrivateText(group, value));
        else
        {
            var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(30)], ColumnSpacing = 3 };
            grid.Add(PrivateText(group, value)); grid.Add(IconButton("pencil", "Edit " + label, null, ApplicationPresentation.EditUnavailable), 1); stack.Add(grid);
        }
        return stack;
    }
    private void Render(ApplicationRecord r)
    {
        ResetReveal(); groupUpdates.Clear();
        sections.Clear(); columns.Clear();
        var profile = Profile(r);
        var personal = new VerticalStackLayout { Spacing = 12 };
        var bio = new VerticalStackLayout { Spacing = 12 };
        var bioFields = new ApplicationField[] { new("Birth full name", r.Name), new("Birthday", r.Birthday), new("Gender", r.Gender), new("Birthplace", r.Birthplace), new("Blood type", r.BloodType) }.Concat(r.BioFields).ToArray();
        bio.Add(Heading("Bio Details", "identity", ApplicationGroup.Bio, () => Details(ApplicationGroup.Bio, "Bio Details", bioFields)));
        bio.Add(Field(ApplicationGroup.Bio, "Birth full name", r.Name, true));
        bio.Add(Field(ApplicationGroup.Bio, "Birthday / Age / Gender", ApplicationPresentation.Join(" · ", ApplicationPresentation.Birthday(r.Birthday, DateOnly.FromDateTime(DashboardPresentation.LocalTime.DateTime)), ApplicationPresentation.Text(r.Gender))));
        bio.Add(Field(ApplicationGroup.Bio, "Birthplace", r.Birthplace)); bio.Add(Field(ApplicationGroup.Bio, "Blood type", r.BloodType));
        sections["bio"] = Card(bio); personal.Add(sections["bio"]);
        var emergency = new VerticalStackLayout { Spacing = 14 };
        emergency.Add(Heading("Emergency Contact Information", "phone", ApplicationGroup.Emergency, () => Details(ApplicationGroup.Emergency, "Emergency Contact Information", [new("Name", r.EmergencyName), new("Relationship", r.EmergencyRelationship), new("Phone", r.EmergencyPhone), new("Address", r.EmergencyAddress)])));
        var person = new Grid { ColumnDefinitions = [new(54), new(GridLength.Star)], ColumnSpacing = 14 };
        var avatar = new Border { Content = Icon("user", 29), HeightRequest = 68, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 } }; avatar.SetDynamicResource(BackgroundColorProperty, "DashBackground"); person.Add(avatar);
        var contact = new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center };
        contact.Add(PrivateText(ApplicationGroup.Emergency, r.EmergencyName, 13, true)); contact.Add(PrivateText(ApplicationGroup.Emergency, ApplicationPresentation.Join(" · ", ApplicationPresentation.Text(r.EmergencyPhone), r.EmergencyRelationship), 11)); contact.Add(PrivateText(ApplicationGroup.Emergency, r.EmergencyAddress, 11, muted: true)); person.Add(contact, 1);
        var contactCard = Card(person, 14); contactCard.BackgroundColor = Color.FromArgb("#28485E98"); emergency.Add(contactCard);
        sections["emergency"] = Card(emergency); personal.Add(sections["emergency"]);
        var education = new VerticalStackLayout { Spacing = 12 };
        education.Add(Heading("Educational Information", "education", ApplicationGroup.Education, () => Details(ApplicationGroup.Education, "Educational Information", [new("Educational attainment", r.Attainment), new("Course", r.Course), new("School", r.School)])));
        education.Add(Field(ApplicationGroup.Education, "Educational attainment", ApplicationPresentation.Join(" · ", ApplicationPresentation.Text(r.Attainment), r.Course), true)); education.Add(Field(ApplicationGroup.Education, "School / Year graduated", ApplicationPresentation.Text(r.School) + " · Year not provided")); education.Add(Field(ApplicationGroup.Education, "Address", ""));
        sections["education"] = Card(education); personal.Add(sections["education"]);
        var files = Files(r);
        var side = new VerticalStackLayout { Spacing = 12 };
        var actions = new VerticalStackLayout { Spacing = 12 }; actions.Add(Text("Actions", 15, true));
        var picker = new Picker { Title = "Select an action", FontSize = 13 };
        picker.Items.Add("Refresh application"); picker.Items.Add("View complete account");
        picker.SetDynamicResource(Picker.TextColorProperty, "DashText");
        if (session?.Current?.Allows("VCARDHOLDERUPDATE") == true) picker.Items.Add("Cardholder update workflow");
        picker.SelectedIndexChanged += async (_, _) => { var choice = picker.SelectedIndex; if (choice < 0) return; picker.SelectedIndex = -1; if (choice == 0) _ = LoadAsync(number); else if (choice == 1) { if (reveal.DetailsAllowed(ApplicationGroup.Profile)) Details(ApplicationGroup.Profile, "Account", r.AccountFields); else if (Window?.Page is Page page) await page.DisplayAlert("Account", "Show the applicant profile first to view the complete account.", "Close"); } else if (session?.Current?.Allows("VCARDHOLDERUPDATE") == true) WorkflowRequested?.Invoke(OBS.Core.Navigation.Entries.Single(entry => entry.Title == "Cardholder Update")); };
        actions.Add(Card(picker, new Thickness(8, 0))); side.Add(Card(actions));
        var links = new VerticalStackLayout { Spacing = 2 }; links.Add(Text("Quick Links", 15, true));
        foreach (var (title, id, icon) in new[] { ("Account", "account", "user"), ("Bio Details", "bio", "identity"), ("Emergency Contact", "emergency", "phone"), ("Educational Info", "education", "education"), ("Biometrics", "biometrics", "capture"), ("Documents", "documents", "document") })
        {
            var row = new Grid { ColumnDefinitions = [new(22), new(GridLength.Star)], ColumnSpacing = 16, HeightRequest = 47 };
            row.Add(Icon(icon)); var button = Button(title, () => _ = ScrollTo(id)); button.HorizontalOptions = LayoutOptions.Start; row.Add(button, 1); links.Add(row);
        }
        side.Add(Card(links));
        columns.Add(profile); columns.Add(personal); columns.Add(files); columns.Add(side);
        Content = columns; ArrangeColumns();
    }
    private View Profile(ApplicationRecord r)
    {
        var content = new VerticalStackLayout { Spacing = 0 };
        content.Add(new Border { HeightRequest = 132, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 14 }, Background = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb("#F48500"), 0), new(Color.FromArgb("#FF662D"), .53f), new(Color.FromArgb("#1D51BC"), 1) }, new Point(0, 0), new Point(1, .3)) });
        var avatar = new Grid { WidthRequest = 88, HeightRequest = 88, Margin = new Thickness(14, -38, 0, 12), HorizontalOptions = LayoutOptions.Start };
        var face = new Border { Content = Icon("user", 38), StrokeThickness = 4, StrokeShape = new RoundRectangle { CornerRadius = 44 }, Shadow = new Shadow { Brush = Colors.Black, Opacity = .12f, Offset = new Point(0, 4), Radius = 8 } };
        face.SetDynamicResource(BackgroundColorProperty, "DashPill"); face.SetDynamicResource(Border.StrokeProperty, "DashSidebar");
        Watch(ApplicationGroup.Profile, () =>
        {
            if (face.Content is Image previous) previous.Source = null;
            face.Content = Icon("user", 38);
            if (reveal.IsVisible(ApplicationGroup.Profile) && reply?.Documents?.FirstOrDefault(file => file.Type.Contains("ICAO", StringComparison.OrdinalIgnoreCase)) is { } photo && ApplicationPresentation.Decode(photo) is { Mime: "image/png" or "image/jpeg" } image)
                face.Content = new Image { Source = ImageSource.FromStream(() => new MemoryStream(image.Bytes)), Aspect = Aspect.AspectFill };
        });
        avatar.Add(face); avatar.Add(IconButton("camera", "Change applicant photo", null, ApplicationPresentation.EditUnavailable)); content.Add(avatar);
        var name = new Grid { ColumnDefinitions = [new(GridLength.Star), new(30), new(30)], ColumnSpacing = 6, Margin = new Thickness(0, 2, 0, 10) };
        name.Add(Text(ApplicationPresentation.Text(r.Name), 22, true)); name.Add(IconButton("pencil", "Edit applicant name", null, ApplicationPresentation.EditUnavailable), 1); name.Add(IconButton("eye", "Profile", () => Toggle(ApplicationGroup.Profile, "Profile"), group: ApplicationGroup.Profile, toggle: true), 2); content.Add(name);
        foreach (var (icon, caption, value) in new[] { ("hash", "Application Number: ", r.Number), ("mail", "Email: ", r.Email), ("phone", "Phone: ", r.Phone) })
        {
            var row = new Grid { ColumnDefinitions = [new(15), new(GridLength.Star)], ColumnSpacing = 8, Margin = new Thickness(0, 4) };
            row.Add(Icon(icon, 15)); row.Add(icon == "hash" ? Text(caption + value, 12, muted: true) : PrivateText(ApplicationGroup.Profile, value, 12, muted: true, prefix: caption), 1); content.Add(row);
        }
        var badges = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8, Margin = new Thickness(0, 14, 0, 16) };
        for (var i = 0; i < 2; i++)
        {
            var labels = new VerticalStackLayout { Spacing = 4 }; labels.Add(Text(i == 0 ? "Account" : "Card", 10, muted: true));
            var status = Text(i == 0 ? ApplicationPresentation.Text(r.Status).ToUpperInvariant() : "NOT PROVIDED", 12, true); status.TextColor = Color.FromArgb(i == 0 ? "#008865" : "#AC6800"); labels.Add(status);
            var badge = Card(labels, 10); badge.Stroke = new SolidColorBrush(Color.FromArgb(i == 0 ? "#91D1C5" : "#E6BC8C")); badge.BackgroundColor = Color.FromArgb(i == 0 ? "#1842BA9B" : "#18D4A968"); badges.Add(badge, i);
        }
        content.Add(badges);
        var line = new BoxView { HeightRequest = 1, Margin = new Thickness(0, 0, 0, 10) }; line.SetDynamicResource(BoxView.ColorProperty, "DashPill"); content.Add(line);
        var address = new Grid { ColumnDefinitions = [new(GridLength.Star), new(30), new(30)], Margin = new Thickness(0, 0, 0, 6) }; address.Add(Text("Address", 13, true)); address.Add(IconButton("pencil", "Edit address", null, ApplicationPresentation.EditUnavailable), 1); address.Add(IconButton("eye", "Address", () => Toggle(ApplicationGroup.Address, "Address"), group: ApplicationGroup.Address, toggle: true), 2); content.Add(address);
        var valueLabel = PrivateText(ApplicationGroup.Address, r.Address, 12); valueLabel.Margin = new Thickness(0, 0, 0, 12); content.Add(valueLabel);
        var map = new Grid { HeightRequest = 112, IsClippedToBounds = true };
        map.SetDynamicResource(BackgroundColorProperty, "DashPill");
        foreach (var rotation in new[] { -16d, 11d, 55d, -64d }) { var road = new BoxView { HeightRequest = 7, Rotation = rotation, VerticalOptions = LayoutOptions.Center, InputTransparent = true }; road.SetDynamicResource(BoxView.ColorProperty, "DashSidebar"); map.Add(road); }
        map.Add(new BoxView { Color = Color.FromArgb("#B6D7A3"), WidthRequest = 38, HeightRequest = 25, CornerRadius = 5, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = 8 });
        var mapButton = Button("View Map", async () => { if (reveal.IsVisible(ApplicationGroup.Address) && session?.ProtectedWorkAllowed == true) await Launcher.Default.OpenAsync(ApplicationPresentation.MapUrl(r.Address)); });
        Watch(ApplicationGroup.Address, () => mapButton.IsEnabled = reveal.IsVisible(ApplicationGroup.Address) && !string.IsNullOrWhiteSpace(r.Address)); mapButton.HorizontalOptions = LayoutOptions.Center; mapButton.VerticalOptions = LayoutOptions.Center; mapButton.SetDynamicResource(BackgroundColorProperty, "DashSidebar"); map.Add(mapButton);
        content.Add(new Border { Content = map, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 } });
        var card = Card(content, new Thickness(15, 15, 15, 42)); sections["account"] = card; return card;
    }
    private View Files(ApplicationRecord r)
    {
        var stack = new VerticalStackLayout { Spacing = 16 }; stack.Add(Text("Biometrics and Documents", 15, true));
        var capture = new VerticalStackLayout { Spacing = 10 }; capture.Add(Text("Biometrics", 13));
#if ANDROID
        var adapter = new BlockedCaptureAdapter(ClientPlatform.Android, CaptureKind.Fingerprint);
        var panel = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(8, 20) };
        var captureButton = Button("Capture", () => { }, true); captureButton.HorizontalOptions = LayoutOptions.Center; captureButton.IsEnabled = adapter.Availability.Available; ToolTipProperties.SetText(captureButton, adapter.Availability.Reason); panel.Add(captureButton);
        var reason = Text(adapter.Availability.Reason, 10, muted: true); reason.HorizontalTextAlignment = TextAlignment.Center; panel.Add(reason);
#else
        var panel = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(8, 20) };
        var captureButton = Button("Capture", () =>
        {
#if WINDOWS
            if (session?.ProtectedWorkAllowed == true) BiometricCaptureRequested?.Invoke();
#endif
        }, true); captureButton.HorizontalOptions = LayoutOptions.Center; panel.Add(captureButton);
#endif
        var surface = Card(panel, 8); surface.SetDynamicResource(BackgroundColorProperty, "DashPill"); capture.Add(surface);
        if (!string.IsNullOrWhiteSpace(r.BiometricsRemarks)) capture.Add(Text(r.BiometricsRemarks, 12, muted: true));
        sections["biometrics"] = capture; stack.Add(capture);
        var documents = new VerticalStackLayout { Spacing = 8 };
        var header = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] }; var title = Text("Documents", 13); title.VerticalOptions = LayoutOptions.Center; header.Add(title);
        var add = Button("Add Attachment", () => { }); add.IsEnabled = false; ToolTipProperties.SetText(add, ApplicationPresentation.AttachmentUnavailable); header.Add(add, 1); documents.Add(header);
        if (reply?.DocumentError is { } error) { documents.Add(Text(error, 12, muted: true)); documents.Add(Button("Retry", () => _ = LoadAsync(number))); }
        else if (reply?.Documents is not { Count: > 0 }) documents.Add(Text("No documents attached to this application.", 12, muted: true));
        else foreach (var file in reply.Documents)
        {
            var row = new Grid { ColumnDefinitions = [new(20), new(GridLength.Star), new(30)], ColumnSpacing = 12, MinimumHeightRequest = 48 };
            row.Add(Icon("document", 20)); var label = Text(ApplicationPresentation.DocumentName(file), 14); label.VerticalOptions = LayoutOptions.Center; row.Add(label, 1); row.Add(IconButton("external", "Open " + ApplicationPresentation.DocumentName(file), () => _ = OpenDocument(file)), 2); documents.Add(row);
            var divider = new BoxView { HeightRequest = 1 }; divider.SetDynamicResource(BoxView.ColorProperty, "DashPill"); documents.Add(divider);
        }
        sections["documents"] = documents; stack.Add(documents); return Card(stack);
    }
    private async Task OpenDocument(ApplicationDocument file)
    {
        if (session?.ProtectedWorkAllowed != true) return;
        if (ApplicationPresentation.Decode(file) is not { } data)
        {
            if (Window?.Page is Page page) await page.DisplayAlert("Document preview", "A preview is not available for this document. PDF, PNG and JPEG attachments are supported.", "Close");
            return;
        }
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, "application-" + Guid.NewGuid().ToString("N") + data.Extension);
        try
        {
            await File.WriteAllBytesAsync(path, data.Bytes);
            if (session?.ProtectedWorkAllowed != true || reply?.Documents?.Contains(file) != true) { File.Delete(path); return; }
            previewFiles.Add(path);
            await Launcher.Default.OpenAsync(new OpenFileRequest(ApplicationPresentation.DocumentName(file), new ReadOnlyFile(path, data.Mime)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FeatureNotSupportedException)
        { if (Window?.Page is Page page) await page.DisplayAlert("Document preview", "Unable to open this attachment on this device.", "Close"); }
    }
    private void ArrangeColumns()
    {
        if (columns.Children.Count != 4) return;
        var count = Width >= 1200 ? 4 : Width >= 650 ? 2 : 1;
        columns.ColumnDefinitions.Clear(); columns.RowDefinitions.Clear();
        for (var i = 0; i < count; i++) columns.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < 4 / count; i++) columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var i = 0; i < 4; i++) { Grid.SetColumn((BindableObject)columns.Children[i], i % count); Grid.SetRow((BindableObject)columns.Children[i], i / count); }
    }
    private async Task ScrollTo(string id)
    {
        if (!sections.TryGetValue(id, out var target)) return;
        var parent = Parent; while (parent is not null && parent is not ScrollView) parent = parent.Parent;
        if (parent is ScrollView scroll) await scroll.ScrollToAsync(target, ScrollToPosition.Center, true);
        SemanticScreenReader.Default.Announce(id == "bio" ? "Bio Details" : id);
    }
    private async void Details(ApplicationGroup group, string title, IReadOnlyList<ApplicationField> fields)
    {
        if (reveal.DetailsAllowed(group) && session?.ProtectedWorkAllowed == true && Window?.Page is Page page)
            await page.DisplayAlert(title, string.Join("\n\n", fields.Select(field => field.Label + "\n" + reveal.DisplayField(group, field))), "Close");
    }
}
