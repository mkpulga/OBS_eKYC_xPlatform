using System.Text.Json;

namespace OBS.Core;

// Opaque, transient PII credential. It is never a staff login token or a client-side grant.
public sealed class PiiAccessToken
{
    internal string Value { get; }
    public PiiAccessToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new ArgumentException("Invalid PII access token.", nameof(value));
        Value = value;
    }
    public override string ToString() => "[PII access token redacted]";

    // The observed server may return only code/message plus the exact cookie. Capturing
    // that credential grants nothing: status must separately establish applicant/expiry/claims.
    public static PiiAccessToken? FromVerification(ApiResult result)
    {
        if (!SourceAuthenticationGateway.IsSuccess(result) || result.Data is not { ValueKind: JsonValueKind.Object } root ||
            !result.Cookies.TryGetValue("pii_access_token", out var value)) return null;
        if (root.EnumerateObject().Any(p => p.Name.Equals("data", StringComparison.OrdinalIgnoreCase)) &&
            (!SourceJson.TryGetProperty(root, "data", out var data) || data.ValueKind != JsonValueKind.Array ||
             data.GetArrayLength() != 1 || !SourceJson.TryGetProperty(data[0], "verified", out var verified) ||
             verified.ValueKind != JsonValueKind.True)) return null;
        try { return new PiiAccessToken(value); }
        catch (ArgumentException) { return null; }
    }
}

