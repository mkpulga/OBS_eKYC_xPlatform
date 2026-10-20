using System.Net;
using System.Text;
using System.Text.Json;
using OBS.Core;

var tests = new List<(string Name, Func<Task> Run)>();
void Add(string name, Func<Task> run) => tests.Add((name, run));
void Check(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); }
StaffSession Staff(string access = "a") => new("staff@example.invalid", new(access, "r", "x"), ["VINFORMATIONGATHERING", "CPW"]);
BackendOptions Options() => new() { BaseUrl = "https://backend.example.invalid/obs/api/v1", TokenHeaderKey = "X-Obs-Token", UserAgent = "test-header" };
const string userBody = """
{"code":0,"data":[{"email":"staff@example.invalid","token":{"accesstoken":"access"},"password":{"isfirstlogon":0,"isexpired":0},"roles":{"modules":["VINFORMATIONGATHERING","CPW"],"minpassword":8,"maxpassword":32}}]}
""";
HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK, bool cookies = false)
{
    var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    if (cookies) response.Headers.TryAddWithoutValidation("Set-Cookie", new[] { "refresh_token=refresh==; Secure; HttpOnly", "XSRF-TOKEN=xsrf%3D; Secure", "unrelated=wrong" });
    return response;
}

Add("API root avoids duplicate suffix and rejects HTTP", () =>
{
    Check(Options().GetApiRoot().AbsolutePath == "/obs/api/v1/", "Duplicate root");
    Check((Options() with { BaseUrl = "https://backend.example.invalid" }).GetApiRoot().AbsolutePath == "/obs/api/v1/", "Missing root");
    try { _ = (Options() with { BaseUrl = "http://backend.example.invalid" }).GetApiRoot(); throw new InvalidOperationException("Accepted insecure URL"); }
    catch (ArgumentException) { }
    return Task.CompletedTask;
});
Add("INI preserves equals in header values and separates feature set", () =>
{
    var options = BackendOptions.ReadIni("baseUrl=https://backend.example.invalid\napiContract=Refactored\nfeatureSet=Bacoor\ntokenHeaderKey=X-Obs-Token\nuserAgent=value==");
    Check(options.UserAgent == "value==" && options.ApiContract == ApiContract.Refactored && options.FeatureSet == FeatureSet.Bacoor, "INI mismatch");
    return Task.CompletedTask;
});
Add("Cookie parser retains exact names and equals", () =>
{
    var cookies = ApiTransport.ParseCookies(["refresh_token=\"a=b==\"; HttpOnly", "XSRF-TOKEN=x%3D; Secure", "OTHER=ignored", "Refresh_Token=wrong"]);
    Check(cookies["refresh_token"] == "a=b==" && cookies["XSRF-TOKEN"] == "x%3D" && cookies.Count == 4, "Unsafe cookie parsing");
    return Task.CompletedTask;
});
Add("HTTP status and business code remain distinct", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => Response("{\"code\":2,\"message\":\"private\"}", HttpStatusCode.Unauthorized)));
    var result = await new ApiTransport(client, Options()).PostAsync("session/authenticate", new { });
    Check(result.HttpStatus == HttpStatusCode.Unauthorized && result.BusinessCode == "2" && !SourceAuthenticationGateway.IsSuccess(result), "Status lost");
});
Add("Spring and malformed responses never become default successes", async () =>
{
    foreach (var body in new[] { "{\"status\":400,\"error\":\"Bad Request\"}", "<html>gateway error</html>", "{}", "" })
    {
        using var client = new HttpClient(new StubHandler(_ => Response(body)));
        var result = await new ApiTransport(client, Options()).PostAsync("session/refresh", new { });
        Check(!SourceAuthenticationGateway.IsSuccess(result), "False success");
    }
});
Add("Transport failure has no successful HTTP or business code", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException("test")));
    var result = await new ApiTransport(client, Options()).PostAsync("session/authenticate", new { });
    Check(result.Failure == ApiFailure.Transport && result.HttpStatus is null && result.BusinessCode is null, "Transport false success");
});
Add("Source authentication keeps request password and exact cookies", async () =>
{
    var calls = 0;
    using var client = new HttpClient(new StubHandler(request =>
    {
        Check(request.RequestUri!.AbsolutePath == "/obs/api/v1/session/authenticate", "Wrong endpoint");
        Check(request.Headers.GetValues("X-Obs-Token").Single() == "test-header", "Configured header lost");
        using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        Check(body.RootElement.GetProperty("password").GetString() == "secret", "Password missing");
        Check(body.RootElement.GetProperty("otp").GetString() == "123456", "OTP missing");
        calls++;
        return Response(userBody, cookies: true);
    }));
    var result = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).AuthenticateAsync("staff@example.invalid", "secret", "123456", default);
    Check(calls == 1 && result.Outcome == AuthOutcome.SignedIn && result.Session!.Tokens.Refresh == "refresh==" && result.Session.Tokens.Xsrf == "xsrf=", "Contract mismatch");
});
Add("Account rejection mappings keep credentials generic", async () =>
{
    foreach (var item in new[] { (HttpStatusCode.Unauthorized, "2", AuthOutcome.InvalidCredentials), (HttpStatusCode.BadRequest, "6", AuthOutcome.Inactive), (HttpStatusCode.BadRequest, "7", AuthOutcome.Locked) })
    {
        using var client = new HttpClient(new StubHandler(_ => Response($"{{\"code\":{item.Item2}}}", item.Item1)));
        var result = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).AuthenticateAsync("x", "y", null, default);
        Check(result.Outcome == item.Item3, "Rejection mismatch");
    }
});
Add("Missing session fields fail closed", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => Response("{\"code\":0,\"data\":[{}]}")));
    var result = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).AuthenticateAsync("x", "y", "123456", default);
    Check(result.Outcome == AuthOutcome.Blocked, "Accepted incomplete session");
});
Add("Refresh bearer and all three rotated tokens follow source", async () =>
{
    using var client = new HttpClient(new StubHandler(request =>
    {
        Check(request.Headers.Authorization?.Parameter == "r" && request.Headers.GetValues("X-XSRF-TOKEN").Single() == "x", "Wrong refresh authorization");
        return Response("{\"code\":0,\"data\":[{\"accesstoken\":\"new-access\"}]}", cookies: true);
    }));
    var result = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).RefreshAsync(Staff(), default);
    Check(result.Outcome == RefreshOutcome.Success && result.Tokens!.Access == "new-access" && result.Tokens.Refresh == "refresh==" && result.Tokens.Xsrf == "xsrf=", "Rotation incomplete");
});
Add("Logout payload casing and bearer are preserved", async () =>
{
    using var client = new HttpClient(new StubHandler(request =>
    {
        using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        Check(body.RootElement.TryGetProperty("userName", out _) && !body.RootElement.TryGetProperty("username", out _) && request.Headers.Authorization?.Parameter == "a", "Wrong logout shape");
        return Response("{\"code\":0}");
    }));
    await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).TerminateAsync(Staff(), default);
});
Add("OTP validation and cooldown are enforced without extra calls", async () =>
{
    var clock = new ManualClock(); var gateway = new FakeGateway { Auth = new(AuthOutcome.OtpRequired) };
    using var controller = new SessionController(gateway, clock);
    await controller.SignInAsync("email", "secret"); await controller.VerifyAsync("12a456"); await controller.ResendAsync();
    Check(gateway.AuthCalls == 1 && gateway.ResendCalls == 0 && controller.ResendSeconds == 60, "OTP guard failed");
    clock.Advance(TimeSpan.FromSeconds(60)); await controller.ResendAsync();
    Check(gateway.ResendCalls == 1 && controller.ResendSeconds == 60, "Cooldown failed");
});
Add("Navigation uses exact backend permissions", () =>
{
    var entries = Navigation.For(Staff()).Select(x => x.Title).ToArray();
    Check(entries.SequenceEqual(new[] { "Information Gathering" }) && !Staff().Allows("ROLEVIEW"), "Permission leakage");
    return Task.CompletedTask;
});
Add("First login requires CPW and blocks protected views", async () =>
{
    var gateway = new FakeGateway { Auth = new(AuthOutcome.PasswordChangeRequired, new StaffSession("email", new("a", "r", "x"), ["CPW"], true)) };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456");
    Check(controller.Stage == SignInStage.PasswordChange && !controller.ProtectedWorkAllowed, "Password bypass");
});
Add("First login without CPW safely returns to login", async () =>
{
    var gateway = new FakeGateway { Auth = new(AuthOutcome.PasswordChangeRequired, new StaffSession("email", new("a", "r", "x"), [], true)) };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456");
    Check(controller.Stage == SignInStage.Login && controller.Current is null && controller.Message.Contains("lacks permission"), "Missing CPW unsafe");
});
Add("Three consecutive connection failures end the session", async () =>
{
    var gateway = new FakeGateway { Auth = new(AuthOutcome.SignedIn, Staff()), Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.ConnectionFailure)) };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456");
    await controller.CheckSessionAsync(); await controller.CheckSessionAsync(); Check(controller.Current is not null, "Early termination");
    await controller.CheckSessionAsync(); Check(controller.Current is null && controller.Message.Contains("three times"), "Failure policy wrong");
});
Add("Successful refresh resets consecutive failures", async () =>
{
    var gateway = new FakeGateway { Auth = new(AuthOutcome.SignedIn, Staff()), Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.ConnectionFailure)) };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456");
    await controller.CheckSessionAsync(); await controller.CheckSessionAsync();
    gateway.Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.Success, new("new", "newr", "newx"))); await controller.CheckSessionAsync();
    gateway.Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.ConnectionFailure)); await controller.CheckSessionAsync(); await controller.CheckSessionAsync();
    Check(controller.Current?.Tokens.Access == "new", "Failure counter not reset");
});
Add("In-flight refresh cannot restore logout and checks never overlap", async () =>
{
    var pending = new TaskCompletionSource<RefreshReply>(TaskCreationOptions.RunContinuationsAsynchronously);
    var gateway = new FakeGateway { Auth = new(AuthOutcome.SignedIn, Staff()), Refresh = _ => pending.Task };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456");
    var first = controller.CheckSessionAsync(); await controller.CheckSessionAsync(); Check(gateway.RefreshCalls == 1, "Overlapping refresh");
    await controller.LogoutAsync(); pending.SetResult(new(RefreshOutcome.Success, new("stale", "staler", "stalex"))); await first;
    Check(controller.Current is null && controller.Stage == SignInStage.Login, "Stale response restored logout");
});
Add("Resume keeps protected work blocked until verification finishes", async () =>
{
    var pending = new TaskCompletionSource<RefreshReply>(TaskCreationOptions.RunContinuationsAsynchronously);
    var gateway = new FakeGateway { Auth = new(AuthOutcome.SignedIn, Staff()), Refresh = _ => pending.Task };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456");
    controller.Pause(); Check(!controller.ProtectedWorkAllowed, "Paused work allowed");
    var resume = controller.ResumeAsync(); Check(!controller.ProtectedWorkAllowed, "Resume bypass");
    pending.SetResult(new(RefreshOutcome.Success, new("new", "r", "x"))); await resume;
    Check(controller.ProtectedWorkAllowed, "Resume never completed");
});
Add("Refresh rejection shows no unsupported takeover claim", async () =>
{
    var gateway = new FakeGateway { Auth = new(AuthOutcome.SignedIn, Staff()), Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.Rejected)) };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456"); await controller.CheckSessionAsync();
    Check(controller.Message == "Your session has ended. Please sign in again.", "Unverified takeover claim");
});
Add("Five-second foreground timer dispatches and pauses", async () =>
{
    var gateway = new FakeGateway { Auth = new(AuthOutcome.SignedIn, Staff()) };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456");
    using var stop = new CancellationTokenSource(); var running = controller.RunChecksAsync(stop.Token);
    await Task.Delay(100); Check(gateway.RefreshCalls == 0, "Immediate refresh");
    await gateway.FirstRefresh.Task.WaitAsync(TimeSpan.FromSeconds(7)); Check(gateway.RefreshCalls == 1, "Timer failed");
    controller.Pause(); await controller.CheckSessionAsync(); Check(gateway.RefreshCalls == 1, "Pause ignored");
    stop.Cancel(); try { await running; } catch (OperationCanceledException) { }
});
Add("Unsupported capture fails explicitly", async () =>
{
    var adapter = new BlockedCaptureAdapter(ClientPlatform.Web, CaptureKind.Fingerprint);
    Check(!adapter.Availability.Available, "Capture enabled");
    try { await adapter.CaptureAsync(default); throw new InvalidOperationException("Fake capture success"); } catch (NotSupportedException) { }
});
Add("INI legacy fallbacks are explicit and diagnosed", () =>
{
    var options = BackendOptions.ReadIni("baseUrl=https://backend.example.invalid\ncity=Bacoor\ntoken=X-Custom-Token");
    Check(options.FeatureSet == FeatureSet.Bacoor && options.ApiContract == ApiContract.Refactored && options.Diagnostics.Count == 2 && options.TokenHeaderKey == "X-Custom-Token", "Compatibility fallback mismatch");
    return Task.CompletedTask;
});
Add("StandAlone wizard navigation never marks saved completion", () =>
{
    var wizard = new WizardState("stable-record", FeatureSet.StandAlone);
    foreach (var step in wizard.Steps) wizard.Navigate(step);
    Check(wizard.Steps.Count == 8 && wizard.Incomplete.Count == 8 && !wizard.CanSubmit && wizard.RecordId == "stable-record", "Navigation incorrectly completed data");
    wizard.ApplyPersistedCompletion(wizard.Steps, false); Check(!wizard.CanSubmit, "Unverified backend submit allowed");
    wizard.ApplyPersistedCompletion(wizard.Steps, true); Check(wizard.CanSubmit && wizard.Progress == 1, "Verified completion ignored");
    return Task.CompletedTask;
});
Add("Unsupported local capture cannot bypass missing saved fingerprint", () =>
{
    var wizard = new WizardState("stable-record", FeatureSet.StandAlone);
    wizard.ApplyPersistedCompletion(wizard.Steps.Where(step => step != EnrollmentStep.Fingerprint), true);
    Check(!wizard.CanSubmit && wizard.Incomplete.SequenceEqual(new[] { EnrollmentStep.Fingerprint }), "Missing fingerprint bypass");
    return Task.CompletedTask;
});
Add("Back ID requirements use source type list", () =>
{
    Check(WizardRules.RequiresBackId(" national id ") && WizardRules.RequiresBackId("HDMFIDPAGIBIG") && !WizardRules.RequiresBackId("PASSPORT") && !WizardRules.RequiresBackId(null), "Back ID type mismatch");
    return Task.CompletedTask;
});
Add("Address limit counts only the source's visible components", () =>
{
    Check(WizardRules.AddressCharacterCount("City", "Barangay", "hidden street", "hidden building", "Line 1", "Line 2", false) == 27, "Free-text address count mismatch");
    Check(WizardRules.AddressCharacterCount("City", "Barangay", "Street", "Building", "hidden line1", "hidden line2", true) == 29, "Structured address count mismatch");
    return Task.CompletedTask;
});
Add("Back to login clears pending OTP credentials", async () =>
{
    var gateway = new FakeGateway(); using var controller = new SessionController(gateway);
    await controller.SignInAsync("email", "secret"); controller.BackToLogin(); await controller.VerifyAsync("123456");
    Check(gateway.AuthCalls == 1 && controller.Stage == SignInStage.Login && controller.Current is null, "Pending login survived back action");
});
Add("Failed resume cannot enable protected work", async () =>
{
    var gateway = new FakeGateway { Auth = new(AuthOutcome.SignedIn, Staff()), Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.ConnectionFailure)) };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456");
    controller.Pause(); await controller.ResumeAsync(); Check(!controller.ProtectedWorkAllowed && controller.Current is not null, "Failed resume enabled work");
    gateway.Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.Success, new("new", "r", "x")));
    await controller.CheckSessionAsync(); Check(controller.ProtectedWorkAllowed, "Later success didn't resume work");
});
Add("Report footer fixes timezone and uses actual page fields", () =>
{
    var report = new ReportContract("Residence Verification", "staff@example.invalid", new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero), ListExportScope.CurrentPage, false);
    Check(report.PageFooter(2, 3) == "Residence Verification – Generated by staff@example.invalid | 2026-10-14 08:00:00 UTC+08:00 | Page 2 of 3", "Footer mismatch");
    Check(report.ExcelPrintFooter.EndsWith("Page &P of &N", StringComparison.Ordinal), "Excel page fields missing");
    return Task.CompletedTask;
});
Add("All-filtered export cannot silently use a partial page", () =>
{
    try { _ = new ReportContract("Module", "staff@example.invalid", DateTimeOffset.UtcNow, ListExportScope.AllFilteredRecords, false); throw new InvalidOperationException("Partial export mislabeled"); }
    catch (InvalidOperationException exception) when (exception.Message.Contains("verified complete")) { }
    return Task.CompletedTask;
});
Add("Refresh failure diagnostics never include backend messages or tokens", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => Response("{\"code\":3,\"message\":\"private-response-value\"}")));
    var result = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).RefreshAsync(Staff("private-access-value"), default);
    Check(result.Outcome == RefreshOutcome.Rejected && result.Diagnostic?.BusinessCode == 3 && result.Diagnostic.HttpStatus == 200, "Missing refresh failure evidence");
    Check(!result.Diagnostic!.ToString().Contains("private"), "Sensitive diagnostic content");
});
Add("Unexpected refresh token response has a distinct login explanation", async () =>
{
    var gateway = new FakeGateway { Auth = new(AuthOutcome.SignedIn, Staff()), Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.ContractBlocked)) };
    using var controller = new SessionController(gateway); await controller.SignInAsync("email", "secret", "123456"); await controller.CheckSessionAsync();
    Check(controller.Current is null && controller.Message.Contains("unsupported response"), "Contract failure hidden as expiry");
});
Add("Refresh preserves source parser's case-insensitive token field matching", async () =>
{
    foreach (var name in new[] { "accesstoken", "accessToken", "AccessToken", "ACCESSTOKEN" })
    {
        using var client = new HttpClient(new StubHandler(_ => Response($"{{\"code\":0,\"data\":[{{\"{name}\":\"rotated\"}}]}}", cookies: true)));
        var reply = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).RefreshAsync(Staff(), default);
        Check(reply.Outcome == RefreshOutcome.Success && reply.Tokens?.Access == "rotated", "Case variant rejected");
    }
});
Add("Case-insensitive envelope fields retain actual HTTP status", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => Response("{\"Code\":0,\"Data\":[{\"AccessToken\":\"rotated\"}]}")));
    var reply = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).RefreshAsync(Staff(), default);
    Check(reply.Outcome == RefreshOutcome.Success && reply.Diagnostic?.HttpStatus == 200, "Envelope casing mismatch");
});
Add("Refresh never guesses unsupported token aliases or container shapes", async () =>
{
    foreach (var body in new[] { "{\"code\":0,\"data\":[{\"access_token\":\"private-value\"}]}",
        "{\"code\":0,\"data\":{\"accesstoken\":\"private-value\"}}",
        "{\"code\":0,\"data\":[{\"token\":{\"accesstoken\":\"private-value\"}}]}" })
    {
        using var client = new HttpClient(new StubHandler(_ => Response(body)));
        var reply = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).RefreshAsync(Staff(), default);
        Check(reply.Outcome == RefreshOutcome.ContractBlocked && reply.Diagnostic?.Shape is not null, "Unsupported token shape accepted");
        Check(!reply.Diagnostic!.ToString().Contains("private-value"), "Shape diagnostics exposed token");
    }
});
Add("Ambiguous access token case variants fail closed", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => Response("{\"code\":0,\"data\":[{\"accesstoken\":\"first\",\"accessToken\":\"second\"}]}")));
    var reply = await new SourceAuthenticationGateway(new ApiTransport(client, Options()), Options()).RefreshAsync(Staff(), default);
    Check(reply.Outcome == RefreshOutcome.ContractBlocked, "Conflicting fields accepted");
});

