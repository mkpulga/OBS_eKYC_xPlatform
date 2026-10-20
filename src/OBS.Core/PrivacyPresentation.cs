namespace OBS.Core;

public sealed record PrivacySectionView(PrivacySection Section, string Title, string Icon);

public static class PrivacyPresentation
{
    public static IReadOnlyList<PrivacySectionView> Sections { get; } = Array.AsReadOnly<PrivacySectionView>([
        new(PrivacySection.Bio, "Bio Details", "identity"), new(PrivacySection.Address, "Address", "home"),
        new(PrivacySection.Emergency, "Emergency Contact", "phone"), new(PrivacySection.Education, "Educational Information", "education")]);

    // Only currently visible controller snapshots become display fields. Never log these
    // transient fields or retain them beyond the current render/update of a screen.
    public static IReadOnlyList<ApplicationField> Fields(ApplicantPrivacyController state, PrivacySection section)
    {
        var fields = new List<ApplicationField>();
        void Add(string label, string? value) => fields.Add(new(label, ApplicationPresentation.Text(value)));
        switch (section)
        {
            case PrivacySection.Bio when state.Bio is { } bio:
                Add("Full name", ApplicationPresentation.Join(" ", bio.FirstName, bio.MiddleName, bio.LastName, bio.Suffix));
                Add("Display name", bio.DisplayName); Add("Date of birth", bio.DateOfBirth); Add("Gender", bio.Gender);
                Add("Birthplace", ApplicationPresentation.Join(", ", bio.BirthCity, bio.BirthProvince, bio.BirthCountry));
                Add("Blood type", bio.BloodType); Add("Civil status", bio.CivilStatus); Add("Nationality", bio.Nationality);
                Add("Account type", bio.AccountType); Add("Tenurial status", bio.TenurialStatus);
                Add("Account status", bio.Status); Add("Application stage", bio.CitizenStatus); break;
            case PrivacySection.Address when state.Addresses is { } addresses:
                for (var i = 0; i < addresses.Count; i++)
                {
                    var row = addresses[i];
                    var prefix = $"Address {i + 1} · {ApplicationPresentation.Text(row.AddressType)}";
                    Add(prefix + " · Specific address", row.SpecificAddress); Add(prefix + " · Building", row.BuildingName);
                    Add(prefix + " · Address line 1", row.AddressLine1); Add(prefix + " · Address line 2", row.AddressLine2);
                    Add(prefix + " · Barangay", row.Barangay); Add(prefix + " · City", row.City); Add(prefix + " · Province", row.Province);
                    Add(prefix + " · Region", row.Region); Add(prefix + " · Country", row.Country); Add(prefix + " · ZIP code", row.ZipCode);
                }
                break;
            case PrivacySection.Emergency when state.EmergencyContacts is { } contacts:
                for (var i = 0; i < contacts.Count; i++)
                {
                    var row = contacts[i]; var prefix = $"Contact {i + 1}";
                    Add(prefix + " · Name", ApplicationPresentation.Join(" ", row.FirstName, row.MiddleName, row.LastName, row.Suffix));
                    Add(prefix + " · Relationship", row.Relationship); Add(prefix + " · Phone", row.ContactNumber);
                }
                break;
            case PrivacySection.Education when state.Education is { } education:
                for (var i = 0; i < education.Count; i++)
                {
                    var row = education[i]; var prefix = $"Education {i + 1}";
                    Add(prefix + " · Attainment", row.EducationalAttainment); Add(prefix + " · School", row.School);
                    Add(prefix + " · Course", row.Course); Add(prefix + " · Department", row.Department);
                    Add(prefix + " · Occupation", row.Occupation); Add(prefix + " · Company", row.Company);
                    Add(prefix + " · Source of income", row.SourceOfIncome); Add(prefix + " · Nature of work", row.NatureOfWork);
                }
                break;
        }
        return state.IsVisible(section) ? fields.AsReadOnly() : [];
    }
}
