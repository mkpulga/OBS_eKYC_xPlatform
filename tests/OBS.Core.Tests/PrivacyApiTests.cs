using System.Net;
using System.Text;
using System.Text.Json;
using OBS.Core;

static class PrivacyApiTests
{
    public static void Register(Action<string, Func<Task>> add, Action<bool, string> check)
    {
        BackendOptions Options() => new()
        {
            BaseUrl = "https://privacy.example.invalid", ApiRootPath = "/api/v1",
            ApiContract = ApiContract.Privacy, TokenHeaderKey = "X-Obs-Token", UserAgent = "synthetic-header"
        };
        StaffSession Staff(params string[] permissions) => new("staff@example.invalid", new("login-token", "refresh-token", "xsrf-token"), permissions);
        HttpResponseMessage Response(string body = "{\"code\":0,\"data\":[]}", HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        PrivacyApiService Service(HttpClient client, BackendOptions? options = null)
        {
            options ??= Options();
            return new(new ApiTransport(client, options), options);
        }

        add("Privacy API root is explicit and preserves existing root defaults", () =>
        {
            check(Options().GetApiRoot().AbsolutePath == "/api/v1/", "Wrong privacy root");
            check((Options() with { BaseUrl = "https://privacy.example.invalid/api/v1/" }).GetApiRoot().AbsolutePath == "/api/v1/", "Duplicated root");
            check(new BackendOptions { BaseUrl = "https://legacy.example.invalid" }.GetApiRoot().AbsolutePath == "/obs/api/v1/", "Changed existing default");
            var ini = BackendOptions.ReadIni("baseUrl=https://privacy.example.invalid\napiRootPath=/api/v1\napiContract=Privacy\nfeatureSet=StandAlone\ntokenHeaderKey=X-Obs-Token");
            check(ini.ApiContract == ApiContract.Privacy && ini.GetApiRoot().AbsolutePath == "/api/v1/", "INI contract lost");
            foreach (var options in new[] { Options() with { ApiRootPath = "/unconfirmed" }, Options() with { ApiRootPath = "/obs/api/v1" }, Options() with { BaseUrl = "http://localhost:8080" },
                Options() with { BaseUrl = "https://privacy.example.invalid/api/v1/dashboard" }, Options() with { TokenHeaderKey = "X-PII-Access-Token" } })
            {
                try { _ = options.GetApiRoot(); throw new InvalidOperationException("Invalid configuration accepted"); }
                catch (ArgumentException) { }
            }
            return Task.CompletedTask;
        });
        add("Privacy OTP requests send exact documented fields and staff bearer", async () =>
        {
            var paths = new List<string>();
            using var client = new HttpClient(new Handler((request, _) =>
            {
                check(request.Method == HttpMethod.Post && request.Headers.Authorization?.Parameter == "login-token", "Wrong method or credential");
                check(!request.Headers.Contains("X-PII-Access-Token") && !request.Headers.Contains("Cookie"), "Leaked PII credential");
                check(request.Headers.GetValues("X-Obs-Token").Single() == "synthetic-header", "Missing configured header");
                check(request.Content?.Headers.ContentType?.MediaType == "application/json", "Wrong content type");
                using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                var path = request.RequestUri!.AbsolutePath;
                paths.Add(path);
                check(body.RootElement.GetProperty("applicationId").GetString() == "APP-1", "Wrong applicant");
                check(body.RootElement.EnumerateObject().Count() == (path.EndsWith("/verify") ? 2 : 1), "Undocumented request fields");
                if (path.EndsWith("/verify")) check(body.RootElement.GetProperty("otp").GetString() == "123456", "Wrong OTP");
                return Task.FromResult(Response());
            }));
            var service = Service(client);
            await service.RequestOtpAsync(Staff(), "APP-1");
            await service.VerifyOtpAsync(Staff(), "APP-1", "123456");
            check(paths.SequenceEqual(new[] { "/api/v1/privacy/otp/request", "/api/v1/privacy/otp/verify" }), "Wrong routes");
        });
        add("Every privacy read uses its documented route and separate PII header", async () =>
        {
            var paths = new List<string>();
            using var client = new HttpClient(new Handler((request, _) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                paths.Add(path);
                check(request.Method == HttpMethod.Get && request.Content is null, "GET body or wrong method");
                check(request.Headers.Authorization?.Parameter == "login-token", "Replaced login token with PII token");
                check(!request.Headers.Contains("Cookie"), "Unexpected cookie handling");
                if (path.EndsWith("/dashboard")) check(!request.Headers.Contains("X-PII-Access-Token"), "Dashboard received PII token");
                else check(request.Headers.GetValues("X-PII-Access-Token").Single() == "pii-token", "Missing separate PII token");
                return Task.FromResult(Response());
            }));
            var service = Service(client);
            var staff = Staff("VIEWDASHBOARD", "VINFORMATIONGATHERING", "VCARDHOLDERUPDATE");
            var pii = new PiiAccessToken("pii-token");
            await service.GetStatusAsync(staff, "APP-1", pii);
            await service.GetBioAsync(staff, "CIT-1", pii);
            await service.GetAddressAsync(staff, "CIT-1", pii);
            await service.GetEmergencyAsync(staff, "CIT-1", pii);
            await service.GetEducationAsync(staff, "CIT-1", pii);
            await service.GetInformationGatheringAsync(staff, "CIT-1", pii);
            await service.GetCardholderUpdateAsync(staff, "CIT-1", pii);
            await service.GetDashboardAsync(staff);
            check(paths.SequenceEqual(new[] { "/api/v1/privacy/verification/status/APP-1", "/api/v1/privacy/applicants/CIT-1/bio",
                "/api/v1/privacy/applicants/CIT-1/address", "/api/v1/privacy/applicants/CIT-1/emergency", "/api/v1/privacy/applicants/CIT-1/education",
                "/api/v1/information/gathering/CIT-1", "/api/v1/cardholder/update/CIT-1", "/api/v1/dashboard" }), "Wrong read routes");
        });
        add("Redacted applicant reads and status omit absent PII credentials", async () =>
        {
            var calls = 0;
            using var client = new HttpClient(new Handler((request, _) =>
            {
                check(!request.Headers.Contains("X-PII-Access-Token"), "Sent fabricated PII token"); calls++;
                return Task.FromResult(Response());
            }));
            var service = Service(client); var staff = Staff("VINFORMATIONGATHERING", "VCARDHOLDERUPDATE");
            await service.GetStatusAsync(staff, "APP-1");
            await service.GetInformationGatheringAsync(staff, "CIT-1");
            await service.GetCardholderUpdateAsync(staff, "CIT-1");
            check(calls == 3, "Missing calls");
        });
        add("Privacy adapters deny missing permissions and unsupported contracts locally", async () =>
        {
            using var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Request sent")));
            var service = Service(client);
            foreach (var result in new[] { await service.GetDashboardAsync(Staff()), await service.GetInformationGatheringAsync(Staff(), "CIT-1"),
                await service.GetCardholderUpdateAsync(Staff(), "CIT-1"),
                await service.RequestOtpAsync(new StaffSession("staff@example.invalid", new("a", "r", "x"), [], true), "APP-1"),
                await service.RequestOtpAsync(new StaffSession("staff@example.invalid", new("", "r", "x"), []), "APP-1") })
                check(result.Failure == ApiFailure.NotPermitted && result.HttpStatus is null, "Permission bypass or fabricated HTTP response");
            var unsupported = Service(client, Options() with { ApiContract = ApiContract.Refactored });
            check((await unsupported.RequestOtpAsync(Staff(), "APP-1")).Failure == ApiFailure.ContractUnavailable, "Silent contract switching");
        });
        add("Invalid privacy identifiers and OTPs dispatch no requests", async () =>
        {
            using var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Request sent")));
            var service = Service(client);
            foreach (var id in new[] { "", " ", "\t\r\n" })
            {
                try { await service.RequestOtpAsync(Staff(), id); throw new InvalidOperationException("Accepted unsafe ID"); }
                catch (ArgumentException) { }
                try { await service.GetBioAsync(Staff(), id, new("pii")); throw new InvalidOperationException("Accepted unsafe path"); }
                catch (ArgumentException) { }
            }
            foreach (var otp in new[] { "", "12345", "1234567", "12a456", "１２３４５６" })
            {
                try { await service.VerifyOtpAsync(Staff(), "APP-1", otp); throw new InvalidOperationException("Accepted invalid OTP"); }
                catch (ArgumentException) { }
            }
        });
        add("PII extraction accepts observed bare success but rejects contradictory metadata", async () =>
        {
            foreach (var (body, cookie, accepted) in new[] {
                ("{\"code\":0,\"message\":\"Success\"}", "pii_access_token=opaque-pii; HttpOnly; Secure", true),
                ("{\"code\":0}", "refresh_token=opaque-pii", false),
                ("{\"code\":0,\"data\":null}", "pii_access_token=opaque-pii", false),
                ("{\"code\":0,\"data\":[{\"verified\":true}],\"Data\":[{\"verified\":true}]}", "pii_access_token=opaque-pii", false),
                ("{\"code\":0,\"data\":[{\"verified\":true}]}", "pii_access_token=opaque-pii; HttpOnly; Secure", true),
                ("{\"code\":0,\"data\":[{\"verified\":false}]}", "pii_access_token=opaque-pii", false),
                ("{\"code\":1,\"data\":[{\"verified\":true}]}", "pii_access_token=opaque-pii", false),
                ("{\"code\":0,\"data\":[{\"verified\":\"true\"}]}", "pii_access_token=opaque-pii", false),
                ("{\"code\":0,\"data\":[{\"verified\":true}]}", "refresh_token=opaque-pii", false),
                ("{\"code\":0,\"data\":[{\"verified\":true}]}", "pii_access_token=", false),
                ("{\"code\":0,\"data\":[]}", "pii_access_token=opaque-pii", false),
                ("{\"code\":0,\"data\":[{\"verified\":true},{\"verified\":true}]}", "pii_access_token=opaque-pii", false) })
            {
                using var client = new HttpClient(new Handler((_, _) =>
                {
                    var response = Response(body); response.Headers.TryAddWithoutValidation("Set-Cookie", cookie); return Task.FromResult(response);
                }));
                var result = await Service(client).VerifyOtpAsync(Staff(), "APP-1", "123456");
                var pii = PiiAccessToken.FromVerification(result);
                check((pii is not null) == accepted, "Unverified credential accepted");
                check(pii is null || !pii.ToString().Contains("opaque-pii"), "Token diagnostic leak");
            }
            check(!new RequestSecurity { PiiToken = new("private-value") }.ToString().Contains("private-value"), "Security diagnostic leak");
        });
        add("Privacy HTTP business and malformed responses remain distinguishable", async () =>
        {
            foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
            {
                var calls = 0;
                using var client = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(Response("{\"code\":0}", status)); }));
                var result = await Service(client).RequestOtpAsync(Staff(), "APP-1");
                check(result.HttpStatus == status && !SourceAuthenticationGateway.IsSuccess(result) && calls == 1, "Error swallowed or OTP retried");
            }
            foreach (var body in new[] { "", "null", "[]", "123", "<html>error</html>" })
            {
                using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(body))));
                var result = await Service(client).RequestOtpAsync(Staff(), "APP-1");
                check(result.Failure == ApiFailure.InvalidResponse && result.HttpStatus == HttpStatusCode.OK, "Malformed envelope accepted");
            }
            using var businessClient = new HttpClient(new Handler((_, _) => Task.FromResult(Response("{\"code\":9}"))));
            check(!SourceAuthenticationGateway.IsSuccess(await Service(businessClient).RequestOtpAsync(Staff(), "APP-1")), "Business rejection accepted");
        });
        add("Privacy cancellation and transport failure never retry an OTP mutation", async () =>
        {
            var calls = 0;
            using var client = new HttpClient(new Handler((_, ct) => { calls++; return Task.FromException<HttpResponseMessage>(new OperationCanceledException(ct)); }));
            using var stop = new CancellationTokenSource(); stop.Cancel();
            var cancelled = await Service(client).RequestOtpAsync(Staff(), "APP-1", stop.Token);
            check(cancelled.Failure == ApiFailure.Cancelled && calls <= 1, "Cancellation lost or replayed");
            calls = 0;
            using var failing = new HttpClient(new Handler((_, _) => { calls++; throw new HttpRequestException("synthetic"); }));
            check((await Service(failing).VerifyOtpAsync(Staff(), "APP-1", "123456")).Failure == ApiFailure.Transport && calls == 1, "Transport failure replayed");
            using var timeout = new HttpClient(new Handler((_, _) => Task.FromException<HttpResponseMessage>(new OperationCanceledException())));
            check((await Service(timeout).RequestOtpAsync(Staff(), "APP-1")).Failure == ApiFailure.Transport, "Timeout reported as caller cancellation");
        });
        add("Privacy login keeps documented password and OTP with no bearer", async () =>
        {
            var calls = 0;
            using var client = new HttpClient(new Handler((request, _) =>
            {
                check(request.RequestUri!.AbsolutePath == "/api/v1/session/authenticate" && request.Headers.Authorization is null, "Wrong login root or bearer");
                using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                check(body.RootElement.GetProperty("username").GetString() == "staff@example.invalid" && body.RootElement.GetProperty("password").GetString() == "synthetic-password", "Login fields changed");
                check(body.RootElement.EnumerateObject().Count() == (++calls == 1 ? 2 : 3), "Wrong OTP shape");
                return Task.FromResult(Response("{\"code\":0}"));
            }));
            var gateway = new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options());
            check(gateway.ContractAvailable, "Privacy login blocked");
            check((await gateway.AuthenticateAsync("staff@example.invalid", "synthetic-password", null, default)).Outcome == AuthOutcome.OtpRequired, "OTP request unavailable");
            // Session response fields still require the existing source-backed contract; no guessed login session.
            check((await gateway.AuthenticateAsync("staff@example.invalid", "synthetic-password", "123456", default)).Outcome == AuthOutcome.Blocked && calls == 2, "Fabricated session");
        });
        add("Privacy observed login structure with both exact cookies builds a complete staff session", async () =>
        {
            using var client = new HttpClient(new Handler((_, _) =>
            {
                var response = Response("""
                    {"code":0,"message":"Success","data":[{"email":"staff@example.invalid","password":{"isexpired":0,"isfirstlogon":0},
                    "roles":{"modules":["VIEWDASHBOARD"],"minpassword":1,"maxpassword":50},
                    "token":{"accesstoken":"synthetic-access","type":"Bearer","expiresin":900}}]}
                    """);
                response.Headers.TryAddWithoutValidation("Set-Cookie", new[] { "refresh_token=synthetic-refresh; HttpOnly; Secure", "XSRF-TOKEN=synthetic-xsrf; Secure" });
                return Task.FromResult(response);
            }));
            var reply = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options())
                .AuthenticateAsync("staff@example.invalid", "synthetic-password", "123456", default);
            check(reply.Outcome == AuthOutcome.SignedIn && reply.Session?.Tokens.IsComplete == true && reply.Session.Allows("VIEWDASHBOARD") &&
                reply.Session.MinPassword == 1 && reply.Session.MaxPassword == 50 && !reply.Session.PasswordChangeRequired, "Observed login shape failed to map");
        });
        add("Privacy logout is bodyless with staff bearer and no PII credential", async () =>
        {
            var calls = 0;
            using var client = new HttpClient(new Handler((request, _) =>
            {
                check(request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/v1/session/terminate", "Wrong logout route");
                check(request.Content is null && request.Headers.Authorization?.Parameter == "login-token", "Wrong logout body or token");
                check(!request.Headers.Contains("X-PII-Access-Token") && !request.Headers.Contains("X-XSRF-TOKEN"), "Undocumented logout headers");
                calls++; return Task.FromResult(Response());
            }));
            await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).TerminateAsync(Staff(), default);
            check(calls == 1, "Logout duplicated");
        });
        add("Transport restricts POST routes and prevents PII credential cross-routing", async () =>
        {
            using var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Request sent")));
            var api = new ApiTransport(client, Options());
            foreach (var route in new[] { "session/unconfirmed", "session/refresh/extra", "session/refresh?x=1", "session/refresh#fragment", "session/\\other", "privacy/otp/request/../verify" })
            {
                try { await api.PostAsync(route, new { }); throw new InvalidOperationException("Undocumented route accepted"); }
                catch (ArgumentException) { }
            }
            foreach (var route in new[] { "privacy/applicants/id/other", "privacy/applicants/a/b/bio", "privacy/verification/status/../id", "dashboard?period=day" })
            {
                try { await api.GetAsync(route); throw new InvalidOperationException("Unsafe read accepted"); }
                catch (ArgumentException) { }
            }
            foreach (var route in new[] { "dashboard", "cardholder/pending" })
            {
                try { await api.GetAsync(route, new RequestSecurity { PiiToken = new("pii-token") }); throw new InvalidOperationException("PII token leaked"); }
                catch (ArgumentException) { }
            }
        });
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }
}
