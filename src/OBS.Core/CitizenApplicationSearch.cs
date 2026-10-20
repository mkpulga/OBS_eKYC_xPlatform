namespace OBS.Core;

public enum CitizenSearchSource { InformationGathering, CardholderUpdate }

public static class CitizenSearchPresentation
{
    public const string Missing = "Not available.";
    public static string Label(CitizenSearchSource source) => source == CitizenSearchSource.InformationGathering ? "IG CID" : "CU CID";
    public static string Authority(CitizenSearchSource source) => source == CitizenSearchSource.InformationGathering ? "VINFORMATIONGATHERING" : "VCARDHOLDERUPDATE";
    public static CitizenSearchSource? Source(string label) => label switch
    { "IG CID" => CitizenSearchSource.InformationGathering, "CU CID" => CitizenSearchSource.CardholderUpdate, _ => null };
    public static bool Allowed(StaffSession? staff, CitizenSearchSource source) => Enum.IsDefined(source) &&
        staff is { PasswordChangeRequired: false } && staff.Tokens.IsComplete && staff.Allows(Authority(source));
    public static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? Missing : value.Trim();
    public static string Error(PrivacyReadOutcome outcome) => outcome switch
    {
        PrivacyReadOutcome.NotFound => "No record found for this Citizen ID. Check the ID and try again.",
        PrivacyReadOutcome.NotPermitted => "Your account is not permitted to use this Citizen ID search.",
        PrivacyReadOutcome.ConnectionFailure => "Unable to reach the server. Check your connection and try again.",
        PrivacyReadOutcome.InvalidResponse => "The server returned an invalid Citizen ID response. Please try again.",
        PrivacyReadOutcome.Unavailable => "Citizen ID search is unavailable for this backend configuration.",
        PrivacyReadOutcome.Cancelled => "Search cancelled. Please try again.",
        _ => "The server declined the Citizen ID search. Please try again."
    };
}

// One dashboard lifetime, shared by web and native clients. Only a validated response
// opens View Application; application-number services never receive a Citizen ID.
public sealed class CitizenApplicationSearch : IDisposable
{
    private readonly PrivacyApiService? api;
    private readonly SessionController session;
    private readonly Func<PrivacyScreenState> createPrivacy;
    private readonly object gate = new();
    private CancellationTokenSource? request;
    private object? identity;
    private long generation;
    private bool disposed, busy;
    private string type = "Application Number", input = "", message = "";
    private CitizenSearchSource? source;
    private CitizenSearchSource? pendingSource;
    private string citizenId = "";
    private ApplicantDetailSnapshot? snapshot;
    private PrivacyScreenState? privacy;
    public event Action? Changed;
    public CitizenApplicationSearch(PrivacyApiService? api, SessionController session, Func<PrivacyScreenState> createPrivacy)
    { this.api = api; this.session = session; this.createPrivacy = createPrivacy; session.Changed += SessionChanged; }
    public bool IsBusy { get { lock (gate) return busy; } }
    public string Message { get { lock (gate) return message; } }
    public CitizenSearchSource? Source { get { lock (gate) return source; } }
    public string CitizenId { get { lock (gate) return citizenId; } }
    public ApplicantDetailSnapshot? Snapshot { get { lock (gate) return CurrentLocked() ? snapshot : null; } }
    public PrivacyScreenState? Privacy { get { lock (gate) return CurrentLocked() && privacy?.CitizenId == citizenId ? privacy : null; } }
    private bool CurrentLocked() => !disposed && identity is not null && identity == session.Identity && session.ProtectedWorkAllowed &&
        source is { } selected && CitizenSearchPresentation.Allowed(session.Current, selected);

