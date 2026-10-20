namespace OBS.Core;

public enum PrivacyStage { Locked, CodeRequested, Verified }

// One instance per applicant screen/circuit. Hosts must dispose it and discard rendered
// copies when Changed fires. No persistence, background OTP delivery, or implicit ID lookup.
public sealed class ApplicantPrivacyController : IDisposable
{
    private readonly PrivacyApiService api;
    private readonly SessionController session;
    private readonly TimeProvider time;
    private readonly object gate = new();
    private CancellationTokenSource lifetime = new();
    private object? identity;
    private string? applicant;
    private long generation;
    private bool disposed, busy;
    private PrivacyStage stage;
    private string message = "";
    private PiiAccessToken? credential;
    private PrivacyVerification? verification;
    private ITimer? expiry;
    private readonly HashSet<PrivacySection> visible = [];
    private readonly long[] sectionVersions = new long[4];
    private ApplicantBio? bio;
    private IReadOnlyList<ApplicantAddress>? addresses;
    private IReadOnlyList<ApplicantEmergency>? emergency;
    private IReadOnlyList<ApplicantEducation>? education;

    public ApplicantPrivacyController(PrivacyApiService api, SessionController session, TimeProvider? clock = null)
    {
        this.api = api; this.session = session; time = clock ?? TimeProvider.System;
        session.Changed += SessionChanged;
    }
    public event Action? Changed;
    public bool HasApplicant { get { lock (gate) return ContextLocked(); } }
    public PrivacyStage Stage { get { lock (gate) return ContextLocked() && (verification is null || verification.ExpiresAt > time.GetUtcNow()) ? stage : PrivacyStage.Locked; } }
    public bool IsBusy { get { lock (gate) return ContextLocked() && busy; } }
    public string Message { get { lock (gate) return message; } }
    public DateTimeOffset? ExpiresAt { get { lock (gate) return ContextLocked() ? verification?.ExpiresAt : null; } }
    public ApplicantBio? Bio { get { lock (gate) return VisibleLocked(PrivacySection.Bio) ? bio : null; } }
    public IReadOnlyList<ApplicantAddress>? Addresses { get { lock (gate) return VisibleLocked(PrivacySection.Address) ? addresses : null; } }
    public IReadOnlyList<ApplicantEmergency>? EmergencyContacts { get { lock (gate) return VisibleLocked(PrivacySection.Emergency) ? emergency : null; } }
    public IReadOnlyList<ApplicantEducation>? Education { get { lock (gate) return VisibleLocked(PrivacySection.Education) ? education : null; } }
    public bool IsVisible(PrivacySection section) { lock (gate) return VisibleLocked(section); }
    public bool CanReveal(PrivacySection section) { lock (gate) return AllowedLocked(section); }
    public override string ToString() => "[applicant privacy state redacted]";

    // The caller must supply an evidenced canonical citizen ID, not an assumed reference alias.
    public bool SelectApplicant(string citizenId)
    {
        if (!CitizenId.IsValid(citizenId)) throw new ArgumentException("Enter a Citizen ID.", nameof(citizenId));
        bool selected;
        lock (gate)
        {
            if (disposed) return false;
            if (!session.ProtectedWorkAllowed || session.Identity is null) { InvalidateLocked(true); selected = false; }
            else
            {
                if (applicant != citizenId || identity != session.Identity)
                { InvalidateLocked(); applicant = citizenId; identity = session.Identity; }
                selected = true;
            }
        }
        Changed?.Invoke(); return selected;
    }

    public async Task RequestCodeAsync(CancellationToken token = default)
    {
        using var operation = Begin(() => true, token, clearAuthorization: true);
        if (operation is null) return;
        Changed?.Invoke();
        try
        {
            var result = await api.RequestOtpAsync(operation.Staff, operation.Applicant, operation.Token);
            lock (gate)
            {
                if (!CurrentLocked(operation)) return;
                if (PrivacyResults.Failure(result) is { } failure) FailLocked(failure);
                else { stage = PrivacyStage.CodeRequested; message = "Enter the six-digit code sent to your email."; }
            }
        }
        catch (OperationCanceledException) { CancelOperation(operation); }
        finally { Finish(operation); }
    }

    public async Task VerifyCodeAsync(string otp, CancellationToken token = default)
    {
        if (otp is null || otp.Length != 6 || otp.Any(c => c is < '0' or > '9'))
        {
            lock (gate) { if (disposed || busy) return; message = "Enter exactly six code digits."; }
            Changed?.Invoke(); return;
        }
        using var operation = Begin(() => stage == PrivacyStage.CodeRequested, token);
        if (operation is null) return;
        Changed?.Invoke();
        try
        {
            var result = await api.VerifyOtpAsync(operation.Staff, operation.Applicant, otp, operation.Token);
            var pii = PiiAccessToken.FromVerification(result);
            lock (gate)
            {
                if (!CurrentLocked(operation)) return;
                if (pii is null)
                {
                    message = "Privacy verification was not confirmed. Check your code or request a new one.";
                    return;
                }
            }
            var status = await api.GetVerificationStatusAsync(operation.Staff, operation.Applicant, pii, operation.Token);
            lock (gate)
            {
                if (!CurrentLocked(operation)) return;
                if (status.Outcome != PrivacyReadOutcome.Success || status.Verification is null) FailLocked(status.Outcome);
                else { credential = pii; ApplyLocked(status.Verification); if (stage == PrivacyStage.Verified) message = ""; }
            }
        }
        catch (OperationCanceledException) { CancelOperation(operation); }
        finally { Finish(operation); }
    }

