using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using OBS.Core;
using OBS.Web.Components;

var passed = 0;
void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
void Pass(string name) { passed++; Console.WriteLine("PASS " + name); }
foreach (var permissions in new[] { Array.Empty<string>(), new[] { "VINFORMATIONGATHERING" }, new[] { "VCARDHOLDERUPDATE" }, new[] { "PENDINGORS" }, new[] { "VINFORMATIONGATHERING", "VCARDHOLDERUPDATE" } })
{
    await using var rig = await Rig.Create(permissions);
    var html = await rig.Html();
    Check(html.Contains("<option>IG CID</option>") == permissions.Contains("VINFORMATIONGATHERING"), "IG option permission mismatch");
    Check(html.Contains("<option>CU CID</option>") == permissions.Contains("VCARDHOLDERUPDATE"), "CU option permission mismatch");
    Check(html.Contains("<option>Application Number</option><option>QR Code</option><option>Cardholder</option><option>Reference No.</option>") && rig.Dashboard.SearchType == "Application Number", "Existing options/default changed");
    Pass("dropdown/default with " + string.Join(",", permissions));
}
foreach (var label in new[] { "IG CID", "CU CID" })
{
    // Deliberately omit PENDINGORS: the CID result must pass its own module gate.
    await using var rig = await Rig.Create([label == "IG CID" ? "VINFORMATIONGATHERING" : "VCARDHOLDERUPDATE"]);
    await rig.Search(label, "   ");
    var html = await rig.Html();
    Check(html.Contains("Enter a Citizen ID.") && rig.Handler.Paths.Count == 0 && !html.Contains("application-profile"), "Blank search navigated/dispatched");
    Pass(label + " blank dashboard error");
    rig.Handler.Status = HttpStatusCode.Unauthorized; rig.Handler.Body = "{\"code\":2004}";
    await rig.Search(label, "101"); html = await rig.Html();
    Check(html.Contains("not permitted") && !html.Contains("application-profile") && rig.Dashboard.Query == "101" && rig.Dashboard.SearchType == label, "Denial navigated or lost search input/type");
    Pass(label + " endpoint denial preserves dashboard criteria");
    rig.Handler.Status = HttpStatusCode.OK; rig.Handler.Body = PrivacyResponseTests.Detail(true);
    await rig.Search(label, " \t101\r\n"); html = await rig.Html();
    Check(html.Contains("application-profile") && html.Contains("REF-TEST") && html.Contains("Citizen ID: 101") && html.Contains(label), "Direct result identity/layout missing");
    Check(html.Contains("privacy-mask") && html.Contains("Photo: Not available.") && !html.Contains("03/04/2001") && !html.Contains("Example school"), "Missing/withheld state leaked or wrong placeholder");
    Check(rig.Handler.Paths.Count == 2 && rig.Records.Calls == 0, "Opened result through pending lookup");
    Pass(label + " direct binding/masking without pending permission");
    var privacy = rig.Dashboard.SearchState.Privacy!;
    await rig.Dispatch(async () => await privacy.ShowOrHideAsync(PrivacySection.Bio)); html = await rig.Html();
    Check(html.Contains("Verify protected access") && !html.Contains("03/04/2001") && rig.Handler.Paths.Count == 2, "OTP prompt bypassed or auto-requested");
    await rig.Dispatch(async () => { await privacy.RequestCodeAsync(); privacy.SetOtp("123456"); await privacy.VerifyCodeAsync(); }); html = await rig.Html();
    Check(html.Contains("03/04/2001") && !html.Contains("value=\"123456\"") && !html.Contains("opaque-pii"), "Verified rendering failed or exposed credential");
    await rig.Dispatch(() => { rig.Application!.OpenProtectedDetails(PrivacySection.Bio); return Task.CompletedTask; }); html = await rig.Html();
    Check(html.Contains("<dialog") && html.Contains("03/04/2001"), "Protected detail dialog not opened");
    await rig.Dispatch(async () => await privacy.ShowOrHideAsync(PrivacySection.Bio)); html = await rig.Html();
    Check(!html.Contains("03/04/2001") && !html.Contains("<dialog"), "Hiding retained protected detail dialog");
    Pass(label + " existing OTP reveal and hide clears details");
    await rig.Dispatch(async () => await privacy.ShowOrHideAsync(PrivacySection.Bio));
    rig.Clock.Advance(); await rig.Dispatch(() => Task.CompletedTask); html = await rig.Html();
    Check(!html.Contains("03/04/2001") && html.Contains("privacy-mask"), "Expiry retained protected display");
    Pass(label + " privacy expiry clears rendered values");
    var initialPath = rig.Handler.Paths[1];
    await rig.Dispatch(() => rig.Application!.Refresh()); html = await rig.Html();
    Check(rig.Handler.Paths.Last() == initialPath && rig.Records.Calls == 0 && html.Contains("application-profile") && !html.Contains("03/04/2001"), "Refresh source changed or grant survived");
    rig.Handler.Status = HttpStatusCode.NotFound; rig.Handler.Body = "{\"code\":2004}";
    await rig.Dispatch(() => rig.Application!.Refresh()); html = await rig.Html();
    Check(!html.Contains("application-profile") && html.Contains("No record found") && rig.Dashboard.Query.Trim() == "101", "Refresh failure not returned to dashboard");
    Pass(label + " source refresh and dashboard refresh error");
}
await using (var rig = await Rig.Create(["VINFORMATIONGATHERING"]))
{
    rig.Handler.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var search = rig.Search("IG CID", "101");
    var html = await rig.Html();
    Check(html.Contains("Searching") && html.Contains("disabled") && !html.Contains("application-profile"), "Loading search did not stay on dashboard/disable submission");
    await rig.Dispatch(() => { rig.Dashboard.Edit("IG CID", "changed CID"); return Task.CompletedTask; });
    rig.Handler.Pending.SetResult(new(HttpStatusCode.OK) { Content = new StringContent(PrivacyResponseTests.Detail(true)) });
    await search; html = await rig.Html();
    Check(!html.Contains("application-profile") && rig.Dashboard.Query == "changed CID", "Edited search accepted stale result");
    Pass("dashboard loading and edited-query stale response");
}
await using (var rig = await Rig.Create(["PENDINGORS"]))
{
    rig.Records.Allow = true;
    foreach (var label in new[] { "Application Number", "Reference No." })
    {
        await rig.Search(label, " REF-101 "); var html = await rig.Html();
        Check(html.Contains("application-profile") && rig.Records.LastNumber == "REF-101" && rig.Handler.Paths.Count == 0, "Existing lookup changed routing/binding");
    }
    var calls = rig.Records.Calls;
    foreach (var label in new[] { "QR Code", "Cardholder" })
    {
        await rig.Search(label, "example"); var html = await rig.Html();
        Check(html.Contains("This search method is not available yet.") && rig.Records.Calls == calls && rig.Handler.Paths.Count == 0, "Existing unavailable search behavior changed");
    }
    Pass("existing application/reference/QR/cardholder search behavior");
}
Console.WriteLine($"{passed}/{passed} web component checks passed. Synthetic in-memory HTTP; no live backend/device claims.");

