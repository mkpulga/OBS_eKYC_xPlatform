# Citizen ID dashboard search

Implement the requested IG CID and CU CID options after the four existing options, retaining Application Number as the default. Exact module permissions control visibility, requests and result display. The existing application-number and reference searches keep their service and permission rules.

Both clients share a CitizenApplicationSearch controller and reuse the typed PrivacyApiService IG/CU readers over the configured authenticated transport. Opaque IDs are trimmed at submission and escaped as one path segment. The controller validates a response before opening the existing View Application layout, retaining source, Citizen ID and the independently returned reference number. Refresh uses the retained source and ID, never the pending-application service. Failed searches stay on the dashboard with the input and selection intact.

The view binds ordinary fields directly from the validated snapshot. Absent ordinary values, photos and documents show “Not available.” Protected sections stay masked until the existing ApplicantPrivacyController/PrivacyScreenState OTP, status, section and expiry requirements are met. No previous applicant data or unrelated record lookup fills gaps. Existing action permissions and disabled actions are retained.

Cancellation and generation/session identity checks discard searches after input/selection changes, navigation, permission loss or session changes. View disposal and refresh clear protected state. Focused shared regression tests cover routing, encoding, validation, direct binding, errors, missing data, privacy and stale responses; web rendering and Windows/web builds validate host integration. Live backend and installed-device acceptance require an authorized runtime session.

## Validation

- Shared regression executable: 137/137 synthetic tests pass. New cases cover IG/CU routing, whitespace, long/non-numeric/Unicode/path-special IDs, exact module permissions without pending access, direct reference mapping, missing data, endpoint errors, duplicates/cancellation/session replacement, OTP hiding/expiry, source refresh and staff rotation during refresh.
- Actual Blazor components: 19/19 checks pass, including permission-based dropdown visibility/defaults, blank/denied searches staying on the dashboard, direct result binding without pending permission or a second lookup, OTP dialogs/reveal/hide/expiry, source-aware refresh and refresh failures, loading/stale responses and existing application/reference/QR/cardholder behavior.
- The full solution (core, Blazor, Windows MAUI and both test projects) builds successfully using isolated output; only the pre-existing People.xaml XC0025 binding warning remains.
- No live backend requests, installed Windows interaction, or Android runtime acceptance were performed. Builds leave the user's running app open.

Repeat with `dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj`, `dotnet run --project tests/OBS.Web.Tests/OBS.Web.Tests.csproj` and `dotnet build OBS_eKYC.sln --artifacts-path artifacts/citizen-search-check`.
