using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OBS.Core;

public enum ApiFailure { None, Transport, Cancelled, InvalidResponse, ContractUnavailable }

// Never log this result: backend messages/data can contain personal information.
public sealed class ApiResult
{
    public HttpStatusCode? HttpStatus { get; init; }
    public string? BusinessCode { get; init; }
    public int? BackendStatus { get; init; }
    public string? BackendError { get; init; }
    public string? Message { get; init; }
    public JsonElement? Data { get; init; }
    public ApiFailure Failure { get; init; }
    public IReadOnlyDictionary<string, string> Cookies { get; init; } = new Dictionary<string, string>();
    public bool HttpSucceeded => HttpStatus is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices;
    public override string ToString() => $"HTTP {(int?)HttpStatus}; failure={Failure}";
}

public sealed class RequestSecurity
{
    public string? Bearer { get; init; }
    public string? Xsrf { get; init; }
    public string? ConfiguredToken { get; init; }
    public string? CookieHeader { get; init; }
    public override string ToString() => "[request security redacted]";
}

public sealed class ApiTransport(HttpClient client, BackendOptions options)
{
    private readonly Uri root = options.GetApiRoot();
    // Read-only routes are allowlisted one at a time as each workflow slice is migrated.
    private static readonly HashSet<string> GetRoutes = new(StringComparer.Ordinal) { "cardholder/pending" };

    public async Task<ApiResult> PostAsync(string route, object body, RequestSecurity? security = null,
        CancellationToken cancellationToken = default)
    {
        if (!route.StartsWith("session/", StringComparison.Ordinal) && route != "user/forgot/password" && route != "user/change/password")
            throw new ArgumentException("Route is outside the implemented authentication slice.", nameof(route));
        if (route.Contains("..") || route.Contains('?') || route.Contains('#')) throw new ArgumentException("Invalid route.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(root, route)) { Content = JsonContent.Create(body) };
        return await SendAsync(request, security, cancellationToken);
    }

    public async Task<ApiResult> GetAsync(string route, RequestSecurity? security = null, CancellationToken cancellationToken = default)
    {
        if (!GetRoutes.Contains(route)) throw new ArgumentException("Route is outside the implemented slices.", nameof(route));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, route));
        return await SendAsync(request, security, cancellationToken);
    }

    private async Task<ApiResult> SendAsync(HttpRequestMessage request, RequestSecurity? security, CancellationToken cancellationToken)
    {
        // AccessAPI_v1 uses the setting called userAgent as the configured token-header value.
        if (!string.IsNullOrEmpty(options.UserAgent))
        {
            if (string.IsNullOrEmpty(options.TokenHeaderKey)) throw new InvalidOperationException("Configured header name is missing.");
            request.Headers.Add(options.TokenHeaderKey, options.UserAgent);
        }
        if (security?.Bearer is { } bearer) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (security?.Xsrf is { } xsrf) request.Headers.Add("X-XSRF-TOKEN", xsrf);
        if (security?.CookieHeader is { } cookie) request.Headers.Add("Cookie", cookie);
        if (security?.ConfiguredToken is { } token)
        {
            if (string.IsNullOrEmpty(options.TokenHeaderKey)) throw new InvalidOperationException("Token header name is unconfigured.");
            request.Headers.Add(options.TokenHeaderKey, token);
        }
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            var cookies = ParseCookies(response.Headers.TryGetValues("Set-Cookie", out var headers) ? headers : []);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content)) return new ApiResult { HttpStatus = response.StatusCode, Cookies = cookies, Failure = ApiFailure.InvalidResponse };
            try
            {
                using var document = JsonDocument.Parse(content);
                var element = document.RootElement;
                string? Read(string name) => SourceJson.TryGetProperty(element, name, out var field)
                    ? field.ValueKind == JsonValueKind.String ? field.GetString() : field.GetRawText() : null;
                return new ApiResult { HttpStatus = response.StatusCode, BusinessCode = Read("code"), Message = Read("message"),
                    BackendStatus = int.TryParse(Read("status"), out var status) ? status : null, BackendError = Read("error"),
                    Data = element.Clone(), Cookies = cookies };
            }
            catch (JsonException) { return new ApiResult { HttpStatus = response.StatusCode, Cookies = cookies, Failure = ApiFailure.InvalidResponse }; }
        }
        catch (OperationCanceledException) { return new ApiResult { Failure = cancellationToken.IsCancellationRequested ? ApiFailure.Cancelled : ApiFailure.Transport }; }
        catch (HttpRequestException) { return new ApiResult { Failure = ApiFailure.Transport }; }
    }

    // Exact names are retained; the verified contract reader must select refresh/XSRF cookies.
    public static IReadOnlyDictionary<string, string> ParseCookies(IEnumerable<string> headers)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var header in headers)
        {
            var pair = header.Split(';', 2)[0];
            var index = pair.IndexOf('=');
            if (index <= 0) continue;
            var value = pair[(index + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
            result[pair[..index].Trim()] = value;
        }
        return result;
    }
}
