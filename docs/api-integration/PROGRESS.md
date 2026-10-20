# API integration progress

Date: 2026-10-20 (Asia/Manila). Overall status: **incomplete**.

## Current View Application Details visual match — 128 tests and nine render states

- User requested the new screen be identical to View Application Details. Both new views now match its four-column profile/personal/files/sidebar layout, card radius/padding, cover/avatar, badges, address/map area, icons, Actions and Quick Links. Existing ViewApplication.razor, ViewApplicationView.cs, application.css and core OTP/session logic are unchanged. Dashboard applies the same details-page spacing to both views.
- Web reuses application.css and native matches existing 1200/650 breakpoints/dimensions. Direct citizen-ID selector and approved OTP remain. Summary cards use observed fields/raw dates; full collections are available via authorized section-detail dialogs. No age, missing photo/contact/card/document values, capture permissions or API endpoints are fabricated. Map/capture/attachments stay disabled with clear unavailable states.
- Detail-dialog contents are read fresh from current authorized screen state; hide/revocation/applicant/session changes close and erase them. Native clears nested labels before removing controls and on disposal; Escape/Back covers both dialog types. No personal data or code is retained in dialog metadata.
- 128 core tests remain passing. Nine actual component states pass, including hidden/revoked detail-dialog clearing and all section collections. A synthetic render of the unchanged reference component checks the same layout structure. Headless local previews measured exact viewports 390/1000/1680 with one/two/four columns and no overflow; light/dark/modal images inspected. Initial CLI screenshot sizing was clipped by headless window limits; exact viewport emulation was used for final checks.
- Logs use privacy-layout- prefixes. Web/Windows solution and Android Compile pass, with only the pre-existing People.xaml XC0025 warning in the solution. Local headless renderer used a fresh isolated profile and was stopped after capture; the user's browser and desktop app were not touched. No dependency/settings/database/commit/deployment changes. Native-device and live backend acceptance remain pending.

## Earlier parallel-screen milestone — 128 tests and four render states

- User explicitly chose a new screen in the same app, retaining existing search; original small OTP dialog approval remains valid. Implemented Privacy Verification navigation and views in web and MAUI. Direct citizen-ID input eliminates pending-list mapping as a prerequisite. No ID alias or new lookup endpoint is inferred.
- PrivacyWorkspace preserves the exact configured API root/header and existing staff login/renewal. It owns a cookie-free, redirect-free client and creates fresh per-view controllers. PrivacyScreenState shares selection, explicit OTP actions, cancellation, session hooks and presentation across both clients. No construction/selection/dialog-open request, automatic email, retry or resend cooldown.
- Seven selected request workflows are wired: request/verify/status/four sections. New views clear rendered/dialog copies on invalidation and dispose on navigation. Windows Escape/focus and Android Back hooks are implemented; native-device/browser keyboard and accessibility acceptance remain pending. Full replacement Privacy sign-in guard stays; dashboard and two detail workflows remain pending.
- Nine added workspace/screen tests bring the core total to 128/128. Isolated solution build passes with existing People.xaml XC0025; Android Compile passes with zero warnings/errors after restore. Four actual web-component HTML render states pass through an ignored synthetic harness: locked/dialog/verified/cleared. No live credentials or authenticated APIs used. Current logs use privacy-workspace- prefixes.
- The running desktop app was not stopped or restarted. No dependency/database/settings/manifest changes, commit, push, release or deployment. Current file accounting is recorded in CHANGE-INVENTORY.md and verification-results.json. Historical counts/status below describe earlier checkpoints.
- Next acceptance: user locally tests updated client with fresh existing staff sign-in and approved synthetic citizen ID. Do not ask for more personal response bodies or ID mapping to complete this screen. Retained search remains in place; broader full-collection integration remains incomplete.

## Earlier pending-lookup clarification — historical 119 tests

- User clarified that the confirmed HTTP 401/business 2004 means no matching pending application. Supplied GET /cardholder/pending as the pending-list source, then reported that list does not include citizenid. This resolves the earlier authentication-versus-not-found question, not the canonical-ID mapping.
- ApplicationRecordsService now maps only Privacy contract + specific pending lookup + failure-free HTTP 401/business 2004 to ApplicationOutcome.NotFound. ApiTransport exposes its contract internally for this decision. No global authorization rule, other contract, status, attachment route, staff-session behavior or screen layout is changed. No record/documents are returned or requested for the missing application.
- Added one parameterized regression including other 401 codes, 403, 500, unexpected 200/business 2004, attachment failure and existing-contract behavior. Current 119/119 synthetic tests pass. Isolated solution build passes with existing XC0025; current logs use pending-lookup- prefixes. Check verification-results.json for Android evidence.
- Current inventory has 38 uncommitted task files. Previous milestones below are historical. Privacy hosts remain disabled; the shared controller and approved dialogs are not yet connected to screens.
- The user did not understand the earlier id/citizenId question. Explain it as a concrete URL substitution: which field from GET /cardholder/pending should fill {citizenId} in GET /privacy/applicants/{citizenId}/bio? Request only field name/type, preferably developer confirmation, no applicant values/full list. Keep existing optional citizenid model field for legacy compatibility; do not create an id/cid alias from absent-field evidence.

