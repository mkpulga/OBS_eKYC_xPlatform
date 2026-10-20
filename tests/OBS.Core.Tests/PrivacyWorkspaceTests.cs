using System.Net;
using OBS.Core;
using Rig = ApplicantPrivacyControllerTests.Rig;

static class PrivacyWorkspaceTests
{
    public static void Register(Action<string, Func<Task>> add, Action<bool, string> check)
    {
        add("Parallel workspace preserves configured API root headers and original contract", async () =>
        {
            foreach (var url in new[] { "https://example.invalid", "https://example.invalid/obs", "https://example.invalid/obs/api/v1", "https://example.invalid/custom/obs/api/v1/" })
            {
                using var rig = await Rig.Create();
                var original = new BackendOptions { BaseUrl = url, ApiContract = ApiContract.Refactored, TokenHeaderKey = "X-Obs-Token", UserAgent = "configured-header" };
                var calls = new List<string>();
                var handler = new Handler(request =>
                {
                    calls.Add(request.RequestUri!.AbsoluteUri);
                    check(request.Headers.Authorization?.Parameter == "staff-access" && request.Headers.GetValues("X-Obs-Token").Single() == "configured-header", "Changed staff/configured headers");
                    check(!request.Headers.Contains("X-PII-Access-Token"), "Sent PII credential on code request");
                    return Rig.Response("{\"code\":0}");
                });
                using var workspace = new PrivacyWorkspace(new HttpClient(handler), original, rig.Clock);
                using var screen = workspace.CreateScreen(rig.Session);
                screen.SetCitizenId("101"); screen.SelectApplicant(); await screen.ShowOrHideAsync(PrivacySection.Bio);
                check(calls.Count == 0, "Construction/selection/reveal automatically sent OTP or lookup");
                await screen.RequestCodeAsync();
                check(calls.Single() == original.GetApiRoot().AbsoluteUri + "privacy/otp/request", "Changed configured API root");
                check(original.ApiContract == ApiContract.Refactored && original.BaseUrl == url, "Mutated existing configuration");
                workspace.Dispose(); check(handler.Disposed && !workspace.Available && !screen.Selected && screen.CitizenId == "", "Workspace did not clear controllers/screen/client");
            }
            var insecure = new BackendOptions { BaseUrl = "http://example.invalid" };
            try { using var bad = new PrivacyWorkspace(insecure); throw new InvalidOperationException("Accepted HTTP"); } catch (ArgumentException) { }
            using var unavailable = new PrivacyWorkspace(); check(!unavailable.Available, "Invalid configuration exposed workspace");
        });
        add("Parallel screen requires explicit selection code request and verified status before selected section", async () =>
        {
            using var rig = await Rig.Create(); using var screen = new PrivacyScreenState(rig.Privacy, rig.Session);
            screen.SetCitizenId("101"); await screen.ShowOrHideAsync(PrivacySection.Bio);
            check(!screen.Selected && !screen.DialogOpen && rig.Calls.Count == 0, "Skipped selection");
            screen.SelectApplicant(); await screen.ShowOrHideAsync(PrivacySection.Bio);
            check(screen.Selected && screen.DialogOpen && !screen.CanVerify && rig.Calls.Count == 0, "Reveal silently requested code");
            await screen.RequestCodeAsync(); screen.SetOtp("123456"); await screen.VerifyCodeAsync();
            check(!screen.DialogOpen && screen.Otp == "" && screen.IsVisible(PrivacySection.Bio) && screen.Fields(PrivacySection.Bio).Count > 0, "Verified section did not open or retained OTP");
            check(rig.Calls.Select(c => c.Path).SequenceEqual(new[] { "/api/v1/privacy/otp/request", "/api/v1/privacy/otp/verify", "/api/v1/privacy/verification/status/101", "/api/v1/privacy/verification/status/101", "/api/v1/privacy/applicants/101/bio" }), "Used pending/reference route or skipped status");
        });
        add("Parallel section presentation shows ordered addresses raw dates and independent hides", async () =>
        {
            using var rig = await Rig.Create(); using var screen = new PrivacyScreenState(rig.Privacy, rig.Session);
            await OpenBio(screen);
            foreach (var section in new[] { PrivacySection.Address, PrivacySection.Emergency, PrivacySection.Education }) await screen.ShowOrHideAsync(section);
            var addresses = screen.Fields(PrivacySection.Address);
            check(addresses.Count == 20 && addresses[0].Label.Contains("PERMANENT") && addresses[10].Label.Contains("PRESENT"), "Lost address order/type");
            var bio = screen.Fields(PrivacySection.Bio);
            check(bio.Single(f => f.Label == "Date of birth").Value == "03/04/2001" && bio.Any(f => f.Label == "Account status") && bio.Any(f => f.Label == "Application stage"), "Guessed date or merged statuses");
            check(screen.Fields(PrivacySection.Emergency).Count == 3 && screen.Fields(PrivacySection.Education).Count == 8, "Lost protected collections");
            await screen.ShowOrHideAsync(PrivacySection.Bio);
            check(screen.Fields(PrivacySection.Bio).Count == 0 && screen.IsVisible(PrivacySection.Address), "Hide retained fields or hid other section");
        });
        add("Cancel and applicant edits erase pending OTP and all prior protected values", async () =>
        {
            using var rig = await Rig.Create(); using var screen = new PrivacyScreenState(rig.Privacy, rig.Session);
            screen.SetCitizenId("101"); screen.SelectApplicant(); await screen.ShowOrHideAsync(PrivacySection.Bio);
            await screen.RequestCodeAsync(); screen.SetOtp("123456"); screen.CancelDialog();
            check(screen.Otp == "" && !screen.DialogOpen && screen.Stage == PrivacyStage.Locked, "Cancelled OTP retained");
            await OpenBio(screen); screen.SetCitizenId("102");
            check(!screen.Selected && !rig.Privacy.HasApplicant && screen.Fields(PrivacySection.Bio).Count == 0 && screen.Otp == "", "Edited applicant retained old access");
            screen.SetCitizenId(" "); screen.SelectApplicant(); check(!screen.Selected && screen.Message == "Enter a valid citizen ID.", "Empty identifier accepted");
        });
        add("Screen expiry and pause logout password change clear rendered state", async () =>
        {
            foreach (var action in new[] { "expiry", "pause", "logout", "password", "replacement" })
            {
                using var rig = await Rig.Create(); using var screen = new PrivacyScreenState(rig.Privacy, rig.Session); await OpenBio(screen);
                switch (action)
                {
                    case "expiry": rig.Clock.Advance(TimeSpan.FromMinutes(5)); break;
                    case "pause": rig.Session.Pause(); break;
                    case "logout": await rig.Session.LogoutAsync(); break;
                    case "password": rig.Session.BeginPasswordChange(); break;
                    default: await rig.Session.SignInAsync("staff@example.invalid", "synthetic"); break;
                }
                check(screen.Fields(PrivacySection.Bio).Count == 0 && !screen.IsVisible(PrivacySection.Bio) && screen.Otp == "" && !screen.DialogOpen, "Transition retained screen values");
                if (action != "expiry") check(screen.CitizenId == "" && !screen.Selected, "Session transition retained applicant");
            }
        });
        add("Late verification cannot open a dialog or section for edited applicant", async () =>
        {
            using var rig = await Rig.Create(); using var screen = new PrivacyScreenState(rig.Privacy, rig.Session);
            screen.SetCitizenId("101"); screen.SelectApplicant(); await screen.ShowOrHideAsync(PrivacySection.Bio); await screen.RequestCodeAsync(); screen.SetOtp("123456");
            var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Override = (request, _) => request.RequestUri!.AbsolutePath.EndsWith("/verify") ? delayed.Task : null;
            var old = screen.VerifyCodeAsync(); screen.SetCitizenId("102"); screen.SelectApplicant(); await screen.ShowOrHideAsync(PrivacySection.Address);
            delayed.SetResult(Rig.VerifiedResponse()); await old;
            check(screen.Selected && screen.DialogOpen && screen.Stage == PrivacyStage.Locked && screen.Fields(PrivacySection.Bio).Count == 0 && screen.Otp == "", "Late verification restored old screen");
        });
        add("Existing staff rotation rechecks privacy and failed staff validation clears screen", async () =>
        {
            using var rig = await Rig.Create(); using var screen = new PrivacyScreenState(rig.Privacy, rig.Session); await OpenBio(screen);
            rig.Gateway.Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.Success, new("rotated-access", "rotated-refresh", "rotated-xsrf")));
            await rig.Session.CheckSessionAsync();
            check(rig.Calls.Last().Path.Contains("/status/") && rig.Calls.Last().Bearer == "rotated-access" && screen.IsVisible(PrivacySection.Bio), "Screen did not recheck with rotated staff session");
            rig.Gateway.Refresh = _ => Task.FromResult(new RefreshReply(RefreshOutcome.ConnectionFailure, Diagnostic: new(null, null, ApiFailure.Transport, RefreshReason.TransportFailure)));
            await rig.Session.CheckSessionAsync();
            check(screen.Fields(PrivacySection.Bio).Count == 0 && screen.Stage == PrivacyStage.Locked && rig.Session.ProtectedWorkAllowed, "Failed check retained privacy or changed staff rules");
        });
        add("Revoked section and failed status cannot be rendered by parallel screen", async () =>
        {
            using var rig = await Rig.Create(); using var screen = new PrivacyScreenState(rig.Privacy, rig.Session); await OpenBio(screen);
            rig.Override = (request, _) => request.RequestUri!.AbsolutePath.Contains("/status/") ? Task.FromResult(Rig.Response(PrivacyResponseTests.Status.Replace("canViewAddress", "unknownClaim"))) : null;
            await rig.Privacy.RevalidateAsync(); var count = rig.Calls.Count; await screen.ShowOrHideAsync(PrivacySection.Address);
            check(!screen.DialogOpen && rig.Calls.Count == count && screen.Fields(PrivacySection.Address).Count == 0 && screen.Message.Contains("does not allow"), "Revoked section bypassed claims or re-sent OTP");
            rig.Override = (_, _) => Task.FromResult(Rig.Response("{\"code\":99}", HttpStatusCode.Forbidden)); await rig.Privacy.RevalidateAsync();
            check(screen.Fields(PrivacySection.Bio).Count == 0 && screen.Stage == PrivacyStage.Locked, "Failed revalidation retained renderable values");
        });
        add("Disposing a screen clears its values and a reopened screen starts locked", async () =>
        {
            using var rig = await Rig.Create(); var screen = new PrivacyScreenState(rig.Privacy, rig.Session); await OpenBio(screen);
            screen.Dispose(); check(!screen.Selected && screen.CitizenId == "" && screen.Otp == "" && screen.Fields(PrivacySection.Bio).Count == 0, "Disposed screen retained values");
            using var workspace = new PrivacyWorkspace(new HttpClient(new Handler(_ => throw new InvalidOperationException("Unexpected request"))), new BackendOptions { BaseUrl = "https://example.invalid" }, rig.Clock);
            using var reopened = workspace.CreateScreen(rig.Session); check(!reopened.Selected && reopened.Stage == PrivacyStage.Locked && !reopened.DialogOpen, "Reopened screen inherited authorization");
        });
    }
    private static async Task OpenBio(PrivacyScreenState screen)
    {
        screen.SetCitizenId("101"); screen.SelectApplicant(); await screen.ShowOrHideAsync(PrivacySection.Bio); await screen.RequestCodeAsync(); screen.SetOtp("123456"); await screen.VerifyCodeAsync();
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
