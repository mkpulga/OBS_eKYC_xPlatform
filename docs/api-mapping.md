# API evidence and differences

This document describes the earlier 99-request migration evidence. The user selected the newer 16-request Privacy PII OTP & Dashboard collection on 2026-10-20; see the [current integration handover](api-integration/README.md) for its scope, mappings, evidence, and blockers. Earlier source-backed integrations below are retained.

Evidence was inspected from the owner-provided source directory, the current Postman collection, and the revised requirements. The collection contains **99 requests and zero saved response examples**. [API inventory](evidence/api-inventory.json), [source hashes](evidence/source-hashes.json), and [RDLC inventory](evidence/report-inventory.json) distinguish examples from verified behavior. Inventory generation omits credentials, body values, and concrete record identifiers.

The original `app.ini` already ends its base URL in `/obs/api/v1`; the new configuration normalizes that root without appending it twice. Its deployment selects Refactored/StandAlone. Its header key setting is `token=X-Obs-Token`; `utils/Dashboard.cs` loads this into `tokenHeaderKey`. `AccessAPI_v1.ApiPostRaw` adds that configured name with the `userAgent` setting as its **value**, not as a separate literal HTTP User-Agent header. Sanitized examples do not copy the real value.

| Operation | Source-backed request | Collection difference / remaining verification |
|---|---|---|
| Request OTP | POST `session/authenticate`, `username,password` | Collection agrees on fields; live outcome mapping untested |
| Verify OTP | Same endpoint, `username,password,otp` | Collection omits password; implementation preserves source password |
| Resend | POST `session/resend/otp`, Refactored `username,password,guiinterface="obs"` | Absent from collection; Legacy adds blank `module,otp`, request factory retained but Legacy sign-in blocked |
| Refresh | POST `session/refresh`, `{}`, refresh bearer, `X-XSRF-TOKEN` | Absent from collection; source Dashboard calls refresh, not monitor |
| Monitor | POST `session/monitor` | Collection historical authentication-like body; not used for refresh |
| Terminate | POST `session/terminate`, `userName`, access bearer, XSRF | Collection uses `username` and differing header examples; casing follows source |
| Forgot password | POST `user/forgot/password`, `email,guiinterface="OBS"` | Collection's request example differs in interface casing; source AccountManagementController preserved |
| Change password | POST `user/change/password`, `username,currentpassword,newpassword`, access bearer | Source checks `CPW`; no invented recovery endpoint |

Workflow requests migrated so far:

| Operation | Source-backed request | Collection difference / remaining verification |
|---|---|---|
| Pending cardholder verification queue | GET `cardholder/pending`, access bearer, configured token header; derived from `getAllCardHolderVerification_v1` | Collection lists `Authorization`, `Content-Type`, `x-obs-token` with no response example; row fields come from the `cardholderVerificationCollectionReport.rdlc` `CollectionModel` inventory, not a live payload |

`ApiTransport` accepts GET only for explicit route shapes: `cardholder/pending`, `cardholder/pending/{number}` and `documents/pending/{number}/ALL`. Identifiers accept at most 100 ASCII letters, digits, hyphens or underscores; path traversal, queries and additional segments are rejected. POST keeps its authentication-slice allowlist. Both share the same header, envelope, and failure handling.

View Application uses the source `getCardHolderVerificationSpecific_v1` and `getPendingDocument_v1` routes with access bearer and the configured token header. Its field mapping comes from `cardholderVerificationSpecificDeserializeObject` and `documents`, including nested personal information, present/permanent addresses, emergency contact, education, identification, specimen remarks and document `filename/doctype/docvalue`. Scalar numeric values are retained as text. Security questions/answers are not retained. A successful detail envelope must contain exactly one record whose reference matches the requested number. An empty data array or HTTP 404 is a missing record. Document business code 11 is the source's no-documents outcome; other document failures preserve the loaded applicant with a separate error. Live response and backend permission enforcement still require deployment acceptance.

Pending cardholder differences from source:

- The client checks `PENDINGORS` before sending; without it, no request is made. Backend enforcement remains unverified.
- Source copied `code`/`message` onto every row and returned a synthetic `909` row on any exception. The migration returns one reply with an explicit outcome: success, not permitted, rejected (missing or non-zero `code`, or a non-2xx status below 500; backend message shown but never logged), connection failure (transport or 5xx), invalid response (non-JSON, missing or non-array `data`, undeserializable rows), or unavailable (invalid configuration).
- Source sorted with `DateTime.Parse` in the machine culture, so one unparseable `datecreated` failed the whole queue. The migration parses with the invariant culture, keeps oldest-first ordering, and places unparseable dates last. `No` is renumbered 1..n after sorting, as in source.
- Source reported progress with a 10 ms sleep per row; the migration loads asynchronously with a loading indicator instead.
- Json.NET coerced JSON numbers into string properties; System.Text.Json does not. If the live payload sends fields such as `cid` or `citizenid` as numbers, the queue currently fails closed as an invalid response. This needs a sanitized live response to resolve.
- Approve/reject/return and the collection/specific reports are not migrated. The read-only specific-record route is now used by View Application.

