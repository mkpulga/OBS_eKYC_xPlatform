using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using OBS.Core;
using Clock = ApplicantPrivacyControllerTests.Clock;

static class CitizenApplicationSearchTests
{
    public static void Register(Action<string, Func<Task>> add, Action<bool, string> check)
    {
        add("Citizen search routes IG and CU directly with trimmed opaque IDs and staff transport", async () =>
        {
            foreach (var label in new[] { "IG CID", "CU CID" })
            foreach (var id in new[] { "101", "CID-A", "a/b?query#part%2F\\tail", ".", "..", "国民 ID", new string('x', 150) })
            {
                using var rig = await Rig.Create();
                rig.Body = Detail(id); rig.Search.SetCriteria(label, " \t" + id + "\r\n");
                check(await rig.Search.SearchAsync(), "Opaque ID did not resolve");
                var route = label == "IG CID" ? "information/gathering/" : "cardholder/update/";
                check(rig.Calls.Single() == "https://example.invalid/api/v1/" + route + CitizenId.Encode(id), "Wrong encoded endpoint or second lookup");
                check(rig.Search.CitizenId == id && rig.Search.Snapshot?.Identity.ReferenceNumber == "REF-TEST" &&
                    CitizenApplicationPresentation.Record(rig.Search)?.Number == "REF-TEST", "Aliased CID to application/reference");
            }
        });
        add("Citizen search preserves configured API root with no duplicate version suffix", async () =>
        {
            foreach (var url in new[] { "https://example.invalid", "https://example.invalid/api/v1", "https://example.invalid/api/v1/", "https://example.invalid/obs/api/v1" })
            {
                using var rig = await Rig.Create(baseUrl: url);
                rig.Search.SetCriteria("IG CID", "101"); check(await rig.Search.SearchAsync(), "Root lookup failed");
                check(rig.Calls.Single() == rig.Options.GetApiRoot().AbsoluteUri + "information/gathering/101", "Changed root or duplicated api/v1");
            }
        });
        add("Citizen search rejects blank input without dispatch and enforces exact source permissions", async () =>
        {
            using (var rig = await Rig.Create())
            {
                foreach (var label in new[] { "IG CID", "CU CID" })
                {
                    rig.Search.SetCriteria(label, " \t\n");
                    check(!await rig.Search.SearchAsync() && rig.Search.Message == "Enter a Citizen ID." && rig.Calls.Count == 0, "Blank input sent a request");
                }
            }
            foreach (var permission in new[] { "VINFORMATIONGATHERING", "VCARDHOLDERUPDATE", "PENDINGORS", "vinformationgathering", "" })
            {
                using var rig = await Rig.Create([permission]);
                foreach (var label in new[] { "IG CID", "CU CID" })
                {
                    var source = CitizenSearchPresentation.Source(label)!.Value;
                    var allowed = permission == CitizenSearchPresentation.Authority(source);
                    check(CitizenSearchPresentation.Allowed(rig.Session.Current, source) == allowed, "Option permission mismatch");
                    var before = rig.Calls.Count; rig.Search.SetCriteria(label, "101");
                    check(await rig.Search.SearchAsync() == allowed && rig.Calls.Count == before + (allowed ? 1 : 0), "Pending permission gate or unauthorized dispatch");
                }
            }
        });
        add("Citizen refresh retains its source and CID independently of the edited search and reference", async () =>
        {
            foreach (var label in new[] { "IG CID", "CU CID" })
            {
                using var rig = await Rig.Create(); rig.Search.SetCriteria(label, "101"); await rig.Search.SearchAsync();
                rig.Search.SetCriteria(label == "IG CID" ? "CU CID" : "IG CID", "OTHER-CID");
                check(await rig.Search.RefreshAsync() && rig.Calls.Count == 2 && rig.Calls[0] == rig.Calls[1], "Refresh changed source/ID or used pending lookup");
            }
        });
        add("Citizen refresh survives staff token rotation after the search dropdown changes", async () =>
        {
            using var rig = await Rig.Create(["VINFORMATIONGATHERING"]);
            rig.Search.SetCriteria("IG CID", "101"); await rig.Search.SearchAsync();
            rig.Search.SetCriteria("Application Number", "different-reference");
            var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Override = (_, _) => delayed.Task;
            var refresh = rig.Search.RefreshAsync(); await rig.Session.CheckSessionAsync();
            check(rig.Search.IsBusy, "Rotation incorrectly cancelled authorized source refresh");
            delayed.SetResult(Response(Detail("101")));
            check(await refresh && rig.Search.Source == CitizenSearchSource.InformationGathering && rig.Search.CitizenId == "101", "Refresh changed source after rotation");
        });
        add("Citizen missing ordinary data remains missing while protected sections stay withheld", async () =>
        {
            using var rig = await Rig.Create();
            rig.Body = """{"code":0,"data":[{"personal-info":[{"citizenid":"101"}],"piiVerificationRequired":true,"piiAuthorizedSections":{"canViewBioDetails":false,"canViewAddress":false,"canViewEmergencyContact":false,"canViewEducationalInformation":false}}]}""";
            rig.Search.SetCriteria("IG CID", "101"); check(await rig.Search.SearchAsync(), "Missing optional values rejected");
            var record = CitizenApplicationPresentation.Record(rig.Search)!;
            check(record.Number == "" && record.Email == "" && record.Name == "" && CitizenSearchPresentation.Text(record.Phone) == "Not available.", "Invented base field or reference");
            foreach (var section in Enum.GetValues<PrivacySection>()) check(rig.Search.Privacy?.IsVisible(section) == false && rig.Search.Privacy.Fields(section).Count == 0, "Withheld treated as visible missing data");
            rig.Body = Detail("101"); await rig.Search.RefreshAsync();
            rig.Body = """{"code":0,"data":[{"personal-info":[{"citizenid":"101"}],"piiVerificationRequired":true,"piiAuthorizedSections":{"canViewBioDetails":false,"canViewAddress":false,"canViewEmergencyContact":false,"canViewEducationalInformation":false}}]}""";
            await rig.Search.RefreshAsync(); check(CitizenApplicationPresentation.Record(rig.Search)!.Name == "", "Previous applicant filled missing field");
        });
        add("Citizen search failures distinguish not found denial connection and invalid responses", async () =>
        {
            foreach (var label in new[] { "IG CID", "CU CID" })
            foreach (var (status, body, outcome) in new[] {
                (HttpStatusCode.NotFound, "{\"code\":2004}", PrivacyReadOutcome.NotFound),
                (HttpStatusCode.Unauthorized, "{\"code\":2004}", PrivacyReadOutcome.NotPermitted),
                (HttpStatusCode.Forbidden, "{\"code\":99}", PrivacyReadOutcome.NotPermitted),
                (HttpStatusCode.InternalServerError, "{\"code\":99}", PrivacyReadOutcome.ConnectionFailure),
                (HttpStatusCode.OK, "not-json", PrivacyReadOutcome.InvalidResponse),
                (HttpStatusCode.OK, "{\"code\":0,\"data\":[]}", PrivacyReadOutcome.NotFound),
                (HttpStatusCode.OK, "{\"code\":0,\"data\":[{},{}]}", PrivacyReadOutcome.InvalidResponse),
                (HttpStatusCode.OK, Detail("OTHER"), PrivacyReadOutcome.InvalidResponse),
                (HttpStatusCode.OK, "{\"code\":2004}", PrivacyReadOutcome.Rejected) })
            {
                using var rig = await Rig.Create(); rig.Body = body; rig.Status = status;
                rig.Search.SetCriteria(label, "101");
                check(!await rig.Search.SearchAsync() && rig.Search.Snapshot is null && rig.Search.Message == CitizenSearchPresentation.Error(outcome) && rig.Calls.Count == 1, "Wrong endpoint error handling or opened failed result");
                rig.Body = Detail("101"); rig.Status = HttpStatusCode.OK; check(await rig.Search.SearchAsync(), "Retry lost search criteria");
            }
            using var offline = await Rig.Create(); offline.Override = (_, _) => throw new HttpRequestException("synthetic");
            offline.Search.SetCriteria("IG CID", "101"); check(!await offline.Search.SearchAsync() && offline.Search.Message.Contains("connection"), "Transport failure unhandled");
        });
        add("Citizen search suppresses duplicates and discards edited navigated and ended-session requests", async () =>
        {
            foreach (var change in new[] { "input", "type", "leave", "logout", "replacement", "permission", "dispose" })
            {
                using var rig = await Rig.Create(); var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                rig.Override = (_, _) => delayed.Task; rig.Search.SetCriteria("IG CID", "101"); var pending = rig.Search.SearchAsync();
                check(rig.Search.IsBusy && !await rig.Search.SearchAsync() && rig.Calls.Count == 1, "Duplicate sent");
                switch (change)
                {
                    case "input": rig.Search.SetCriteria("IG CID", "102"); break;
                    case "type": rig.Search.SetCriteria("CU CID", "101"); break;
                    case "leave": rig.Search.Leave(); break;
                    case "logout": rig.Session.BackToLogin(); break;
                    case "replacement": await rig.Session.SignInAsync("staff@example.invalid", "synthetic"); break;
                    case "permission": rig.Gateway.Auth = new(AuthOutcome.SignedIn, Staff([])); await rig.Session.SignInAsync("staff@example.invalid", "synthetic"); break;
                    default: rig.Search.Dispose(); break;
                }
                delayed.SetResult(Response(Detail("101")));
                check(!await pending && !rig.Search.IsBusy && rig.Search.Snapshot is null && rig.Search.Privacy is null, "Stale result restored applicant");
            }
        });
        add("Citizen OTP uses existing controller claims independent hiding expiry and source refresh", async () =>
        {
            using var rig = await Rig.Create(); rig.Search.SetCriteria("CU CID", "101"); await rig.Search.SearchAsync();
            var privacy = rig.Search.Privacy!;
            check(CitizenApplicationPresentation.Record(rig.Search)!.Birthday == "", "Snapshot bypassed OTP");
            await privacy.ShowOrHideAsync(PrivacySection.Bio); check(privacy.DialogOpen && rig.Calls.Count == 1, "Reveal auto-sent OTP");
            await privacy.RequestCodeAsync(); privacy.SetOtp("123456"); await privacy.VerifyCodeAsync();
            check(privacy.IsVisible(PrivacySection.Bio) && CitizenApplicationPresentation.Record(rig.Search)!.Birthday == "03/04/2001", "Authorized bio not bound");
            await privacy.ShowOrHideAsync(PrivacySection.Address); await privacy.ShowOrHideAsync(PrivacySection.Bio);
            check(privacy.IsVisible(PrivacySection.Address) && CitizenApplicationPresentation.Record(rig.Search)!.Birthday == "", "Hide leaked fields or hid another section");
            rig.Clock.Advance(TimeSpan.FromMinutes(5));
            check(!privacy.IsVisible(PrivacySection.Address) && CitizenApplicationPresentation.Record(rig.Search)!.Address == "", "Expiry retained fields");
            check(await rig.Search.RefreshAsync() && rig.Search.Privacy?.Stage == PrivacyStage.Locked && rig.Calls.Last().EndsWith("cardholder/update/101"), "Refresh changed source or retained grant");
        });
    }
    internal static string Detail(string id)
    {
        var node = JsonNode.Parse(PrivacyResponseTests.Detail(true))!;
        void Change(JsonNode? current)
        {
            if (current is JsonObject obj) { if (obj.ContainsKey("citizenid")) obj["citizenid"] = id; foreach (var child in obj.ToArray()) Change(child.Value); }
            else if (current is JsonArray array) foreach (var child in array) Change(child);
        }
        Change(node); return node.ToJsonString();
    }
    internal static StaffSession Staff(string[] permissions) => new("staff@example.invalid", new("staff-access", "staff-refresh", "staff-xsrf"), permissions);
    internal static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) => ApplicantPrivacyControllerTests.Rig.Response(body, status);
    private sealed class Rig : IDisposable
    {
        public readonly Clock Clock = new();
        public readonly FakeGateway Gateway;
        public readonly SessionController Session;
        public readonly PrivacyWorkspace Workspace;
        public readonly CitizenApplicationSearch Search;
        public readonly BackendOptions Options;
        public List<string> Calls { get; } = [];
        public string Body = Detail("101");
        public HttpStatusCode Status = HttpStatusCode.OK;
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Override;
        private Rig(string[] permissions, string baseUrl)
        {
            Gateway = new() { Auth = new(AuthOutcome.SignedIn, Staff(permissions)) }; Session = new(Gateway);
            Options = new() { BaseUrl = baseUrl, ApiRootPath = "/api/v1", TokenHeaderKey = "X-Obs-Token", UserAgent = "configured-header" };
            Workspace = new(new HttpClient(new Handler(async (request, token) =>
            {
                Calls.Add(request.RequestUri!.OriginalString);
                if (request.Headers.Authorization?.Parameter != "staff-access" || request.Headers.GetValues("X-Obs-Token").Single() != "configured-header") throw new Exception("Wrong authenticated transport");
                if (Override is not null) return await Override(request, token);
                var path = request.RequestUri.AbsolutePath;
                if (path.Contains("/information/gathering/") || path.Contains("/cardholder/update/"))
                {
                    if (request.Method != HttpMethod.Get || request.Content is not null || request.Headers.Contains("X-PII-Access-Token")) throw new Exception("Invalid CID request");
                    return Response(Body, Status);
                }
                return path.Split('/').Last() switch
                {
                    "request" => Response("{\"code\":0}"), "verify" => ApplicantPrivacyControllerTests.Rig.VerifiedResponse(),
                    "bio" => Response(PrivacyResponseTests.Bio), "address" => Response(PrivacyResponseTests.Address),
                    _ => Response(PrivacyResponseTests.Status)
                };
            })), Options, Clock);
            Search = Workspace.CreateCitizenSearch(Session);
        }
        public static async Task<Rig> Create(string[]? permissions = null, string baseUrl = "https://example.invalid")
        { var rig = new Rig(permissions ?? ["VINFORMATIONGATHERING", "VCARDHOLDERUPDATE"], baseUrl); await rig.Session.SignInAsync("staff@example.invalid", "synthetic"); return rig; }
        public void Dispose() { Search.Dispose(); Workspace.Dispose(); Session.Dispose(); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token); }
}
