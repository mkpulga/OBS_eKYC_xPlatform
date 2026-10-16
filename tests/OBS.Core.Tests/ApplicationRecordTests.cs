using System.Net;
using System.Text;
using OBS.Core;

static class ApplicationRecordTests
{
    public const string Body = """
    {"code":0,"data":[{"id":123,"referencenumber":"123","status":"PENDING","email":"alex@example.invalid","msisdn":"+63 900 000 0123",
    "personalinformation":{"firstname":"Alex","middlename":"Santos","lastname":"Cruz","dateofbirth":"2000-06-07","gender":"Female","pobprovince":"ABRA","pobcity":"BANGUED","pobcountry":"Philippines","bloodtype":"A-","nationality":"Filipino","civilstatus":"Single"},
    "address":{"presentaddress":{"addressLine1":"1901 Example Boulevard","barangay":"Molino IV","city":"Bacoor City","zipcode":"4102"}},
    "emergencycontact":{"firstname":"Maria","lastname":"Cruz","relationship":"Spouse","contactnumber":"+63 900 000 0456","addressinformation":{"barangay":"Molino IV","city":"Bacoor City"}},
    "educationalinformation":{"educationalattainment":"Bachelor of Science","course":"Information Technology","schoolname":"Example State University"},
    "specimen":{"remarks":"Awaiting capture"}}]}
    """;
    public static void Register(Action<string, Func<Task>> add, Action<bool, string> check, Func<BackendOptions> options, Func<StaffSession> verifier)
    {
        HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        ApplicationRecordsService Service(HttpClient client) => new(new ApiTransport(client, options()));
        add("Application lookup sends source-backed authorized detail and attachment GETs", async () =>
        {
            var calls = new List<string>();
            using var client = new HttpClient(new Handler(request =>
            {
                check(request.Method == HttpMethod.Get && request.Content is null, "Wrong method/body");
                check(request.Headers.Authorization?.Parameter == "a" && request.Headers.GetValues("X-Obs-Token").Single() == "test-header", "Security headers missing");
                var path = request.RequestUri!.AbsolutePath;
                calls.Add(path);
                return Response(path.EndsWith("/ALL") ? """{"code":0,"data":[{"filename":"ID.pdf","doctype":"PRESENTED ID PICTURE (FRONT)","docvalue":"JVBERi0xLjQ="}]}""" : Body);
            }));
            var reply = await Service(client).FindAsync(verifier(), " 123 ", default);
            check(reply.Outcome == ApplicationOutcome.Success && reply.Record?.Name == "Alex Santos Cruz", "Record not mapped");
            check(reply.Record?.Address == "1901 Example Boulevard, Molino IV, Bacoor City, 4102", "Address not mapped");
            check(reply.Record?.EmergencyName == "Maria Cruz" && reply.Record.School == "Example State University" && reply.Documents?.Single().Name == "ID.pdf", "Sections missing");
            check(calls.SequenceEqual(new[] { "/obs/api/v1/cardholder/pending/123", "/obs/api/v1/documents/pending/123/ALL" }), "Incorrect routes");
        });
        add("Application permission and invalid identifiers send no requests", async () =>
        {
            using var client = new HttpClient(new Handler(_ => throw new InvalidOperationException("Request sent")));
            var denied = new StaffSession("staff@example.invalid", new("a", "r", "x"), []);
            check((await Service(client).FindAsync(denied, "123", default)).Outcome == ApplicationOutcome.NotPermitted, "Permission bypass");
            foreach (var number in new[] { "", "../123", "123?x=1", "a/b", "a%2fb", new string('1', 101) })
                check((await Service(client).FindAsync(verifier(), number, default)).Outcome == ApplicationOutcome.InvalidNumber, "Unsafe identifier");
        });
        add("Application empty, malformed, mismatched and ambiguous records fail safely", async () =>
        {
            foreach (var (body, expected) in new[] { ("{\"code\":0,\"data\":[]}", ApplicationOutcome.NotFound),
                ("{\"code\":0,\"data\":{}}", ApplicationOutcome.InvalidResponse), ("{\"code\":0,\"data\":[{},{}]}", ApplicationOutcome.InvalidResponse),
                (Body.Replace("\"123\"", "\"456\""), ApplicationOutcome.InvalidResponse), ("<html>error</html>", ApplicationOutcome.InvalidResponse), ("{}", ApplicationOutcome.Rejected) })
            {
                using var client = new HttpClient(new Handler(_ => Response(body)));
                var reply = await Service(client).FindAsync(verifier(), "123", default);
                check(reply.Outcome == expected && reply.Record is null, "Invalid record displayed");
            }
        });
        add("Application HTTP authorization and connection errors remain distinct", async () =>
        {
            foreach (var (status, expected) in new[] { (HttpStatusCode.Forbidden, ApplicationOutcome.NotPermitted), (HttpStatusCode.NotFound, ApplicationOutcome.NotFound), (HttpStatusCode.BadGateway, ApplicationOutcome.ConnectionFailure), (HttpStatusCode.BadRequest, ApplicationOutcome.Rejected) })
            {
                using var client = new HttpClient(new Handler(_ => Response("{}", status)));
                check((await Service(client).FindAsync(verifier(), "123", default)).Outcome == expected, "Incorrect error classification");
            }
        });
        add("Application attachment failure preserves applicant details and enables retry", async () =>
        {
            foreach (var documentBody in new[] { "{\"code\":99}", "{\"code\":0,\"data\":{}}", "<html>error</html>" })
            {
                using var client = new HttpClient(new Handler(request => Response(request.RequestUri!.AbsolutePath.EndsWith("/ALL") ? documentBody : Body)));
                var reply = await Service(client).FindAsync(verifier(), "123", default);
                check(reply.Record is not null && reply.Outcome == ApplicationOutcome.Success && reply.DocumentError is not null, "Attachment failure hid applicant");
            }
        });
        add("Source no-document code is an empty attachment list", async () =>
        {
            using var client = new HttpClient(new Handler(request => Response(request.RequestUri!.AbsolutePath.EndsWith("/ALL") ? "{\"code\":11}" : Body)));
            var reply = await Service(client).FindAsync(verifier(), "123", default);
            check(reply.Documents?.Count == 0 && reply.DocumentError is null, "No-document outcome lost");
        });
        add("Age handles birthdays, leap days, missing values and future dates", () =>
        {
            check(ApplicationPresentation.Birthday("2000-06-07", new(2026, 6, 6)).Contains("25 years"), "Early birthday");
            check(ApplicationPresentation.Birthday("2000-06-07", new(2026, 6, 7)).Contains("26 years"), "Birthday boundary");
            check(ApplicationPresentation.Birthday("2000-06-07T00:00:00Z", new(2026, 6, 7)).Contains("26 years"), "ISO timestamp birthday");
            check(ApplicationPresentation.Birthday("2000-02-29", new(2025, 2, 28)).Contains("24 years"), "Leap birthday");
            check(!ApplicationPresentation.Birthday("2030-01-01", new(2026, 1, 1)).Contains("years"), "Negative age");
            check(ApplicationPresentation.Birthday("", new(2026, 1, 1)) == "Not provided", "Missing birthday");
            return Task.CompletedTask;
        });
        add("Document previews validate binary formats and reject URLs and active content", () =>
        {
            foreach (var value in new[] { "https://example.invalid/id.pdf", "javascript:alert(1)", Convert.ToBase64String("<svg onload='alert(1)'/>"u8.ToArray()), "not base64" })
                check(ApplicationPresentation.Decode(new() { Value = value }) is null, "Unsafe content accepted");
            check(ApplicationPresentation.Decode(new() { Value = "data:application/pdf;base64,JVBERi0xLjQ=" })?.Mime == "application/pdf", "PDF not recognized");
            return Task.CompletedTask;
        });
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }
}