// Transport adapters and observed-response readers for the owner-selected collection.
// ApiResult can contain PII: never log or serialize it into diagnostics.
public sealed class PrivacyApiService(ApiTransport api, BackendOptions options, TimeProvider? clock = null)
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    // Both hosts must fail closed until response mappings and OTP-gated reveal are complete.
    public const string ClientUnavailableReason = "The replacement Privacy sign-in configuration is not available yet. Use the existing configured sign-in and the Privacy Verification screen. Contact your administrator for setup.";

    public Task<ApiResult> RequestOtpAsync(StaffSession session, string applicationId, CancellationToken token = default)
    {
        ValidateIdentifier(applicationId);
        return Post(session, "privacy/otp/request", new { applicationId }, token);
    }

    public Task<ApiResult> VerifyOtpAsync(StaffSession session, string applicationId, string otp, CancellationToken token = default)
    {
        ValidateIdentifier(applicationId);
        if (otp is null || otp.Length != 6 || otp.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Enter exactly six privacy OTP digits.", nameof(otp));
        return Post(session, "privacy/otp/verify", new { applicationId, otp }, token);
    }

    public Task<ApiResult> GetStatusAsync(StaffSession session, string applicationId, PiiAccessToken? pii = null, CancellationToken token = default) =>
        Get(session, "privacy/verification/status/" + Identifier(applicationId), pii, token);

    public Task<ApiResult> GetBioAsync(StaffSession session, string citizenId, PiiAccessToken pii, CancellationToken token = default) =>
        Section(session, citizenId, "bio", pii, token);
    public Task<ApiResult> GetAddressAsync(StaffSession session, string citizenId, PiiAccessToken pii, CancellationToken token = default) =>
        Section(session, citizenId, "address", pii, token);
    public Task<ApiResult> GetEmergencyAsync(StaffSession session, string citizenId, PiiAccessToken pii, CancellationToken token = default) =>
        Section(session, citizenId, "emergency", pii, token);
    public Task<ApiResult> GetEducationAsync(StaffSession session, string citizenId, PiiAccessToken pii, CancellationToken token = default) =>
        Section(session, citizenId, "education", pii, token);

    public Task<ApiResult> GetInformationGatheringAsync(StaffSession session, string citizenId, PiiAccessToken? pii = null, CancellationToken token = default) =>
        Get(session, "information/gathering/" + Identifier(citizenId), pii, token, "VINFORMATIONGATHERING");
    public Task<ApiResult> GetCardholderUpdateAsync(StaffSession session, string citizenId, PiiAccessToken? pii = null, CancellationToken token = default) =>
        Get(session, "cardholder/update/" + Identifier(citizenId), pii, token, "VCARDHOLDERUPDATE");
    public Task<ApiResult> GetDashboardAsync(StaffSession session, CancellationToken token = default) =>
        Get(session, "dashboard", null, token, "VIEWDASHBOARD");

    public async Task<DashboardReply> GetDashboardSnapshotAsync(StaffSession session, CancellationToken token = default) =>
        DashboardReply.FromApiResult(await GetDashboardAsync(session, token));

    public async Task<PrivacyStatusReply> GetVerificationStatusAsync(StaffSession session, string applicationId,
        PiiAccessToken pii, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(pii);
        var result = await GetStatusAsync(session, applicationId, pii, token);
        return token.IsCancellationRequested ? new(PrivacyReadOutcome.Cancelled)
            : PrivacyStatusReply.FromApiResult(result, applicationId, time.GetUtcNow());
    }

    public Task<ProtectedReply<ApplicantBio>> GetBioSnapshotAsync(StaffSession session, string citizenId, PiiAccessToken pii,
        PrivacyVerification verification, CancellationToken token = default) =>
        ReadSection(session, citizenId, "bio", PrivacySection.Bio, pii, verification, ProtectedApplicantJson.Bio, token);
    public Task<ProtectedReply<IReadOnlyList<ApplicantAddress>>> GetAddressSnapshotAsync(StaffSession session, string citizenId,
        PiiAccessToken pii, PrivacyVerification verification, CancellationToken token = default) =>
        ReadSection(session, citizenId, "address", PrivacySection.Address, pii, verification, ProtectedApplicantJson.Addresses, token);
    public Task<ProtectedReply<IReadOnlyList<ApplicantEmergency>>> GetEmergencySnapshotAsync(StaffSession session, string citizenId,
        PiiAccessToken pii, PrivacyVerification verification, CancellationToken token = default) =>
        ReadSection(session, citizenId, "emergency", PrivacySection.Emergency, pii, verification, ProtectedApplicantJson.Emergency, token);
    public Task<ProtectedReply<IReadOnlyList<ApplicantEducation>>> GetEducationSnapshotAsync(StaffSession session, string citizenId,
        PiiAccessToken pii, PrivacyVerification verification, CancellationToken token = default) =>
        ReadSection(session, citizenId, "education", PrivacySection.Education, pii, verification, ProtectedApplicantJson.Education, token);

    public Task<ProtectedReply<ApplicantDetailSnapshot>> GetInformationGatheringSnapshotAsync(StaffSession session, string citizenId,
        PiiAccessToken? pii = null, PrivacyVerification? verification = null, CancellationToken token = default) =>
        ReadDetail(session, citizenId, false, pii, verification, token);
    public Task<ProtectedReply<ApplicantDetailSnapshot>> GetCardholderUpdateSnapshotAsync(StaffSession session, string citizenId,
        PiiAccessToken? pii = null, PrivacyVerification? verification = null, CancellationToken token = default) =>
        ReadDetail(session, citizenId, true, pii, verification, token);

    private async Task<ProtectedReply<ApplicantDetailSnapshot>> ReadDetail(StaffSession session, string citizenId, bool update,
        PiiAccessToken? pii, PrivacyVerification? verification, CancellationToken token)
    {
        ValidateIdentifier(citizenId);
        if (token.IsCancellationRequested) return new(PrivacyReadOutcome.Cancelled);
        if (verification is not null && (pii is null || verification.ApplicationId != citizenId || verification.ExpiresAt <= time.GetUtcNow()))
            return new(PrivacyReadOutcome.NotPermitted);
        var result = update ? await GetCardholderUpdateAsync(session, citizenId, pii, token)
            : await GetInformationGatheringAsync(session, citizenId, pii, token);
        if (token.IsCancellationRequested) return new(PrivacyReadOutcome.Cancelled);
        if (PrivacyResults.Failure(result) is { } failure) return new(failure);
        var now = time.GetUtcNow();
        if (verification is not null && verification.ExpiresAt <= now) return new(PrivacyReadOutcome.Expired);
        try { return new(PrivacyReadOutcome.Success, ApplicantDetailJson.Parse(result, citizenId, verification, now)); }
        catch (JsonException) { return new(PrivacyReadOutcome.InvalidResponse); }
    }

    private async Task<ProtectedReply<T>> ReadSection<T>(StaffSession session, string citizenId, string route, PrivacySection section,
        PiiAccessToken pii, PrivacyVerification verification, Func<ApiResult, string, T> parse, CancellationToken token) where T : class
    {
        ArgumentNullException.ThrowIfNull(pii);
        ArgumentNullException.ThrowIfNull(verification);
        ValidateIdentifier(citizenId);
        if (token.IsCancellationRequested) return new(PrivacyReadOutcome.Cancelled);
        // Deliberately require the canonical ID, never guess reference/CID aliases.
        if (!verification.Allows(section, citizenId, time.GetUtcNow())) return new(PrivacyReadOutcome.NotPermitted);
        var result = await Section(session, citizenId, route, pii, token);
        if (token.IsCancellationRequested) return new(PrivacyReadOutcome.Cancelled);
        if (PrivacyResults.Failure(result) is { } failure) return new(failure);
        if (!verification.Allows(section, citizenId, time.GetUtcNow())) return new(PrivacyReadOutcome.Expired);
        try { return new(PrivacyReadOutcome.Success, parse(result, citizenId)); }
        catch (JsonException) { return new(PrivacyReadOutcome.InvalidResponse); }
    }

    private Task<ApiResult> Section(StaffSession session, string citizenId, string section, PiiAccessToken pii, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(pii);
        return Get(session, $"privacy/applicants/{Identifier(citizenId)}/{section}", pii, token);
    }
    private Task<ApiResult> Post(StaffSession session, string route, object body, CancellationToken token) =>
        Guard(session) is { } blocked ? Task.FromResult(blocked)
            : api.PostAsync(route, body, new RequestSecurity { Bearer = session.Tokens.Access }, token);
    private Task<ApiResult> Get(StaffSession session, string route, PiiAccessToken? pii, CancellationToken token, string? authority = null) =>
        Guard(session, authority) is { } blocked ? Task.FromResult(blocked)
            : api.GetAsync(route, new RequestSecurity { Bearer = session.Tokens.Access, PiiToken = pii }, token);
    private ApiResult? Guard(StaffSession session, string? authority = null)
    {
        if (options.ApiContract != ApiContract.Privacy) return new() { Failure = ApiFailure.ContractUnavailable };
        return !session.Tokens.IsComplete || session.PasswordChangeRequired || (authority is not null && !session.Allows(authority))
            ? new() { Failure = ApiFailure.NotPermitted } : null;
    }
    private static string Identifier(string value) { ValidateIdentifier(value); return Uri.EscapeDataString(value); }
    private static void ValidateIdentifier(string value)
    {
        // Reuse the existing client's conservative path identifier policy. Broader API formats
        // require contract evidence; do not silently coerce a reference into a citizenId.
        if (value is null || !ApplicationPresentation.ValidNumber(value))
            throw new ArgumentException("Enter a valid applicant identifier.", nameof(value));
    }
}