    // Status is checked before each new reveal. Hiding drops that section's data immediately.
    public async Task RevealAsync(PrivacySection section, CancellationToken token = default)
    {
        if (!Enum.IsDefined(section)) throw new ArgumentOutOfRangeException(nameof(section));
        using var operation = Begin(() => AllowedLocked(section), token, section: section);
        if (operation is null) return;
        var pii = operation.Credential!;
        Changed?.Invoke();
        try
        {
            var status = await api.GetVerificationStatusAsync(operation.Staff, operation.Applicant, pii, operation.Token);
            lock (gate)
            {
                if (!CurrentLocked(operation)) return;
                if (status.Outcome != PrivacyReadOutcome.Success || status.Verification is null) { FailLocked(status.Outcome); return; }
                ApplyLocked(status.Verification);
                if (!CurrentLocked(operation) || operation.SectionVersion != sectionVersions[(int)section]) return;
                if (!AllowedLocked(section)) { message = "You are not authorized to view this section."; return; }
            }
            var grant = status.Verification!;
            PrivacyReadOutcome outcome;
            object? snapshot;
            switch (section)
            {
                case PrivacySection.Bio:
                    var bioReply = await api.GetBioSnapshotAsync(operation.Staff, operation.Applicant, pii, grant, operation.Token);
                    outcome = bioReply.Outcome; snapshot = bioReply.Snapshot; break;
                case PrivacySection.Address:
                    var addressReply = await api.GetAddressSnapshotAsync(operation.Staff, operation.Applicant, pii, grant, operation.Token);
                    outcome = addressReply.Outcome; snapshot = addressReply.Snapshot; break;
                case PrivacySection.Emergency:
                    var emergencyReply = await api.GetEmergencySnapshotAsync(operation.Staff, operation.Applicant, pii, grant, operation.Token);
                    outcome = emergencyReply.Outcome; snapshot = emergencyReply.Snapshot; break;
                default:
                    var educationReply = await api.GetEducationSnapshotAsync(operation.Staff, operation.Applicant, pii, grant, operation.Token);
                    outcome = educationReply.Outcome; snapshot = educationReply.Snapshot; break;
            }
            lock (gate)
            {
                if (!CurrentLocked(operation)) return;
                if (outcome != PrivacyReadOutcome.Success || snapshot is null || !AllowedLocked(section)) { FailLocked(outcome); return; }
                if (operation.SectionVersion != sectionVersions[(int)section]) return;
                switch (section)
                {
                    case PrivacySection.Bio: bio = (ApplicantBio)snapshot; break;
                    case PrivacySection.Address: addresses = (IReadOnlyList<ApplicantAddress>)snapshot; break;
                    case PrivacySection.Emergency: emergency = (IReadOnlyList<ApplicantEmergency>)snapshot; break;
                    case PrivacySection.Education: education = (IReadOnlyList<ApplicantEducation>)snapshot; break;
                }
                visible.Add(section); message = "";
            }
        }
        catch (OperationCanceledException) { CancelOperation(operation); }
        finally { Finish(operation); }
    }

    // Hosts should call on foreground revalidation; no undocumented polling interval is added.
    public async Task RevalidateAsync(CancellationToken token = default)
    {
        using var operation = Begin(() => credential is not null && stage == PrivacyStage.Verified &&
            verification is not null && verification.ExpiresAt > time.GetUtcNow(), token);
        if (operation is null) return;
        var pii = operation.Credential!;
        Changed?.Invoke();
        try
        {
            var reply = await api.GetVerificationStatusAsync(operation.Staff, operation.Applicant, pii, operation.Token);
            lock (gate)
            {
                if (!CurrentLocked(operation)) return;
                if (reply.Outcome != PrivacyReadOutcome.Success || reply.Verification is null) FailLocked(reply.Outcome);
                else { ApplyLocked(reply.Verification); if (stage == PrivacyStage.Verified) message = ""; }
            }
        }
        catch (OperationCanceledException) { CancelOperation(operation); }
        finally { Finish(operation); }
    }

    public void Hide(PrivacySection section) { lock (gate) { if (disposed) return; HideLocked(section); } Changed?.Invoke(); }
    public void Cancel() { lock (gate) { if (disposed) return; InvalidateLocked(); } Changed?.Invoke(); }
    public void ClearApplicant() { lock (gate) { if (disposed) return; InvalidateLocked(true); } Changed?.Invoke(); }

