# Technical implementation report

## Architecture and evidence

Inspected the solution/projects, core authentication/session/configuration/transport/navigation and detail/queue services, their tests, web/native composition and applicant/dashboard screens, existing documentation/evidence generator, and selected legacy controllers/models. This was a focused review, not an exhaustive audit of all source or generated files. No AGENTS.md was found in the workspace or its ancestors. No CI workflow files were found under `.github` during discovery.

`OBS.Core` is shared by Blazor Interactive Server and Windows/optional Android MAUI. The inspected projects have no local database/ORM dependency. Authentication and record persistence are backend responsibilities; no schema, SQL, migration, or persistence model was changed. The earlier 99-request contract remains supporting historical evidence, not the selected API scope.

The selected collection is `D:\Downloads\OBS - Privacy PII OTP & Dashboard.postman_collection.json`, SHA-256 `052A352871191D39209951F755CC76969DF9D20B50178E0D7779849D2495FABE`. All 16 requests, descriptions, headers/disabled headers, raw body structures, variables, inherited authentication, scripts, and empty response-example lists were examined. No collection pre-request logic or live server behavior was executed. Its scripts sometimes log response JSON; the application does not copy that behavior.

## Smallest compatible implementation

`PrivacyApiService` reuses `ApiTransport`, `BackendOptions`, `StaffSession`, and the existing identifier policy. Raw adapters preserve ApiResult; typed helpers map observed dashboard, status, four section and two detail structures with explicit validation/outcomes. The service is stateless, accepts cancellation and an optional TimeProvider, sends one request per invocation, and schedules no background work or retries. Callers must supply owned/per-circuit HttpClient and the existing 30-second timeout convention when composed.

Core coverage includes request/verify OTP, verification status, four protected sections, two redacted-or-authorized detail reads, and dashboard. Request/verify/status and the four sections are now called by the new parallel Privacy Verification screens through PrivacyWorkspace, PrivacyScreenState and ApplicantPrivacyController. Dashboard and two detail adapters still have no screen caller. The new screen accepts citizen ID directly and does not resolve reference numbers, mutate applicant data or persist OTPs/tokens.

The chosen implementation adds focused adapters and a parallel screen. The user approved the minimal OTP dialog and subsequently selected a new screen in the same app, preserving existing reference search. See UI-PROPOSAL.md and the parallel-screen design.

## Authentication and tokens

`ApiContract.Privacy` is an explicit opt-in core contract. Existing `Refactored`/`Legacy` enum values and defaults are preserved. Login bodies match `AuthenticationRequests.Authenticate`: username/password, followed by username/password/otp; no bearer. Source-derived session parsing requires access/refresh/XSRF tokens, permissions, email and password flags. The user supplied the successful body structure and confirmed both exact refresh/XSRF cookie names. A synthetic happy-path test covers those fields together. Refresh rotation/renewal and full client-session acceptance remain unverified.

The Privacy logout request has no body and sends the staff bearer and configured header. Existing Refactored logout retains its source-backed userName body and XSRF header. Existing resend, refresh, forgot/change-password routes remain intact even though absent from the selected collection; their compatibility at the new root is **Not tested**. No refresh endpoint/policy was invented.

`PiiAccessToken` is separate from the staff bearer, transient, opaque and redacted. `FromVerification` accepts the observed bare HTTP/business success with exact cookie `pii_access_token`; if data is present it must still be one record with boolean verified=true. Contradictory/malformed metadata and missing/invalid cookies yield no token. Credential capture is not authorization. `PrivacyVerification.cs` separately parses observed status metadata, binds canonical application ID, requires an absolute zoned expiry, and uses exact observed claim names. The clock is read after network completion; expired/invalid results grant nothing.

The documented `X-PII-Access-Token` header fallback is used explicitly on allowed applicant/privacy GETs. Automatic cookie handling is not introduced. The header cannot be sent through this transport to dashboard, pending-cardholder reads, authentication, or other POST routes. No JWT claims are decoded to authorize local UI. Server validation remains authoritative.

The core checks VIEWDASHBOARD; detail adapters preserve VINFORMATIONGATHERING/VCARDHOLDERUPDATE. Protected helpers require the exact status claim for their section before dispatch and recheck expiry afterward. Detail mapping intersects object-shaped server flags with current status claims and withholds protected values without both. No privacy staff-role names, JWT authorization, reference aliases or TTL are invented. OTP actions require a complete staff session without mandatory password change.

## Configuration and compatibility

`ApiRootPath` explicitly selects the documented `/api/v1` suffix for Privacy. Existing deployments default to `/obs/api/v1`. Invalid suffixes, route-level base URLs, credentials/query/fragment in base URLs, HTTP, and configured-header collisions fail validation. There is no environment fallback and no imported collection URL/credential value.

Both host composition roots retain the guard on replacement Privacy sign-in using the existing configuration-unavailable presentation. Existing application screens still use their original masking/record routes; the parallel screen alone uses OTP-controlled fetching. PrivacyWorkspace derives a separate service contract from the exact existing configured API root and header without mutating settings, switching login or falling back between environments. Enabling replacement sign-in still requires full renewal and retained-workflow acceptance.

## Transport and errors

- Exact POST routes replace the previous session-prefix acceptance. All source-backed current callers are retained; unimplemented session routes are rejected before dispatch.
- Optional null POST content permits the documented bodyless logout. Existing POST bodies remain JSON.
- Existing configured-header behavior is retained. PII header collision with deployment configuration is prohibited; new adapters do not supply a second configured-header override.
- Only documented new GET shapes are allowed. Identifiers reuse the existing maximum-100 ASCII letters/digits/hyphen/underscore policy. Broader identifiers are a documented compatibility limitation, not a claimed API restriction.
- Non-object JSON envelopes, empty bodies, and invalid JSON are invalid responses. HTTP status and business code remain separate.
- HTTP 401/403/429/5xx, business rejection, cancellation, timeout and connection failure are preserved for the caller. No retry, idempotency header, offline queue, pagination parameter, or non-idempotent replay is added.
- Responses and cookies may contain sensitive data. No raw-result logging is added. They must not be logged by future callers.

