using System.Net;
using System.Text;
using OBS.Core;

static class ApplicantPrivacyControllerTests
{
    public static void Register(Action<string, Func<Task>> add, Action<bool, string> check)
    {
        add("Privacy controller starts locked and requires an active staff session and explicit applicant", async () =>
        {
            using var rig = await Rig.Create();
            await rig.Privacy.RequestCodeAsync();
            await rig.Privacy.RevealAsync(PrivacySection.Bio);
            check(rig.Calls.Count == 0 && rig.Privacy.Stage == PrivacyStage.Locked, "Dispatched without applicant");
            check(rig.Privacy.SelectApplicant("101"), "Could not select canonical test ID");
            await rig.Privacy.RevealAsync(PrivacySection.Bio);
            await rig.Privacy.VerifyCodeAsync("123456");
            check(rig.Calls.Count == 0 && !rig.Privacy.CanReveal(PrivacySection.Bio), "Skipped request/verify gate");
            rig.Session.BackToLogin();
            check(!rig.Privacy.SelectApplicant("101"), "Accepted signed-out staff");
            await rig.Privacy.RequestCodeAsync();
            check(rig.Calls.Count == 0 && rig.Privacy.Bio is null, "Signed-out request dispatched");
            try { rig.Privacy.SelectApplicant(" "); throw new InvalidOperationException("Empty identifier accepted"); }
            catch (ArgumentException) { }
        });
        add("Privacy controller prevents duplicate OTP actions and validates digits without a guessed cooldown", async () =>
        {
            using var rig = await Rig.Create(); rig.Privacy.SelectApplicant("101");
            var sent = Signal();
            rig.Override = (request, _) => request.RequestUri!.AbsolutePath.EndsWith("/request") ? sent.Task : null;
            var first = rig.Privacy.RequestCodeAsync();
            await rig.Privacy.RequestCodeAsync();
            await rig.Privacy.VerifyCodeAsync("123456");
            check(rig.Calls.Count == 1 && rig.Privacy.IsBusy, "Replayed OTP action while pending");
            sent.SetResult(Rig.Response("{\"code\":0}")); await first;
            check(rig.Privacy.Stage == PrivacyStage.CodeRequested && !rig.Privacy.IsBusy, "Code request did not finish");
            foreach (var otp in new[] { "", "12345", "1234567", "123x56", "１２３４５６" }) await rig.Privacy.VerifyCodeAsync(otp);
            check(rig.Calls.Count == 1, "Invalid digits dispatched");
            rig.Override = null;
            await rig.Privacy.RequestCodeAsync();
            check(rig.Calls.Count == 2 && rig.Privacy.Stage == PrivacyStage.CodeRequested, "Invented privacy resend cooldown");
        });
        add("Privacy controller requires exact cookie and status before granting any reveal", async () =>
        {
            using var rig = await Rig.Create(); rig.Privacy.SelectApplicant("101");
            await rig.Privacy.RequestCodeAsync();
            rig.Override = (request, _) => request.RequestUri!.AbsolutePath.EndsWith("/verify") ? Task.FromResult(Rig.Response("{\"code\":0}")) : null;
            await rig.Privacy.VerifyCodeAsync("123456");
            check(rig.Calls.Count == 2 && rig.Privacy.Stage == PrivacyStage.CodeRequested && !rig.Privacy.CanReveal(PrivacySection.Bio), "Bare success granted without credential/status");
            rig.Override = null;
            await rig.Privacy.VerifyCodeAsync("123456");
            check(rig.Privacy.Stage == PrivacyStage.Verified && rig.Privacy.Bio is null && rig.Privacy.CanReveal(PrivacySection.Bio), "Valid status failed or prefetched protected data");
            check(rig.Calls.Select(call => call.Path).SequenceEqual(new[] { "/api/v1/privacy/otp/request", "/api/v1/privacy/otp/verify",
                "/api/v1/privacy/otp/verify", "/api/v1/privacy/verification/status/101" }), "Wrong OTP/status sequence");
            check(!rig.Privacy.ToString().Contains("101"), "Controller diagnostics leaked applicant");
        });
        add("Privacy controller reveals only selected sections after status checks and hides independently", async () =>
        {
            using var rig = await Rig.Create(); await rig.Authorize();
            await rig.Privacy.RevealAsync(PrivacySection.Bio);
            await rig.Privacy.RevealAsync(PrivacySection.Address);
            check(rig.Privacy.Bio?.FirstName == "Alex" && rig.Privacy.Addresses?.Count == 2 && rig.Privacy.Education is null, "Wrong protected data mapped");
            check(rig.Calls.Skip(3).Select(call => call.Path.Split('/').Last()).SequenceEqual(new[] { "101", "bio", "101", "address" }), "Fetched unrelated section or omitted status check");
            rig.Privacy.Hide(PrivacySection.Bio);
            check(rig.Privacy.Bio is null && !rig.Privacy.IsVisible(PrivacySection.Bio) && rig.Privacy.IsVisible(PrivacySection.Address), "Masks are not independent");
            await rig.Privacy.RevealAsync(PrivacySection.Emergency);
            await rig.Privacy.RevealAsync(PrivacySection.Education);
            check(rig.Privacy.EmergencyContacts?.Single().FirstName == "Casey" && rig.Privacy.Education?.Single().School == "Example school", "Emergency/education reveal failed");
            check(rig.Calls.Where(call => call.Path.Contains("/applicants/")).All(call => call.Pii == "opaque-pii" && call.Bearer == "staff-access"), "PII/staff token routing changed");
        });
        add("Privacy controller clears visible data at absolute expiry and notifies without a new request", async () =>
        {
            using var rig = await Rig.Create(); await rig.Authorize(); await rig.Privacy.RevealAsync(PrivacySection.Bio);
            var notifications = 0; rig.Privacy.Changed += () => notifications++;
            rig.Clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
            check(rig.Privacy.Bio is not null, "Expired early");
            rig.Clock.Advance(TimeSpan.FromTicks(1), fireTimers: false);
            check(rig.Privacy.Stage == PrivacyStage.Locked && rig.Privacy.Bio is null && !rig.Privacy.CanReveal(PrivacySection.Bio), "Timer delay exposed expired data");
            var calls = rig.Calls.Count; await rig.Privacy.RevalidateAsync(); await rig.Privacy.RevealAsync(PrivacySection.Bio);
            check(rig.Calls.Count == calls, "Expired state dispatched protected read");
            rig.Clock.Advance(TimeSpan.Zero);
            check(rig.Privacy.Bio is null && notifications > 0, "Expiry did not erase/notify");
        });
        add("Privacy controller clears state on pause logout password change and same-email replacement login", async () =>
        {
            foreach (var action in new[] { "pause", "logout", "password", "same-email", "other-staff" })
            {
                using var rig = await Rig.Create(); await rig.Authorize(); await rig.Privacy.RevealAsync(PrivacySection.Bio);
                switch (action)
                {
                    case "pause": rig.Session.Pause(); await rig.Session.ResumeAsync(); break;
                    case "logout": await rig.Session.LogoutAsync(); break;
                    case "password": rig.Session.BeginPasswordChange(); break;
                    case "other-staff":
                        rig.Gateway.Auth = new(AuthOutcome.SignedIn, Rig.Staff("other@example.invalid"));
                        await rig.Session.SignInAsync("other@example.invalid", "synthetic"); break;
                    default: await rig.Session.SignInAsync("staff@example.invalid", "synthetic"); break;
                }
                check(rig.Privacy.Stage == PrivacyStage.Locked && rig.Privacy.Bio is null && !rig.Privacy.IsVisible(PrivacySection.Bio), "Session transition retained privacy state");
                var calls = rig.Calls.Count; await rig.Privacy.RequestCodeAsync();
                check(rig.Calls.Count == calls, "Transition retained old applicant selection");
            }
        });
        add("Ordinary staff token rotation preserves privacy lineage and uses new bearer on revalidation", async () =>
        {
            using var rig = await Rig.Create(); await rig.Authorize(); await rig.Privacy.RevealAsync(PrivacySection.Bio);
            rig.Gateway.Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.Success, new("rotated-access", "rotated-refresh", "rotated-xsrf")));
            await rig.Session.CheckSessionAsync();
            check(rig.Privacy.Bio is not null && rig.Privacy.Stage == PrivacyStage.Verified, "Token rotation mistaken for new login");
            await rig.Privacy.RevalidateAsync();
            check(rig.Calls.Last().Bearer == "rotated-access" && rig.Calls.Last().Pii == "opaque-pii" && rig.Privacy.Bio is not null, "Revalidation used stale staff bearer");
        });
        add("Late OTP verification cannot restore another applicant or clear a newer busy operation", async () =>
        {
            using var rig = await Rig.Create(); rig.Privacy.SelectApplicant("101"); await rig.Privacy.RequestCodeAsync();
            var oldVerify = Signal(); var newRequest = Signal();
            rig.Override = (request, _) => request.RequestUri!.AbsolutePath.EndsWith("/verify") ? oldVerify.Task : newRequest.Task;
            var old = rig.Privacy.VerifyCodeAsync("123456");
            rig.Privacy.SelectApplicant("102");
            var next = rig.Privacy.RequestCodeAsync();
            oldVerify.SetResult(Rig.VerifiedResponse()); await old;
            check(rig.Privacy.IsBusy && rig.Privacy.Stage == PrivacyStage.Locked && !rig.Calls.Any(call => call.Path.Contains("/status/")), "Stale verification restored access or cleared busy flag");
            newRequest.SetResult(Rig.Response("{\"code\":0}")); await next;
            check(rig.Privacy.Stage == PrivacyStage.CodeRequested && !rig.Privacy.IsBusy && rig.Privacy.Bio is null, "New request lost state");
        });
        add("Late protected reads cannot repopulate cancelled disposed logged-out or changed-applicant state", async () =>
        {
            foreach (var action in new[] { "cancel", "dispose", "logout", "applicant" })
            {
                using var rig = await Rig.Create(); await rig.Authorize();
                var delayed = Signal();
                rig.Override = (request, _) => request.RequestUri!.AbsolutePath.EndsWith("/bio") ? delayed.Task : null;
                var reveal = rig.Privacy.RevealAsync(PrivacySection.Bio);
                switch (action)
                {
                    case "cancel": rig.Privacy.Cancel(); break;
                    case "dispose": rig.Privacy.Dispose(); break;
                    case "logout": await rig.Session.LogoutAsync(); break;
                    default: rig.Privacy.SelectApplicant("102"); break;
                }
                delayed.SetResult(Rig.Response(PrivacyResponseTests.Bio)); await reveal;
                check(rig.Privacy.Bio is null && rig.Privacy.Stage == PrivacyStage.Locked && !rig.Privacy.IsBusy, "Late read exposed data after reset");
            }
        });
        add("Hiding a section while its read is pending discards that response without hiding other sections", async () =>
        {
            using var rig = await Rig.Create(); await rig.Authorize(); await rig.Privacy.RevealAsync(PrivacySection.Address);
            var delayed = Signal();
            rig.Override = (request, _) => request.RequestUri!.AbsolutePath.EndsWith("/bio") ? delayed.Task : null;
            var reveal = rig.Privacy.RevealAsync(PrivacySection.Bio);
            rig.Privacy.Hide(PrivacySection.Bio);
            delayed.SetResult(Rig.Response(PrivacyResponseTests.Bio)); await reveal;
            check(rig.Privacy.Bio is null && !rig.Privacy.IsVisible(PrivacySection.Bio) && rig.Privacy.Addresses is not null, "Late read undid independent hide");
            var denied = Signal();
            rig.Override = (request, _) => request.RequestUri!.AbsolutePath.EndsWith("/bio") ? denied.Task : null;
            var rejectedRead = rig.Privacy.RevealAsync(PrivacySection.Bio);
            rig.Privacy.Hide(PrivacySection.Bio);
            denied.SetResult(Rig.Response("{\"code\":99}", HttpStatusCode.Forbidden)); await rejectedRead;
            check(rig.Privacy.Stage == PrivacyStage.Locked && rig.Privacy.Addresses is null, "Hiding suppressed a later server denial and retained other data");
        });
        add("Revalidation removes revoked claims and obeys a shortened server expiry", async () =>
        {
            using var rig = await Rig.Create(); await rig.Authorize();
            await rig.Privacy.RevealAsync(PrivacySection.Bio); await rig.Privacy.RevealAsync(PrivacySection.Address);
            rig.Override = (request, _) => request.RequestUri!.AbsolutePath.Contains("/status/") ? Task.FromResult(Rig.Response(
                PrivacyResponseTests.Status.Replace("canViewBioDetails", "unknownClaim").Replace("00:05:00Z", "00:01:00Z"))) : null;
            await rig.Privacy.RevalidateAsync();
            check(rig.Privacy.Bio is null && rig.Privacy.Addresses is not null && !rig.Privacy.CanReveal(PrivacySection.Bio), "Revoked claim retained protected data");
            var calls = rig.Calls.Count; await rig.Privacy.RevealAsync(PrivacySection.Bio);
            check(rig.Calls.Count == calls, "Locally revoked section dispatched");
            rig.Clock.Advance(TimeSpan.FromMinutes(1));
            check(rig.Privacy.Addresses is null && rig.Privacy.Stage == PrivacyStage.Locked, "Shorter server expiry ignored");
        });
        add("Privacy failures clear local authorization notify completion and never replay OTP or terminate staff", async () =>
        {
            using (var rig = await Rig.Create())
            {
                rig.Privacy.SelectApplicant("101");
                rig.Override = (_, _) => throw new HttpRequestException("synthetic");
                var busyStates = new List<bool>(); rig.Privacy.Changed += () => busyStates.Add(rig.Privacy.IsBusy);
                await rig.Privacy.RequestCodeAsync();
                check(rig.Calls.Count == 1 && rig.Privacy.Stage == PrivacyStage.Locked && !rig.Privacy.IsBusy && busyStates.Last() == false, "OTP replay or failure did not notify completion");
            }
            foreach (var failure in new[] { "unauthorized", "unverified", "transport", "malformed" })
            {
                using var rig = await Rig.Create(); await rig.Authorize(); await rig.Privacy.RevealAsync(PrivacySection.Bio);
                rig.Override = (request, _) => !request.RequestUri!.AbsolutePath.Contains("/status/") ? null : failure switch
                {
                    "unauthorized" => Task.FromResult(Rig.Response("{\"code\":2004}", HttpStatusCode.Unauthorized)),
                    "unverified" => Task.FromResult(Rig.Response("{\"code\":0,\"data\":[{\"verified\":false}]}")),
                    "malformed" => Task.FromResult(Rig.Response("not-json")),
                    _ => throw new HttpRequestException("synthetic")
                };
                await rig.Privacy.RevalidateAsync();
                check(rig.Privacy.Stage == PrivacyStage.Locked && rig.Privacy.Bio is null && !rig.Privacy.IsBusy && rig.Session.ProtectedWorkAllowed,
                    "Failed privacy status retained data or silently logged out staff");
            }
        });
    }

    private static TaskCompletionSource<HttpResponseMessage> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal sealed class Rig : IDisposable
    {
        public readonly Clock Clock = new();
        public readonly FakeGateway Gateway;
        public readonly SessionController Session;
        public readonly HttpClient Client;
        public readonly ApplicantPrivacyController Privacy;
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>?>? Override;
        public List<(string Path, string? Bearer, string? Pii)> Calls { get; } = [];
        private Rig()
        {
            Gateway = new() { Auth = new(AuthOutcome.SignedIn, Staff("staff@example.invalid")) };
            Session = new(Gateway);
            Client = new(new Handler((request, token) =>
            {
                Calls.Add((request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Parameter,
                    request.Headers.TryGetValues("X-PII-Access-Token", out var pii) ? pii.Single() : null));
                return Override?.Invoke(request, token) ?? Task.FromResult(DefaultResponse(request));
            }));
            var options = new BackendOptions { BaseUrl = "https://privacy.example.invalid", ApiRootPath = "/api/v1", ApiContract = ApiContract.Privacy };
            Privacy = new(new(new ApiTransport(Client, options), options, Clock), Session, Clock);
        }
        public static StaffSession Staff(string email) => new(email, new("staff-access", "staff-refresh", "staff-xsrf"), ["CPW"]);
        public static async Task<Rig> Create()
        { var rig = new Rig(); await rig.Session.SignInAsync("staff@example.invalid", "synthetic"); return rig; }
        public async Task Authorize()
        { Privacy.SelectApplicant("101"); await Privacy.RequestCodeAsync(); await Privacy.VerifyCodeAsync("123456"); }
        public static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        public static HttpResponseMessage VerifiedResponse()
        { var response = Response("{\"code\":0,\"message\":\"Success\"}"); response.Headers.TryAddWithoutValidation("Set-Cookie", "pii_access_token=opaque-pii; Secure; HttpOnly"); return response; }
        private static HttpResponseMessage DefaultResponse(HttpRequestMessage request) => request.RequestUri!.AbsolutePath.Split('/').Last() switch
        {
            "request" => Response("{\"code\":0}"), "verify" => VerifiedResponse(), "bio" => Response(PrivacyResponseTests.Bio),
            "address" => Response(PrivacyResponseTests.Address), "emergency" => Response(PrivacyResponseTests.Emergency),
            "education" => Response(PrivacyResponseTests.Education), _ => Response(PrivacyResponseTests.Status)
        };
        public void Dispose() { Privacy.Dispose(); Session.Dispose(); Client.Dispose(); }
    }
    internal sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private readonly List<ManualTimer> timers = [];
        public override DateTimeOffset GetUtcNow() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { var timer = new ManualTimer(this, callback, state); timers.Add(timer); timer.Change(dueTime, period); return timer; }
        public void Advance(TimeSpan elapsed, bool fireTimers = true)
        {
            now += elapsed;
            if (fireTimers) foreach (var timer in timers.ToArray()) timer.FireIfDue();
        }
        private sealed class ManualTimer(Clock clock, TimerCallback callback, object? state) : ITimer
        {
            private DateTimeOffset? due;
            private TimeSpan period;
            private bool disposed;
            public bool Change(TimeSpan dueTime, TimeSpan nextPeriod)
            { if (disposed) return false; due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime; period = nextPeriod; return true; }
            public void FireIfDue()
            { if (disposed || due is null || due > clock.now) return; due = period == Timeout.InfiniteTimeSpan ? null : clock.now + period; callback(state); }
            public void Dispose() { disposed = true; due = null; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken); }
}
