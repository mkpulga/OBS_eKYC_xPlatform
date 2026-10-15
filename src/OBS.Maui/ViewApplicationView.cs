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
    private SessionController? session;
    private IApplicationRecords records = new UnavailableApplicationRecords();
    private CancellationTokenSource? load;
    private string number = "";
    private ApplicationReply? reply;
    public event Action<NavigationEntry>? WorkflowRequested;
    public ViewApplicationView() { SizeChanged += (_, _) => ArrangeColumns(); ShowState("Search by application number to view an applicant’s complete record."); }
    public void Configure(SessionController controller, IApplicationRecords service) { session = controller; records = service; }

    public async Task LoadAsync(string applicationNumber)
    {
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
    private static Grid IconButton(string icon, string label, Action? action, string? unavailable = null)
    {
        var grid = new Grid { WidthRequest = 30, HeightRequest = 32, VerticalOptions = LayoutOptions.Center };
        var button = new Button { BackgroundColor = Colors.Transparent, Padding = 0, MinimumWidthRequest = 30, MinimumHeightRequest = 30, IsEnabled = action is not null };
        SemanticProperties.SetDescription(button, label); ToolTipProperties.SetText(button, unavailable ?? label);
        button.Clicked += (_, _) => action?.Invoke(); grid.Add(button); grid.Add(Icon(icon)); return grid;
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
    private View Heading(string title, string icon, Action view)
    {
        var grid = new Grid { ColumnDefinitions = [new(34), new(GridLength.Star), new(30)], ColumnSpacing = 10 };
        var tile = new Border { Content = Icon(icon), BackgroundColor = Color.FromArgb("#18485E98"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 11 }, HeightRequest = 34 };
        grid.Add(tile); var label = Text(title, 15, true); label.VerticalOptions = LayoutOptions.Center; grid.Add(label, 1);
        grid.Add(IconButton("eye", "View " + title, view), 2); return grid;
    }
    private static View Field(string label, string value, bool edit = false)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        stack.Add(Text(label, 11, muted: true));
        if (!edit) stack.Add(Text(ApplicationPresentation.Text(value)));
        else
        {
            var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(30)], ColumnSpacing = 3 };
            grid.Add(Text(ApplicationPresentation.Text(value))); grid.Add(IconButton("pencil", "Edit " + label, null, ApplicationPresentation.EditUnavailable), 1); stack.Add(grid);
        }
        return stack;
    }
    private void Render(ApplicationRecord r)
    {
        sections.Clear(); columns.Clear();
        var profile = Profile(r);
        var personal = new VerticalStackLayout { Spacing = 12 };
        var bio = new VerticalStackLayout { Spacing = 12 };
        var bioFields = new ApplicationField[] { new("Birth full name", r.Name), new("Birthday", r.Birthday), new("Gender", r.Gender), new("Birthplace", r.Birthplace), new("Blood type", r.BloodType) }.Concat(r.BioFields).ToArray();
        bio.Add(Heading("Bio Details", "identity", () => Details("Bio Details", bioFields)));
        bio.Add(Field("Birth full name", r.Name, true));
        bio.Add(Field("Birthday / Age / Gender", ApplicationPresentation.Join(" · ", ApplicationPresentation.Birthday(r.Birthday, DateOnly.FromDateTime(DashboardPresentation.LocalTime.DateTime)), ApplicationPresentation.Text(r.Gender))));
        bio.Add(Field("Birthplace", r.Birthplace)); bio.Add(Field("Blood type", r.BloodType));
        sections["bio"] = Card(bio); personal.Add(sections["bio"]);
        var emergency = new VerticalStackLayout { Spacing = 14 };
        emergency.Add(Heading("Emergency Contact Information", "phone", () => Details("Emergency Contact Information", [new("Name", r.EmergencyName), new("Relationship", r.EmergencyRelationship), new("Phone", r.EmergencyPhone), new("Address", r.EmergencyAddress)])));
        var person = new Grid { ColumnDefinitions = [new(54), new(GridLength.Star)], ColumnSpacing = 14 };
        var avatar = new Border { Content = Icon("user", 29), HeightRequest = 68, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 } }; avatar.SetDynamicResource(BackgroundColorProperty, "DashBackground"); person.Add(avatar);
        var contact = new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center };
        contact.Add(Text(ApplicationPresentation.Text(r.EmergencyName), 13, true)); contact.Add(Text(ApplicationPresentation.Join(" · ", ApplicationPresentation.Text(r.EmergencyPhone), r.EmergencyRelationship), 11)); contact.Add(Text(ApplicationPresentation.Text(r.EmergencyAddress), 11, muted: true)); person.Add(contact, 1);
        var contactCard = Card(person, 14); contactCard.BackgroundColor = Color.FromArgb("#28485E98"); emergency.Add(contactCard);
        sections["emergency"] = Card(emergency); personal.Add(sections["emergency"]);
        var education = new VerticalStackLayout { Spacing = 12 };
        education.Add(Heading("Educational Information", "education", () => Details("Educational Information", [new("Educational attainment", r.Attainment), new("Course", r.Course), new("School", r.School)])));
        education.Add(Field("Educational attainment", ApplicationPresentation.Join(" · ", ApplicationPresentation.Text(r.Attainment), r.Course), true)); education.Add(Field("School / Year graduated", ApplicationPresentation.Text(r.School) + " · Year not provided")); education.Add(Field("Address", ""));
        sections["education"] = Card(education); personal.Add(sections["education"]);
        var files = Files(r);
        var side = new VerticalStackLayout { Spacing = 12 };
        var actions = new VerticalStackLayout { Spacing = 12 }; actions.Add(Text("Actions", 15, true));
        var picker = new Picker { Title = "Select an action", FontSize = 13 };
        picker.Items.Add("Refresh application"); picker.Items.Add("View complete account");
        picker.SetDynamicResource(Picker.TextColorProperty, "DashText");
        if (session?.Current?.Allows("VCARDHOLDERUPDATE") == true) picker.Items.Add("Cardholder update workflow");
        picker.SelectedIndexChanged += (_, _) => { var choice = picker.SelectedIndex; if (choice < 0) return; picker.SelectedIndex = -1; if (choice == 0) _ = LoadAsync(number); else if (choice == 1) Details("Account", r.AccountFields); else if (session?.Current?.Allows("VCARDHOLDERUPDATE") == true) WorkflowRequested?.Invoke(OBS.Core.Navigation.Entries.Single(entry => entry.Title == "Cardholder Update")); };
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
        if (reply?.Documents?.FirstOrDefault(file => file.Type.Contains("ICAO", StringComparison.OrdinalIgnoreCase)) is { } photo && ApplicationPresentation.Decode(photo) is { Mime: "image/png" or "image/jpeg" } image)
            face.Content = new Image { Source = ImageSource.FromStream(() => new MemoryStream(image.Bytes)), Aspect = Aspect.AspectFill };
        avatar.Add(face); avatar.Add(IconButton("camera", "Change applicant photo", null, ApplicationPresentation.EditUnavailable)); content.Add(avatar);
        var name = new Grid { ColumnDefinitions = [new(GridLength.Star), new(30)], ColumnSpacing = 6, Margin = new Thickness(0, 2, 0, 10) };
        name.Add(Text(ApplicationPresentation.Text(r.Name), 22, true)); name.Add(IconButton("pencil", "Edit applicant name", null, ApplicationPresentation.EditUnavailable), 1); content.Add(name);
        foreach (var (icon, value) in new[] { ("hash", "Application Number: " + r.Number), ("mail", "Email: " + ApplicationPresentation.Text(r.Email)), ("phone", "Phone: " + ApplicationPresentation.Text(r.Phone)) })
        {
            var row = new Grid { ColumnDefinitions = [new(15), new(GridLength.Star)], ColumnSpacing = 8, Margin = new Thickness(0, 4) };
            row.Add(Icon(icon, 15)); row.Add(Text(value, 12, muted: true), 1); content.Add(row);
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
        var address = new Grid { ColumnDefinitions = [new(GridLength.Star), new(30)], Margin = new Thickness(0, 0, 0, 6) }; address.Add(Text("Address", 13, true)); address.Add(IconButton("pencil", "Edit address", null, ApplicationPresentation.EditUnavailable), 1); content.Add(address);
        var valueLabel = Text(ApplicationPresentation.Text(r.Address), 12); valueLabel.Margin = new Thickness(0, 0, 0, 12); content.Add(valueLabel);
        var map = new Grid { HeightRequest = 112, IsClippedToBounds = true };
        map.SetDynamicResource(BackgroundColorProperty, "DashPill");
        foreach (var rotation in new[] { -16d, 11d, 55d, -64d }) { var road = new BoxView { HeightRequest = 7, Rotation = rotation, VerticalOptions = LayoutOptions.Center, InputTransparent = true }; road.SetDynamicResource(BoxView.ColorProperty, "DashSidebar"); map.Add(road); }
        map.Add(new BoxView { Color = Color.FromArgb("#B6D7A3"), WidthRequest = 38, HeightRequest = 25, CornerRadius = 5, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = 8 });
        var mapButton = Button("View Map", async () => { if (session?.ProtectedWorkAllowed == true) await Launcher.Default.OpenAsync(ApplicationPresentation.MapUrl(r.Address)); });
        mapButton.IsEnabled = !string.IsNullOrWhiteSpace(r.Address); mapButton.HorizontalOptions = LayoutOptions.Center; mapButton.VerticalOptions = LayoutOptions.Center; mapButton.SetDynamicResource(BackgroundColorProperty, "DashSidebar"); map.Add(mapButton);
        content.Add(new Border { Content = map, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 } });
        var card = Card(content, new Thickness(15, 15, 15, 42)); sections["account"] = card; return card;
    }
    private View Files(ApplicationRecord r)
    {
        var stack = new VerticalStackLayout { Spacing = 16 }; stack.Add(Text("Biometrics and Documents", 15, true));
        var capture = new VerticalStackLayout { Spacing = 10 }; capture.Add(Text("Biometrics", 13));
#if ANDROID
        var adapter = new BlockedCaptureAdapter(ClientPlatform.Android, CaptureKind.Fingerprint);
#else
        var adapter = new BlockedCaptureAdapter(ClientPlatform.Windows, CaptureKind.Fingerprint);
#endif
        var panel = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(8, 20) };
        var captureButton = Button("Capture", () => { }, true); captureButton.HorizontalOptions = LayoutOptions.Center; captureButton.IsEnabled = adapter.Availability.Available; ToolTipProperties.SetText(captureButton, adapter.Availability.Reason); panel.Add(captureButton);
        var reason = Text(adapter.Availability.Reason, 10, muted: true); reason.HorizontalTextAlignment = TextAlignment.Center; panel.Add(reason);
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
    private async void Details(string title, IReadOnlyList<ApplicationField> fields)
    {
        if (session?.ProtectedWorkAllowed == true && Window?.Page is Page page)
            await page.DisplayAlert(title, string.Join("\n\n", fields.Select(field => field.Label + "\n" + ApplicationPresentation.Text(field.Value))), "Close");
    }
}
