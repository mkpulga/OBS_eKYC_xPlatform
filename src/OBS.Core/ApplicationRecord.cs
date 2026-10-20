using System.Globalization;
using System.Net;
using System.Text.Json;

namespace OBS.Core;

public enum ApplicationOutcome { Success, NotFound, NotPermitted, InvalidNumber, ConnectionFailure, InvalidResponse, Rejected, Unavailable }
public sealed record ApplicationField(string Label, string Value);

// Source: cardholderVerificationSpecificDeserializeObject and documents in the original client.
// Only display fields are retained; contact security answers and other authentication data are excluded.
public sealed class ApplicationRecord
{
    public string Number { get; init; } = "";
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Status { get; init; } = "";
    public string Address { get; init; } = "";
    public string Birthday { get; init; } = "";
    public string Gender { get; init; } = "";
    public string Birthplace { get; init; } = "";
    public string BloodType { get; init; } = "";
    public string EmergencyName { get; init; } = "";
    public string EmergencyPhone { get; init; } = "";
    public string EmergencyRelationship { get; init; } = "";
    public string EmergencyAddress { get; init; } = "";
    public string Attainment { get; init; } = "";
    public string School { get; init; } = "";
    public string Course { get; init; } = "";
    public string BiometricsRemarks { get; init; } = "";
    public IReadOnlyList<ApplicationField> BioFields { get; init; } = [];
    public IReadOnlyList<ApplicationField> AccountFields { get; init; } = [];
    public override string ToString() => "[application record redacted]";
}

public sealed class ApplicationDocument
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Value { get; init; } = "";
    public override string ToString() => "[application document redacted]";
}
public sealed record ApplicationReply(ApplicationOutcome Outcome, ApplicationRecord? Record = null,
    IReadOnlyList<ApplicationDocument>? Documents = null, string? DocumentError = null);

public interface IApplicationRecords
{
    Task<ApplicationReply> FindAsync(StaffSession session, string number, CancellationToken token);
}
public sealed class UnavailableApplicationRecords : IApplicationRecords
{
    public Task<ApplicationReply> FindAsync(StaffSession session, string number, CancellationToken token) =>
        Task.FromResult(new ApplicationReply(ApplicationOutcome.Unavailable));
}

public sealed class ApplicationRecordsService(ApiTransport api) : IApplicationRecords
{
    public async Task<ApplicationReply> FindAsync(StaffSession session, string number, CancellationToken token)
    {
        if (!session.Allows(CardholderVerificationService.Authority)) return new(ApplicationOutcome.NotPermitted);
        number = number.Trim();
        if (!ApplicationPresentation.ValidNumber(number)) return new(ApplicationOutcome.InvalidNumber);
        var security = new RequestSecurity { Bearer = session.Tokens.Access };
        var result = await api.GetAsync("cardholder/pending/" + Uri.EscapeDataString(number), security, token);
        if (result.HttpStatus == HttpStatusCode.NotFound) return new(ApplicationOutcome.NotFound);
        // Owner-confirmed selected-server behavior: this specific lookup uses HTTP 401
        // with business 2004 for no matching pending application. Do not generalize this
        // exception to other 401 responses, endpoints, or the global staff session.
        if (api.Contract == ApiContract.Privacy &&
            result is { Failure: ApiFailure.None, HttpStatus: HttpStatusCode.Unauthorized, BusinessCode: "2004" })
            return new(ApplicationOutcome.NotFound);
        if (Failure(result) is { } failure) return new(failure);
        try
        {
            var rows = Rows(result);
            if (rows.GetArrayLength() == 0) return new(ApplicationOutcome.NotFound);
            // A specific route must resolve exactly one record, never silently choose the first.
            if (rows.GetArrayLength() != 1) return new(ApplicationOutcome.InvalidResponse);
            var record = Parse(rows[0]);
            if (!string.Equals(record.Number, number, StringComparison.OrdinalIgnoreCase)) return new(ApplicationOutcome.InvalidResponse);
            var documents = await api.GetAsync($"documents/pending/{Uri.EscapeDataString(number)}/ALL", security, token);
            if (token.IsCancellationRequested) return new(ApplicationOutcome.ConnectionFailure);
            if (documents.HttpSucceeded && documents.BusinessCode == "11") return new(ApplicationOutcome.Success, record, []);
            if (Failure(documents) is not null) return new(ApplicationOutcome.Success, record, [], "Unable to load attachments. Refresh to try again.");
            try
            {
                var files = Rows(documents).EnumerateArray().Select(item => new ApplicationDocument
                {
                    Name = Read(item, "filename"), Type = Read(item, "doctype"), Value = Read(item, "docvalue")
                }).ToArray();
                return new(ApplicationOutcome.Success, record, files);
            }
            catch (JsonException) { return new(ApplicationOutcome.Success, record, [], "The server returned unexpected attachment data. Refresh to try again."); }
        }
        catch (JsonException) { return new(ApplicationOutcome.InvalidResponse); }
    }

