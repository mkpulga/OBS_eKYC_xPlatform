# Security, compatibility and remaining risks

## Implemented safeguards

- The selected collection is not checked into the repository. Generated evidence omits credential, header-value, URL-host/query-value, body-value and concrete-identifier content.
- Staff Bearer and PII credentials use distinct types/headers. PII credentials are transient, redacted in `ToString()`, and restricted by transport destination checks. They are not added to HttpClient default headers or global storage.
- PII cookie capture accepts observed bare success but rejects HTTP/business failure, contradictory/malformed data if present, wrong/missing/invalid cookie. It grants nothing. Status separately validates applicant, absolute zoned expiry and exact claims. Typed protected helpers recheck expiry after requests and expose no snapshot on failures/cancellation.
- Typed detail mapping intersects server flags with status grants. Redacted arrays remain withheld (null), distinct from an authorized empty collection. Raw token presence or server flags alone never populate protected snapshots. Mixed applicant rows and ambiguous flags reject the full snapshot.
- The shared privacy controller binds to an opaque sign-in identity and selected canonical applicant, never email alone. Duplicate actions are blocked; status is checked before each reveal. Expiry, pause, replacement login, logout, applicant change, Cancel and disposal invalidate credentials/data/work. Independent hides discard data and suppress late responses. Ordinary staff token rotation preserves the sign-in scope.
- Local dashboard/module permission denial and password-change/incomplete-session denial dispatch no request. Backend permission enforcement still requires test accounts.
- All newly accepted routes are explicit and path identifiers reuse existing conservative validation. No arbitrary query string, path traversal, new permission name for privacy sections, JWT-claim interpretation, or new mutation route was invented.
- HTTP/business outcomes stay distinct. Invalid response envelopes cannot authorize success. OTP POSTs are never automatically replayed after error, rate limiting, timeout or cancellation.
- Owner-confirmed pending not-found behavior is scoped to Privacy + specific pending lookup + HTTP 401/business 2004. It yields no record/documents and does not relax other HTTP 401, attachment, contract or staff-session rules. Raw transport evidence is retained.
- Both hosts retain the replacement Privacy sign-in guard pending renewal/full workflow acceptance. The new parallel screen uses an existing protected staff session and exact configured root, with no environment fallback or shared cookies. Existing application masks are unchanged and must not be treated as backend privacy authorization.
- Both new views remove rendered fields and dialog inputs on state invalidation/navigation. Screen state clears OTP before awaiting Verify; disposal cancels late work. Web conditionally renders sensitive text; MAUI clears removed labels. Actual browser/native-device layout, keyboard and accessibility acceptance remains pending.
- TLS verification remains enabled. HTTP is still rejected. No certificate bypass, device permission broadening, signing change, database access or schema change was introduced.

## Unresolved / untested

| Risk | Status / effect | Required evidence or action |
|---|---|---|
| Privacy UI/session lifecycle | Parallel screens wired; core state and four actual web-render states synthetic-tested | Live browser/native-device focus, lifecycle and authorized/denied synthetic-record acceptance remain pending |
| Response schemas | Observed samples mapped and mock-tested | Dashboard, status, four sections and two detail bodies supplied; full backend schema, variant nullability/failures and dashboard periods remain unverified |
| ID resolution | Deferred bridge; not needed by new screen | New screen takes citizen ID directly; establish relation only before connecting old reference lookup |
| Authentication on selected server | User reported successful login and both exact cookie names | Synthetic complete-session mapping passes; actual renewal/rotation and full UI acceptance remain unverified |
| PII token lifetime/authorizedSections | Core status/controller expiry and revalidation tests pass | UTC expiry and claims observed; live revocation/server enforcement and UI clearing remain untested |
| Backend role/claim enforcement | Not tested | Allowed/denied synthetic accounts for VIEWDASHBOARD, module access and four privacy claims |
| New API root compatibility for retained auth helpers | Not tested | Verify source-backed resend/refresh/recovery/change-password availability without deleting them |
| Live server availability | Partially observed | Supplied preproduction address has an authentication boundary; user completed manual login and supplied dashboard response; assistant-authenticated workflow tests remain pending |
| Legacy OTP query credential | Deferred facade; query value never copied | Prefer documented modern privacy calls; do not put secrets into URLs |
| Raw collection script logging | Not executed/copied | Do not run its response-JSON logging on real personal data |
| Sensitive response retention | No new persistence; raw ApiResult contains sensitive material | Future callers must scope/discard results, avoid logging, cancel stale requests and clear data on logout/expiry |
| Existing native embedded settings | Existing deployment limitation | Keep tracked placeholders; establish protected deployment configuration before packaging real secrets |
| Existing toolchain / hardware / accessibility acceptance | Outside this implementation | Review existing docs/setup.md and acceptance documents before any production claim |

