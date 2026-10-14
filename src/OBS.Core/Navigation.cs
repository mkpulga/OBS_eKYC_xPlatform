namespace OBS.Core;

public sealed record NavigationEntry(string Title, string[] Authorities);
public static class Navigation
{
    // Exact roles.modules strings from Pages/Dashboard.cs; these are backend authorities.
    public static IReadOnlyList<NavigationEntry> Entries { get; } = [
        new("Cardholder Verification", ["PENDINGORS"]),
        new("Residence Verification", ["VRESIDENCEVERIFICATION"]),
        new("Residence Verification Status", ["VRESIDENCESTATUS"]),
        new("Initial KYC Validation", ["VINITIALKYCVALIDATION"]),
        new("Initial KYC Result", ["VINITIALKYCRESULT"]),
        new("Information Gathering", ["VINFORMATIONGATHERING"]),
        new("Cardholder Update", ["VCARDHOLDERUPDATE"]),
        new("Cardholder For Printing", ["VCARDHOLDERFORPRINTING"]),
        new("Cardholder Printed Result", ["UPLOADCARDHOLDERPRINTEDRESULT"]),
        new("ID Status", ["VISSUEDID", "VUNCLAIMEDID", "VUNPRINTEDID"]),
        new("Role Management", ["ROLEVIEW"]), new("User Access", ["VIEWUSERS"])
    ];
    public static IEnumerable<NavigationEntry> For(StaffSession session) =>
        Entries.Where(entry => entry.Authorities.Any(session.Allows));
}
