namespace OBS.Core;

public enum ApplicationGroup { Profile, Bio, Emergency, Education, Address }

public sealed class ApplicationRevealState
{
    public const string Mask = "••••••••";
    private readonly HashSet<ApplicationGroup> visible = [];
    public bool IsVisible(ApplicationGroup group) => visible.Contains(group);
    public void Toggle(ApplicationGroup group) { if (!visible.Remove(group)) visible.Add(group); }
    public void Reset() => visible.Clear();
    public bool DetailsAllowed(ApplicationGroup group) => IsVisible(group);
    public string Display(ApplicationGroup group, string? value) => IsVisible(group) ? ApplicationPresentation.Text(value) : Mask;
    public string DisplayField(ApplicationGroup group, ApplicationField field) => Display(
        group == ApplicationGroup.Profile && field.Label.Contains("address", StringComparison.OrdinalIgnoreCase) ? ApplicationGroup.Address : group, field.Value);
}