No identity values, biometric images, document images or real applicant data were copied into fixtures or the handover. The assistant made an unauthenticated API HEAD probe; authenticated observations came from the user's manual tests. Existing application business rules, status transitions, database behavior and offline/capture/reporting integrations were not changed; preservation is supported by the focused diff and original 71 passing synthetic tests, not by completed live backend/UI/device acceptance.

No performance improvement is claimed or measured. The new stateless adapters do not allocate per-call HttpClient instances. PrivacyWorkspace owns a dedicated client per web circuit/installed app and releases active controllers before the client. No dependency security audit or full repository penetration test was performed.

Final focused audit accounted for all 24 changed files, checked the source diff and the complete generated inventory, confirmed the collection hash, found no broken local handover links, and found no matches for known populated collection credential/header literals in changed files. No package/SDK manifest or staged-file changes were present. `git diff --check` passed. This literal comparison does not establish an exhaustive secret scan or comprehensive application security review.

Dashboard follow-up adds typed numeric/activity mapping and six synthetic tests without retaining deployment statistics. All metrics must be present and valid; invalid data cannot become zero or sample data. The earlier 24-file audit is a historical snapshot; the updated inventory includes the saved guide, dashboard implementation/tests and response-evidence report. No UI guard was removed and no previously exposed credential was used.

Privacy/status/detail follow-up: 106 synthetic tests pass; full solution and optional Android Compile pass. Focused accounting covers 33 task files, no missing inventory entries or broken local handover links, zero JWT-like credential literals, matching collection hash, no manifest/staged changes and passing diff whitespace checks. Source personal values were not copied into fixtures. Earlier 24/28-file audits remain historical. No assistant-authenticated workflow or UI lifecycle acceptance is claimed.

Controller follow-up supersedes those counts: 118 synthetic tests pass and 36 task files are accounted for. Focused checks again found no missing inventory entry, broken local handover link, JWT-like credential literal or package/SDK manifest/staged change; source collection hash matches and diff whitespace passes. Normal build output was locked by the running desktop app; isolated full solution validation and Android Compile passed without stopping it. Tests cover core privacy lifecycle, not completed screen/live enforcement. No new UI, device, database, deployment or credential-use authorization is inferred.

Pending-lookup clarification supersedes current counts: 119 tests pass and all 38 task files are inventoried. Isolated solution and Android Compile pass. Focused checks again find no missing inventory entry, broken local handover link, JWT-like credential literal or manifest/staged change, with a passing tracked whitespace check. The scoped business-code adapter grants no access; canonical ID mapping remains unresolved because the user reports citizenid absent from the list.

The parallel-screen milestone supersedes those historical counts: 128/128 core tests and four actual web-component HTML states pass; web/Windows solution and Android Compile pass. All 52 task files are inventoried, no local documentation links are broken, no JWT-like literals or package/SDK/manifest/existing settings/staged changes are found, source collection hash matches and whitespace check passes. The new screen takes citizen ID directly; no ID alias or lookup fallback is added. Live browser/native/server-enforcement acceptance remains pending.
