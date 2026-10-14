using System.Net;
using System.Text.Json;

namespace OBS.Core;

// Derived from Models/userData.cs and Controllers/sessionController.cs. Live behavior remains unverified.
public sealed class SourceAuthenticationGateway(ApiTransport api, BackendOptions options) : IAuthenticationGateway
{
    public bool ContractAvailable => options.ApiContract == ApiContract.Refactored;
    public async Task<AuthReply> AuthenticateAsync(string email, string password, string? otp, CancellationToken token)
    {
        if (!ContractAvailable) return new(AuthOutcome.Blocked);
        var result = await api.PostAsync("session/authenticate", AuthenticationRequests.Authenticate(email, password, otp), cancellationToken: token);
        var outcome = ClassifyAuthentication(result);
        if (outcome != AuthOutcome.SignedIn) return new(outcome);
        if (otp is null) return new(AuthOutcome.OtpRequired);
        try
        {
            if (FirstData(result) is not { } data) return new(AuthOutcome.Blocked);
            var user = data.Deserialize<SessionUser>(SourceJson.Options);
            var refresh = result.Cookies.GetValueOrDefault("refresh_token");
            var xsrf = result.Cookies.GetValueOrDefault("XSRF-TOKEN");
            if (user?.token?.accesstoken is not { Length: > 0 } access || user.roles?.modules is null ||
                string.IsNullOrWhiteSpace(user.email) || user.password?.isfirstlogon is null || user.password.isexpired is null ||
                string.IsNullOrWhiteSpace(refresh) || string.IsNullOrWhiteSpace(xsrf))
                return new(AuthOutcome.Blocked);
            var session = new StaffSession(user.email, new(access, refresh, Uri.UnescapeDataString(xsrf)), user.roles.modules,
                user.password.isfirstlogon == 1 || user.password.isexpired == 1, user.roles.minpassword, user.roles.maxpassword);
            if (!session.Tokens.IsComplete) return new(AuthOutcome.Blocked);
            return new(session.PasswordChangeRequired ? AuthOutcome.PasswordChangeRequired : AuthOutcome.SignedIn, session);
        }
        catch (JsonException) { return new(AuthOutcome.Blocked); }
    }

    public async Task<AuthReply> ResendAsync(string email, string password, CancellationToken token)
    {
        var result = await api.PostAsync("session/resend/otp", AuthenticationRequests.Resend(options.ApiContract, email, password), cancellationToken: token);
        return new(IsSuccess(result) ? AuthOutcome.OtpRequired : ClassifyAuthentication(result));
    }

