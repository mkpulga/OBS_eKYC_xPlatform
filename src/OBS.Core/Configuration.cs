namespace OBS.Core;

public enum ApiContract { Refactored, Legacy }
public enum FeatureSet { StandAlone, Bacoor, Makati }

public sealed record BackendOptions
{
    public string BaseUrl { get; init; } = "";
    public ApiContract ApiContract { get; init; } = ApiContract.Refactored;
    public FeatureSet FeatureSet { get; init; } = FeatureSet.StandAlone;
    public string TokenHeaderKey { get; init; } = "";
    public string UserAgent { get; init; } = "";
    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public Uri GetApiRoot()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Configure an HTTPS backend URL without credentials, query, or fragment.");
        if (!Enum.IsDefined(ApiContract) || !Enum.IsDefined(FeatureSet))
            throw new ArgumentException("Unknown API contract or feature set.");
        if (TokenHeaderKey.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
            TokenHeaderKey.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
            TokenHeaderKey.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
            TokenHeaderKey.Equals("X-XSRF-TOKEN", StringComparison.OrdinalIgnoreCase) ||
            UserAgent.Contains('\r') || UserAgent.Contains('\n'))
            throw new ArgumentException("Invalid configured header name or user agent.");
        var path = uri.AbsolutePath.TrimEnd('/');
        const string suffix = "/obs/api/v1";
        if (path.Contains(suffix, StringComparison.OrdinalIgnoreCase) && !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Backend URL must end at the API root, not an API route.");
        return new Uri(uri.GetLeftPart(UriPartial.Authority) + path +
            (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? "/" : suffix + "/"));
    }

    public static BackendOptions ReadIni(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in content.Split('\n'))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith(';') || text.StartsWith('#') || text.StartsWith('[')) continue;
            var index = text.IndexOf('=');
            if (index <= 0 || !values.TryAdd(text[..index].Trim(), text[(index + 1)..].Trim()))
                throw new ArgumentException("Malformed or duplicate configuration setting.");
        }
        string Get(string key) => values.GetValueOrDefault(key, "");
        T ReadEnum<T>(string key) where T : struct, Enum =>
            Enum.TryParse<T>(Get(key), true, out var result) && Enum.IsDefined(result)
                ? result : throw new ArgumentException($"Invalid {key}.");
        var diagnostics = new List<string>();
        var features = Get("featureSet").Length > 0 ? ReadEnum<FeatureSet>("featureSet")
            : Get("city").Equals("Bacoor", StringComparison.OrdinalIgnoreCase) ? FeatureSet.Bacoor
            : Get("city").Equals("Makati", StringComparison.OrdinalIgnoreCase) ? FeatureSet.Makati : FeatureSet.StandAlone;
        if (Get("featureSet").Length == 0) diagnostics.Add("featureSet is absent; using the original city fallback.");
        var contract = Get("apiContract").Length > 0 ? ReadEnum<ApiContract>("apiContract")
            : features == FeatureSet.Bacoor ? ApiContract.Refactored : ApiContract.Legacy;
        if (Get("apiContract").Length == 0) diagnostics.Add("apiContract is absent; using the original feature-set fallback. Verify this deployment.");
        var options = new BackendOptions { BaseUrl = Get("baseUrl"), ApiContract = contract,
            FeatureSet = features, TokenHeaderKey = Get("tokenHeaderKey").Length > 0 ? Get("tokenHeaderKey") : Get("token"),
            UserAgent = Get("userAgent"), Diagnostics = diagnostics };
        _ = options.GetApiRoot();
        return options;
    }
}
