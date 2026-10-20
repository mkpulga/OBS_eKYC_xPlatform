namespace OBS.Core;

// Citizen IDs are opaque strings, independent of application/reference numbers.
public static class CitizenId
{
    public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value);
    public static string Encode(string value)
    {
        if (!IsValid(value)) throw new ArgumentException("Enter a Citizen ID.", nameof(value));
        var encoded = Uri.EscapeDataString(value);
        return encoded is "." or ".." ? encoded.Replace(".", "%2E") : encoded;
    }
    internal static bool IsEncodedSegment(string value)
    {
        try { return IsValid(Uri.UnescapeDataString(value)) && Encode(Uri.UnescapeDataString(value)) == value; }
        catch (UriFormatException) { return false; }
    }
}
