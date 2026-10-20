using System.Net;
using System.Text.Json;

namespace OBS.Core;

// Field names/types come from the owner's preproduction dashboard response.
// It establishes one activity series, not date-range filtering semantics.
public sealed class DashboardSnapshot
{
    public int AccountsOnboarded { get; }
    public int CardsRequested { get; }
    public int CardsReleased { get; }
    public int ReleaseProgressPercent { get; }
    public int CardHolderVerification { get; }
    public int ScheduledHomevisits { get; }
    public int ForCardRequests { get; }
    public IReadOnlyList<DashboardMetric> Portfolio { get; }
    public IReadOnlyList<DashboardMetric> Queues { get; }
    public IReadOnlyList<DashboardActivity> ActivityOverview { get; }

    internal DashboardSnapshot(int accountsOnboarded, int cardsRequested, int cardsReleased, int releaseProgressPercent,
        int cardHolderVerification, int scheduledHomevisits, int forCardRequests, DashboardActivity[] activity)
    {
        AccountsOnboarded = accountsOnboarded;
        CardsRequested = cardsRequested;
        CardsReleased = cardsReleased;
        ReleaseProgressPercent = releaseProgressPercent;
        CardHolderVerification = cardHolderVerification;
        ScheduledHomevisits = scheduledHomevisits;
        ForCardRequests = forCardRequests;
        Portfolio = Array.AsReadOnly<DashboardMetric>([
            new("Accounts Onboarded", accountsOnboarded, "users", "mint"),
            new("Cards Requested", cardsRequested, "card", "blue"),
            new("Cards Released", cardsReleased, "verified", "orange")]);
        Queues = Array.AsReadOnly<DashboardMetric>([
            new("Card Holder Verification", cardHolderVerification, "identity", "mint"),
            new("Scheduled Homevisits", scheduledHomevisits, "home", "orange"),
            new("For Card Requests", forCardRequests, "printer", "purple")]);
        ActivityOverview = Array.AsReadOnly(activity);
    }
    public override string ToString() => "[dashboard snapshot]";
}

public enum DashboardOutcome { Success, NotPermitted, Rejected, ConnectionFailure, InvalidResponse, Cancelled, Unavailable }

public sealed record DashboardReply(DashboardOutcome Outcome, DashboardSnapshot? Snapshot = null)
{
    internal static DashboardReply FromApiResult(ApiResult result)
    {
        if (result.Failure == ApiFailure.ContractUnavailable) return new(DashboardOutcome.Unavailable);
        if (result.Failure == ApiFailure.NotPermitted || result.HttpStatus is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new(DashboardOutcome.NotPermitted);
        if (result.Failure == ApiFailure.Cancelled) return new(DashboardOutcome.Cancelled);
        if (result.Failure == ApiFailure.Transport || result.HttpStatus is >= HttpStatusCode.InternalServerError)
            return new(DashboardOutcome.ConnectionFailure);
        if (result.Failure == ApiFailure.InvalidResponse) return new(DashboardOutcome.InvalidResponse);
        if (!SourceAuthenticationGateway.IsSuccess(result)) return new(DashboardOutcome.Rejected);
        try
        {
            if (result.Data is not { } root || !SourceJson.TryGetProperty(root, "data", out var data) ||
                data.ValueKind != JsonValueKind.Array || data.GetArrayLength() != 1)
                return new(DashboardOutcome.InvalidResponse);
            var item = data[0];
            if (!SourceJson.TryGetProperty(item, "activityOverview", out var activity) || activity.ValueKind != JsonValueKind.Array)
                return new(DashboardOutcome.InvalidResponse);
            var rows = activity.EnumerateArray().Select(row =>
            {
                if (!SourceJson.TryGetProperty(row, "category", out var category) || category.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(category.GetString())) throw new JsonException();
                return new DashboardActivity(category.GetString()!, Count(row, "completed"), Count(row, "inProgress"));
            }).ToArray();
            var percent = Count(item, "releaseProgressPercent");
            if (percent > 100) return new(DashboardOutcome.InvalidResponse);
            return new(DashboardOutcome.Success, new DashboardSnapshot(
                Count(item, "accountsOnboarded"), Count(item, "cardsRequested"), Count(item, "cardsReleased"), percent,
                Count(item, "cardHolderVerification"), Count(item, "scheduledHomevisits"), Count(item, "forCardRequests"), rows));
        }
        catch (JsonException) { return new(DashboardOutcome.InvalidResponse); }
    }

    private static int Count(JsonElement item, string name)
    {
        // No missing/null/string-to-zero fallback: every displayed number must be trustworthy.
        if (!SourceJson.TryGetProperty(item, name, out var field) || field.ValueKind != JsonValueKind.Number ||
            !field.TryGetInt32(out var value) || value < 0) throw new JsonException();
        return value;
    }
}
