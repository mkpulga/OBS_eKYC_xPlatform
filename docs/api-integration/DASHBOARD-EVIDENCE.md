# Dashboard response mapping — 2026-10-20 follow-up

The user supplied the JSON response after being guided through the preproduction collection's Get Dashboard request. It has business code 0 and one data object. The dashboard HTTP status was not supplied. This is user-provided response evidence, not an authenticated request executed by the assistant. No real deployment statistics are copied into fixtures or this report.

## Observed fields and existing presentation mapping

| JSON field | Observed type | Existing destination |
|---|---|---|
| accountsOnboarded | Integer | Accounts Onboarded portfolio metric |
| cardsRequested | Integer | Cards Requested portfolio metric |
| cardsReleased | Integer | Cards Released portfolio metric |
| releaseProgressPercent | Integer | Release progress percentage; retain the server value |
| cardHolderVerification | Integer | Card Holder Verification queue metric |
| scheduledHomevisits | Integer | Scheduled Homevisits queue metric |
| forCardRequests | Integer | For Card Requests queue metric |
| activityOverview | Array of objects | Existing DashboardActivity list |
| activityOverview[].category | String | Activity label |
| activityOverview[].completed | Integer | Completed count |
| activityOverview[].inProgress | Integer | In-progress count |

`DashboardSnapshot` in `src/OBS.Core/DashboardApi.cs` maps into the existing DashboardMetric/DashboardActivity types. Portfolio/queue labels, icons, accents and order match the existing view. The server's activity order/labels and percentage are retained; no business calculation or date-range query was added.

`PrivacyApiService.GetDashboardSnapshotAsync` reuses the existing authorized GetDashboardAsync request, then parses the result. There is one HTTP call, no new HTTP client, no PII token, and the existing VIEWDASHBOARD guard remains.

## Conservative client validation

The client requires HTTP/business success, exactly one data object, all seven displayed numeric fields, and an activity array. Displayed counts must be nonnegative Int32 values; the rendered percentage must be 0–100. These are client display-safety policies, not a claim that the single observed payload is a complete formal backend schema. Missing/null/malformed/overflow fields fail closed without defaulting to zero or sample data. Explicit zero and an empty activity array are valid. Conflicting case variants fail closed; unknown extra fields are ignored. HTTP denial, business rejection, connection failure, invalid response and cancellation remain distinct.

Six new top-level tests use synthetic values. They exercise mapping, authoritative percentage retention, missing/null/invalid/overflow fields, malformed envelopes/activity, zero/empty handling, case ambiguity, one-call authorization and failure classification. Real preproduction metric values are not retained.

## Still pending

- This payload has no time-range metadata or request parameters. Existing 1D/1W/1MO/1YR behavior cannot be inferred from it. No period filtering is invented.
- Screen binding, loading/error presentation, cancellation/stale-state handling and chart scaling remain pending with the existing client composition. XAML/Razor/CSS and sample displays are unchanged by this mapping slice.
- The Privacy startup guard remains until the approved OTP workflow and all sensitive-data lifecycle rules are complete.
- The user confirmed an approved synthetic applicant is available for the next privacy OTP/verification/section checks. Keep identifiers and credentials local; share only sanitized response structures.