## Earlier shared-controller milestone — historical 118 tests

- User confirmed the original approved reference lookup is HTTP 401 with business 2004/message Pending application not found. The successful pending-detail attachment was for a different applicant; it cannot link id to citizenId for the privacy record. Ask the developer to clarify lookup scope, status semantics and canonical ID mapping. Do not automatically log the user out, assign a workflow stage, substitute IDs or repeatedly send the same request.
- Implemented ApplicantPrivacyController with OTP/action serialization, opaque staff-session/applicant binding, status-before-reveal, section-specific data/hide state, absolute expiry timer, explicit status revalidation, cancellation/disposal and stale-response protection. A narrow internal SessionController Identity is stable through token rotation and replaced/cleared on sign-in/logout. Existing authentication/permission/refresh behavior is preserved.
- Twelve new synthetic lifecycle/concurrency cases pass; current total 118/118. Normal full build failed because the running desktop app locked its output DLL (2 errors/11 warnings). Initial isolated full build passed with existing XC0025; latest incremental isolated solution check after the hide/denial race fix passed with zero warnings/errors, as did tests. Android Compile passes after restore. The user app was not stopped; no dependency/project workaround or release.
- Current inventory: 36 uncommitted task files. Current evidence logs use privacy-controller- prefixes. Earlier 24/28/33-file and 83/89/106-test milestones are historical.
- Controller is not registered by either host; no screen/layout/eye handler or Privacy startup guard changed. Dialog/host integration must subscribe on the UI context, clear rendered/dialog copies, revalidate on foreground/protected boundaries and dispose per screen. No polling interval, ID alias, backend rule or new endpoint is invented.
- Next user-facing step: provide a short revised developer message for the confirmed 401/business-2004 lookup and id/citizenid questions. No further personal payload is needed. Remaining: canonical lookup, renewal compatibility, approved dialogs/host wiring, dashboard periods/binding and full acceptance.

## Earlier privacy/status/detail milestone — historical 106 tests

- User supplied HTTP 200/business 0 verification status with boolean verified, string applicationId, absolute UTC expiresAt and four exact section claims. Confirmed exact pii_access_token cookie on bare-success Verify response. The earlier status/cookie blocker below is resolved.
- User supplied Bio personal-info, Address address, Emergency emergency and Education other-info arrays; all have numeric citizenid. Also supplied authorized Information Gathering and redacted Cardholder Update layouts, including object-shaped boolean piiAuthorizedSections. HTTP statuses for those bodies were not provided. No supplied personal values were retained in fixtures/evidence.
- User confirmed both exact login cookie names refresh_token and XSRF-TOKEN. A synthetic complete-session case passes with the earlier observed body. Renewal/rotation, restoration and actual client-session acceptance remain unverified.
- Added PrivacyVerification.cs, ProtectedApplicant.cs and ApplicantDetailApi.cs; extended PrivacyApiService with typed readers. Token capture alone grants nothing. Protected helpers check applicant/section/expiry before and after requests; detail mapping intersects server flags with status claims and distinguishes withheld data from authorized empty arrays.
- Added sixteen synthetic response tests and one complete-login test; original 89 tests retained. Current result: 106/106 pass. Full solution passes with existing People.xaml XC0025 warning. Latest Android result is recorded in TESTING.md and verification-results.json. Current logs use privacy- prefixes; previous 83/89 snapshots below are history.
- Current inventory: 33 task files, no UI layout/eye-handler/startup-guard, dependency-version, database, commit, push or deployment change. Overall integration remains incomplete.
- Existing-reference lookup correction: after the earlier HTTP 500/business 99 generic error, user suspected the address/method and supplied an attached code-0 one-row detail response. They confirmed GET /obs/api/v1/cardholder/pending/{referenceNumber}; corrected HTTP status was not supplied. The attachment was inspected through a sanitized field/type projection only; no personal values, security answers, beneficiary data or full response were copied into fixtures/evidence. The successful body matches the existing nested personalinformation/address/emergencycontact/educationalinformation layout. It has numeric id but no explicit citizenid. Do not silently alias them or assume this is the same record as earlier privacy observations. The developer error message is on hold; earlier failed-attempt method/resolved URL was not independently confirmed. Current question: compare id locally with the Bio request's citizenId for the same approved record (match/different/different applicant); share no values. User prefers one small step per reply.
- Remaining implementation: canonical lookup, selected-host renewal compatibility, approved OTP dialog/controller with staff/applicant lifetime, revocation/cancellation/expiry clearing, screen bindings, dashboard period behavior and full acceptance. See PRIVACY-EVIDENCE.md.