StaffSession Verifier() => new("staff@example.invalid", new("a", "r", "x"), [CardholderVerificationService.Authority]);
CardholderVerificationService Cardholders(HttpClient client) => new(new ApiTransport(client, Options()));
Add("Pending cardholders use an authorized GET without a body", async () =>
{
    var calls = 0;
    using var client = new HttpClient(new StubHandler(request =>
    {
        Check(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/obs/api/v1/cardholder/pending", "Wrong request");
        Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "a", "Bearer missing");
        Check(request.Headers.GetValues("X-Obs-Token").Single() == "test-header", "Configured header lost");
        Check(request.Content is null, "GET sent a body");
        calls++;
        return Response("{\"code\":0,\"data\":[]}");
    }));
    var reply = await Cardholders(client).GetPendingAsync(Verifier(), default);
    Check(calls == 1 && reply.Outcome == PendingOutcome.Success && reply.Items.Count == 0, "Empty queue mismatch");
    Check(CardholderVerificationPresentation.Status(reply) == "No cardholders are pending verification.", "Empty queue status");
});
Add("GET rejects routes outside the allowlist", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => throw new InvalidOperationException("Request sent")));
    foreach (var route in new[] { "cardholder/pending/../1", "cardholder/pending/a%2Fb", "documents/pending/1/OTHER", "session/refresh", "cardholder/pending?x=1", "" })
    {
        try { _ = await new ApiTransport(client, Options()).GetAsync(route); throw new InvalidOperationException($"Accepted {route}"); }
        catch (ArgumentException) { }
    }
});
Add("Pending queue without PENDINGORS sends no request", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => throw new InvalidOperationException("Request sent")));
    var reply = await Cardholders(client).GetPendingAsync(Staff(), default);
    Check(reply.Outcome == PendingOutcome.NotPermitted && reply.Items.Count == 0, "Permission bypassed");
});
Add("Pending queue sorts oldest first and numbers rows", async () =>
{
    const string body = """
    {"code":0,"message":"ok","data":[
      {"referencenumber":"R3","fullname":"Third","datecreated":"2026-03-01 08:00:00"},
      {"referencenumber":"RX","fullname":"Unparseable","datecreated":"not a date"},
      {"ReferenceNumber":"R1","firstname":"Ana","lastname":"Cruz","suffix":"Jr.","DateCreated":"2026-01-01T08:00:00"},
      {"referencenumber":"R2","fullname":"Second","datecreated":"2026-02-01 08:00:00"}]}
    """;
    using var client = new HttpClient(new StubHandler(_ => Response(body)));
    var reply = await Cardholders(client).GetPendingAsync(Verifier(), default);
    Check(reply.Outcome == PendingOutcome.Success && reply.Message == "ok", "Success lost");
    Check(string.Join(",", reply.Items.Select(item => item.referencenumber)) == "R1,R2,R3,RX", "Wrong order");
    Check(string.Join(",", reply.Items.Select(item => item.No)) == "1,2,3,4", "Wrong numbering");
    Check(CardholderVerificationPresentation.Name(reply.Items[0]) == "Ana Cruz Jr.", "Name fallback");
    Check(CardholderVerificationPresentation.Status(reply) is null && CardholderVerificationPresentation.Count(reply) == "4 pending", "Row status");
});
Add("Pending queue rejections keep the backend message", async () =>
{
    foreach (var (status, body) in new[] { (HttpStatusCode.OK, "{\"code\":3,\"message\":\"Token expired\"}"), (HttpStatusCode.Unauthorized, "{\"code\":401,\"message\":\"Token expired\"}") })
    {
        using var client = new HttpClient(new StubHandler(_ => Response(body, status)));
        var reply = await Cardholders(client).GetPendingAsync(Verifier(), default);
        Check(reply.Outcome == PendingOutcome.Rejected && reply.Items.Count == 0 && CardholderVerificationPresentation.Status(reply) == "Token expired", "Rejection lost");
    }
});
Add("Pending queue connection failures are not rejections", async () =>
{
    foreach (Func<HttpRequestMessage, HttpResponseMessage> respond in new Func<HttpRequestMessage, HttpResponseMessage>[]
        { _ => throw new HttpRequestException("test"), _ => Response("{\"code\":0,\"data\":[]}", HttpStatusCode.BadGateway) })
    {
        using var client = new HttpClient(new StubHandler(respond));
        var reply = await Cardholders(client).GetPendingAsync(Verifier(), default);
        Check(reply.Outcome == PendingOutcome.ConnectionFailure && reply.Items.Count == 0, "Connection failure misclassified");
    }
});
Add("Pending queue malformed payloads fail closed", async () =>
{
    // The cid case documents a known gap: Json.NET coerced numbers to strings, System.Text.Json does not.
    foreach (var body in new[] { "{\"code\":0}", "{\"code\":0,\"data\":{\"fullname\":\"x\"}}", "{\"code\":0,\"data\":[{\"cid\":123}]}", "<html>gateway</html>" })
    {
        using var client = new HttpClient(new StubHandler(_ => Response(body)));
        var reply = await Cardholders(client).GetPendingAsync(Verifier(), default);
        Check(reply.Outcome == PendingOutcome.InvalidResponse && reply.Items.Count == 0, $"Malformed payload accepted: {body}");
    }
});
Add("Unavailable cardholder service never fabricates a queue", async () =>
{
    var reply = await new UnavailableCardholderVerification().GetPendingAsync(Verifier(), default);
    Check(reply.Outcome == PendingOutcome.Unavailable && reply.Items.Count == 0, "Unavailable queue mismatch");
});

