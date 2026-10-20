using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OBS.Core;

static class PrivacyResponseTests
{
    // Invented values only. Field structures reflect user-reported responses, not a live test.
    internal const string Status = """
        {"code":0,"data":[{"verified":true,"applicationId":"101","expiresAt":"2030-01-01T00:05:00Z",
        "authorizedSections":["canViewAddress","canViewEmergencyContact","canViewEducationalInformation","canViewBioDetails"]}]}
        """;
    internal const string Bio = """
        {"code":0,"data":[{"personal-info":[{"citizenid":101,"firstname":"Alex","middlename":null,"lastname":"Example","suffix":"",
        "displayname":"Alex Example","dateofbirth":"03/04/2001","gender":"EXAMPLE","bloodtype":null,"civilstatus":"SINGLE",
        "nationality":"EXAMPLE","accounttype":"RESIDENT","placeofbirthcity":" ","placeofbirthprovince":" ","placeofbirthcountry":"EXAMPLE",
        "tenurialstatus":" ","status":"ACTIVE","citizenstatus":"FOR_INITIAL_KYC","accountnumber":"excluded-value","mothermaidenname":"excluded-answer"}]}]}
        """;
    internal const string Address = """
        {"code":0,"data":[{"address":[
        {"citizenid":101,"addresstype":"PERMANENT","specificaddress":"Example block","buildingname":"","addressline1":null,"addressline2":null,
        "barangay":"Example barangay","city":"Example city","province":"Example province","region":"Example region","country":"Example country","zipcode":"0000"},
        {"citizenid":101,"addresstype":"PRESENT","specificaddress":"Another block","buildingname":"","addressline1":null,"addressline2":null,
        "barangay":"Another barangay","city":"Example city","province":"Example province","region":"Example region","country":"Example country","zipcode":"0001"}]}]}
        """;
    internal const string Emergency = """
        {"code":0,"data":[{"emergency":[{"citizenid":101,"firstname":"Casey","middlename":null,"lastname":"Example","suffix":null,
        "relationship":"PARENT","contactnumber":"00000000000"}]}]}
        """;
    internal const string Education = """
        {"code":0,"data":[{"other-info":[{"citizenid":101,"educationalattainment":"EXAMPLE ATTAINMENT","school":"Example school","course":"Example course",
        "department":"Example department","occupation":null,"company":null,"sourceofincome":" ","natureofwork":" ","annualincome":null,
        "idtype":"EXAMPLE ID","idnumber":"excluded-id"}]}]}
        """;
    private static readonly StaffSession Staff = new("staff@example.invalid", new("login", "refresh", "xsrf"), []);
    private static readonly PiiAccessToken Pii = new("synthetic-pii");
    private static readonly BackendOptions Options = new()
    { BaseUrl = "https://privacy.example.invalid", ApiRootPath = "/api/v1", ApiContract = ApiContract.Privacy };
    private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static PrivacyApiService Service(HttpClient client, Clock clock, BackendOptions? options = null) =>
        new(new ApiTransport(client, options ?? Options), options ?? Options, clock);
    private static string Payload(string route) => route.Split('/').Last() switch
    { "bio" => Bio, "address" => Address, "emergency" => Emergency, "education" => Education, _ => Status };