Latest lookup clarification: the user confirmed the successful pending-detail response is for a different applicant from the privacy checks. It cannot establish an id/citizenId relationship for the original record; request no further data from that other applicant. User then confirmed preparing the original approved reference and, after Send, reported HTTP 401. The next assistant turn was interrupted after read-only file inspection; no login request or source edit was issued. User subsequently supplied business code 2004/message Pending application not found. Current question: read the HTTP status associated with that exact body, without resending. Do not assume expired login or map a business not-found code across an unconfirmed HTTP/status pairing. No canonical ID, same-record success or universal ID alias is established. No application code or tests changed at this evidence-only checkpoint.

## Earlier privacy verification observation — superseded

- Following the privacy OTP instructions, the user reports HTTP 200 with business code 0/message Success and no data object; the PII-token variable reportedly has a value. The exact request and whether the value was freshly captured were not independently confirmed. No credential value was supplied for this observation.
- This differs from the collection description/script, which expects verified/expiresAt/authorizedSections in data[0]. Do not fabricate those fields, infer expiry, or equate token presence with authorization.
- The current conservative FromVerification reader requires verified=true and the exact cookie. Its behavior remains unchanged pending verification-status response and cookie-name evidence. No UI guard or sensitive reveal behavior was relaxed.
- Next: GET the collection's Verification status for the same local applicationId with the documented X-PII-Access-Token header fallback enabled. Collect HTTP status and sanitized body. Also ask whether the Verify privacy OTP response has a pii_access_token cookie (name/presence only). No authenticated request is executed by the assistant.
- Source/tests are unchanged since the 89/89 dashboard milestone; no repeat build/test is required for this documentation-only checkpoint.

## Earlier dashboard milestone — historical 89-test follow-up

- The user supplied the successful-business-envelope dashboard response. The seven integer metrics and activityOverview/category/completed/inProgress layout are now established as observed response evidence. The user did not provide the dashboard HTTP status. No real aggregate values were copied into repository fixtures.
- Implemented `DashboardSnapshot`/`DashboardReply` in `src/OBS.Core/DashboardApi.cs` and `PrivacyApiService.GetDashboardSnapshotAsync`. These map into existing DashboardMetric/DashboardActivity models, retain the server percentage/order/labels, validate all displayed values, and preserve error/cancellation distinctions. No extra request or guessed period query is added.
- Six synthetic mapping/error tests were added in DashboardApiTests.cs and registered. Current result: 89/89 pass (original 71 plus 18 new). Full web/Windows solution build passes with existing XC0025 warning; Android Compile passes after optional-target restore. New logs use the `dashboard-` prefix in the same ignored artifacts directory.
- Screens, sample data displays, styles, UI period controls, existing settings and Privacy startup guards are unchanged. Dashboard payload mapping is complete at the core level; screen binding, period semantics and the remaining privacy workflow are pending. No end-to-end completion is claimed.
- The user confirmed an approved synthetic test record is available. Next manual sequence: request privacy OTP using local applicationId, enter the received code in local otp, verify once, and supply sanitized verification metadata and token-presence confirmation only. Do not use the earlier pasted staff credential.
- Current changed-file inventory includes 28 files. Initial 24-file/83-test results and earlier access notes below are historical snapshots. No commit, push, dependency version or database change.

## Earlier access observations — after initial handover

- User-reported login completion: HTTP 200, business code 0, and the collection's accessToken variable populated. The supplied response establishes the existing `data[]` layout with email, password flags, role/password-policy/module fields, and `token.accesstoken/type/expiresin`. VIEWDASHBOARD is present. Identity values, token contents, and the full response were not copied into repository evidence.
- Refresh/XSRF cookie presence, token rotation, session restoration and termination semantics are still unverified; the body alone does not establish complete existing-client session compatibility.
- The user's pasted response included an access credential. Advised ending that test session through the supplied logout request and signing in again locally. Do not use or retain the pasted credential. No authenticated requests were executed by the assistant.
- Next response needed: the collection's **4. Dashboard → Get Dashboard**, sent manually with the fresh test session. Request only HTTP status and sanitized JSON preserving field names/types/nesting, with personal values and all credentials omitted.

