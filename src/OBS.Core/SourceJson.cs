using System.Text.Json;

namespace OBS.Core;

// Preserve the original Json.NET field-name matching without adding undocumented token aliases.
internal static class SourceJson
{
    public static JsonSerializerOptions Options { get; } = new() { PropertyNameCaseInsensitive = true };

    public static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        var found = false;
        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            // Conflicting case variants aren't a trustworthy token/envelope field.
            if (found) { value = default; return false; }
            value = property.Value; found = true;
        }
        return found;
    }
}
