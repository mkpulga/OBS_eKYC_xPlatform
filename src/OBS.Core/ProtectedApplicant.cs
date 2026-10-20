using System.Globalization;
using System.Text.Json;

namespace OBS.Core;

// Minimal display models for the observed protected responses. No record IDs, account
// numbers, security answers or workflow decisions are inferred from unrelated fields.
public sealed record ApplicantBio(string CitizenId, string? FirstName, string? MiddleName, string? LastName, string? Suffix,
    string? DisplayName, string? DateOfBirth, string? Gender, string? BloodType, string? CivilStatus, string? Nationality,
    string? AccountType, string? BirthCity, string? BirthProvince, string? BirthCountry, string? TenurialStatus,
    string? Status, string? CitizenStatus)
{
    // DateOfBirth remains server text: the observed slash-separated format is ambiguous.
    public override string ToString() => "[applicant bio redacted]";
}
public sealed record ApplicantAddress(string CitizenId, string? AddressType, string? SpecificAddress, string? BuildingName,
    string? AddressLine1, string? AddressLine2, string? Barangay, string? City, string? Province, string? Region, string? Country, string? ZipCode)
{
    public override string ToString() => "[applicant address redacted]";
}
public sealed record ApplicantEmergency(string CitizenId, string? FirstName, string? MiddleName, string? LastName, string? Suffix,
    string? Relationship, string? ContactNumber)
{
    public override string ToString() => "[applicant emergency contact redacted]";
}
public sealed record ApplicantEducation(string CitizenId, string? EducationalAttainment, string? School, string? Course,
    string? Department, string? Occupation, string? Company, string? SourceOfIncome, string? NatureOfWork)
{
    public override string ToString() => "[applicant education redacted]";
}

internal static class ProtectedApplicantJson
{
    internal static ApplicantBio Bio(ApiResult result, string id)
    {
        var rows = Rows(result, "personal-info");
        if (rows.GetArrayLength() != 1) throw new JsonException();
        var row = rows[0];
        return new(Id(row, id), Text(row, "firstname"), Text(row, "middlename"), Text(row, "lastname"), Text(row, "suffix"),
            Text(row, "displayname"), Text(row, "dateofbirth"), Text(row, "gender"), Text(row, "bloodtype"), Text(row, "civilstatus"),
            Text(row, "nationality"), Text(row, "accounttype"), Text(row, "placeofbirthcity"), Text(row, "placeofbirthprovince"),
            Text(row, "placeofbirthcountry"), Text(row, "tenurialstatus"), Text(row, "status"), Text(row, "citizenstatus"));
    }
    internal static IReadOnlyList<ApplicantAddress> Addresses(ApiResult result, string id) => Array.AsReadOnly(
        Rows(result, "address").EnumerateArray().Select(row => new ApplicantAddress(Id(row, id), Text(row, "addresstype"),
            Text(row, "specificaddress"), Text(row, "buildingname"), Text(row, "addressline1"), Text(row, "addressline2"),
            Text(row, "barangay"), Text(row, "city"), Text(row, "province"), Text(row, "region"), Text(row, "country"), Text(row, "zipcode"))).ToArray());
    internal static IReadOnlyList<ApplicantEmergency> Emergency(ApiResult result, string id) => Array.AsReadOnly(
        Rows(result, "emergency").EnumerateArray().Select(row => new ApplicantEmergency(Id(row, id), Text(row, "firstname"),
            Text(row, "middlename"), Text(row, "lastname"), Text(row, "suffix"), Text(row, "relationship"), Text(row, "contactnumber"))).ToArray());
    internal static IReadOnlyList<ApplicantEducation> Education(ApiResult result, string id) => Array.AsReadOnly(
        Rows(result, "other-info").EnumerateArray().Select(row => new ApplicantEducation(Id(row, id), Text(row, "educationalattainment"),
            Text(row, "school"), Text(row, "course"), Text(row, "department"), Text(row, "occupation"), Text(row, "company"),
            Text(row, "sourceofincome"), Text(row, "natureofwork"))).ToArray());
    internal static JsonElement Rows(ApiResult result, string name)
    {
        if (!SourceJson.TryGetProperty(PrivacyResults.SingleData(result), name, out var rows) || rows.ValueKind != JsonValueKind.Array)
            throw new JsonException();
        return rows;
    }
    internal static string Id(JsonElement row, string expected)
    {
        // Observed citizenid is an integer; status applicationId is a canonical string.
        if (!SourceJson.TryGetProperty(row, "citizenid", out var id) || id.ValueKind != JsonValueKind.Number ||
            !id.TryGetInt64(out var number) || number < 0) throw new JsonException();
        var value = number.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(value, expected, StringComparison.Ordinal)) throw new JsonException();
        return value;
    }
    internal static string? Text(JsonElement row, string name)
    {
        // A missing field differs from a server-declared null. Do not silently invent it.
        if (!SourceJson.TryGetProperty(row, name, out var value)) throw new JsonException();
        return value.ValueKind switch { JsonValueKind.String => value.GetString(), JsonValueKind.Null => null, _ => throw new JsonException() };
    }
}
