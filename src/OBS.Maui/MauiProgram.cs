using Microsoft.Extensions.DependencyInjection;
using OBS.Core;

namespace OBS.Maui;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        IAuthenticationGateway gateway;
        ICardholderVerification cardholders;
        IApplicationRecords applications;
        string? error = null;
        using var stream = typeof(MauiProgram).Assembly.GetManifestResourceStream("OBS.Maui.app.ini")!;
        using var reader = new StreamReader(stream);
        try
        {
            var options = BackendOptions.ReadIni(reader.ReadToEnd());
            gateway = new OwnedGateway(options);
            cardholders = new OwnedCardholderVerification(options);
            applications = new OwnedApplicationRecords(options);
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            gateway = new UnverifiedAuthenticationGateway();
            cardholders = new UnavailableCardholderVerification();
            applications = new UnavailableApplicationRecords();
        }
        builder.Services.AddSingleton(new ClientStatus(error));
        using var themeStream = typeof(MauiProgram).Assembly.GetManifestResourceStream("OBS.Maui.theme.ini")!;
        using var themeReader = new StreamReader(themeStream);
        builder.Services.AddSingleton(ThemeOptions.ReadIni(themeReader.ReadToEnd()));
        builder.Services.AddSingleton<IAuthenticationGateway>(gateway);
        builder.Services.AddSingleton(cardholders);
        builder.Services.AddSingleton(applications);
        builder.Services.AddSingleton<SessionController>();
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
public sealed record ClientStatus(string? ConfigurationError);
public sealed class OwnedApplicationRecords : IApplicationRecords, IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ApplicationRecordsService inner;
    public OwnedApplicationRecords(BackendOptions options) => inner = new(new ApiTransport(client, options));
    public Task<ApplicationReply> FindAsync(StaffSession session, string number, CancellationToken token) => inner.FindAsync(session, number, token);
    public void Dispose() => client.Dispose();
}
public sealed class OwnedGateway : IAuthenticationGateway, IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SourceAuthenticationGateway inner;
    public OwnedGateway(BackendOptions options) => inner = new(new ApiTransport(client, options), options);
    public bool ContractAvailable => inner.ContractAvailable;
    public Task<AuthReply> AuthenticateAsync(string email, string password, string? otp, CancellationToken token) => inner.AuthenticateAsync(email, password, otp, token);
    public Task<AuthReply> ResendAsync(string email, string password, CancellationToken token) => inner.ResendAsync(email, password, token);
    public Task<RefreshReply> RefreshAsync(StaffSession session, CancellationToken token) => inner.RefreshAsync(session, token);
    public Task TerminateAsync(StaffSession session, CancellationToken token) => inner.TerminateAsync(session, token);
    public Task<ApiResult> ForgotPasswordAsync(string email, CancellationToken token) => inner.ForgotPasswordAsync(email, token);
    public Task<ApiResult> ChangePasswordAsync(StaffSession session, string currentPassword, string newPassword, CancellationToken token) => inner.ChangePasswordAsync(session, currentPassword, newPassword, token);
    public void Dispose() => client.Dispose();
}
public sealed class OwnedCardholderVerification : ICardholderVerification, IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private readonly CardholderVerificationService inner;
    public OwnedCardholderVerification(BackendOptions options) => inner = new(new ApiTransport(client, options));
    public Task<PendingReply> GetPendingAsync(StaffSession session, CancellationToken token) => inner.GetPendingAsync(session, token);
    public void Dispose() => client.Dispose();
}
