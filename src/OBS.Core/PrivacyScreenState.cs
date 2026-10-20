namespace OBS.Core;

// Shared interaction state for the parallel web/native view. Views dispatch Changed on
// their UI context and render fresh fields; no personal snapshots are cached here.
public sealed class PrivacyScreenState : IDisposable
{
    private readonly ApplicantPrivacyController flow;
    private readonly SessionController session;
    private StaffSession? observedStaff;
    private bool disposed, selected, dialog;
    private string citizenId = "", otp = "", notice = "";
    private PrivacySection? pending;
    private long generation;
    public event Action? Changed;
    public string CitizenId => citizenId;
    public string Otp => otp;
    public bool Selected => !disposed && selected && flow.HasApplicant;
    public bool DialogOpen => !disposed && dialog && Selected && session.ProtectedWorkAllowed;
    public bool IsBusy => !disposed && flow.IsBusy;
    public PrivacyStage Stage => flow.Stage;
    public string Message => string.IsNullOrEmpty(notice) ? flow.Message : notice;
    public bool CanVerify => DialogOpen && !IsBusy && Stage == PrivacyStage.CodeRequested;
    public bool IsVisible(PrivacySection section) => Selected && flow.IsVisible(section);
    public string Status => Stage == PrivacyStage.Verified ? "Privacy verification complete." : "Protected details are hidden.";
    public IReadOnlyList<ApplicationField> Fields(PrivacySection section, string missing = "Not provided") => Selected ? PrivacyPresentation.Fields(flow, section, missing) : [];

    public PrivacyScreenState(ApplicantPrivacyController flow, SessionController session)
    {
        this.flow = flow; this.session = session; observedStaff = session.Current;
        flow.Changed += FlowChanged; session.Changed += SessionChanged; session.RefreshChecked += RefreshChecked;
    }
    public void SetCitizenId(string value)
    {
        if (disposed || citizenId == value) return;
        generation++; selected = false; dialog = false; pending = null; otp = ""; notice = ""; citizenId = value;
        flow.ClearApplicant();
    }
    public void SetOtp(string value) { if (!disposed && DialogOpen && !IsBusy) otp = value; }
    public void SelectApplicant()
    {
        if (disposed || IsBusy) return;
        generation++; dialog = false; pending = null; otp = ""; notice = "";
        try { citizenId = citizenId.Trim(); selected = flow.SelectApplicant(citizenId); }
        catch (ArgumentException) { selected = false; notice = "Enter a valid citizen ID."; }
        Changed?.Invoke();
    }
    public async Task ShowOrHideAsync(PrivacySection section)
    {
        if (disposed || !Selected || IsBusy) return;
        notice = "";
        if (flow.IsVisible(section)) flow.Hide(section);
        else if (flow.Stage == PrivacyStage.Verified)
        {
            if (flow.CanReveal(section)) await flow.RevealAsync(section);
            else { notice = "Your verification does not allow this section."; Changed?.Invoke(); }
        }
        else { pending = section; otp = ""; dialog = true; Changed?.Invoke(); }
    }
    public async Task RequestCodeAsync()
    {
        if (!DialogOpen || IsBusy) return;
        notice = ""; otp = "";
        await flow.RequestCodeAsync();
    }
    public async Task VerifyCodeAsync()
    {
        if (!CanVerify) return;
        var epoch = generation; var requested = pending; var code = otp; otp = ""; notice = "";
        await flow.VerifyCodeAsync(code);
        if (disposed || epoch != generation || !DialogOpen || flow.Stage != PrivacyStage.Verified) return;
        dialog = false; pending = null;
        if (requested is { } section && flow.CanReveal(section)) await flow.RevealAsync(section);
        else notice = "Your verification does not allow this section.";
        if (!disposed) Changed?.Invoke();
    }
    public void CancelDialog()
    {
        if (disposed) return;
        generation++; dialog = false; pending = null; otp = ""; notice = "";
        flow.Cancel();
    }
    private void FlowChanged()
    {
        if (disposed) return;
        if (!session.ProtectedWorkAllowed || (selected && !flow.HasApplicant))
        { generation++; selected = false; citizenId = otp = notice = ""; dialog = false; pending = null; }
        Changed?.Invoke();
    }
    private void SessionChanged()
    {
        if (disposed) return;
        var current = session.Current;
        var replaced = !ReferenceEquals(current, observedStaff); observedStaff = current;
        if (replaced && Selected && flow.Stage == PrivacyStage.Verified) _ = RevalidateAsync();
    }
    private async Task RevalidateAsync()
    {
        try { await flow.RevalidateAsync(); }
        catch (OperationCanceledException) { }
    }
    private void RefreshChecked(RefreshDiagnostic result)
    {
        if (disposed || result.Reason == RefreshReason.Success) return;
        CancelDialog(); notice = "Your staff session could not be checked. Verify privacy again after session validation.";
        Changed?.Invoke();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; generation++; selected = dialog = false; citizenId = otp = notice = ""; pending = null; observedStaff = null;
        flow.Changed -= FlowChanged; session.Changed -= SessionChanged; session.RefreshChecked -= RefreshChecked;
        flow.Dispose();
    }
    public override string ToString() => "[privacy screen state redacted]";
}
