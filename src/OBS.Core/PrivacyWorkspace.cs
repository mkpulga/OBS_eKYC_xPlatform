namespace OBS.Core;

// Scoped to a web circuit or installed app. Controllers remain scoped to individual
// screens and own all transient privacy state. No request is made during construction.
public sealed class PrivacyWorkspace : IDisposable
{
    private readonly HttpClient? client;
    private readonly PrivacyApiService? api;
    private readonly TimeProvider clock = TimeProvider.System;
    private bool disposed;
    private readonly object gate = new();
    private readonly List<WeakReference<ApplicantPrivacyController>> controllers = [];
    public bool Available => !disposed && api is not null;
    public PrivacyWorkspace() { }
    public PrivacyWorkspace(BackendOptions existingOptions)
    {
        var privacy = ForExistingRoot(existingOptions);
        client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        api = new(new ApiTransport(client, privacy), privacy, clock);
    }
    // Ownership of the supplied client transfers on successful construction.
    public PrivacyWorkspace(HttpClient client, BackendOptions existingOptions, TimeProvider? clock = null)
    {
        var privacy = ForExistingRoot(existingOptions);
        this.client = client; this.clock = clock ?? TimeProvider.System;
        api = new(new ApiTransport(client, privacy), privacy, this.clock);
    }
    private static BackendOptions ForExistingRoot(BackendOptions existing)
    {
        // Preserve the exact configured API root/environment. This adapts only the new
        // service's contract; existing login, reference lookup and configuration are retained.
        var privacy = existing with { BaseUrl = existing.GetApiRoot().AbsoluteUri, ApiRootPath = "/api/v1", ApiContract = ApiContract.Privacy };
        if (privacy.GetApiRoot() != existing.GetApiRoot()) throw new ArgumentException("Privacy workspace must use the existing backend API root.");
        return privacy;
    }
    public ApplicantPrivacyController CreateController(SessionController session)
    {
        lock (gate)
        {
            if (!Available) throw new InvalidOperationException("Privacy verification is unavailable for this configuration.");
            controllers.RemoveAll(item => !item.TryGetTarget(out _));
            var controller = new ApplicantPrivacyController(api!, session, clock);
            controllers.Add(new(controller)); return controller;
        }
    }
    public PrivacyScreenState CreateScreen(SessionController session) => new(CreateController(session), session);
    public CitizenApplicationSearch CreateCitizenSearch(SessionController session) => new(api, session, () => CreateScreen(session));
    public void Dispose()
    {
        ApplicantPrivacyController[] active;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            active = controllers.Select(item => item.TryGetTarget(out var controller) ? controller : null).OfType<ApplicantPrivacyController>().ToArray();
            controllers.Clear();
        }
        foreach (var controller in active) controller.Dispose();
        client?.Dispose();
    }
    public override string ToString() => "[privacy workspace]";
}