    public async Task<RefreshReply> RefreshAsync(StaffSession session, CancellationToken token)
    {
        var result = await api.PostAsync("session/refresh", AuthenticationRequests.Refresh(), AuthenticationRequests.RefreshSecurity(session.Tokens), token);
        RefreshReply Reply(RefreshOutcome outcome, RefreshReason reason, SessionTokens? tokens = null) =>
            new(outcome, tokens, new((int?)result.HttpStatus,
                int.TryParse(result.BusinessCode, out var code) ? code : null, result.Failure, reason, DescribeShape(result)));
        if (result.Failure is ApiFailure.Transport or ApiFailure.Cancelled)
            return Reply(RefreshOutcome.ConnectionFailure, RefreshReason.TransportFailure);
        if (result.HttpStatus is >= HttpStatusCode.InternalServerError)
            return Reply(RefreshOutcome.ConnectionFailure, RefreshReason.ServerFailure);
        // A 400 is a rejection, but never evidence of another-device takeover.
        if (result.HttpStatus is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
            return Reply(RefreshOutcome.Rejected, RefreshReason.HttpRejection);
        // Business code 3 is retained as an endpoint-specific source rule, not takeover evidence.
        if (result.BusinessCode is "3" or "400" or "401" or "403") return Reply(RefreshOutcome.Rejected, RefreshReason.BusinessRejection);
        if (!IsSuccess(result)) return Reply(RefreshOutcome.ConnectionFailure, RefreshReason.UnsuccessfulEnvelope);
        var data = FirstData(result);
        if (data is null || !SourceJson.TryGetProperty(data.Value, "accesstoken", out var field) || field.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(field.GetString())) return Reply(RefreshOutcome.ContractBlocked, RefreshReason.MissingAccessToken);
        var refresh = result.Cookies.GetValueOrDefault("refresh_token", session.Tokens.Refresh);
        var xsrf = result.Cookies.GetValueOrDefault("XSRF-TOKEN");
        var rotated = new SessionTokens(field.GetString()!, refresh, xsrf is null ? session.Tokens.Xsrf : Uri.UnescapeDataString(xsrf));
        return rotated.IsComplete ? Reply(RefreshOutcome.Success, RefreshReason.Success, rotated)
            : Reply(RefreshOutcome.ContractBlocked, RefreshReason.InvalidTokens);
    }

    public async Task TerminateAsync(StaffSession session, CancellationToken token) =>
        _ = await api.PostAsync("session/terminate", AuthenticationRequests.Terminate(session.Email), AuthenticationRequests.TerminateSecurity(session.Tokens), token);

    public Task<ApiResult> ForgotPasswordAsync(string email, CancellationToken token) =>
        api.PostAsync("user/forgot/password", new { email, guiinterface = "OBS" }, cancellationToken: token);
    public Task<ApiResult> ChangePasswordAsync(StaffSession session, string currentPassword, string newPassword, CancellationToken token) =>
        api.PostAsync("user/change/password", new { username = session.Email, currentpassword = currentPassword, newpassword = newPassword },
            new RequestSecurity { Bearer = session.Tokens.Access }, token);

    public static bool IsSuccess(ApiResult result) => result.Failure == ApiFailure.None && result.HttpSucceeded && result.BusinessCode == "0";
    private static AuthOutcome ClassifyAuthentication(ApiResult result)
    {
        if (result.Failure is ApiFailure.Transport or ApiFailure.Cancelled || result.HttpStatus is >= HttpStatusCode.InternalServerError) return AuthOutcome.ConnectionFailure;
        // Source Login.cs explicitly maps HTTP 401 to generic credentials; 6 and 7 retain account explanations.
        if (result.HttpStatus == HttpStatusCode.Unauthorized) return AuthOutcome.InvalidCredentials;
        if (result.BusinessCode == "6") return AuthOutcome.Inactive;
        if (result.BusinessCode == "7") return AuthOutcome.Locked;
        return IsSuccess(result) ? AuthOutcome.SignedIn : AuthOutcome.Rejected;
    }
    private static JsonElement? FirstData(ApiResult result) => result.Data is { ValueKind: JsonValueKind.Object } root &&
        SourceJson.TryGetProperty(root, "data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0 &&
        data[0].ValueKind == JsonValueKind.Object ? data[0] : null;

    private static RefreshPayloadShape DescribeShape(ApiResult result)
    {
        // Enumerated JSON kinds only. No property names, body text, cookie values, or token values are logged.
        var data = default(JsonElement);
        if (result.Data is { } root) _ = SourceJson.TryGetProperty(root, "data", out data);
        var item = data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0 ? data[0]
            : data.ValueKind == JsonValueKind.Object ? data : default;
        _ = SourceJson.TryGetProperty(item, "accesstoken", out var access);
        _ = SourceJson.TryGetProperty(item, "token", out var token);
        return new(data.ValueKind, item.ValueKind, access.ValueKind, token.ValueKind);
    }

    // Intentionally exclude the backend's password.password field from deserialization.
    private sealed class SessionUser
    {
        public string? email { get; set; }
        public PasswordFlags? password { get; set; }
        public RolePayload? roles { get; set; }
        public TokenPayload? token { get; set; }
    }
    private sealed class PasswordFlags { public int? isfirstlogon { get; set; } public int? isexpired { get; set; } }
    private sealed class RolePayload
    {
        public List<string>? modules { get; set; }
        public int? minpassword { get; set; }
        public int? maxpassword { get; set; }
    }
    private sealed class TokenPayload { public string? accesstoken { get; set; } }
}
