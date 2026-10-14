using System.Globalization;

namespace OBS.Core;

public enum DashboardPeriod { Day, Week, Month, Year }
public sealed record DashboardActivity(string Label, int Completed, int InProgress);
public sealed record DashboardMetric(string Label, int Value, string Icon, string Accent);
public sealed record DashboardModule(NavigationEntry Entry, string Label, string Group, string Icon);

// Deliberately synthetic until a reporting contract is available. Both clients use the same fixtures.
public static class DashboardSample
{
    public static IReadOnlyList<DashboardMetric> Portfolio { get; } = Array.AsReadOnly<DashboardMetric>([
        new("Accounts Onboarded", 12480, "users", "mint"),
        new("Cards Requested", 6240, "card", "blue"),
        new("Cards Released", 3120, "verified", "orange")]);
    public static IReadOnlyList<DashboardMetric> Queues { get; } = Array.AsReadOnly<DashboardMetric>([
        new("Card Holder Verification", 128, "identity", "mint"),
        new("Scheduled Homevisits", 34, "home", "orange"),
        new("For Card Requests", 76, "printer", "purple")]);
    public static int ReleasePercent => (int)Math.Round(100d * Portfolio[2].Value / Portfolio[1].Value);
    public static IReadOnlyList<DashboardPeriod> Periods { get; } = Array.AsReadOnly(Enum.GetValues<DashboardPeriod>());
    public static string Label(DashboardPeriod period) => period switch
    {
        DashboardPeriod.Day => "1D", DashboardPeriod.Week => "1W", DashboardPeriod.Month => "1MO", _ => "1YR"
    };
    public static string Description(DashboardPeriod period) => period switch
    {
        DashboardPeriod.Day => "Past day", DashboardPeriod.Week => "Past week", DashboardPeriod.Month => "Past month", _ => "Past year"
    };
    public static int AxisMaximum(DashboardPeriod period) => period switch
    {
        DashboardPeriod.Day => 12, DashboardPeriod.Week => 40, DashboardPeriod.Month => 100, _ => 400
    };
    public static IReadOnlyList<DashboardActivity> Activity(DashboardPeriod period) => period switch
    {
        DashboardPeriod.Day => [new("Accounts", 7, 3), new("Home Visits", 5, 4), new("Biometrics", 3, 2), new("Documents", 4, 2), new("Cards", 3, 1)],
        DashboardPeriod.Week => [new("Accounts", 24, 12), new("Home Visits", 18, 14), new("Biometrics", 12, 5), new("Documents", 16, 7), new("Cards", 10, 5)],
        DashboardPeriod.Month => [new("Accounts", 60, 30), new("Home Visits", 48, 36), new("Biometrics", 28, 15), new("Documents", 35, 18), new("Cards", 24, 12)],
        _ => [new("Accounts", 180, 120), new("Home Visits", 160, 140), new("Biometrics", 90, 45), new("Documents", 90, 45), new("Cards", 90, 45)]
    };
    public static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}

public static class DashboardPresentation
{
    // Presentation only: existing Navigation entries remain the source of authorization.
    public static DashboardModule Module(NavigationEntry entry) => entry.Title switch
    {
        "Cardholder Verification" => new(entry, "Verification · Card Holder", "IDENTITY", "identity"),
        "Residence Verification" => new(entry, "Verification · Residence", "IDENTITY", "home"),
        "Residence Verification Status" => new(entry, "Residence · Results", "IDENTITY", "list"),
        "Initial KYC Validation" => new(entry, "KYC · Validation", "IDENTITY", "verified"),
        "Initial KYC Result" => new(entry, "KYC · Results", "IDENTITY", "list"),
        "Information Gathering" => new(entry, "Information Gathering", "IDENTITY", "users"),
        "Cardholder Update" => new(entry, "Cardholder · For Update", "RECORDS", "edit"),
        "Cardholder For Printing" => new(entry, "Cardholder · For Printing", "RECORDS", "printer"),
        "Cardholder Printed Result" => new(entry, "Cardholder · Results", "RECORDS", "clipboard"),
        "ID Status" => new(entry, "ID Status", "RECORDS", "card"),
        _ => new(entry, entry.Title, "ADMINISTRATION", "users")
    };
    public static string Initials(string email)
    {
        var name = email.Split('@')[0].Split(['.', '_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);
        return name.Length switch { 0 => "ST", 1 => name[0][..Math.Min(2, name[0].Length)].ToUpperInvariant(), _ => $"{name[0][0]}{name[^1][0]}".ToUpperInvariant() };
    }
    public static DateTimeOffset LocalTime => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
    public static string IconPath(string icon) => icon switch
    {
        "menu" => "M4 6H20 M4 12H20 M4 18H20",
        "dashboard" => "M3 3H10V10H3Z M14 3H21V7H14Z M14 11H21V21H14Z M3 14H10V21H3Z",
        "users" => "M15 21V18C15 15 3 15 3 18V21 M9 3A4 4 0 1 1 9 11A4 4 0 1 1 9 3 M16 4C20 4 20 10 16 10 M18 15C21 15 22 17 22 20",
        "identity" => "M3 5H21V19H3Z M8 8A2 2 0 1 1 8 12A2 2 0 1 1 8 8 M5 16C5 13 11 13 11 16 M15 9H18 M15 13H18",
        "card" => "M3 5H21V19H3Z M3 10H21 M6 15H11",
        "home" => "M3 10L12 3L21 10V21H15V14H9V21H3Z",
        "verified" => "M12 2L16 4L20 5L21 10L22 13L19 17L17 21L12 22L8 20L4 19L3 14L2 10L5 6L7 3Z M8 12L11 15L16 9",
        "list" => "M3 5L5 7L8 3 M11 5H21 M3 12L5 14L8 10 M11 12H21 M3 19L5 21L8 17 M11 19H21",
        "edit" => "M14 3H5V21H18V14 M14 3V8H19 M10 15L18 7L21 10L13 18L9 19Z",
        "printer" => "M6 8V3H18V8 M6 17H3V9H21V17H18 M6 14H18V22H6Z M17 11H18",
        "clipboard" => "M8 4H4V22H20V4H16 M8 2H16V6H8Z M8 11H9 M12 11H16 M8 16H9 M12 16H16",
        "search" => "M10 3A7 7 0 1 1 10 17A7 7 0 1 1 10 3 M15 15L21 21",
        "filter" => "M3 5H21 M6 12H18 M10 19H14",
        "qr" => "M3 3H9V9H3Z M15 3H21V9H15Z M3 15H9V21H3Z M15 15H18V18H21V21H15Z M12 3V5 M12 9V12H18 M3 12H8 M12 16V21 M21 12V14",
        "bell" => "M5 17V10C5 2 19 2 19 10V17L21 19H3Z M10 22H14",
        "moon" => "M20 15A9 9 0 1 1 9 3A7 7 0 0 0 20 15Z",
        "sun" => "M12 7A5 5 0 1 1 12 17A5 5 0 1 1 12 7 M12 1V3 M12 21V23 M1 12H3 M21 12H23 M4 4L6 6 M18 18L20 20 M4 20L6 18 M18 6L20 4",
        "logout" => "M9 3H3V21H9 M8 12H22 M17 7L22 12L17 17",
        "lock" => "M6 10V7C6 0 18 0 18 7V10 M4 10H20V22H4Z M12 14V18",
        _ => "M12 3A9 9 0 1 1 12 21A9 9 0 1 1 12 3"
    };
}
