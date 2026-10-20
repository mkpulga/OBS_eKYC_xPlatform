using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using OBS.Core;

static class DashboardApiTests
{
    // Synthetic values preserve the observed shape without copying deployment statistics.
    private const string Body = """
    {"code":0,"message":"Success","data":[{
      "accountsOnboarded":50,"cardsRequested":20,"cardsReleased":3,"releaseProgressPercent":40,
      "cardHolderVerification":7,"scheduledHomevisits":9,"forCardRequests":2,
      "activityOverview":[{"category":"Accounts","completed":4,"inProgress":6},{"category":"Cards","completed":8,"inProgress":1}]
    }]}
    """;

    public static void Register(Action<string, Func<Task>> add, Action<bool, string> check)
    {
        BackendOptions Options() => new() { BaseUrl = "https://dashboard.example.invalid", ApiContract = ApiContract.Privacy,
            ApiRootPath = "/api/v1", TokenHeaderKey = "X-Obs-Token", UserAgent = "synthetic-header" };
        StaffSession Staff(params string[] permissions) => new("staff@example.invalid", new("access", "refresh", "xsrf"), permissions);
        PrivacyApiService Service(HttpClient client)
        {
            var options = Options();
            return new(new ApiTransport(client, options), options);
        }
        HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        async Task<DashboardReply> Load(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(body, status))));
            return await Service(client).GetDashboardSnapshotAsync(Staff("VIEWDASHBOARD"));
        }

        add("Dashboard maps observed fields into existing metric and chart models", async () =>
        {
            var reply = await Load(Body);
            check(reply.Outcome == DashboardOutcome.Success && reply.Snapshot is not null, "Valid dashboard rejected");
            var data = reply.Snapshot!;
            check(data.AccountsOnboarded == 50 && data.CardsRequested == 20 && data.CardsReleased == 3, "Portfolio mapping lost");
            check(data.Portfolio.Select(item => item.Value).SequenceEqual(new[] { 50, 20, 3 }), "Wrong card order");
            check(data.Queues.Select(item => item.Value).SequenceEqual(new[] { 7, 9, 2 }), "Wrong queue mapping");
            check(data.Portfolio.Select(item => item.Label).SequenceEqual(DashboardSample.Portfolio.Select(item => item.Label)), "Changed portfolio labels");
            check(data.Queues.Select(item => item.Label).SequenceEqual(DashboardSample.Queues.Select(item => item.Label)), "Changed queue labels");
            // Intentionally different from cardsReleased/cardsRequested: server percentage is authoritative.
            check(data.ReleaseProgressPercent == 40, "Recomputed server percentage");
            check(data.ActivityOverview.SequenceEqual(new[] { new DashboardActivity("Accounts", 4, 6), new DashboardActivity("Cards", 8, 1) }), "Activity labels/order/counts lost");
        });
        add("Dashboard rejects absent null malformed and out-of-range displayed counts", async () =>
        {
            foreach (var name in new[] { "accountsOnboarded", "cardsRequested", "cardsReleased", "releaseProgressPercent", "cardHolderVerification", "scheduledHomevisits", "forCardRequests" })
            {
                foreach (var value in new[] { "null", "\"12\"", "-1", "1.5", "2147483648", "true", "{}" })
                {
                    var root = JsonNode.Parse(Body)!; root["data"]![0]![name] = JsonNode.Parse(value);
                    var reply = await Load(root.ToJsonString());
                    check(reply.Outcome == DashboardOutcome.InvalidResponse && reply.Snapshot is null, "Invalid metric accepted");
                }
                var missing = JsonNode.Parse(Body)!; missing["data"]![0]!.AsObject().Remove(name);
                check((await Load(missing.ToJsonString())).Outcome == DashboardOutcome.InvalidResponse, "Missing metric defaulted to zero");
            }
            check((await Load(Body.Replace("\"releaseProgressPercent\":40", "\"releaseProgressPercent\":101"))).Outcome == DashboardOutcome.InvalidResponse, "Invalid percent accepted");
        });
        add("Dashboard rejects ambiguous envelopes and malformed activity rows", async () =>
        {
            foreach (var envelope in new[] { "{\"code\":0}", "{\"code\":0,\"data\":[]}", "{\"code\":0,\"data\":{}}", "{\"code\":0,\"data\":[{},{}]}" })
                check((await Load(envelope)).Outcome == DashboardOutcome.InvalidResponse, "Invalid dashboard envelope accepted");
            foreach (var activity in new[] { "null", "{}", "[null]", "[{}]", "[{\"category\":\"\",\"completed\":0,\"inProgress\":0}]",
                "[{\"category\":\"Cards\",\"completed\":1}]", "[{\"category\":\"Cards\",\"completed\":0,\"inProgress\":-1}]" })
            {
                var root = JsonNode.Parse(Body)!; root["data"]![0]!["activityOverview"] = JsonNode.Parse(activity);
                check((await Load(root.ToJsonString())).Outcome == DashboardOutcome.InvalidResponse, "Malformed activity accepted");
            }
        });
        add("Dashboard accepts valid zero and empty activity while rejecting duplicate fields", async () =>
        {
            var root = JsonNode.Parse(Body)!; var item = root["data"]![0]!.AsObject();
            foreach (var key in item.Select(entry => entry.Key).Where(key => key != "activityOverview").ToArray()) item[key] = 0;
            item["activityOverview"] = new JsonArray(); item["futureField"] = "ignored";
            var reply = await Load(root.ToJsonString());
            check(reply.Outcome == DashboardOutcome.Success && reply.Snapshot?.ActivityOverview.Count == 0 && reply.Snapshot.ReleaseProgressPercent == 0, "Valid zero state rejected");
            check((await Load(Body.Replace("\"accountsOnboarded\":50", "\"accountsOnboarded\":50,\"AccountsOnboarded\":60"))).Outcome == DashboardOutcome.InvalidResponse, "Ambiguous metric accepted");
            check((await Load(Body.Replace("accountsOnboarded", "AccountsOnboarded"))).Outcome == DashboardOutcome.Success, "Source case matching lost");
        });
        add("Typed dashboard performs one authorized bodyless GET and denies missing authority", async () =>
        {
            var calls = 0;
            using var client = new HttpClient(new Handler((request, _) =>
            {
                calls++;
                check(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/api/v1/dashboard" && request.RequestUri.Query == "" && request.Content is null, "Wrong dashboard request or guessed period query");
                check(request.Headers.Authorization?.Parameter == "access" && !request.Headers.Contains("X-PII-Access-Token"), "Wrong dashboard credentials");
                return Task.FromResult(Response(Body));
            }));
            var service = Service(client);
            check((await service.GetDashboardSnapshotAsync(Staff("VIEWDASHBOARD"))).Outcome == DashboardOutcome.Success && calls == 1, "Typed request failed");
            check((await service.GetDashboardSnapshotAsync(Staff())).Outcome == DashboardOutcome.NotPermitted && calls == 1, "Denied account sent a request");
        });
        add("Typed dashboard keeps business authorization server and cancellation failures distinct", async () =>
        {
            foreach (var (status, outcome) in new[] { (HttpStatusCode.Unauthorized, DashboardOutcome.NotPermitted), (HttpStatusCode.Forbidden, DashboardOutcome.NotPermitted),
                (HttpStatusCode.BadGateway, DashboardOutcome.ConnectionFailure), (HttpStatusCode.TooManyRequests, DashboardOutcome.Rejected) })
                check((await Load(Body, status)).Outcome == outcome, "HTTP failure misclassified");
            check((await Load(Body.Replace("\"code\":0", "\"code\":9"))).Outcome == DashboardOutcome.Rejected, "Business rejection accepted");
            check((await Load("<html>error</html>")).Outcome == DashboardOutcome.InvalidResponse, "Malformed response accepted");
            using var client = new HttpClient(new Handler((_, token) => Task.FromException<HttpResponseMessage>(new OperationCanceledException(token))));
            using var stop = new CancellationTokenSource(); stop.Cancel();
            check((await Service(client).GetDashboardSnapshotAsync(Staff("VIEWDASHBOARD"), stop.Token)).Outcome == DashboardOutcome.Cancelled, "Cancellation lost");
        });
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }
}