The general envelope is `code,message,data[]`. Login data fields mapped from source are `email`, `token.accesstoken`, `password.isfirstlogon`, `password.isexpired`, and `roles.modules/minpassword/maxpassword`. Login refresh and XSRF state comes from exactly `refresh_token` and `XSRF-TOKEN`. Other cookies aren't classified as XSRF. Values retain embedded equals; percent-encoded XSRF is decoded for the request header. That encoding choice needs live comparison against the backend. Refresh data maps `accesstoken`; cookies rotate independently, and absent rotation retains the previous token rather than inventing a new one. Explicit blank tokens invalidate a success.

JSON field matching now preserves the source Newtonsoft case-insensitive behavior: `accessToken` and `accesstoken` name the same mapped field. Undocumented aliases such as `access_token`, object-versus-array changes, or nested refresh token containers are not guessed. Ambiguous duplicate case variants fail closed. Debug refresh diagnostics report only fixed JSON value kinds for the data/item/access/token-container fields; they never print token values or raw JSON.

The owner's runtime diagnostic reported HTTP 200/business 0 with MissingAccessToken. This proves the migrated parser failed to obtain a new access token despite a successful envelope; it does not identify a backend session rejection or establish the full live response shape. The case-matching regression is corrected and tested, but resolution on that live deployment still needs a rerun. If it persists, the structural diagnostic distinguishes an array/object, missing field, or nested-container mismatch without exposing credentials.

The transport keeps HTTP status, `code`, body `status`, and body `error` distinct. Missing `code` never defaults to zero. Non-JSON/empty responses are invalid, and transport failures have no HTTP/business success. Non-2xx response codes never authorize login even if the body says code zero.

Source Login.cs maps HTTP 401 to generic credentials and documents inactive=6 / locked=7. These mappings are unit-tested with synthetic fixtures and remain live-unverified. OTP rejection preserves the pending OTP screen for correction. Session source checks business 3 and 400/401/403; the new refresh adapter ends the session with generic wording for those rejection cases. A generic 400 never identifies takeover. Actual code-3 meaning and 400 handling still need endpoint-specific backend confirmation.

Navigation authority strings: `PENDINGORS`, `VRESIDENCEVERIFICATION`, `VRESIDENCESTATUS`, `VINITIALKYCVALIDATION`, `VINITIALKYCRESULT`, `VINFORMATIONGATHERING`, `VCARDHOLDERUPDATE`, `VCARDHOLDERFORPRINTING`, `UPLOADCARDHOLDERPRINTEDRESULT`, `VISSUEDID`, `VUNCLAIMEDID`, `VUNPRINTEDID`, `ROLEVIEW`, `VIEWUSERS`, and `CPW`. Backend enforcement is documented in source comments but has not been verified by unauthorized requests. `PENDINGORS` additionally gates the pending cardholder request client-side. Audit permission/action mapping has not yet been finalized.

The collection confirms route examples for personal/address/contact/emergency/identification updates, documents, workflow queues/actions, lookup cascades, printing results, ID tracking, users, roles, and audit. Those route examples do not establish response schemas, mandatory fields, concurrency semantics, or valid transitions. The `ors - disregard` folder is marked historical and is not used to implement enrollment.

Wizard source findings:

- StandAlone has Personal, Contact, Address, Emergency Contact, Identification, Face Photo/ICAO, Signature, and Biometrics; Education/Additional Requirements are Bacoor-only.
- `DialogNavigationBar` creates a new step control and disposes the previous control. Navigation has no save call. Explicit section saves reload the authoritative record. The current migration does not promise unsaved field retention or introduce automatic field clearing as a new workflow rule.
- Civil Status is required. StandAlone uses Address Line 1; Bacoor's structured layout uses Street.
- The 90-character source limit applies separately to each present/permanent address: visible city length plus one separator and each nonblank visible barangay, street/building (structured), or line1/line2 (free text). It does not count hidden address groups, province, region, ZIP, or structured unit/house number.
- The ID back list is reproduced in WizardRules from `utils/BackIdTypes.cs`. Unlisted ID types are not forced to provide a back image.
- The progress bar's capture checks aren't sufficient to prove backend completion: fingerprint completion checks remarks; photo checks non-null image; signature checks transparency. Backend quality, absence reasons, duplicate matching, and persisted completion must be verified before enabling real Submit.
- Some Cardholder Update action permission mappings are expressly marked unconfirmed in source. No guessed action codes or concurrency headers have been introduced.

No live requests with staff credentials or applicant data were made. An unauthenticated OPTIONS probe to the configured authentication endpoint failed with SocketException under restricted networking. CORS, cookies, password states, rate limits, permission enforcement, token rotation, and persistence require an accessible non-production environment.
