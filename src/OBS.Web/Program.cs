using OBS.Core;
using OBS.Web.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using System.Xml.Linq;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
    // Keep development key management in this process, away from the host user's key store.
    builder.Services.Configure<KeyManagementOptions>(options => options.XmlRepository = new DevelopmentKeyRepository());
}
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.Services.AddRazorComponents().AddInteractiveServerComponents(options =>
{
    options.DetailedErrors = false;
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.Zero;
});
var backend = builder.Configuration.GetSection("Backend").Get<BackendOptions>() ?? new();
var theme = builder.Configuration.GetSection("Theme").Get<ThemeOptions>() ?? new();
theme.Validate();
string? configurationError = null;
try
{
    _ = backend.GetApiRoot();
    if (backend.ApiContract == ApiContract.Privacy) configurationError = PrivacyApiService.ClientUnavailableReason;
}
catch (ArgumentException exception) { configurationError = exception.Message; }
builder.Services.AddSingleton(theme);
builder.Services.AddSingleton(new DeploymentStatus(configurationError, backend.ApiContract, backend.FeatureSet));
builder.Services.AddScoped<IAuthenticationGateway>(_ =>
{
    if (configurationError is not null) return new UnverifiedAuthenticationGateway();
    // A dedicated handler per circuit: no shared cookies, bearer headers, or credentials.
    return new OwnedGateway(backend);
});
builder.Services.AddScoped<ICardholderVerification>(_ =>
    configurationError is not null ? new UnavailableCardholderVerification() : new OwnedCardholderVerification(backend));
builder.Services.AddScoped<SessionController>();
builder.Services.AddScoped<PrivacyWorkspace>(_ => configurationError is null ? new(backend) : new());
builder.Services.AddScoped<IApplicationRecords>(_ =>
    configurationError is not null ? new UnavailableApplicationRecords() : new OwnedApplicationRecords(backend));
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'";
    await next();
});
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

public sealed record DeploymentStatus(string? Error, ApiContract Contract, FeatureSet Features);
public sealed class OwnedApplicationRecords : IApplicationRecords, IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ApplicationRecordsService inner;
    public OwnedApplicationRecords(BackendOptions options) => inner = new(new ApiTransport(client, options));
    public Task<ApplicationReply> FindAsync(StaffSession session, string number, CancellationToken token) => inner.FindAsync(session, number, token);
    public void Dispose() => client.Dispose();
}
public sealed class DevelopmentKeyRepository : IXmlRepository
{
    private readonly List<XElement> elements = [];
    public IReadOnlyCollection<XElement> GetAllElements() { lock (elements) return elements.Select(element => new XElement(element)).ToArray(); }
    public void StoreElement(XElement element, string friendlyName) { lock (elements) elements.Add(new XElement(element)); }
}

// Own HttpClient explicitly so circuit disposal releases its handler.
public sealed class OwnedGateway : IAuthenticationGateway, IDisposable
{
    private readonly HttpClient client;
    private readonly SourceAuthenticationGateway inner;
    public OwnedGateway(BackendOptions options)
    {
        client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        inner = new(new ApiTransport(client, options), options);
    }
    public bool ContractAvailable => inner.ContractAvailable;
    public Task<AuthReply> AuthenticateAsync(string email, string password, string? otp, CancellationToken token) => inner.AuthenticateAsync(email, password, otp, token);
    public Task<AuthReply> ResendAsync(string email, string password, CancellationToken token) => inner.ResendAsync(email, password, token);
    public Task<RefreshReply> RefreshAsync(StaffSession session, CancellationToken token) => inner.RefreshAsync(session, token);
    public Task TerminateAsync(StaffSession session, CancellationToken token) => inner.TerminateAsync(session, token);
    public Task<ApiResult> ForgotPasswordAsync(string email, CancellationToken token) => inner.ForgotPasswordAsync(email, token);
    public Task<ApiResult> ChangePasswordAsync(StaffSession session, string currentPassword, string newPassword, CancellationToken token) => inner.ChangePasswordAsync(session, currentPassword, newPassword, token);
    public void Dispose() => client.Dispose();
}

// Same per-circuit ownership as OwnedGateway: no shared bearer headers across circuits.
public sealed class OwnedCardholderVerification : ICardholderVerification, IDisposable
{
    private readonly HttpClient client;
    private readonly CardholderVerificationService inner;
    public OwnedCardholderVerification(BackendOptions options)
    {
        client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        inner = new(new ApiTransport(client, options));
    }
    public Task<PendingReply> GetPendingAsync(StaffSession session, CancellationToken token) => inner.GetPendingAsync(session, token);
    public void Dispose() => client.Dispose();
}