## Missing mappings and acceptance prerequisites

The user supplied dashboard field/type evidence after the initial handover. `DashboardSnapshot` now maps it into the existing metric/activity models, and GetDashboardSnapshotAsync wraps the existing single authorized request with strict parsing and explicit outcomes. See DASHBOARD-EVIDENCE.md. Screens and sample displays are unchanged.

See PRIVACY-EVIDENCE.md for status, section and authorized/redacted detail observations. ProtectedApplicant.cs keeps minimal display data, raw ambiguous birth dates, separate status fields, nullable values and collection order. ApplicantDetailApi.cs retains observed baseline identity/contact/classification/module data while withholding protected values unless both authorization sources agree. It does not compute eligibility, age, primary contacts or preferred addresses.

Canonical citizen/reference/CID lookup, dashboard periods and existing renewal endpoint compatibility remain unresolved. The pending-record model includes citizenid; the supplied successful detail body for a different applicant has id without citizenid. The original approved record's lookup returned the confirmed HTTP 401/business 2004 pairing. Do not invent an ID alias, classify the record's workflow stage or infer an expired staff login from that conflicting status/message.

## Shared privacy controller and parallel screen

Pending-lookup clarification: user subsequently confirmed the HTTP 401/business 2004 pairing means no matching pending application. ApplicationRecordsService handles that exact pairing only for the selected Privacy contract's specific pending route, before general HTTP authorization handling. ApiTransport exposes its contract internally; original contracts retain existing behavior. No raw HTTP status is rewritten, data granted, global logout/auth rule changed or attachment request issued for not-found. GET /cardholder/pending was identified as the list source; user reports citizenid absent, so canonical mapping remains unestablished. Ask which actual list field/type belongs in the privacy URL rather than assuming id/cid is equivalent.

ApplicantPrivacyController owns only one selected canonical applicant's transient credential, verified metadata and visible section snapshots. It requires an active protected staff session, serializes request/verify/reveal/revalidation, requires Request before Verify, validates six ASCII digits and captures no OTP input field. A successful cookie is followed by a separate status check before any grant. Every reveal rechecks status, then fetches only its selected section through the typed reader. Hiding drops that section's retained snapshot without hiding other sections; a hide revision prevents a late request from undoing it.

SessionController now exposes an internal opaque Identity object for this scope. A new successful sign-in replaces it even for the same email; source-backed token rotation preserves it, and Clear invalidates it before logout network work. This does not alter token parsing, permissions, renewal or existing authentication outcomes. Pause, logout, password-change stage, replacement login, applicant change, Cancel and disposal invalidate privacy work and data. Epoch checks prevent older requests from restoring cleared state or clearing a newer operation's busy flag.

An absolute-expiry timer clears authorization and snapshots, with bounded timer slices for long server expiries rather than an invented TTL. Getters also deny expired data when a timer callback is delayed. RevalidateAsync removes revoked claims and adopts the server's shorter expiry without automatically revealing anything. No undocumented revocation polling interval is introduced; the eventual host must invoke revalidation at foreground/protected-work boundaries. Network/status/access failures fail closed for privacy and do not silently terminate the global staff session.

PrivacyWorkspace is scoped to the web circuit and singleton-owned in the installed app. It owns one cookie-free, redirect-free 30-second HttpClient and creates a fresh controller per screen. Weak controller references let workspace disposal clear active privacy state before releasing the client. Construction, selection and opening the dialog dispatch no request. HTTPS/root/header validation is retained.

PrivacyScreenState shares selection, explicit OTP actions, dialog state, claims and current-field presentation across both clients. Editing input invalidates the applicant immediately. It clears code input before verification awaits, rejects old completions and clears all state on disposal. It subscribes to existing staff-session changes: token rotation triggers status revalidation, failed refresh diagnostics cancel privacy, and foreground/session transitions invalidate selection through the controller. No new polling timer or resend cooldown is introduced.

The web component dispatches Changed through its renderer, conditionally renders protected fields and a native modal dialog, and disposes screen state on navigation. privacy.js handles native Escape cancellation; CSS is limited to privacy selectors. MAUI dispatches to the main thread, clears labels before replacing them and hosts the small modal outside the scrolling content. Windows Escape/focus handling and Android Back cancel it. All navigation paths dispose the view. Both clients retain the old reference search and its UI.

PrivacyPresentation maps only currently visible snapshots to fresh display fields, preserves address/collection order and raw date text, and rechecks visibility before returning. Host copies are cleared on Changed and disposal. Nine additional synthetic workspace/screen cases and four actual web-component render states validate local behavior. Browser/native-device focus, lifecycle and server authorization acceptance remain unverified. The unresolved reference mapping affects a future bridge, not this direct citizen-ID screen.

The later requested visual match reuses unchanged application.css for the web record layout and copies the native reference's four/two/one-column thresholds and card dimensions. Both views retain their direct-ID selector/OTP actions and use existing authorized fields for profile/personal summaries. Full section details are rendered from fresh state, with metadata-only dialog selection and clearing on hide/revocation/selection/session/disposal. Native recursively erases nested labels. Missing age/photo/email/phone/card/documents/capture permissions are not guessed; unsupported controls remain unavailable. Nine actual component states and measured headless viewports now supersede the earlier four-state validation. No existing ViewApplication source or core privacy/session logic changed for this visual request.
