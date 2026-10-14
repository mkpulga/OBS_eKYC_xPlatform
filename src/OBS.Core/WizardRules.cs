namespace OBS.Core;

public enum EnrollmentStep { Personal, Contact, Address, EmergencyContact, Identification, AdditionalRequirements, Education, FacePhoto, Signature, Fingerprint }
public static class WizardRules
{
    public static IReadOnlyList<EnrollmentStep> Steps(FeatureSet features) => features == FeatureSet.Bacoor
        ? [EnrollmentStep.Personal, EnrollmentStep.Contact, EnrollmentStep.Address, EnrollmentStep.EmergencyContact, EnrollmentStep.Identification,
           EnrollmentStep.AdditionalRequirements, EnrollmentStep.Education, EnrollmentStep.FacePhoto, EnrollmentStep.Signature, EnrollmentStep.Fingerprint]
        : [EnrollmentStep.Personal, EnrollmentStep.Contact, EnrollmentStep.Address, EnrollmentStep.EmergencyContact, EnrollmentStep.Identification,
           EnrollmentStep.FacePhoto, EnrollmentStep.Signature, EnrollmentStep.Fingerprint];

    private static readonly HashSet<string> BackIdTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALIEN/IMMIGRANT CERTIFICATION REGISTRATION", "ALIEN/IMMIGRANT CERTIFICATION OF REGISTRATION", "HDMF ID (PAG-IBIG)",
        "HDMFIDPAGIBIG", "NATIONAL ID", "SENIOR CITIZEN CARD WITH PHOTO", "STUDENT'S ID"
    };
    public static bool RequiresBackId(string? type) => type is not null && BackIdTypes.Contains(type.Trim());

    // Address.cs counts visible City, then one separator plus each other nonblank visible component.
    // Province/Region/ZIP and structured unit/house number are not part of the source's 90-character sum.
    public static int AddressCharacterCount(string? city, string? barangay, string? street, string? building,
        string? line1, string? line2, bool structured)
    {
        static bool Has(string? value) => !string.IsNullOrWhiteSpace(value) && !value.StartsWith("-Select a ", StringComparison.Ordinal);
        var count = Has(city) ? city!.Length : 0;
        var visible = structured ? new[] { barangay, street, building } : new[] { barangay, line1, line2 };
        return count + visible.Where(Has).Sum(value => value!.Length + 1);
    }
}

// Only successful remote save followed by an authoritative reload may update saved completion.
// UI field edits and navigation have no API side effects. The actual section forms remain unimplemented.
public sealed class WizardState
{
    private readonly HashSet<EnrollmentStep> savedComplete = [];
    public string RecordId { get; }
    public IReadOnlyList<EnrollmentStep> Steps { get; }
    public EnrollmentStep CurrentStep { get; private set; }
    public bool BackendSubmissionRulesVerified { get; private set; }
    public IReadOnlyList<EnrollmentStep> Incomplete => Steps.Where(step => !savedComplete.Contains(step)).ToArray();
    public double Progress => (double)(Steps.Count - Incomplete.Count) / Steps.Count;
    public bool CanSubmit => BackendSubmissionRulesVerified && Incomplete.Count == 0;
    public string SubmissionBlock => !BackendSubmissionRulesVerified ? "Submission is blocked until backend completion rules are verified."
        : Incomplete.Count > 0 ? "Save and complete: " + string.Join(", ", Incomplete) : "";
    public WizardState(string recordId, FeatureSet features)
    {
        if (string.IsNullOrWhiteSpace(recordId)) throw new ArgumentException("Stable backend record identifier is required.");
        RecordId = recordId; Steps = WizardRules.Steps(features); CurrentStep = Steps[0];
    }
    public void Navigate(EnrollmentStep step)
    {
        if (!Steps.Contains(step)) throw new ArgumentException("Step is not available in this feature set.");
        CurrentStep = step;
    }
    public void ApplyPersistedCompletion(IEnumerable<EnrollmentStep> completedSteps, bool verifiedSubmissionRules)
    {
        savedComplete.Clear(); savedComplete.UnionWith(completedSteps.Where(Steps.Contains));
        BackendSubmissionRulesVerified = verifiedSubmissionRules;
    }
}
