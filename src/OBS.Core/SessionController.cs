namespace OBS.Core;

public enum SignInStage { Login, Otp, PasswordChange, Protected }

// Scoped per Blazor circuit and per installed app. No static credentials or session state.
public sealed class SessionController(IAuthenticationGateway gateway, TimeProvider? clock = null) : IDisposable
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly SemaphoreSlim authentication = new(1, 1);
    private readonly SemaphoreSlim refresh = new(1, 1);
    private CancellationTokenSource lifetime = new();
    private long generation;
    private string? password;
    private string? email;
    private DateTimeOffset resendAfter;
    private int failures;
    private bool active = true;
    private bool disposed;
    private readonly object stateLock = new();
    private bool revalidating;
    private bool validated;
    public StaffSession? Current { get; private set; }
    // Stable through token rotation; replaced on a new sign-in, cleared before logout work.
    internal object? Identity { get; private set; }
    public SignInStage Stage { get; private set; }
    public string Message { get; private set; } = "";
    public bool ContractAvailable => gateway.ContractAvailable;
    public bool ProtectedWorkAllowed => active && validated && !revalidating && Stage == SignInStage.Protected && Current is not null;
    public int ResendSeconds => Math.Max(0, (int)Math.Ceiling((resendAfter - time.GetUtcNow()).TotalSeconds));
    public event Action? Changed;
    public event Action<RefreshDiagnostic>? RefreshChecked;

    public async Task SignInAsync(string username, string secret, string? otp = null)
    {
        if (!await authentication.WaitAsync(0)) return;
        var epoch = generation;
        try
        {
            if (!gateway.ContractAvailable) { Message = "Sign-in is blocked: this API contract is not implemented and verified."; return; }
            if (otp is not null && (otp.Length != 6 || otp.Any(c => c is < '0' or > '9'))) { Message = "Enter exactly six OTP digits."; return; }
            var reply = await gateway.AuthenticateAsync(username, secret, otp, lifetime.Token);
            lock (stateLock)
            {
            if (epoch != generation || disposed) return;
            switch (reply.Outcome)
            {
                case AuthOutcome.OtpRequired:
                    email = username; password = secret; Stage = SignInStage.Otp;
                    resendAfter = time.GetUtcNow().AddSeconds(60); Message = "Enter the six-digit OTP sent to you."; break;
                case AuthOutcome.SignedIn when reply.Session?.Tokens.IsComplete == true:
                    Current = reply.Session; Identity = new(); password = null; email = null; failures = 0; validated = true;
                    Stage = SignInStage.Protected; Message = ""; break;
                case AuthOutcome.InvalidCredentials: Clear("Invalid email or password."); break;
                case AuthOutcome.Locked: Clear("Your account is locked. Contact your administrator."); break;
                case AuthOutcome.Inactive: Clear("Your account is inactive. Contact your administrator."); break;
                case AuthOutcome.PasswordChangeRequired:
                    password = null; email = null;
                    if (reply.Session?.Tokens.IsComplete == true && reply.Session.Allows("CPW"))
                    {
                        Current = reply.Session; Stage = SignInStage.PasswordChange;
                        Message = "Change your password before continuing.";
                    }
                    else
                    {
                        Clear("Your password must be changed, but your role lacks permission. Contact your administrator.");
                    }
                    break;
                case AuthOutcome.ConnectionFailure: Message = "Unable to connect. Check your connection before trying again."; break;
                case AuthOutcome.Rejected when otp is not null:
                    Message = "OTP verification failed. Check your OTP or request a new one."; break;
                default: Clear("Sign-in could not be verified. Please sign in again."); break;
            }
            }
            if (reply.Outcome == AuthOutcome.PasswordChangeRequired && reply.Session is { } blocked && !blocked.Allows("CPW"))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try { await gateway.TerminateAsync(blocked, timeout.Token); }
                catch (OperationCanceledException) { }
                catch (HttpRequestException) { }
            }
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { if (epoch == generation) Message = "Unable to connect. Check your connection before trying again."; }
        finally { authentication.Release(); Changed?.Invoke(); }
    }

    public Task VerifyAsync(string otp) => Stage == SignInStage.Otp && email is not null && password is not null
        ? SignInAsync(email, password, otp) : Task.CompletedTask;

    public async Task ResendAsync()
    {
        if (Stage != SignInStage.Otp || ResendSeconds > 0 || !await authentication.WaitAsync(0)) return;
        var epoch = generation;
        try
        {
            if (email is null || password is null) return;
            // Start cooldown before dispatch: a lost response may still have sent an OTP.
            resendAfter = time.GetUtcNow().AddSeconds(60);
            var reply = await gateway.ResendAsync(email, password, lifetime.Token);
            if (epoch == generation) Message = reply.Outcome == AuthOutcome.OtpRequired
                ? "OTP resent." : "OTP resend was not confirmed. Wait before trying again.";
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { if (epoch == generation) Message = "OTP resend was not confirmed. Wait before trying again."; }
        finally { authentication.Release(); Changed?.Invoke(); }
    }

    public async Task CheckSessionAsync(bool waitForPending = false)
    {
        if (disposed || !active || Current is null) return;
        if (waitForPending) await refresh.WaitAsync();
        else if (!await refresh.WaitAsync(0)) return;
        var epoch = generation;
        var snapshot = Current;
        try
        {
            if (snapshot is null || !active || disposed) return;
            var reply = await gateway.RefreshAsync(snapshot, lifetime.Token);
            lock (stateLock)
            {
            if (epoch != generation) return;
            if (reply.Diagnostic is not null) RefreshChecked?.Invoke(reply.Diagnostic);
            switch (reply.Outcome)
            {
                case RefreshOutcome.Success when reply.Tokens?.IsComplete == true:
                    Current = snapshot.WithTokens(reply.Tokens); failures = 0; validated = true; Message = ""; break;
                case RefreshOutcome.ConnectionFailure: ConnectionFailed(); break;
                case RefreshOutcome.ContractBlocked:
                    Clear("Session refresh returned an unsupported response. Please sign in again."); break;
                default: Clear("Your session has ended. Please sign in again."); break;
            }
            }
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { if (epoch == generation) ConnectionFailed(); }
        finally { refresh.Release(); Changed?.Invoke(); }
    }

    private void ConnectionFailed()
    {
        if (++failures >= 3) Clear("Connection checks failed three times. Please sign in again.");
        else if (!validated) Message = "Unable to validate your session. Protected work remains paused.";
    }

    public async Task RunChecksAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), time);
        while (await timer.WaitForNextTickAsync(cancellationToken)) await CheckSessionAsync();
    }

    public void Pause() { active = false; validated = false; Changed?.Invoke(); }
    public async Task ResumeAsync()
    {
        if (disposed) return;
        revalidating = true; active = true; Changed?.Invoke();
        try { await CheckSessionAsync(waitForPending: true); }
        finally { revalidating = false; Changed?.Invoke(); }
    }
    public void BackToLogin() => Clear("");
    public void BeginPasswordChange()
    {
        if (Current?.Allows("CPW") != true) return;
        Stage = SignInStage.PasswordChange; Changed?.Invoke();
    }
    public async Task ChangePasswordAsync(string currentPassword, string newPassword, string confirmation)
    {
        if (Current?.Allows("CPW") != true || !await authentication.WaitAsync(0)) return;
        var epoch = generation;
        try
        {
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || newPassword != confirmation)
            { Message = "Complete all password fields and match the confirmation."; return; }
            if ((Current.MinPassword is > 0 && newPassword.Length < Current.MinPassword) ||
                (Current.MaxPassword is > 0 && newPassword.Length > Current.MaxPassword))
            { Message = "The new password does not meet your role's length requirements."; return; }
            var result = await gateway.ChangePasswordAsync(Current, currentPassword, newPassword, lifetime.Token);
            if (epoch != generation) return;
            if (SourceAuthenticationGateway.IsSuccess(result))
            { await LogoutAsync(); Message = "Password changed. Please sign in again."; }
            else if (result.HttpStatus == System.Net.HttpStatusCode.Unauthorized) Clear("Your session has ended. Please sign in again.");
            else Message = "Password change was not confirmed. Check your current password and backend password requirements.";
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { if (epoch == generation) Message = "Password change was not confirmed. Sign in again to check the result."; }
        finally { authentication.Release(); Changed?.Invoke(); }
    }
    public async Task ForgotPasswordAsync(string username)
    {
        if (Stage != SignInStage.Login || !await authentication.WaitAsync(0)) return;
        var epoch = generation;
        try
        {
            if (string.IsNullOrWhiteSpace(username)) { Message = "Enter your email address."; return; }
            var result = await gateway.ForgotPasswordAsync(username, lifetime.Token);
            if (epoch == generation) Message = SourceAuthenticationGateway.IsSuccess(result)
                ? "Password recovery request received. Check your email."
                : "Password recovery was not confirmed. Contact your administrator if needed.";
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { if (epoch == generation) Message = "Password recovery was not confirmed. Check your connection."; }
        finally { authentication.Release(); Changed?.Invoke(); }
    }
    public async Task LogoutAsync()
    {
        var snapshot = Current;
        Clear(""); // Invalidate pending work before any network termination request.
        if (snapshot is null) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await gateway.TerminateAsync(snapshot, timeout.Token); }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { }
    }

    private void Clear(string message)
    {
        lock (stateLock)
        {
        generation++;
        lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        Current = null; Identity = null; password = null; email = null; failures = 0; resendAfter = default; validated = false;
        Stage = SignInStage.Login; Message = message; Changed?.Invoke();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Clear(""); lifetime.Cancel(); lifetime.Dispose();
        // Semaphores stay alive for pending operations to release safely.
    }
}
