using System.Globalization;
using System.Net;
using System.Text.Json;

namespace OBS.Core;

public enum PrivacySection { Bio, Address, Emergency, Education }
public enum PrivacyReadOutcome { Success, NotVerified, Expired, WrongApplicant, NotPermitted, Rejected, ConnectionFailure, InvalidResponse, Cancelled, Unavailable, NotFound }

// Server metadata, not a credential or a cached UI session. ApplicantPrivacyController
// binds it to staff/applicant lifetime; callers of the raw readers must enforce that too.
public sealed class PrivacyVerification
{
    public string ApplicationId { get; }
    public DateTimeOffset ExpiresAt { get; }
    public IReadOnlyList<string> AuthorizedSections { get; }
    internal PrivacyVerification(string applicationId, DateTimeOffset expiresAt, string[] sections)
    {
        ApplicationId = applicationId;
        ExpiresAt = expiresAt;
        AuthorizedSections = Array.AsReadOnly(sections);
    }
    public bool Allows(PrivacySection section, string applicationId, DateTimeOffset now)
    {
        var claim = section switch
        {
            PrivacySection.Bio => "canViewBioDetails", PrivacySection.Address => "canViewAddress",
            PrivacySection.Emergency => "canViewEmergencyContact", PrivacySection.Education => "canViewEducationalInformation",
            _ => null
        };
        return claim is not null && string.Equals(ApplicationId, applicationId, StringComparison.Ordinal) && now < ExpiresAt &&
            AuthorizedSections.Contains(claim, StringComparer.Ordinal);
    }
    public override string ToString() => "[privacy verification redacted]";
}

public sealed record PrivacyStatusReply(PrivacyReadOutcome Outcome, PrivacyVerification? Verification = null)
{
    internal static PrivacyStatusReply FromApiResult(ApiResult result, string expectedApplicationId, DateTimeOffset now)
    {
        if (PrivacyResults.Failure(result) is { } failure) return new(failure);
        try
        {
            var row = PrivacyResults.SingleData(result);
            if (!SourceJson.TryGetProperty(row, "verified", out var verified) || verified.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new JsonException();
            if (verified.ValueKind == JsonValueKind.False) return new(PrivacyReadOutcome.NotVerified);
            if (!SourceJson.TryGetProperty(row, "applicationId", out var id) || id.ValueKind != JsonValueKind.String ||
                !CitizenId.IsValid(id.GetString())) throw new JsonException();
            if (!string.Equals(id.GetString(), expectedApplicationId, StringComparison.Ordinal)) return new(PrivacyReadOutcome.WrongApplicant);
            if (!SourceJson.TryGetProperty(row, "expiresAt", out var expiry) || expiry.ValueKind != JsonValueKind.String ||
                !UtcExpiry(expiry.GetString()!, out var expiresAt)) throw new JsonException();
            if (!SourceJson.TryGetProperty(row, "authorizedSections", out var sections) || sections.ValueKind != JsonValueKind.Array)
                throw new JsonException();
            var claims = sections.EnumerateArray().Select(claim =>
                claim.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(claim.GetString()) ? claim.GetString()! : throw new JsonException()).ToArray();
            if (expiresAt <= now) return new(PrivacyReadOutcome.Expired);
            return new(PrivacyReadOutcome.Success, new(id.GetString()!, expiresAt, claims));
        }
        catch (JsonException) { return new(PrivacyReadOutcome.InvalidResponse); }
    }
    private static bool UtcExpiry(string value, out DateTimeOffset expiry) => DateTimeOffset.TryParseExact(value,
        ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"],
        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out expiry);
}

public sealed record ProtectedReply<T>(PrivacyReadOutcome Outcome, T? Snapshot = null) where T : class;

internal static class PrivacyResults
{
    internal static PrivacyReadOutcome? Failure(ApiResult result) => result switch
    {
        { Failure: ApiFailure.ContractUnavailable } => PrivacyReadOutcome.Unavailable,
        { Failure: ApiFailure.NotPermitted } or { HttpStatus: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => PrivacyReadOutcome.NotPermitted,
        { Failure: ApiFailure.Cancelled } => PrivacyReadOutcome.Cancelled,
        { Failure: ApiFailure.Transport } or { HttpStatus: >= HttpStatusCode.InternalServerError } => PrivacyReadOutcome.ConnectionFailure,
        { Failure: ApiFailure.InvalidResponse } => PrivacyReadOutcome.InvalidResponse,
        _ => SourceAuthenticationGateway.IsSuccess(result) ? null : PrivacyReadOutcome.Rejected
    };
    internal static JsonElement SingleData(ApiResult result)
    {
        if (result.Data is not { } root || !SourceJson.TryGetProperty(root, "data", out var data) ||
            data.ValueKind != JsonValueKind.Array || data.GetArrayLength() != 1 || data[0].ValueKind != JsonValueKind.Object)
            throw new JsonException();
        return data[0];
    }
}
