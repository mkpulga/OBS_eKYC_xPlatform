using System.Globalization;
using System.Net;
using System.Text.Json;

namespace OBS.Core;

// Fields derived from cardholderVerificationCollectionReport.rdlc (CollectionModel). Live payload remains unverified.
// Never log these rows: they contain cardholder personal information.
public sealed class PendingCardholder
{
    public string? No { get; set; }
    public string? cid { get; set; }
    public string? citizenid { get; set; }
    public string? referencenumber { get; set; }
    public string? firstname { get; set; }
    public string? middlename { get; set; }
    public string? lastname { get; set; }
    public string? suffix { get; set; }
    public string? fullname { get; set; }
    public string? dateofbirth { get; set; }
    public string? msisdn { get; set; }
    public string? unitnumber { get; set; }
    public string? streetname { get; set; }
    public string? barangay { get; set; }
    public string? status { get; set; }
    public string? remarks { get; set; }
    public string? createdby { get; set; }
    public string? issuedby { get; set; }
    public string? datecreated { get; set; }
    public string? timestamp { get; set; }
}

public enum PendingOutcome { Success, NotPermitted, Rejected, ConnectionFailure, InvalidResponse, Unavailable }

// Source copied code/message onto every row; the envelope result is carried once here instead.
public sealed record PendingReply(PendingOutcome Outcome, IReadOnlyList<PendingCardholder> Items, string? Message = null);

public interface ICardholderVerification
{
    Task<PendingReply> GetPendingAsync(StaffSession session, CancellationToken token);
}

// Used when backend configuration is invalid; never fabricates a queue.
public sealed class UnavailableCardholderVerification : ICardholderVerification
{
    public Task<PendingReply> GetPendingAsync(StaffSession session, CancellationToken token) =>
        Task.FromResult(new PendingReply(PendingOutcome.Unavailable, []));
}

// Derived from the source getAllCardHolderVerification_v1. Live behavior remains unverified.
public sealed class CardholderVerificationService(ApiTransport api) : ICardholderVerification
{
    public const string Authority = "PENDINGORS";

    public async Task<PendingReply> GetPendingAsync(StaffSession session, CancellationToken token)
    {
        if (!session.Allows(Authority)) return new(PendingOutcome.NotPermitted, []);
        var result = await api.GetAsync("cardholder/pending", new RequestSecurity { Bearer = session.Tokens.Access }, token);
        if (result.Failure is ApiFailure.Transport or ApiFailure.Cancelled || result.HttpStatus is >= HttpStatusCode.InternalServerError)
            return new(PendingOutcome.ConnectionFailure, []);
        // A non-JSON or empty body carries no business code; it is not a backend rejection.
        if (result.Failure == ApiFailure.InvalidResponse) return new(PendingOutcome.InvalidResponse, []);
        if (!SourceAuthenticationGateway.IsSuccess(result)) return new(PendingOutcome.Rejected, [], result.Message);
        try
        {
            if (result.Data is not { } root || !SourceJson.TryGetProperty(root, "data", out var data) || data.ValueKind != JsonValueKind.Array)
                return new(PendingOutcome.InvalidResponse, []);
            var items = data.Deserialize<List<PendingCardholder>>(SourceJson.Options) ?? [];
            // Source sorted oldest first with DateTime.Parse; an unparseable date sorts last instead of failing the whole queue.
            var sorted = items.OrderBy(item => DateTime.TryParse(item.datecreated, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var created) ? created : DateTime.MaxValue).ToList();
            for (var index = 0; index < sorted.Count; index++) sorted[index].No = (index + 1).ToString(CultureInfo.InvariantCulture);
            return new(PendingOutcome.Success, sorted, result.Message);
        }
        catch (JsonException) { return new(PendingOutcome.InvalidResponse, []); }
    }
}

// Shared by the web and MAUI queues so both clients word outcomes identically.
public static class CardholderVerificationPresentation
{
    public const string Title = "Cardholder Verification";
    public static bool IsQueue(NavigationEntry? entry) => entry?.Title == Title;

    // Null means rows are shown with no status line.
    public static string? Status(PendingReply reply) => reply.Outcome switch
    {
        PendingOutcome.Success => reply.Items.Count == 0 ? "No cardholders are pending verification." : null,
        PendingOutcome.NotPermitted => "Your account is not permitted to view cardholder verification.",
        PendingOutcome.Rejected => string.IsNullOrWhiteSpace(reply.Message) ? "The server declined the request. Please try again." : reply.Message,
        PendingOutcome.ConnectionFailure => "Unable to reach the server. Check your connection and try again.",
        PendingOutcome.Unavailable => "Cardholder verification is unavailable because the backend configuration is invalid.",
        _ => "The server returned an unexpected response. Please try again."
    };
    public static string Count(PendingReply reply) => reply.Items.Count == 1 ? "1 pending" : $"{reply.Items.Count:N0} pending";

    public static string Name(PendingCardholder item) => !string.IsNullOrWhiteSpace(item.fullname) ? item.fullname.Trim()
        : Join(" ", item.firstname, item.middlename, item.lastname, item.suffix) is { Length: > 0 } name ? name : "(no name)";
    public static string Address(PendingCardholder item) => Join(", ", Join(" ", item.unitnumber, item.streetname), item.barangay);
    public static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string Join(string separator, params string?[] parts) =>
        string.Join(separator, parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
}