- User-reported manual test: the first login OTP request returned HTTP 200 and the OTP email arrived. This is live evidence from the user for request/delivery only, not an agent-executed login or completed authentication/session verification. The next manual step is the collection's login-OTP submission. No secret values were shared.

- Follow-up: the user reported a 404 webpage. The in-app browser confirmed the supplied `/obs` URL displays the site's “Page not found” screen.
- A single unauthenticated HEAD to the collection-composed `/obs/api/v1/dashboard` returned HTTP 401 with JSON content type. This is evidence of an authentication boundary at the API address; an authorization filter can run before route resolution, so dashboard deployment/response shape is not proved by this result.
- Next step is an approved preproduction staff test account, with credentials entered locally. No authenticated call, OTP generation, applicant access or response-body inspection was performed. Functional endpoint Live-verified coverage remains zero.
- This unauthenticated API probe supersedes the earlier no-API-contact observation below. The original build/test handover remains a historical snapshot.

- The user has no backend source access; the developer supplied only the new collection. Do not require source code as the only way forward.
- The user supplied a preproduction HTTPS address ending in `/obs`. Host details remain in the chat rather than copied into versioned environment defaults.
- A single unauthenticated GET of the supplied `/obs` address returned HTTP 403 with HTML, no redirect, and no API route references. The web-reading tool also could not access the page. This does not establish whether the cause is VPN/IP restrictions, gateway policy, or a different deployment detail.
- No login, email OTP request, protected applicant request, real-record access, or authenticated API test was made. API response schemas and endpoint availability remain unverified.
- The selected collection appends `/api/v1/...` to `baseUrl`; retaining the supplied `/obs` context would construct `/obs/api/v1/...`. This is URL composition evidence, not proof that the new endpoints are deployed there.
- Next step: the user should check the supplied address in their normal browser with their usual company network/VPN, and report whether a login page or 403 appears. Then confirm the resolved collection URL and arrange approved test-account access locally.
- The original handover's 24-file snapshot is historical. The subsequently requested guide is saved under `plans/provide-api-backend-details-plan.md`; no application code was changed for this access check.

## Scope and checkpoint

- User selected `D:\Downloads\OBS - Privacy PII OTP & Dashboard.postman_collection.json` on 2026-10-20: 16 requests, no saved response examples.
- SHA-256: `052A352871191D39209951F755CC76969DF9D20B50178E0D7779849D2495FABE`.
- The older 99-request collection is background evidence only. Existing integrations remain necessary and are preserved.
- Initial branch: `main`; initial HEAD: `6af963b`; initial working tree: clean, including untracked files.
- Inspected local and remote-tracking branches before creating `codex/api-integration-audit`. No stash, reset, commit, push, or deployment.
- No applicable AGENTS.md found in workspace or its ancestors.

## Discovery completed

- Shared `OBS.Core` (.NET 9), Blazor Interactive Server `OBS.Web`, Windows/optional Android `OBS.Maui`.
- SDK pinned to 9.0.301; MAUI Controls 9.0.51; dependency-free executable core tests.
- Authentication/session and pending-cardholder/detail/document services inspected; permissions default to denial; foreground session refresh is serialized.
- No database dependencies or local persistence layer found in the inspected projects. Existing backend persistence is untouched.
- Existing dashboard metrics are explicitly synthetic. Applicant reveal controls are local masking only; they are not privacy authorization.
- Selected collection, including every request, authentication/header settings, variables, body fields, descriptions, and test scripts, inspected in memory. Credential/body values must never be copied into documentation.
- Legacy source authentication/controllers inspected as supporting evidence, without modifying the original repository.

## Baseline evidence

| Command | Actual result |
|---|---|
| `dotnet --info` | Pinned SDK 9.0.301 and Windows/Android workloads available |
| `dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj --no-restore` | 71/71 synthetic tests pass |
| `dotnet build OBS_eKYC.sln --no-restore` | Pass; zero errors; existing People.xaml:14 XC0025 warning |

These checks made no live API requests. Baseline build/test results do not establish API compatibility.

## Implemented design and current scope

1. Preserve existing clients and UI. Add narrowly scoped calls using the existing transport.
2. Configure the selected `/api/v1` root explicitly, preserving the existing `/obs/api/v1` default for existing deployments.
3. Separate login Bearer tokens from PII tokens; never automatically turn an HTTP success into permission to reveal personal data.
4. Match documented bodyless logout for the selected contract, preserving source-backed logout/resend/refresh/recovery for existing deployments.
5. Add controlled transport/authorization/serialization/error tests without real records.
6. Complete 16-request inventory, mapping, configuration/test/security reports, modified-file/dependency inventory, and this recovery checkpoint are saved.