    private bool ContextLocked() => !disposed && applicant is not null && identity is not null && identity == session.Identity && session.ProtectedWorkAllowed;
    private bool AllowedLocked(PrivacySection section) => ContextLocked() && stage == PrivacyStage.Verified && credential is not null &&
        verification?.Allows(section, applicant!, time.GetUtcNow()) == true;
    private bool VisibleLocked(PrivacySection section) => visible.Contains(section) && AllowedLocked(section);
    private bool CurrentLocked(Operation operation) => ContextLocked() && operation.Generation == generation && !operation.Token.IsCancellationRequested;
    private Operation? Begin(Func<bool> allowed, CancellationToken token, bool clearAuthorization = false, PrivacySection? section = null)
    {
        lock (gate)
        {
            if (!ContextLocked() || busy || token.IsCancellationRequested || !allowed()) return null;
            if (clearAuthorization) InvalidateLocked();
            busy = true; message = "";
            return new(generation, session.Current!, applicant!, credential, section is { } group ? sectionVersions[(int)group] : 0,
                CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token));
        }
    }
    private void Finish(Operation operation)
    {
        bool notify;
        lock (gate) { notify = !disposed; if (operation.Generation == generation && !disposed) busy = false; }
        if (notify) Changed?.Invoke();
    }
    private void CancelOperation(Operation operation)
    { lock (gate) { if (!disposed && operation.Generation == generation) InvalidateLocked(); } }
    private void FailLocked(PrivacyReadOutcome outcome)
    {
        InvalidateLocked();
        message = outcome switch
        {
            PrivacyReadOutcome.ConnectionFailure => "Unable to confirm privacy access. Check your connection and verify again.",
            PrivacyReadOutcome.Expired => "Privacy verification has expired. Verify again to view protected details.",
            PrivacyReadOutcome.NotPermitted => "Privacy access was denied. Verify your access before continuing.",
            _ => "Privacy access could not be confirmed. Verify again before viewing protected details."
        };
    }
    private void ApplyLocked(PrivacyVerification grant)
    {
        verification = grant; stage = PrivacyStage.Verified;
        foreach (var section in visible.ToArray()) if (!AllowedLocked(section)) HideLocked(section);
        ScheduleExpiryLocked();
    }
    private void ScheduleExpiryLocked()
    {
        expiry?.Dispose(); expiry = null;
        if (verification is null) return;
        var remaining = verification.ExpiresAt - time.GetUtcNow();
        if (remaining <= TimeSpan.Zero) { FailLocked(PrivacyReadOutcome.Expired); return; }
        // Short timer slices support long absolute server expiries without inventing a TTL.
        expiry = time.CreateTimer(_ => ExpiryReached(), null, remaining < TimeSpan.FromDays(1) ? remaining : TimeSpan.FromDays(1), Timeout.InfiniteTimeSpan);
    }
    private void ExpiryReached()
    {
        bool notify = false;
        lock (gate)
        {
            if (disposed || verification is null) return;
            if (!ContextLocked() || verification.ExpiresAt <= time.GetUtcNow()) { FailLocked(PrivacyReadOutcome.Expired); notify = true; }
            else ScheduleExpiryLocked();
        }
        if (notify) Changed?.Invoke();
    }
    private void HideLocked(PrivacySection section)
    {
        if (!Enum.IsDefined(section)) return;
        sectionVersions[(int)section]++;
        visible.Remove(section);
        switch (section)
        {
            case PrivacySection.Bio: bio = null; break;
            case PrivacySection.Address: addresses = null; break;
            case PrivacySection.Emergency: emergency = null; break;
            case PrivacySection.Education: education = null; break;
        }
    }
    private void InvalidateLocked(bool clearApplicant = false)
    {
        generation++; lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        expiry?.Dispose(); expiry = null; credential = null; verification = null; visible.Clear();
        bio = null; addresses = null; emergency = null; education = null;
        busy = false; stage = PrivacyStage.Locked; message = "";
        if (clearApplicant) { applicant = null; identity = null; }
    }
    private void SessionChanged()
    {
        bool notify;
        lock (gate)
        {
            notify = !disposed && (identity != session.Identity || !session.ProtectedWorkAllowed);
            if (notify) InvalidateLocked(true);
        }
        if (notify) Changed?.Invoke();
    }
    public void Dispose()
    {
        session.Changed -= SessionChanged;
        lock (gate)
        {
            if (disposed) return;
            disposed = true; InvalidateLocked(true); lifetime.Cancel(); lifetime.Dispose();
        }
        Changed?.Invoke();
    }
    private sealed class Operation(long generation, StaffSession staff, string applicant, PiiAccessToken? credential,
        long sectionVersion, CancellationTokenSource cancellation) : IDisposable
    {
        public long Generation { get; } = generation;
        public StaffSession Staff { get; } = staff;
        public string Applicant { get; } = applicant;
        public PiiAccessToken? Credential { get; } = credential;
        public long SectionVersion { get; } = sectionVersion;
        public CancellationToken Token { get; } = cancellation.Token;
        public void Dispose() => cancellation.Dispose();
    }
}