    internal static string Detail(bool locked = false)
    {
        var root = JsonNode.Parse(Bio)!;
        var row = root["data"]![0]!;
        row["personal-info"]![0]!["referencenumber"] = "REF-TEST";
        if (locked)
        {
            var person = row["personal-info"]![0]!.AsObject();
            var retained = new[] { "citizenid", "referencenumber", "firstname", "middlename", "lastname", "suffix", "displayname", "accounttype", "status", "citizenstatus" };
            foreach (var key in person.Select(p => p.Key).Where(key => !retained.Contains(key)).ToArray()) person.Remove(key);
        }
        foreach (var (body, field) in new[] { (Address, "address"), (Emergency, "emergency"), (Education, "other-info") })
            row[field] = locked ? new JsonArray() : JsonNode.Parse(body)!["data"]![0]![field]!.DeepClone();
        row["contact"] = JsonNode.Parse("""[{"citizenid":101,"type":"EMAIL_ADDRESS","value":"applicant@example.invalid"},{"citizenid":101,"type":"EWALLET","value":"00000000000"}]""");
        row["account-classification"] = JsonNode.Parse("""[{"citizenid":101,"type":"EXAMPLE CLASSIFICATION"}]""");
        row["last-module-updated"] = JsonNode.Parse("""{"citizenid":101,"module":"Example module","timestamp":"2030-01-01T01:02:03.123456"}""");
        row["piiVerificationRequired"] = locked;
        row["piiAuthorizedSections"] = new JsonObject
        {
            ["canViewBioDetails"] = !locked, ["canViewAddress"] = !locked,
            ["canViewEmergencyContact"] = !locked, ["canViewEducationalInformation"] = !locked
        };
        return root.ToJsonString();
    }