The core design is implemented and mock-tested; no privacy/dashboard screen callers have been added. Both client composition roots block Privacy mode until the full OTP-gated workflow is complete. No dependencies, database schema, business rules, screen layouts or existing deployment settings were changed.

## Initial evidence / decisions — historical, see current milestone above

- Asked for backend source or sanitized response schemas. The user did not know where to obtain them. Explained the missing information in plain language and searched the available project paths; no matching privacy backend was found. The collection's local HTTP port 8080 had no listener. No API request was sent. Dashboard field names, date encoding, section identifiers/shapes, and status response schema are not established.
- Collection uses `/api/v1`; existing source uses `/obs/api/v1`. Deployment must explicitly select the matching root; no server environment is authorized or contacted.
- The user approved the concrete small email-code dialog proposal in UI-PROPOSAL.md. Do not ask again for this same interaction. Its implementation remains blocked on response/expiry/ID evidence, not on UI approval.
- New information-gathering/cardholder-update detail routes use citizenId; existing View Application uses a pending-record reference number. Alias/canonical-ID mapping and screen workflow choice need evidence before substituting routes.
- Three legacy personal-info OTP calls are alternate facades; collection explicitly recommends modern privacy OTP. No legacy fallback or automatic OTP retry is authorized.

## Recovery

1. Inspect git status, branch and HEAD. Current inventory has 38 unstaged/untracked task files; the initial tree was clean. Do not overwrite later work.
2. Read this checkpoint, README.md in this directory, ENDPOINT-MAPPING.md, ENVIRONMENT.md, SECURITY.md and verification-results.json. Confirm the 16-request source hash still matches if reanalyzing it; do not print its values.
3. Latest tests: 119/119 pass. Isolated-output build passes with existing XC0025; normal output was previously locked by the running desktop app. Current logs use pending-lookup- prefixes; check TESTING.md for latest Android result.
4. Pending evidence: which pending-list field/type supplies the privacy citizenId. User says citizenid absent; do not infer id/cid aliases or request more data from the other applicant. Not-found meaning is now clarified and narrowly mapped.
5. Resolve actual response mappings and selected-server auth/cookie compatibility. Preserve existing source-backed integrations absent from this collection. Do not derive canonical citizenId from a reference/CID without evidence.
6. Reuse the implemented shared privacy controller for approved web/native dialogs and screen bindings. Clear UI copies on Changed, bind foreground revalidation/disposal, preserve independent masks and layouts, and validate complete host lifecycles.
7. Wire verified detail/dashboard mappings. Only then replace the composition guards together with the protected workflow, and validate denied/expired/cancelled/cross-applicant/session paths. No release or production testing is authorized.
8. Update all coverage, test and checkpoint documents from actual results. Do not repeat a timed-out/failed OTP mutation automatically.

## Initial adapter checkpoint — historical 83-test milestone

- Current phase: core implementation/contract mocks and handover complete; end-to-end integration **Blocked** by backend evidence/availability. Overall **incomplete**.
- Coverage: 16 reviewed; 2 Already supported login formats, 11 Implemented core adapters/variant, 3 Deferred alternate legacy facades. Request-level 13 Mock-tested, 3 Not tested, 0 Live-verified. Ten new screen workflows remain Blocked.
- Baseline 71/71 tests retained; final 83/83 pass with 12 new cases. `dotnet build OBS_eKYC.sln --no-restore` passes with the existing warning.
- Optional Android Compile initially failed with 57 missing MAUI-reference errors. Restoring the opt-in target corrected it. It failed again after a Windows build; restoring Android immediately before Compile passed again. This is an observed restore/cache sensitivity; root cause was not established and no project/toolchain workaround was added. See TESTING.md for exact sequence and logs.
- Inventory generation initially failed on a null/bodyless request; repaired and successfully regenerated. All 16 requests, raw/structured query names and classifications were checked. Secret values are not exported.
- Modified files and reasons are listed in CHANGE-INVENTORY.md. No dependency versions or manifests changed. Working tree intentionally contains only uncommitted task work.
- Evidence: `artifacts/api-integration/2026-10-20/core-tests.log`, `solution-build.log`, `android-compile.log`, `android-restore.log`, and `android-compile-after-windows-failure.log`; artifacts are ignored/local.
- Final diff/source/secret/documentation audits are recorded in the test/security handover. No live API, UI/device acceptance, performance measurement, commit, push or deployment is claimed.
