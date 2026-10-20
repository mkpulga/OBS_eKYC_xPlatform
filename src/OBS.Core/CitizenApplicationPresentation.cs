namespace OBS.Core;

public static class CitizenApplicationPresentation
{
    public static ApplicationRecord? Record(CitizenApplicationSearch search)
    {
        if (search.Snapshot is not { } snapshot) return null;
        var identity = snapshot.Identity;
        var state = search.Privacy;
        IReadOnlyList<ApplicationField> Fields(PrivacySection section) => state?.Fields(section, CitizenSearchPresentation.Missing) ?? [];
        string Value(PrivacySection section, string label) => Fields(section).FirstOrDefault(f => f.Label == label)?.Value ?? "";
        string Address() => ApplicationPresentation.Join(", ", Fields(PrivacySection.Address)
            .Where(f => f.Label.StartsWith("Address 1 ·", StringComparison.Ordinal) &&
                f.Value != CitizenSearchPresentation.Missing).Select(f => f.Value).ToArray());
        var source = search.Source is { } selected ? CitizenSearchPresentation.Label(selected) : "";
        return new()
        {
            Number = identity.ReferenceNumber ?? "",
            Name = ApplicationPresentation.Join(" ", identity.FirstName, identity.MiddleName, identity.LastName, identity.Suffix) is { Length: > 0 } name ? name : identity.DisplayName ?? "",
            Email = snapshot.Contacts.FirstOrDefault(c => c.Type == "EMAIL_ADDRESS")?.Value ?? "",
            // EWALLET and unrelated identifiers are not assumed to be phone numbers.
            Phone = snapshot.Contacts.FirstOrDefault(c => c.Type == "MOBILE_NUMBER")?.Value ?? "",
            Status = identity.Status ?? "", Address = Address(), Birthday = Value(PrivacySection.Bio, "Date of birth"),
            Gender = Value(PrivacySection.Bio, "Gender"), Birthplace = Value(PrivacySection.Bio, "Birthplace"), BloodType = Value(PrivacySection.Bio, "Blood type"),
            EmergencyName = Value(PrivacySection.Emergency, "Contact 1 · Name"), EmergencyPhone = Value(PrivacySection.Emergency, "Contact 1 · Phone"),
            EmergencyRelationship = Value(PrivacySection.Emergency, "Contact 1 · Relationship"),
            Attainment = Value(PrivacySection.Education, "Education 1 · Attainment"), School = Value(PrivacySection.Education, "Education 1 · School"),
            Course = Value(PrivacySection.Education, "Education 1 · Course"), BioFields = Fields(PrivacySection.Bio),
            AccountFields = new ApplicationField[] { new("Search source", source), new("Citizen ID", search.CitizenId),
                new("Application number", identity.ReferenceNumber ?? ""), new("Status", identity.Status ?? ""),
                new("Last module", snapshot.LastModuleUpdated.Module ?? ""), new("Last modified", snapshot.LastModuleUpdated.Timestamp ?? "") }
                .Concat(snapshot.Contacts.Select(c => new ApplicationField(c.Type ?? "Contact", c.Value ?? "")))
                .Concat(snapshot.Classifications.Select(c => new ApplicationField("Classification", c ?? ""))).ToArray()
        };
    }
    public static PrivacySection? Section(ApplicationGroup group) => group switch
    {
        ApplicationGroup.Bio => PrivacySection.Bio, ApplicationGroup.Address => PrivacySection.Address,
        ApplicationGroup.Emergency => PrivacySection.Emergency, ApplicationGroup.Education => PrivacySection.Education, _ => null
    };
}