ApplicationRecordTests.Register(Add, Check, Options, Verifier);
ApplicationRevealTests.Register(Add, Check);
FingerprintScannerTests.Register(Add, Check);
PrivacyApiTests.Register(Add, Check);
DashboardApiTests.Register(Add, Check);
PrivacyResponseTests.Register(Add, Check);
ApplicantPrivacyControllerTests.Register(Add, Check);
PrivacyWorkspaceTests.Register(Add, Check);
CitizenApplicationSearchTests.Register(Add, Check);
var failed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failed++; Console.WriteLine($"FAIL {test.Name}: {exception.Message}"); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed. Synthetic fixtures; no backend or device verification.");
return failed == 0 ? 0 : 1;

sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
}
sealed class ManualClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan elapsed) => now += elapsed;
}
sealed class FakeGateway : IAuthenticationGateway
{
    public bool ContractAvailable => true;
    public AuthReply Auth { get; set; } = new(AuthOutcome.OtpRequired);
    public Func<StaffSession, Task<RefreshReply>> Refresh { get; set; } = session => Task.FromResult(new RefreshReply(RefreshOutcome.Success, session.Tokens));
    public int AuthCalls, ResendCalls, RefreshCalls;
    public TaskCompletionSource FirstRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<AuthReply> AuthenticateAsync(string email, string password, string? otp, CancellationToken token) { AuthCalls++; return Task.FromResult(Auth); }
    public Task<AuthReply> ResendAsync(string email, string password, CancellationToken token) { ResendCalls++; return Task.FromResult(new AuthReply(AuthOutcome.OtpRequired)); }
    public Task<RefreshReply> RefreshAsync(StaffSession session, CancellationToken token) { RefreshCalls++; FirstRefresh.TrySetResult(); return Refresh(session); }
    public Task TerminateAsync(StaffSession session, CancellationToken token) => Task.CompletedTask;
    public Task<ApiResult> ForgotPasswordAsync(string email, CancellationToken token) => Task.FromResult(new ApiResult { HttpStatus = HttpStatusCode.OK, BusinessCode = "0" });
    public Task<ApiResult> ChangePasswordAsync(StaffSession session, string currentPassword, string newPassword, CancellationToken token) => Task.FromResult(new ApiResult { HttpStatus = HttpStatusCode.OK, BusinessCode = "0" });
}