    private static ApplicationOutcome? Failure(ApiResult result) => result switch
    {
        { HttpStatus: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => ApplicationOutcome.NotPermitted,
        { Failure: ApiFailure.Transport or ApiFailure.Cancelled } => ApplicationOutcome.ConnectionFailure,
        { HttpStatus: >= HttpStatusCode.InternalServerError } => ApplicationOutcome.ConnectionFailure,
        { Failure: ApiFailure.InvalidResponse } => ApplicationOutcome.InvalidResponse,
        _ => SourceAuthenticationGateway.IsSuccess(result) ? null : ApplicationOutcome.Rejected
    };
    private static JsonElement Rows(ApiResult result)
    {
        if (result.Data is not { } root || !SourceJson.TryGetProperty(root, "data", out var rows) || rows.ValueKind != JsonValueKind.Array)
            throw new JsonException();
        return rows;
    }
    private static JsonElement Child(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && SourceJson.TryGetProperty(parent, name, out var child) ? child : default;
    private static string Read(JsonElement parent, string name)
    {
        var value = Child(parent, name);
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            _ => throw new JsonException()
        };
    }
    private static string Name(JsonElement item) => ApplicationPresentation.Join(" ", Read(item, "firstname"), Read(item, "middlename"), Read(item, "lastname"), Read(item, "suffix"));
    private static string Address(JsonElement item) => ApplicationPresentation.Join(", ",
        ApplicationPresentation.Join(" ", First(Read(item, "unitno"), Read(item, "unitnumber")),
            First(Read(item, "addressLine1"), Read(item, "street"), Read(item, "streetname")),
            First(Read(item, "addressLine2"), Read(item, "specificaddress")), First(Read(item, "bldgname"), Read(item, "buildingname"))),
        Read(item, "barangay"), Read(item, "city"), Read(item, "province"), Read(item, "zipcode"), Read(item, "country"));
    private static string First(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    private static ApplicationRecord Parse(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) throw new JsonException();
        var bio = Child(item, "personalinformation");
        var emergency = Child(item, "emergencycontact");
        var education = Child(item, "educationalinformation");
        var address = Child(item, "address");
        var id = Child(item, "identification");
        return new ApplicationRecord
        {
            Number = Read(item, "referencenumber"), Name = First(Name(bio), Name(item)), Email = Read(item, "email"), Phone = Read(item, "msisdn"),
            Status = Read(item, "status"), Address = Address(Child(address, "presentaddress")),
            Birthday = First(Read(bio, "dateofbirth"), Read(item, "dateofbirth")), Gender = Read(bio, "gender"),
            Birthplace = ApplicationPresentation.Join(", ", Read(bio, "pobprovince"), Read(bio, "pobcity"), Read(bio, "pobcountry")),
            BloodType = Read(bio, "bloodtype"), EmergencyName = Name(emergency), EmergencyPhone = Read(emergency, "contactnumber"),
            EmergencyRelationship = Read(emergency, "relationship"), EmergencyAddress = Address(Child(emergency, "addressinformation")),
            Attainment = Read(education, "educationalattainment"), Course = Read(education, "course"), School = Read(education, "schoolname"),
            BiometricsRemarks = Read(Child(item, "specimen"), "remarks"),
            BioFields = new[] { "civilstatus", "nationality", "accounttype", "natureofwork", "sourceofincome", "tenurialstatus", "department", "displayname" }
                .Select((key, index) => new ApplicationField(new[] { "Civil status", "Nationality", "Account type", "Nature of work", "Source of income", "Tenurial status", "Department", "Display name" }[index], Read(bio, key))).ToArray(),
            AccountFields = [new("Application number", Read(item, "referencenumber")), new("Status", Read(item, "status")),
                new("Email", Read(item, "email")), new("Phone", Read(item, "msisdn")), new("Permanent address", Address(Child(address, "permanentaddress"))),
                new("ID type", Read(id, "idtype")), new("ID number", Read(id, "idnumber")), new("Remarks", Read(item, "remarks")),
                new("Last modified", Read(item, "datemodified"))]
        };
    }
}

public static class ApplicationPresentation
{
    public static bool ValidNumber(string number) => number.Length is > 0 and <= 100 && number.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "Not provided" : value.Trim();
    public static string Join(string separator, params string?[] values) => string.Join(separator, values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
    public static string Birthday(string birthday, DateOnly today)
    {
        var parsed = DateOnly.TryParse(birthday, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birth);
        if (!parsed && birthday.Length >= 10) parsed = DateOnly.TryParseExact(birthday[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out birth);
        if (!parsed || birth > today) return Text(birthday);
        var age = today.Year - birth.Year;
        if (birth > today.AddYears(-age)) age--;
        return $"{birth:MM/dd/yyyy} · {age} years old";
    }
    public static string Status(ApplicationOutcome outcome) => outcome switch
    {
        ApplicationOutcome.NotFound => "No application found. Check the application number and try again.",
        ApplicationOutcome.NotPermitted => "Your account is not permitted to view this application.",
        ApplicationOutcome.InvalidNumber => "Enter a valid application number using letters, numbers, hyphens or underscores.",
        ApplicationOutcome.ConnectionFailure => "Unable to reach the server. Check your connection and try again.",
        ApplicationOutcome.InvalidResponse => "The server returned an unexpected application response. Please try again.",
        ApplicationOutcome.Unavailable => "Application records are unavailable because the backend configuration is invalid.",
        _ => "The server declined the application request. Please try again."
    };
    public const string EditUnavailable = "Editing is not available in this client yet.";
    public const string AttachmentUnavailable = "Attachment upload is not available in this client yet.";
    public static string DocumentName(ApplicationDocument file) => Text(string.IsNullOrWhiteSpace(file.Name) ? file.Type : file.Name);
    public static string MapUrl(string address) => "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(address);

    // Only known binary formats may be previewed. Never open server-supplied URLs or active HTML/SVG.
    public static (byte[] Bytes, string Mime, string Extension)? Decode(ApplicationDocument file)
    {
        try
        {
            var value = file.Value;
            if (value.Length > 32 * 1024 * 1024) return null;
            if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = value.IndexOf(',');
                if (comma < 0 || !value[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
                value = value[(comma + 1)..];
            }
            var bytes = Convert.FromBase64String(value);
            if (bytes.AsSpan().StartsWith("%PDF-"u8)) return (bytes, "application/pdf", ".pdf");
            if (bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return (bytes, "image/png", ".png");
            if (bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 })) return (bytes, "image/jpeg", ".jpg");
        }
        catch (FormatException) { }
        return null;
    }
}