    // Editing a search cancels its pending request without changing an already displayed record.
    public void SetCriteria(string searchType, string value)
    {
        lock (gate)
        {
            if (disposed || (type == searchType && input == value)) return;
            CancelLocked(); type = searchType; input = value; message = "";
        }
        Changed?.Invoke();
    }
    public Task<bool> SearchAsync() => RunAsync(false);
    public Task<bool> RefreshAsync() => RunAsync(true);
    private async Task<bool> RunAsync(bool refresh)
    {
        CancellationTokenSource operation;
        CitizenSearchSource selected;
        StaffSession staff;
        string id;
        long version;
        object? staffIdentity;
        lock (gate)
        {
            if (disposed || busy) return false;
            var selectedSource = refresh ? source : CitizenSearchPresentation.Source(type);
            id = refresh ? citizenId : input.Trim();
            if (selectedSource is null) return false;
            selected = selectedSource.Value;
            ClearResultLocked(); message = "";
            if (id.Length == 0) { message = "Enter a Citizen ID."; }
            else if (!session.ProtectedWorkAllowed || !CitizenSearchPresentation.Allowed(session.Current, selected))
                message = CitizenSearchPresentation.Error(PrivacyReadOutcome.NotPermitted);
            else if (api is null) message = CitizenSearchPresentation.Error(PrivacyReadOutcome.Unavailable);
            if (message.Length > 0) { Changed?.Invoke(); return false; }
            staff = session.Current!; staffIdentity = session.Identity;
            identity = staffIdentity;
            pendingSource = selected;
            operation = request = new(); version = ++generation; busy = true;
        }
        Changed?.Invoke();
        try
        {
            var result = selected == CitizenSearchSource.InformationGathering
                ? await api!.GetInformationGatheringSnapshotAsync(staff, id, token: operation.Token)
                : await api!.GetCardholderUpdateSnapshotAsync(staff, id, token: operation.Token);
            lock (gate)
            {
                if (disposed || operation.IsCancellationRequested || version != generation || staffIdentity != session.Identity || !session.ProtectedWorkAllowed) return false;
                if (!CitizenSearchPresentation.Allowed(session.Current, selected))
                { message = CitizenSearchPresentation.Error(PrivacyReadOutcome.NotPermitted); return false; }
                if (result.Outcome != PrivacyReadOutcome.Success || result.Snapshot is null)
                { message = CitizenSearchPresentation.Error(result.Outcome); return false; }
                source = selected; citizenId = id; identity = staffIdentity; snapshot = result.Snapshot;
                privacy = createPrivacy(); privacy.SetCitizenId(id); privacy.SelectApplicant(); privacy.Changed += PrivacyChanged;
                return true;
            }
        }
        catch (OperationCanceledException) { return false; }
        finally
        {
            lock (gate) { if (request == operation) { request = null; pendingSource = null; busy = false; } }
            operation.Dispose(); Changed?.Invoke();
        }
    }
    private void PrivacyChanged() => Changed?.Invoke();
    private void SessionChanged()
    {
        lock (gate)
        {
            if (disposed) return;
            if (session.ProtectedWorkAllowed && (identity is null || identity == session.Identity) &&
                (source is null || CitizenSearchPresentation.Allowed(session.Current, source.Value)) &&
                (!busy || (pendingSource is { } pending && CitizenSearchPresentation.Allowed(session.Current, pending)))) return;
            CancelLocked(); ClearResultLocked(); message = "";
        }
        Changed?.Invoke();
    }
    public void Leave()
    {
        lock (gate) { if (disposed) return; CancelLocked(); ClearResultLocked(); message = ""; }
        Changed?.Invoke();
    }
    private void CancelLocked() { generation++; request?.Cancel(); request = null; pendingSource = null; busy = false; }
    private void ClearResultLocked()
    {
        if (privacy is not null) { privacy.Changed -= PrivacyChanged; privacy.Dispose(); privacy = null; }
        snapshot = null; source = null; citizenId = ""; identity = null;
    }
    public void Dispose()
    {
        session.Changed -= SessionChanged;
        lock (gate) { if (disposed) return; disposed = true; CancelLocked(); ClearResultLocked(); message = input = ""; }
    }
    public override string ToString() => "[Citizen ID search redacted]";
}
