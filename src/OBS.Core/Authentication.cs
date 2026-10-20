namespace OBS.Core;

public enum AuthOutcome { OtpRequired, SignedIn, PasswordChangeRequired, InvalidCredentials, Locked, Inactive, Rejected, ConnectionFailure, Blocked }
public sealed class SessionTokens(string access, string refresh, string xsrf)
{
    public string Access { get; } = access;
    public string Refresh { get; } = refresh;
    public string Xsrf { get; } = xsrf;
    public bool IsComplete => !string.IsNullOrWhiteSpace(Access) && !string.IsNullOrWhiteSpace(Refresh) && !string.IsNullOrWhiteSpace(Xsrf)
        && !Access.Any(char.IsWhiteSpace) && !Refresh.Any(char.IsWhiteSpace) && !Xsrf.Any(char.IsControl);
    public override string ToString() => "[session tokens redacted]";
}
public sealed class StaffSession(string email, SessionTokens tokens, IEnumerable<string> permissions,
    bool passwordChangeRequired = false, int? minPassword = null, int? maxPassword = null)
{
    public string Email { get; } = email;
    public SessionTokens Tokens { get; } = tokens;
    public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(permissions, StringComparer.Ordinal);
    public bool PasswordChangeRequired { get; } = passwordChangeRequired;
    public int? MinPassword { get; } = minPassword;
    public int? MaxPassword { get; } = maxPassword;
    public bool Allows(string authority) => Permissions.Contains(authority);
    public StaffSession WithTokens(SessionTokens replacement) => new(Email, replacement, Permissions, PasswordChangeRequired, MinPassword, MaxPassword);
    public override string ToString() => "[staff session redacted]";
}
public sealed record AuthReply(AuthOutcome Outcome, StaffSession? Session = null);
public enum RefreshOutcome { Success, Rejected, ConnectionFailure, ContractBlocked }
public enum RefreshReason { Success, TransportFailure, ServerFailure, HttpRejection, BusinessRejection, UnsuccessfulEnvelope, MissingAccessToken, InvalidTokens }
// Safe to log: fixed categories and numeric codes only. Never contains response text, identity, or tokens.
public sealed record RefreshPayloadShape(System.Text.Json.JsonValueKind Data, System.Text.Json.JsonValueKind FirstItem,
    System.Text.Json.JsonValueKind AccessToken, System.Text.Json.JsonValueKind TokenContainer);
public sealed record RefreshDiagnostic(int? HttpStatus, int? BusinessCode, ApiFailure Failure, RefreshReason Reason,
    RefreshPayloadShape? Shape = null);
public sealed record RefreshReply(RefreshOutcome Outcome, SessionTokens? Tokens = null, RefreshDiagnostic? Diagnostic = null);

public interface IAuthenticationGateway
{
    bool ContractAvailable { get; }
    Task<AuthReply> AuthenticateAsync(string email, string password, string? otp, CancellationToken token);
    Task<AuthReply> ResendAsync(string email, string password, CancellationToken token);
    Task<RefreshReply> RefreshAsync(StaffSession session, CancellationToken token);
    Task TerminateAsync(StaffSession session, CancellationToken token);
    Task<ApiResult> ForgotPasswordAsync(string email, CancellationToken token);
    Task<ApiResult> ChangePasswordAsync(StaffSession session, string currentPassword, string newPassword, CancellationToken token);
}

// Replace only after inspecting source DTOs and sanitized server responses. No guessed success mapping.
public sealed class UnverifiedAuthenticationGateway : IAuthenticationGateway
{
    public bool ContractAvailable => false;
    public Task<AuthReply> AuthenticateAsync(string email, string password, string? otp, CancellationToken token) => Task.FromResult(new AuthReply(AuthOutcome.Blocked));
    public Task<AuthReply> ResendAsync(string email, string password, CancellationToken token) => Task.FromResult(new AuthReply(AuthOutcome.Blocked));
    public Task<RefreshReply> RefreshAsync(StaffSession session, CancellationToken token) => Task.FromResult(new RefreshReply(RefreshOutcome.ContractBlocked));
    public Task TerminateAsync(StaffSession session, CancellationToken token) => Task.CompletedTask;
    public Task<ApiResult> ForgotPasswordAsync(string email, CancellationToken token) => Task.FromResult(new ApiResult { Failure = ApiFailure.ContractUnavailable });
    public Task<ApiResult> ChangePasswordAsync(StaffSession session, string currentPassword, string newPassword, CancellationToken token) => Task.FromResult(new ApiResult { Failure = ApiFailure.ContractUnavailable });
}

// Request shapes are evidence from the owner's revised prompt; they aren't proof of live responses.
public static class AuthenticationRequests
{
    public static object Authenticate(string username, string password, string? otp) => otp is null
        ? new { username, password } : (object)new { username, password, otp };
    public static object Resend(ApiContract contract, string username, string password) => contract is ApiContract.Refactored or ApiContract.Privacy
        ? new { username, password, guiinterface = "obs" } : (object)new { username, password, guiinterface = "obs", module = "", otp = "" };
    public static object Terminate(string userName) => new { userName };
    public static object Refresh() => new { };
    public static RequestSecurity RefreshSecurity(SessionTokens tokens) => new() { Bearer = tokens.Refresh, Xsrf = tokens.Xsrf };
    public static RequestSecurity TerminateSecurity(SessionTokens tokens) => new() { Bearer = tokens.Access, Xsrf = tokens.Xsrf };
}
