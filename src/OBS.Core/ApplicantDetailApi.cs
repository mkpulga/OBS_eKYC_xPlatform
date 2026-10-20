using System.Text.Json;

namespace OBS.Core;

public sealed record ApplicantIdentity(string CitizenId, string? ReferenceNumber, string? FirstName, string? MiddleName,
    string? LastName, string? Suffix, string? DisplayName, string? AccountType, string? Status, string? CitizenStatus)
{
    public override string ToString() => "[applicant identity redacted]";
}
public sealed record ApplicantContact(string? Type, string? Value)
{
    public override string ToString() => "[applicant contact redacted]";
}
public sealed record ApplicantModuleUpdate(string? Module, string? Timestamp)
{
    // The observed timestamp has no timezone. Retain text, do not assume UTC/local time.
    public override string ToString() => "[applicant module update redacted]";
}
public sealed class ApplicantDetailSnapshot
{
    public ApplicantIdentity Identity { get; }
    public IReadOnlyList<ApplicantContact> Contacts { get; }
    public IReadOnlyList<string?> Classifications { get; }
    public ApplicantModuleUpdate LastModuleUpdated { get; }
    public bool ServerRequiresPrivacyVerification { get; }
    public IReadOnlyList<PrivacySection> EffectiveSections { get; }
    // Null means withheld; empty collection means authorized but no rows returned.
    public ApplicantBio? Bio { get; }
    public IReadOnlyList<ApplicantAddress>? Addresses { get; }
    public IReadOnlyList<ApplicantEmergency>? EmergencyContacts { get; }
    public IReadOnlyList<ApplicantEducation>? Education { get; }
    internal ApplicantDetailSnapshot(ApplicantIdentity identity, ApplicantContact[] contacts, string?[] classifications,
        ApplicantModuleUpdate lastModuleUpdated, bool required, PrivacySection[] effective,
        ApplicantBio? bio, IReadOnlyList<ApplicantAddress>? addresses, IReadOnlyList<ApplicantEmergency>? emergency, IReadOnlyList<ApplicantEducation>? education)
    {
        Identity = identity; Contacts = Array.AsReadOnly(contacts); Classifications = Array.AsReadOnly(classifications);
        LastModuleUpdated = lastModuleUpdated; ServerRequiresPrivacyVerification = required; EffectiveSections = Array.AsReadOnly(effective);
        Bio = bio; Addresses = addresses; EmergencyContacts = emergency; Education = education;
    }
    public override string ToString() => "[applicant detail redacted]";
}

internal static class ApplicantDetailJson
{
    internal static ApplicantDetailSnapshot Parse(ApiResult result, string id, PrivacyVerification? verification, DateTimeOffset now)
    {
        var row = PrivacyResults.SingleData(result);
        var required = Boolean(row, "piiVerificationRequired");
        if (!SourceJson.TryGetProperty(row, "piiAuthorizedSections", out var flags) || flags.ValueKind != JsonValueKind.Object) throw new JsonException();
        var serverSections = new List<PrivacySection>();
        foreach (var (section, claim) in new[] { (PrivacySection.Bio, "canViewBioDetails"), (PrivacySection.Address, "canViewAddress"),
            (PrivacySection.Emergency, "canViewEmergencyContact"), (PrivacySection.Education, "canViewEducationalInformation") })
            if (Boolean(flags, claim)) serverSections.Add(section);
        if (required && serverSections.Count != 0) throw new JsonException();
        var effective = serverSections.Where(section => verification?.Allows(section, id, now) == true).ToArray();
        var personal = ProtectedApplicantJson.Rows(result, "personal-info");
        if (personal.GetArrayLength() != 1) throw new JsonException();
        var person = personal[0];
        var identity = new ApplicantIdentity(ProtectedApplicantJson.Id(person, id), Text(person, "referencenumber"), Text(person, "firstname"),
            Text(person, "middlename"), Text(person, "lastname"), Text(person, "suffix"), Text(person, "displayname"), Text(person, "accounttype"),
            Text(person, "status"), Text(person, "citizenstatus"));
        var contacts = ProtectedApplicantJson.Rows(result, "contact").EnumerateArray().Select(contact =>
        {
            _ = ProtectedApplicantJson.Id(contact, id);
            return new ApplicantContact(Text(contact, "type"), Text(contact, "value"));
        }).ToArray();
        var classifications = ProtectedApplicantJson.Rows(result, "account-classification").EnumerateArray().Select(item =>
        { _ = ProtectedApplicantJson.Id(item, id); return Text(item, "type"); }).ToArray();
        if (!SourceJson.TryGetProperty(row, "last-module-updated", out var module)) throw new JsonException();
        _ = ProtectedApplicantJson.Id(module, id);
        var lastModule = new ApplicantModuleUpdate(Text(module, "module"), Text(module, "timestamp"));
        // Shape checks never convert an unauthorized empty array into authoritative "no data".
        foreach (var name in new[] { "address", "emergency", "other-info" }) _ = ProtectedApplicantJson.Rows(result, name);
        return new(identity, contacts, classifications, lastModule, required, effective,
            effective.Contains(PrivacySection.Bio) ? ProtectedApplicantJson.Bio(result, id) : null,
            effective.Contains(PrivacySection.Address) ? ProtectedApplicantJson.Addresses(result, id) : null,
            effective.Contains(PrivacySection.Emergency) ? ProtectedApplicantJson.Emergency(result, id) : null,
            effective.Contains(PrivacySection.Education) ? ProtectedApplicantJson.Education(result, id) : null);
    }
    private static string? Text(JsonElement row, string name) => ProtectedApplicantJson.Text(row, name);
    private static bool Boolean(JsonElement row, string name)
    {
        if (!SourceJson.TryGetProperty(row, name, out var field) || field.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new JsonException();
        return field.GetBoolean();
    }
}