    public static void Register(Action<string, Func<Task>> add, Action<bool, string> check)
    {
        add("Privacy status binds exact claims applicant and absolute UTC expiry", async () =>
        {
            var calls = 0;
            using var client = new HttpClient(new Handler((request, _) =>
            {
                calls++;
                check(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/api/v1/privacy/verification/status/101", "Wrong status request");
                check(request.Headers.Authorization?.Parameter == "login" && request.Headers.GetValues("X-PII-Access-Token").Single() == "synthetic-pii", "Wrong status credentials");
                return Task.FromResult(Response(Status));
            }));
            var clock = new Clock();
            var reply = await Service(client, clock).GetVerificationStatusAsync(Staff, "101", Pii);
            check(reply.Outcome == PrivacyReadOutcome.Success && reply.Verification is not null && calls == 1, "Missing verified metadata");
            var verification = reply.Verification!;
            foreach (var section in Enum.GetValues<PrivacySection>()) check(verification.Allows(section, "101", clock.GetUtcNow()), "Known claim denied");
            check(!verification.Allows(PrivacySection.Bio, "0101", clock.GetUtcNow()) && !verification.Allows((PrivacySection)99, "101", clock.GetUtcNow()), "Alias or unknown section granted");
            check(!verification.ToString().Contains("101") && verification.ExpiresAt.Offset == TimeSpan.Zero, "Metadata diagnostic leak or non-UTC expiry");
            clock.Advance(TimeSpan.FromMinutes(5));
            check(!verification.Allows(PrivacySection.Bio, "101", clock.GetUtcNow()), "Expiry boundary granted");
        });
        add("Privacy status supports zoned fractional timestamps and rejects local dates", async () =>
        {
            foreach (var (expiry, expected) in new[] {
                ("2030-01-01T08:05:00+08:00", PrivacyReadOutcome.Success),
                ("2030-01-01T00:05:00.1234567Z", PrivacyReadOutcome.Success),
                ("2030-01-01T00:00:00Z", PrivacyReadOutcome.Expired),
                ("2029-12-31T23:59:59Z", PrivacyReadOutcome.Expired),
                ("2030-01-01T00:05:00", PrivacyReadOutcome.InvalidResponse),
                ("01/01/2030 00:05:00", PrivacyReadOutcome.InvalidResponse),
                ("tomorrow", PrivacyReadOutcome.InvalidResponse) })
            {
                using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(Status.Replace("2030-01-01T00:05:00Z", expiry)))));
                var reply = await Service(client, new()).GetVerificationStatusAsync(Staff, "101", Pii);
                check(reply.Outcome == expected && (expected == PrivacyReadOutcome.Success || reply.Verification is null), "Unsafe expiry accepted");
            }
        });
        add("Privacy status rejects malformed ambiguous or mismatched verification metadata", async () =>
        {
            foreach (var body in new[] {
                Status.Replace("\"verified\":true", "\"verified\":\"true\""),
                Status.Replace("\"verified\":true", "\"verified\":true,\"Verified\":true"),
                Status.Replace("\"applicationId\":\"101\"", "\"applicationId\":101"),
                Status.Replace("\"expiresAt\":", "\"missingExpiry\":"),
                Status.Replace("\"canViewAddress\"", "null"),
                Status.Replace("\"canViewAddress\"", "\" \""),
                "{\"code\":0}", "{\"code\":0,\"data\":[]}", "{\"code\":0,\"data\":[{},{}]}" })
            {
                using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(body))));
                var reply = await Service(client, new()).GetVerificationStatusAsync(Staff, "101", Pii);
                check(reply.Outcome == PrivacyReadOutcome.InvalidResponse && reply.Verification is null, "Malformed status granted");
            }
            using var other = new HttpClient(new Handler((_, _) => Task.FromResult(Response(Status.Replace("\"101\"", "\"102\"")))));
            var mismatch = await Service(other, new()).GetVerificationStatusAsync(Staff, "101", Pii);
            check(mismatch.Outcome == PrivacyReadOutcome.WrongApplicant && mismatch.Verification is null, "Wrong applicant granted");
            using var no = new HttpClient(new Handler((_, _) => Task.FromResult(Response("{\"code\":0,\"data\":[{\"verified\":false}]}"))));
            var denied = await Service(no, new()).GetVerificationStatusAsync(Staff, "101", Pii);
            check(denied.Outcome == PrivacyReadOutcome.NotVerified && denied.Verification is null, "Unverified status granted");
        });
        add("Empty unknown or differently cased privacy claims grant no sections", async () =>
        {
            foreach (var claims in new[] { "[]", "[\"*\",\"CANVIEWBIODETAILS\",\"canViewBioDetails \" ]" })
            {
                var body = JsonSerializer.Serialize(new { code = 0, data = new[] { new { verified = true, applicationId = "101", expiresAt = "2030-01-01T00:05:00Z",
                    authorizedSections = JsonSerializer.Deserialize<string[]>(claims) } } });
                using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(body))));
                var reply = await Service(client, new()).GetVerificationStatusAsync(Staff, "101", Pii);
                check(reply.Outcome == PrivacyReadOutcome.Success && reply.Verification is not null, "Valid metadata rejected");
                foreach (var section in Enum.GetValues<PrivacySection>()) check(!reply.Verification!.Allows(section, "101", new Clock().GetUtcNow()), "Unknown claim granted");
            }
            foreach (var (claim, allowed) in new[] { ("canViewBioDetails", PrivacySection.Bio), ("canViewAddress", PrivacySection.Address),
                ("canViewEmergencyContact", PrivacySection.Emergency), ("canViewEducationalInformation", PrivacySection.Education) })
            {
                var root = JsonNode.Parse(Status)!; root["data"]![0]!["authorizedSections"] = new JsonArray(JsonValue.Create(claim));
                using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(root.ToJsonString()))));
                var metadata = (await Service(client, new()).GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
                foreach (var section in Enum.GetValues<PrivacySection>())
                    check(metadata.Allows(section, "101", new Clock().GetUtcNow()) == (section == allowed), "Claim granted a different section");
            }
        });
        add("Privacy status checks expiry after the server request completes", async () =>
        {
            var clock = new Clock();
            using var client = new HttpClient(new Handler((_, _) => { clock.Advance(TimeSpan.FromMinutes(5)); return Task.FromResult(Response(Status)); }));
            var reply = await Service(client, clock).GetVerificationStatusAsync(Staff, "101", Pii);
            check(reply.Outcome == PrivacyReadOutcome.Expired && reply.Verification is null, "Used stale request-start time");
        });
        add("Protected sections map observed nested arrays and preserve nullable text and order", async () =>
        {
            var calls = 0;
            using var client = new HttpClient(new Handler((request, _) => { calls++; return Task.FromResult(Response(Payload(request.RequestUri!.AbsolutePath))); }));
            var service = Service(client, new());
            var verified = (await service.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
            var bio = await service.GetBioSnapshotAsync(Staff, "101", Pii, verified);
            check(bio.Outcome == PrivacyReadOutcome.Success && bio.Snapshot is { CitizenId: "101", FirstName: "Alex", MiddleName: null,
                DateOfBirth: "03/04/2001", BirthCity: " ", Status: "ACTIVE", CitizenStatus: "FOR_INITIAL_KYC" }, "Bio mapping guessed or lost values");
            var address = await service.GetAddressSnapshotAsync(Staff, "101", Pii, verified);
            check(address.Outcome == PrivacyReadOutcome.Success && address.Snapshot is { Count: 2 } &&
                address.Snapshot[0] is { AddressType: "PERMANENT", AddressLine1: null, BuildingName: "", ZipCode: "0000" } &&
                address.Snapshot[1].AddressType == "PRESENT", "Address rows collapsed or reordered");
            var emergency = await service.GetEmergencySnapshotAsync(Staff, "101", Pii, verified);
            check(emergency.Outcome == PrivacyReadOutcome.Success && emergency.Snapshot is { Count: 1 } &&
                emergency.Snapshot[0] is { FirstName: "Casey", Suffix: null, ContactNumber: "00000000000" }, "Emergency text/nullable mapping wrong");
            var education = await service.GetEducationSnapshotAsync(Staff, "101", Pii, verified);
            check(education.Outcome == PrivacyReadOutcome.Success && education.Snapshot is { Count: 1 } &&
                education.Snapshot[0] is { School: "Example school", Course: "Example course", Occupation: null, SourceOfIncome: " " }, "Education mapping wrong");
            check(calls == 5, "Typed helpers made extra requests");
            foreach (var value in new[] { bio.Snapshot!.ToString(), address.Snapshot![0].ToString(), emergency.Snapshot![0].ToString(), education.Snapshot![0].ToString() })
                check(value.Contains("redacted") && !value.Contains("Example"), "Protected diagnostic leak");
            check(typeof(ApplicantBio).GetProperties().All(p => !p.Name.Contains("AccountNumber") && !p.Name.Contains("Maiden")) &&
                typeof(ApplicantEducation).GetProperties().All(p => !p.Name.Contains("IdNumber")), "Retained unnecessary identity/security data");
        });
        add("Protected collection arrays may be empty without fabricating a record", async () =>
        {
            using var client = new HttpClient(new Handler((request, _) => Task.FromResult(Response(request.RequestUri!.AbsolutePath.EndsWith("/101") ? Status :
                "{\"code\":0,\"data\":[{\"address\":[],\"emergency\":[],\"other-info\":[]}]}"))));
            var service = Service(client, new()); var verified = (await service.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
            check((await service.GetAddressSnapshotAsync(Staff, "101", Pii, verified)).Snapshot is { Count: 0 } &&
                (await service.GetEmergencySnapshotAsync(Staff, "101", Pii, verified)).Snapshot is { Count: 0 } &&
                (await service.GetEducationSnapshotAsync(Staff, "101", Pii, verified)).Snapshot is { Count: 0 }, "Invented missing rows");
        });
        add("Protected reads reject mixed applicants invalid field types and ambiguous rows", async () =>
        {
            foreach (var body in new[] {
                Bio.Replace("\"citizenid\":101", "\"citizenid\":102"), Bio.Replace("\"citizenid\":101", "\"citizenid\":\"other\""),
                Bio.Replace("\"citizenid\":101", "\"citizenid\":101.5"), Bio.Replace("\"firstname\":\"Alex\"", "\"firstname\":false"),
                Bio.Replace("\"firstname\":\"Alex\"", "\"firstname\":\"Alex\",\"FirstName\":\"Other\""),
                "{\"code\":0,\"data\":[{\"personal-info\":[]}]}", "{\"code\":0,\"data\":[{\"personal-info\":[{},{}]}]}",
                "{\"code\":0,\"data\":[{\"personal-info\":{}}]}", "{\"code\":0,\"data\":[{},{}]}" })
            {
                using var client = new HttpClient(new Handler((request, _) => Task.FromResult(Response(request.RequestUri!.AbsolutePath.EndsWith("/bio") ? body : Status))));
                var service = Service(client, new()); var verified = (await service.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
                var reply = await service.GetBioSnapshotAsync(Staff, "101", Pii, verified);
                check(reply.Outcome == PrivacyReadOutcome.InvalidResponse && reply.Snapshot is null, "Invalid protected bio exposed");
            }
            using var mixed = new HttpClient(new Handler((request, _) => Task.FromResult(Response(request.RequestUri!.AbsolutePath.EndsWith("/address") ?
                Address.Replace("\"specificaddress\":\"Another block\"", "\"specificaddress\":false") : Status))));
            var mixedService = Service(mixed, new()); var metadata = (await mixedService.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
            var invalidRows = await mixedService.GetAddressSnapshotAsync(Staff, "101", Pii, metadata);
            check(invalidRows.Outcome == PrivacyReadOutcome.InvalidResponse && invalidRows.Snapshot is null, "Exposed partial address rows");
        });
        add("Protected reads deny missing claim wrong applicant expired metadata and incomplete staff locally", async () =>
        {
            var calls = 0; var clock = new Clock();
            using var client = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(Response(Status.Replace("canViewBioDetails", "unknownClaim"))); }));
            var service = Service(client, clock); var verified = (await service.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
            check((await service.GetBioSnapshotAsync(Staff, "101", Pii, verified)).Outcome == PrivacyReadOutcome.NotPermitted, "Missing claim dispatched");
            check((await service.GetAddressSnapshotAsync(Staff, "102", Pii, verified)).Outcome == PrivacyReadOutcome.NotPermitted, "Wrong applicant dispatched");
            var incomplete = new StaffSession("staff@example.invalid", new("", "r", "x"), []);
            check((await service.GetAddressSnapshotAsync(incomplete, "101", Pii, verified)).Outcome == PrivacyReadOutcome.NotPermitted, "Incomplete staff dispatched");
            clock.Advance(TimeSpan.FromMinutes(5));
            check((await service.GetAddressSnapshotAsync(Staff, "101", Pii, verified)).Outcome == PrivacyReadOutcome.NotPermitted && calls == 1, "Expired metadata dispatched");
        });
        add("Protected response finishing after expiry or cancellation exposes no snapshot", async () =>
        {
            foreach (var cancel in new[] { false, true })
            {
                var clock = new Clock(); using var stop = new CancellationTokenSource();
                using var client = new HttpClient(new Handler((request, _) =>
                {
                    if (request.RequestUri!.AbsolutePath.EndsWith("/bio")) { if (cancel) stop.Cancel(); else clock.Advance(TimeSpan.FromMinutes(5)); return Task.FromResult(Response(Bio)); }
                    return Task.FromResult(Response(Status));
                }));
                var service = Service(client, clock); var verified = (await service.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
                var reply = await service.GetBioSnapshotAsync(Staff, "101", Pii, verified, stop.Token);
                check(reply.Outcome == (cancel ? PrivacyReadOutcome.Cancelled : PrivacyReadOutcome.Expired) && reply.Snapshot is null, "Stale protected response exposed");
            }
        });
        add("Typed privacy status preserves HTTP business transport and cancellation outcomes", async () =>
        {
            foreach (var (http, body, expected) in new[] {
                (HttpStatusCode.Unauthorized, Status, PrivacyReadOutcome.NotPermitted),
                (HttpStatusCode.Forbidden, Status, PrivacyReadOutcome.NotPermitted),
                (HttpStatusCode.TooManyRequests, Status, PrivacyReadOutcome.Rejected),
                (HttpStatusCode.InternalServerError, Status, PrivacyReadOutcome.ConnectionFailure),
                (HttpStatusCode.OK, "{\"code\":9}", PrivacyReadOutcome.Rejected),
                (HttpStatusCode.OK, "not JSON", PrivacyReadOutcome.InvalidResponse) })
            {
                using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(body, http))));
                var reply = await Service(client, new()).GetVerificationStatusAsync(Staff, "101", Pii);
                check(reply.Outcome == expected && reply.Verification is null, "Typed status swallowed error");
            }
            using var failing = new HttpClient(new Handler((_, _) => throw new HttpRequestException("synthetic")));
            check((await Service(failing, new()).GetVerificationStatusAsync(Staff, "101", Pii)).Outcome == PrivacyReadOutcome.ConnectionFailure, "Transport failure lost");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            check((await Service(failing, new()).GetVerificationStatusAsync(Staff, "101", Pii, cancelled.Token)).Outcome == PrivacyReadOutcome.Cancelled, "Cancellation lost");
            check((await Service(failing, new(), Options with { ApiContract = ApiContract.Refactored }).GetVerificationStatusAsync(Staff, "101", Pii)).Outcome == PrivacyReadOutcome.Unavailable, "Unsupported mode accepted");
        });
        add("Protected typed errors never retain data from a rejected server response", async () =>
        {
            foreach (var (http, expected) in new[] { (HttpStatusCode.Unauthorized, PrivacyReadOutcome.NotPermitted),
                (HttpStatusCode.InternalServerError, PrivacyReadOutcome.ConnectionFailure), (HttpStatusCode.TooManyRequests, PrivacyReadOutcome.Rejected) })
            {
                using var client = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/bio") ? Response(Bio, http) : Response(Status))));
                var service = Service(client, new()); var verified = (await service.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
                var reply = await service.GetBioSnapshotAsync(Staff, "101", Pii, verified);
                check(reply.Outcome == expected && reply.Snapshot is null, "Rejected protected data retained");
            }
        });
        add("Redacted detail retains observed base fields while protected sections remain withheld", async () =>
        {
            var staff = new StaffSession("staff@example.invalid", Staff.Tokens, ["VINFORMATIONGATHERING", "VCARDHOLDERUPDATE"]);
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(Detail(true)))));
            var service = Service(client, new());
            foreach (var reply in new[] { await service.GetInformationGatheringSnapshotAsync(staff, "101"), await service.GetCardholderUpdateSnapshotAsync(staff, "101") })
            {
                check(reply.Outcome == PrivacyReadOutcome.Success && reply.Snapshot is { ServerRequiresPrivacyVerification: true, Bio: null,
                    Addresses: null, EmergencyContacts: null, Education: null, EffectiveSections.Count: 0 }, "Redacted arrays treated as authorized empty data");
                var snapshot = reply.Snapshot!;
                check(snapshot.Identity is { CitizenId: "101", ReferenceNumber: "REF-TEST", FirstName: "Alex", Status: "ACTIVE", CitizenStatus: "FOR_INITIAL_KYC" } &&
                    snapshot.Contacts.Count == 2 && snapshot.Contacts[1].Type == "EWALLET" && snapshot.Classifications.Single() == "EXAMPLE CLASSIFICATION" &&
                    snapshot.LastModuleUpdated.Timestamp == "2030-01-01T01:02:03.123456", "Base mapping guessed or lost fields");
                check(!snapshot.ToString().Contains("Alex") && !snapshot.Identity.ToString().Contains("Alex") &&
                    !snapshot.Contacts[0].ToString().Contains("applicant@"), "Detail diagnostic leak");
            }
        });
        add("Authorized detail intersects server flags with status claims and expiry", async () =>
        {
            using var client = new HttpClient(new Handler((request, _) => Task.FromResult(Response(request.RequestUri!.AbsolutePath.Contains("/status/") ? Status : Detail()))));
            var service = Service(client, new()); var verified = (await service.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
            var staff = new StaffSession("staff@example.invalid", Staff.Tokens, ["VINFORMATIONGATHERING", "VCARDHOLDERUPDATE"]);
            var authorized = await service.GetInformationGatheringSnapshotAsync(staff, "101", Pii, verified);
            check(authorized.Outcome == PrivacyReadOutcome.Success && authorized.Snapshot is { ServerRequiresPrivacyVerification: false,
                Bio.FirstName: "Alex", Addresses.Count: 2, EmergencyContacts.Count: 1, Education.Count: 1, EffectiveSections.Count: 4 }, "Verified detail mapping failed");
            // A token or server flag alone never grants protected snapshot data.
            var unproven = await service.GetCardholderUpdateSnapshotAsync(staff, "101", Pii);
            check(unproven.Outcome == PrivacyReadOutcome.Success && unproven.Snapshot is { Bio: null, Addresses: null, Education: null, EmergencyContacts: null,
                EffectiveSections.Count: 0 }, "Server flags or credential bypassed status metadata");
            using var subset = new HttpClient(new Handler((request, _) => Task.FromResult(Response(request.RequestUri!.AbsolutePath.Contains("/status/") ?
                Status.Replace("canViewBioDetails", "unknownClaim") : Detail().Replace("\"canViewAddress\":true", "\"canViewAddress\":false")))));
            var subsetService = Service(subset, new()); var subsetStatus = (await subsetService.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
            var partial = await subsetService.GetCardholderUpdateSnapshotAsync(staff, "101", Pii, subsetStatus);
            check(partial.Outcome == PrivacyReadOutcome.Success && partial.Snapshot is { Bio: null, Addresses: null, EmergencyContacts.Count: 1,
                Education.Count: 1, EffectiveSections.Count: 2 }, "Did not intersect both permission sources");
        });
        add("Detail rejects ambiguous flags malformed base rows and mixed applicant contacts", async () =>
        {
            foreach (var body in new[] { Detail(true).Replace("\"piiVerificationRequired\":true", "\"piiVerificationRequired\":\"true\""),
                Detail(true).Replace("\"canViewAddress\":false", "\"canViewAddress\":true"),
                Detail(true).Replace("\"canViewAddress\":false", "\"canViewAddress\":false,\"CanViewAddress\":true"),
                Detail(true).Replace("\"piiAuthorizedSections\":", "\"missingFlags\":"),
                Detail(true).Replace("\"type\":\"EMAIL_ADDRESS\"", "\"citizenid\":102,\"type\":\"EMAIL_ADDRESS\""),
                Detail(true).Replace("\"type\":\"EMAIL_ADDRESS\"", "\"type\":false") })
            {
                using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(body))));
                var staff = new StaffSession("staff@example.invalid", Staff.Tokens, ["VCARDHOLDERUPDATE"]);
                var reply = await Service(client, new()).GetCardholderUpdateSnapshotAsync(staff, "101");
                check(reply.Outcome == PrivacyReadOutcome.InvalidResponse && reply.Snapshot is null, "Invalid detail exposed");
            }
        });
        add("Detail checks module authority and metadata applicant lifetime before and after dispatch", async () =>
        {
            var calls = 0; var clock = new Clock();
            using var client = new HttpClient(new Handler((request, _) =>
            {
                calls++; if (request.RequestUri!.AbsolutePath.Contains("/status/")) return Task.FromResult(Response(Status));
                clock.Advance(TimeSpan.FromMinutes(5)); return Task.FromResult(Response(Detail()));
            }));
            var service = Service(client, clock); var verified = (await service.GetVerificationStatusAsync(Staff, "101", Pii)).Verification!;
            var staff = new StaffSession("staff@example.invalid", Staff.Tokens, ["VINFORMATIONGATHERING", "VCARDHOLDERUPDATE"]);
            check((await service.GetInformationGatheringSnapshotAsync(Staff, "101")).Outcome == PrivacyReadOutcome.NotPermitted &&
                (await service.GetCardholderUpdateSnapshotAsync(Staff, "101")).Outcome == PrivacyReadOutcome.NotPermitted, "Module bypass");
            check((await service.GetInformationGatheringSnapshotAsync(staff, "102", Pii, verified)).Outcome == PrivacyReadOutcome.NotPermitted &&
                (await service.GetInformationGatheringSnapshotAsync(staff, "101", null, verified)).Outcome == PrivacyReadOutcome.NotPermitted && calls == 1, "Mismatched credential dispatched");
            var late = await service.GetCardholderUpdateSnapshotAsync(staff, "101", Pii, verified);
            check(late.Outcome == PrivacyReadOutcome.Expired && late.Snapshot is null && calls == 2, "Late detail exposed");
        });
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan elapsed) => now += elapsed;
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken); }
}
