# Modified-file and dependency inventory

The initial working tree was clean at main / 6af963b. All files listed below belong to this task; nothing was staged or committed. The working branch is `codex/api-integration-audit`.

| File | Change and reason | Risk / limit |
|---|---|---|
| `src/OBS.Core/PrivacyWorkspace.cs` | Own same-root dedicated client and create fresh screen controllers | No mutation of existing root/settings/login; scoped web/singleton native; disposal clears active controllers first |
| `src/OBS.Core/PrivacyScreenState.cs` | Shared direct-ID selection, explicit OTP dialog/actions, session hooks and stale-completion protection | No lookup alias, automatic OTP/retry, persistence or new polling timer |
| `src/OBS.Core/PrivacyPresentation.cs` | Map currently visible minimal snapshots to ordered display fields | Raw dates preserved; visibility rechecked; views discard rendered copies |
| `src/OBS.Web/Components/PrivacyVerification.razor` | New screen now matches View Application Details with existing application classes; fresh authorized section-detail dialogs | Existing application view unchanged; OTP gating retained; live acceptance pending |
| `src/OBS.Web/Components/Dashboard.razor` | Parallel menu and exclusive screen selection | Old reference search/navigation preserved; component disposed on leaving |
| `src/OBS.Web/Components/App.razor` | Load scoped privacy CSS and small native-dialog helper | Existing assets retained |
| `src/OBS.Web/wwwroot/privacy.css` | Small selector/mask/modal additions; record layout reuses unchanged application.css | Exact 390/1000/1680 viewport previews checked; no changes to existing record styling |
| `src/OBS.Web/wwwroot/privacy.js` | Native dialog open/Escape cancellation | No tokens/PII/logging; live keyboard acceptance pending |
| `src/OBS.Maui/PrivacyVerificationView.cs` | Match existing details layout/dimensions; nested label clearing and root-hosted OTP/section dialogs | Main-thread updates, navigation disposal, Windows Escape/Back; device QA pending |
| `src/OBS.Maui/DashboardView.xaml` | Hosts for parallel screen and full-root dialog | Old search/application layout retained |
| `src/OBS.Maui/DashboardView.xaml.cs` | Parallel menu, screen construction/disposal, overlay ownership | Existing modules/search retain behavior |
| `src/OBS.Maui/MainPage.xaml.cs` | Inject workspace and cancel dialog on Android Back | Existing lifecycle/session behavior retained |
| `tests/OBS.Core.Tests/PrivacyWorkspaceTests.cs` | Nine root/screen/lifecycle/presentation cases | Synthetic fixtures and in-memory HTTP only |
| `docs/plans/2026-10-20-parallel-privacy-workspace-design.md` | Record approved same-app parallel approach and acceptance limits | No commit/deployment or extra approval inferred |
| `src/OBS.Core/ApplicationRecord.cs` | Handle owner-confirmed HTTP 401/business 2004 as NotFound only for Privacy pending-detail lookup | Other statuses/contracts/routes preserve authorization behavior; no personal data or document request on not-found |
| `tests/OBS.Core.Tests/ApplicationRecordTests.cs` | One parameterized regression for the scoped pending not-found rule and negative contract/attachment cases | Synthetic responses only; no global 401 override |
| `src/OBS.Core/ApplicantPrivacyController.cs` | Session/applicant-bound OTP state, serialized actions, status-before-reveal, independent hides, absolute expiry and stale-response protection; clear selection and notify disposal | Used by both new views; owners clear UI copies on Changed and dispose per screen |
| `src/OBS.Core/SessionController.cs` | Internal opaque sign-in identity retained across token rotation and replaced/cleared on sign-in/logout | Existing authentication, permissions and renewal rules unchanged; lifecycle covered by original and new tests |
| `tests/OBS.Core.Tests/ApplicantPrivacyControllerTests.cs` | Twelve synthetic lifecycle/concurrency/expiry/revocation/session tests with a controllable timer and delayed HTTP replies | No real accounts, applicant values, live server requests or screen acceptance |
| `src/OBS.Core/PrivacyVerification.cs` | Map observed status fields into immutable applicant/expiry/exact-claim metadata and explicit outcomes | Does not own staff session or revoke/clear UI state |
| `src/OBS.Core/ProtectedApplicant.cs` | Map observed four protected arrays with minimal display models and matching numeric IDs | No ambiguous-date parsing, account/ID/security-answer retention or UI caller |
| `src/OBS.Core/ApplicantDetailApi.cs` | Map baseline identity/contact/classification/module values and authorized/redacted detail shapes | Protected output requires both status claims and server flags; canonical lookup unresolved |
| `tests/OBS.Core.Tests/PrivacyResponseTests.cs` | Sixteen synthetic status/section/detail validation and authorization cases | No supplied personal values or authenticated assistant API requests |
| `docs/api-integration/PRIVACY-EVIDENCE.md` | Field/type provenance, cookie/status divergence, section/detail mapping and remaining limits | No source personal values, credentials or full response bodies |
| `src/OBS.Core/DashboardApi.cs` | Map user-observed dashboard fields into existing metric/activity models; strict parsing and typed outcomes | No real statistics retained; no screen binding or guessed period filter |
| `tests/OBS.Core.Tests/DashboardApiTests.cs` | Six synthetic dashboard mapping/validation/authorization/error cases | Response structure evidence from user; no assistant-authenticated API call |
| `docs/api-integration/DASHBOARD-EVIDENCE.md` | Record field-level evidence, mapping, client validation and remaining limits | Excludes deployment statistics and credentials |
| `plans/provide-api-backend-details-plan.md` | User-requested saved guide, updated with current Postman steps | Earlier steps marked as history; keep secrets/IDs local |
| `src/OBS.Core/ApiTransport.cs` | Allow documented privacy/detail/dashboard reads and privacy OTP POSTs; bodyless POST support; separate PII header and local denial category; exact POST allowlist; invalid root-JSON rejection | Shared transport: original tests retained; no retries or legacy caller removal |
| `src/OBS.Core/Configuration.cs` | Add explicit Privacy contract and API-root suffix selection; prohibit PII configured-header collision | Defaults/enum ordinals retained; new mode requires explicit `/api/v1`; HTTPS unchanged |
| `src/OBS.Core/Authentication.cs` | Reuse source-backed Refactored resend shape for Privacy when used at core level | Selected-server resend availability Not tested; no new endpoint |
| `src/OBS.Core/SourceAuthenticationGateway.cs` | Permit Privacy core authentication request construction; bodyless bearer-only logout variant | Existing session parser retained; selected-server full auth remains unverified |
| `src/OBS.Core/PrivacyApi.cs` | Ten raw adapters plus typed dashboard/status/section/detail helpers, observed bare-success cookie extraction and pre/post-request expiry checks | Seven new screen request callers; dashboard/two detail bindings and live acceptance pending |
| `src/OBS.Web/Program.cs` | Retain replacement Privacy sign-in guard; register scoped parallel workspace | Existing contracts/settings retained; workspace uses same exact root |
| `src/OBS.Maui/MauiProgram.cs` | Retain replacement sign-in guard; register native workspace | No embedded settings, manifest, permission or dependency changes |
| `tests/OBS.Core.Tests/Program.cs` | Register new regression cases | Original 71 tests retained |
| `tests/OBS.Core.Tests/PrivacyApiTests.cs` | Thirteen synthetic contract/security/transport/login cases; extend extraction for observed bare success | Mocks use invented values; complete login format observed by user, renewal unverified |
| `config/privacy-api.ini.example` | Placeholder-only opt-in configuration | No actual deployment values; native external loader remains unavailable |
| `scripts/Inspect-PrivacyContract.ps1` | Reproducible full 16-request sanitized inventory/classification | Intentionally scoped to the selected collection; exports no source values/scripts |
| `README.md` | Link latest selected-contract handover and incomplete status | Earlier migration documentation preserved |
| `docs/api-mapping.md` | Distinguish earlier 99-request evidence from newly selected 16-request scope | Historical source evidence retained |
| `docs/api-integration/README.md` | Executive summary, coverage, status and next steps | Explicitly incomplete; no production claim |
| `docs/api-integration/TECHNICAL.md` | Architecture, authentication, transport and mapping decisions | Distinguishes known formats from missing schemas |
| `docs/api-integration/ENDPOINT-MAPPING.md` | One classification/evidence/implementation entry per selected request | Separates core implementation, screen blockers and verification |
| `docs/api-integration/endpoint-inventory.json` | Generated sanitized headers/body types/variables/scripts metadata and provenance hash | No credentials/body values/raw responses |
| `docs/api-integration/ENVIRONMENT.md` | Explicit roots/configuration, prerequisites, protected setup and backend evidence request | Example settings do not enable the incomplete client mode |
| `docs/api-integration/TESTING.md` | Actual commands/results, initial failure/recovery, reproduction and remaining tests | Builds and mocks do not establish live behavior |
| `docs/api-integration/verification-results.json` | Machine-readable verified counts/status/commands | No sensitive test payloads |
| `docs/api-integration/CHANGE-INVENTORY.md` | Full changed-file accounting and unchanged dependency inventory | Includes this handover file itself |
| `docs/api-integration/SECURITY.md` | Implemented safeguards and unresolved security/compatibility risks | No claim of full security audit or backend enforcement |
| `docs/api-integration/UI-PROPOSAL.md` | Approved minimal privacy OTP dialog interaction | Approval recorded; implementation requires backend evidence |
| `docs/api-integration/PROGRESS.md` | Milestones, Git/baseline/current evidence, blockers and exact recovery sequence | Must be read with current Git state before resuming |

This inventory covers 52 task files after the parallel-screen implementation. Local ignored logs, synthetic HTML-render harness/previews and isolated build outputs under artifacts/api-integration/2026-10-20/ are additional evidence, not tracked source. Generated bin/obj and restored package files remain ignored.

## Dependencies

**None added, upgraded, removed, or replaced.** No project/package manifest, dependency lockfile, SDK pin, Directory.Build.props, CI workflow, or generated source-of-truth setting was changed. The optional Android target was restored using existing declared versions to repair missing build references. No machine workload or package-source settings were changed. No dependency license change or new transitive-dependency decision was required.

## Compatibility scope

The approved parallel screen adds a menu, two hosts/views, scoped styles and a dialog helper. The shared business navigation registry, existing search/application view source, database schema/persistence, status transitions, eligibility/classification rules, biometric implementation, permission manifests, report templates, secret files, signing/release settings and unrelated integration callers remain unchanged. Focused diff and original tests support source-level compatibility; live UI/backend/device acceptance remains pending.