sealed class CapturedDashboard : Dashboard
{
    public static CapturedDashboard? Instance;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public CitizenApplicationSearch SearchState => (CitizenApplicationSearch)typeof(Dashboard).GetField("citizenSearch", Flags)!.GetValue(this)!;
    public string Query => (string)typeof(Dashboard).GetField("query", Flags)!.GetValue(this)!;
    public string SearchType => (string)typeof(Dashboard).GetField("searchType", Flags)!.GetValue(this)!;
    protected override void OnInitialized() { base.OnInitialized(); Instance = this; }
    public async Task Submit(string label, string value)
    {
        Edit(label, value);
        await (Task)typeof(Dashboard).GetMethod("Search", Flags)!.Invoke(this, null)!; StateHasChanged();
    }
    public void Edit(string label, string value)
    {
        typeof(Dashboard).GetMethod("SearchTypeChanged", Flags)!.Invoke(this, [new ChangeEventArgs { Value = label }]);
        typeof(Dashboard).GetMethod("QueryChanged", Flags)!.Invoke(this, [new ChangeEventArgs { Value = value }]);
        StateHasChanged();
    }
}
sealed class CapturedApplication : ViewApplication
{
    public static CapturedApplication? Instance;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    protected override Task OnParametersSetAsync() { Instance = this; return base.OnParametersSetAsync(); }
    public async Task Refresh() { await (Task)typeof(ViewApplication).GetMethod("LoadAsync", Flags)!.Invoke(this, null)!; StateHasChanged(); }
    public void OpenProtectedDetails(PrivacySection section) { typeof(ViewApplication).GetMethod("ShowProtectedDetails", Flags)!.Invoke(this, ["Bio Details", section]); StateHasChanged(); }
}
// HtmlRenderer's activator lets us observe the real nested ViewApplication instance.
sealed class CaptureActivator : IComponentActivator
{
    public IComponent CreateInstance(Type componentType) => componentType == typeof(ViewApplication) ? new CapturedApplication() : (IComponent)Activator.CreateInstance(componentType)!;
}
sealed class Rig : IAsyncDisposable
{
    public readonly Handler Handler = new();
    public readonly NeverPendingRecords Records = new();
    public readonly Clock Clock = new();
    private readonly SessionController session;
    private readonly PrivacyWorkspace workspace;
    private readonly ServiceProvider services;
    private readonly HtmlRenderer renderer;
    private Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent root;
    public CapturedDashboard Dashboard { get; private set; } = null!;
    public CapturedApplication? Application => CapturedApplication.Instance;
    private Rig(string[] permissions)
    {
        session = new(new Gateway(permissions));
        workspace = new(new HttpClient(Handler), new BackendOptions { BaseUrl = "https://example.invalid", ApiRootPath = "/api/v1" }, Clock);
        services = new ServiceCollection().AddLogging().AddSingleton(session).AddSingleton(workspace).AddSingleton<IApplicationRecords>(Records)
            .AddSingleton<IJSRuntime, SilentJs>().AddSingleton<IComponentActivator, CaptureActivator>().BuildServiceProvider();
        renderer = new(services, services.GetRequiredService<ILoggerFactory>());
    }
    public static async Task<Rig> Create(string[] permissions)
    {
        var rig = new Rig(permissions); await rig.session.SignInAsync("staff@example.invalid", "synthetic");
        rig.root = await rig.renderer.Dispatcher.InvokeAsync(() => rig.renderer.RenderComponentAsync<CapturedDashboard>(ParameterView.FromDictionary(
            new Dictionary<string, object?> { ["Email"] = "staff@example.invalid", ["Modules"] = Navigation.For(rig.session.Current!).ToArray() })));
        rig.Dashboard = CapturedDashboard.Instance!; return rig;
    }
    public Task Dispatch(Func<Task> action) => renderer.Dispatcher.InvokeAsync(action);
    public Task Search(string label, string value) => Dispatch(() => Dashboard.Submit(label, value));
    public Task<string> Html() => renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
    public async ValueTask DisposeAsync() { await renderer.DisposeAsync(); await services.DisposeAsync(); workspace.Dispose(); session.Dispose(); }
}
sealed class NeverPendingRecords : IApplicationRecords
{
    public int Calls;
    public bool Allow;
    public string? LastNumber;
    public Task<ApplicationReply> FindAsync(StaffSession session, string number, CancellationToken token)
    {
        Calls++; LastNumber = number;
        if (!Allow) throw new Exception("CID was passed to pending application lookup");
        return Task.FromResult(new ApplicationReply(ApplicationOutcome.Success, new ApplicationRecord { Number = number, Name = "Reference Example" }, []));
    }
}
sealed class Handler : HttpMessageHandler
{
    public List<string> Paths { get; } = [];
    public string Body = PrivacyResponseTests.Detail(true);
    public HttpStatusCode Status = HttpStatusCode.OK;
    public TaskCompletionSource<HttpResponseMessage>? Pending;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Paths.Add(request.RequestUri!.AbsolutePath); var route = request.RequestUri.AbsolutePath;
        if (Pending is not null) return Pending.Task;
        var body = route.Contains("/information/gathering/") || route.Contains("/cardholder/update/") ? Body : route.Split('/').Last() switch
        { "request" or "verify" => "{\"code\":0}", "bio" => PrivacyResponseTests.Bio, "address" => PrivacyResponseTests.Address, _ => PrivacyResponseTests.Status };
        var response = new HttpResponseMessage(Status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        if (route.EndsWith("/verify")) response.Headers.TryAddWithoutValidation("Set-Cookie", "pii_access_token=opaque-pii; Secure; HttpOnly");
        return Task.FromResult(response);
    }
}
sealed class Clock : TimeProvider
{
    private DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly List<ManualTimer> timers = [];
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance() { now = now.AddMinutes(5); foreach (var timer in timers.ToArray()) timer.Fire(); }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) { var timer = new ManualTimer(callback, state); timers.Add(timer); return timer; }
    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        private bool disposed;
        public void Fire() { if (!disposed) callback(state); }
        public bool Change(TimeSpan dueTime, TimeSpan period) => !disposed;
        public void Dispose() => disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
sealed class SilentJs : IJSRuntime
{
    public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
    public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(T)!);
}
sealed class Gateway(string[] permissions) : IAuthenticationGateway
{
    public bool ContractAvailable => true;
    public Task<AuthReply> AuthenticateAsync(string email, string password, string? otp, CancellationToken token) => Task.FromResult(new AuthReply(AuthOutcome.SignedIn, new(email, new("staff", "refresh", "xsrf"), permissions)));
    public Task<RefreshReply> RefreshAsync(StaffSession staff, CancellationToken token) => Task.FromResult(new RefreshReply(RefreshOutcome.Success, staff.Tokens));
    public Task TerminateAsync(StaffSession staff, CancellationToken token) => Task.CompletedTask;
    public Task<AuthReply> ResendAsync(string email, string password, CancellationToken token) => throw new NotSupportedException();
    public Task<ApiResult> ForgotPasswordAsync(string email, CancellationToken token) => throw new NotSupportedException();
    public Task<ApiResult> ChangePasswordAsync(StaffSession staff, string current, string next, CancellationToken token) => throw new NotSupportedException();
}
