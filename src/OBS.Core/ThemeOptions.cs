namespace OBS.Core;

public sealed record ThemeOptions
{
    public string ProductName { get; init; } = "OBS_eKYC";
    public string CityName { get; init; } = "StandAlone";
    public string Navigation { get; init; } = "#17324D";
    public string Action { get; init; } = "#007F82";
    public string Background { get; init; } = "#F4F6F8";
    public string Text { get; init; } = "#202B36";
    public static ThemeOptions ReadIni(string content)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in content.Split('\n'))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('[') || text.StartsWith(';') || text.StartsWith('#')) continue;
            var index = text.IndexOf('=');
            if (index <= 0 || !settings.TryAdd(text[..index].Trim(), text[(index + 1)..].Trim())) throw new ArgumentException("Invalid theme setting.");
        }
        var defaults = new ThemeOptions();
        var theme = new ThemeOptions { ProductName = settings.GetValueOrDefault("productName", defaults.ProductName),
            CityName = settings.GetValueOrDefault("cityName", defaults.CityName), Navigation = settings.GetValueOrDefault("navigation", defaults.Navigation),
            Action = settings.GetValueOrDefault("action", defaults.Action), Background = settings.GetValueOrDefault("background", defaults.Background),
            Text = settings.GetValueOrDefault("text", defaults.Text) };
        theme.Validate(); return theme;
    }
    public void Validate()
    {
        foreach (var color in new[] { Navigation, Action, Background, Text })
            if (color.Length != 7 || color[0] != '#' || color[1..].Any(c => !Uri.IsHexDigit(c)))
                throw new ArgumentException("Theme colors must be #RRGGBB values.");
    }
}
