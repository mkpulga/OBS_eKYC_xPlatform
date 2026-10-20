# Testing and reproducible verification

## Current View Application Details visual match

128/128 core tests pass; nine actual web-component states pass after the requested layout alignment. The ignored HtmlRenderer harness also renders the unchanged ViewApplication component with synthetic values to compare profile/personal/files/sidebar structure. Added states cover full authorized collections, section-detail dialogs and their clearing on hide/server rejection. No new package or production test route was added.

Headless rendering used a fresh isolated profile and exact 390/1000/1680 viewport emulation. Measured columns: one/two/four; document width never exceeded viewport. Light, dark, mobile and open-dialog screenshots were inspected. Ignored evidence: ui-render/layout-measurements.json and desktop.png/mobile.png/tablet.png/dark.png/dialog.png. Window-size-only CLI capture initially cropped the minimum browser viewport; final emulated measurements replaced those mobile/desktop captures. These checks establish local web rendering, not live staff/browser/circuit/device acceptance.

Current logs under artifacts/api-integration/2026-10-20/: privacy-layout-tests.log, privacy-layout-web-render.log, privacy-layout-web-build.log, privacy-layout-solution-build.log, privacy-layout-android-restore.log and privacy-layout-android-compile.log. The isolated solution and Android Compile pass; only the existing People.xaml XC0025 warning remains in the solution. An initial native local-variable naming collision was fixed before the passing build. Earlier four-state and other counts below are historical.

## Earlier parallel Privacy Verification screen — 128 core tests

- Core executable: **128/128 pass** with synthetic fixtures; log privacy-workspace-tests.log. Nine new cases cover exact existing-root/header preservation, no automatic calls, explicit OTP/status/reveal sequence, raw dates/ordered rows, independent hides, edited IDs, cancellation, expiry/session changes, stale verification, rotation/status failure and fresh screen lifetime.
- Actual web-component HTML rendering: **4/4 states pass** (locked, dialog, verified selected section, cleared after logout). The ignored local ui-render/Harness.csproj uses HtmlRenderer, the real component, shared fixtures and synthetic in-memory HTTP. Log privacy-workspace-web-render.log; generated HTML is synthetic only. Initial harness build had an ambiguous type name, corrected with an explicit alias. This is not browser JS/focus/device acceptance.
- Isolated web/Windows solution build passes with existing People.xaml:14 XC0025; log privacy-workspace-solution-build.log. Running desktop app left open.
- Android restore followed immediately by Compile passes with zero warnings/errors; logs privacy-workspace-android-restore.log and privacy-workspace-android-compile.log. No new APK/install/device test.
- Logs/harness live under ignored artifacts/api-integration/2026-10-20/. Earlier counts below are historical. No supplied credentials, identifiers or personal response values are in tests/harness. No production/API-authenticated test was performed.

The first new test run found three incorrect test expectations (an invalid legacy root suffix, omitted privacy route segment and mismatched synthetic birth-date value). Expectations were corrected against existing source/fixtures; the subsequent complete run passed. No production behavior was changed to satisfy those expectations.

Date: 2026-10-20 (Asia/Manila). All executed builds/tests used Windows and pinned .NET SDK 9.0.301. Automated regressions use synthetic fixtures. The assistant has not executed authenticated API calls, browser login, installed-app, emulator or hardware workflows; unauthenticated probes and user-reported Postman observations are recorded separately below.

## Pending-lookup clarification — current 119-test results

User clarified the observed specific pending lookup's HTTP 401/business 2004 as no matching pending application. A narrowly scoped Privacy-only NotFound mapping and one parameterized regression were added. Current tests: **119/119 pass**. Negative cases preserve other 401 codes, other HTTP statuses, existing contracts and attachment handling; not-found performs no document call or data display. No global 401 behavior is relaxed.

Isolated solution build passes with existing XC0025 warning. Android validation follows the existing immediate restore/Compile sequence; see verification-results.json. Current logs use pending-lookup-tests.log, pending-lookup-isolated-solution-build.log, pending-lookup-android-restore.log and pending-lookup-android-compile.log. Earlier controller/mapping logs are historical. User also reports citizenid absent from the pending list; no alias/type coercion is implemented without mapping evidence.

## Shared privacy controller — historical 118-test results

- `dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj --no-restore`: **118/118 pass**, including all earlier 106 cases plus 12 controller cases.
- Normal `dotnet build OBS_eKYC.sln --no-restore`: failed with MSB3027/MSB3021, 2 errors and 11 warnings because the running OBS.Maui.exe locks the normal output OBS.Core.dll. No process was stopped.
- `dotnet build OBS_eKYC.sln --artifacts-path artifacts/api-integration/2026-10-20/controller-isolated`: full solution passed with zero errors and the existing XC0025 warning. This uses SDK-supported isolated output/restore with unchanged declared dependency versions; no project/toolchain workaround.
- After the final hide/denial race fix, the same isolated solution build with --no-restore passed incrementally with zero warnings/errors, and 118/118 tests passed again. The current isolated-build log records this latest incremental check; the initial full-build result is retained in the tool execution history.
- Android restore followed immediately by the existing optional-target Compile command: pass, zero errors/warnings. No new APK/device test.

Current local logs use privacy-controller-tests.log, privacy-controller-solution-build.log (normal-output lock), privacy-controller-isolated-solution-build.log, privacy-controller-android-restore.log and privacy-controller-android-compile.log. All logs and isolated outputs are ignored artifacts.

Controller tests cover locked/selected/session prerequisites, duplicate requests/verification, six digits without a fabricated resend cooldown, cookie then status before grant, one-section fetch/status order, independent hide/discard, expiry with delayed timer callbacks, pause/logout/password/replacement-login clearing, ordinary token rotation, stale verification across applicant changes, preservation of newer busy state, late protected reads after Cancel/disposal/logout/change, hiding during a pending read, revoked claims/shortened expiry and fail-closed privacy errors without replaying OTP or silently terminating staff. Fixtures and timers are synthetic; this does not establish UI-copy clearing or server enforcement.

Latest manual lookup evidence: user confirmed HTTP 401 is paired with code 2004/Pending application not found for the original approved reference. A separate successful pending-detail sample concerns a different applicant. No canonical ID alias or diagnosis is inferred. The assistant issued no authenticated request. Historical results follow.

## Privacy/status/detail follow-up — historical 106-test results

- Core regression command with --no-restore: **106/106 pass**, including original 71, 13 privacy contract/login cases, 6 dashboard cases and 16 privacy/status/section/detail cases.
- Full solution build with --no-restore: pass, zero errors and the existing People.xaml XC0025 warning.
- Optional Android target restore immediately before Compile: pass, zero warnings/errors; same commands below, no package version changes or APK/device acceptance.
- Current ignored logs use privacy-core-tests.log, privacy-solution-build.log, privacy-android-restore.log and privacy-android-compile.log under artifacts/api-integration/2026-10-20/.

New tests use only invented values. They cover observed bare-success PII cookie capture, complete login with both exact cookies, UTC/offset/fractional and invalid/local expiries, exact individual claims, wrong applicants, malformed/duplicate fields, nullable text, multiple addresses, withheld versus authorized empty data, detail flags intersected with status claims, module denial and stale/cancelled responses. No protected snapshot survives an error or late expiry. These checks do not implement UI state clearing or establish server revocation/enforcement.

The user confirmed HTTP 200/business 0 verification status and exact PII/login cookie names. Section/detail bodies were supplied without HTTP statuses. Their structures are evidence of samples, not a full formal schema. The assistant made no authenticated request. Historical results follow.

## Dashboard follow-up — historical 89-test results

The user supplied dashboard JSON after manual Postman testing; the assistant did not run an authenticated request. The dashboard HTTP status was not provided. The earlier assistant unauthenticated HEAD probe returned 401. These observations are separate from the following local tests.

- `dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj --no-restore`: 89/89 pass after typed dashboard mapping and six new top-level tests.
- `dotnet build OBS_eKYC.sln --no-restore`: pass; zero errors; existing XC0025 warning.
- `dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj --no-build --no-restore`: 89/89 pass after the build.
- `dotnet restore src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true`, then `dotnet build src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true -f net9.0-android -t:Compile --no-restore`: pass, zero warnings/errors in Compile.

New local logs are `artifacts/api-integration/2026-10-20/dashboard-core-tests.log`, `dashboard-solution-build.log`, `dashboard-android-restore.log`, and `dashboard-android-compile.log`. Earlier 83-test logs are retained as historical evidence.

The new tests use synthetic values with the observed shape. They cover card/chart mapping, server-percentage retention, missing/null/negative/fractional/overflow/incorrect-type metrics, ambiguous fields, malformed activity/envelopes, explicit zero and empty activity, one-call permission-gated requests, HTTP/business errors and cancellation. They do not validate live date-range semantics or display real server metrics in either UI.

The results below describe the initial adapter milestone; current follow-up results above supersede its final-test count.

## Actual results

| Command | Result | Classification |
|---|---|---|
| `dotnet --info` | SDK 9.0.301; installed Windows/Android workloads | Locally validated toolchain |
| `dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj --no-restore` before changes | 71/71 pass, exit 0 | Mock-tested baseline |
| `dotnet build OBS_eKYC.sln --no-restore` before changes | Pass, exit 0; existing People.xaml:14 XC0025 warning | Locally validated baseline |
| `./scripts/Inspect-PrivacyContract.ps1 -CollectionPath 'D:\Downloads\OBS - Privacy PII OTP & Dashboard.postman_collection.json'` | 16 classified requests; no sensitive source values exported | Locally validated inventory |
| `dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj --no-restore` after adapters/tests | 83/83 pass, exit 0 | Mock-tested |
| `dotnet build OBS_eKYC.sln --no-restore` after all source changes | Pass, exit 0; zero errors; existing XC0025 warning | Locally validated web/Windows build |
| `dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj --no-build --no-restore` after build | 83/83 pass, exit 0; log saved | Mock-tested final regression |
| `dotnet build src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true -f net9.0-android -t:Compile --no-restore` before optional-target restore | Failed, exit 1; 57 missing MAUI/framework-reference errors | Environment/restore-related; resolved below |
| `dotnet restore src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true` | Succeeded, exit 0; same declared package versions | Locally validated optional-target restore |
| Same Android Compile command after restore | Pass, exit 0; zero warnings/errors | Locally validated Android compilation only |
| `git diff --check` | Pass, exit 0; Git CRLF conversion notices only | Locally validated tracked diff whitespace |

The first inventory-generator run also failed on a bodyless-request null access. The generator was repaired to handle absent bodies, then successfully regenerated and reviewed all 16 entries. This was a tooling implementation error, not a backend observation. The first test command was repeated when combined tool output had been truncated; final results were captured separately.

Android Compile failed a second time with the same 57 missing-reference errors when invoked after a subsequent Windows solution build. Rerunning the Android restore immediately before Compile passed again. The documented reproduction order therefore restores Android immediately before that check. The root cause of this restore/cache sensitivity was not established; no project, dependency or machine setting was changed to hide it. The second failure and recovery logs are retained.

## What the twelve new cases test

- New/old root normalization, explicit opt-in, INI parsing, HTTP/root/header-collision rejection.
- Exact privacy request/verify field names, six-digit validation, headers and methods.
- All eight new GET paths, PII/staff token separation and bodyless GETs.
- No absent-token fabrication for redacted detail/status reads.
- VIEWDASHBOARD and existing module permission checks; password-state/session/unsupported-contract denials.
- Identifier validation and unsafe path/query rejection without dispatch.
- Verified-cookie extraction failures, business-success checks and redacted token diagnostics.
- HTTP 401/403/429/500, business failure, non-object/empty/invalid JSON handling.
- Controlled cancellation/timeout/transport failure; one OTP mutation per invocation with no retry.
- Selected-contract login serialization and fail-closed incomplete session parsing.
- Selected-contract bodyless logout; retained existing logout behavior remains covered by original tests.
- Exact POST allowlist and rejection of PII tokens on dashboard/pending/authentication routes.

Fixtures are synthetic transport responses. An empty data array used for routing tests does not establish a real dashboard/section response schema. No test claims backend cookie/JWT validation, expiry/revocation, persistence, email delivery, role enforcement, cross-applicant security or completed UI interaction.

## Local evidence files

- `artifacts/api-integration/2026-10-20/core-tests.log`
- `artifacts/api-integration/2026-10-20/solution-build.log`
- `artifacts/api-integration/2026-10-20/android-compile.log`
- `artifacts/api-integration/2026-10-20/android-restore.log`
- `artifacts/api-integration/2026-10-20/android-compile-after-windows-failure.log`
- `docs/api-integration/verification-results.json`

Artifacts are ignored by Git and local only. The machine-readable summary is versionable and contains no sensitive payloads.

## Reproduce from the repository root

Prerequisites: Windows, SDK selected by global.json (9.0.301), installed MAUI Windows tooling; Android workload/SDK/JDK only for the opt-in target. Existing package versions are unchanged. A dependency restore may need the approved package feed/cache.

```powershell
dotnet restore OBS_eKYC.sln
dotnet build OBS_eKYC.sln --no-restore
dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj --no-build --no-restore
dotnet restore src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true
dotnet build src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true -f net9.0-android -t:Compile --no-restore
./scripts/Inspect-PrivacyContract.ps1 -CollectionPath '<path-to-selected-collection>'
git diff --check
```

The first general restore command above is a reproduction prerequisite, not a claim it was executed during this session. Android Compile does not build/install a fresh APK or run the app. No signed release or deployment was performed.

## Remaining acceptance

Use the approved synthetic test backend and a fresh existing staff session to verify the implemented parallel screen's email OTP/status/sections, denied/expired/revoked access, renewal, expiry/logout/applicant-change clearing, duplicate actions, cancellation, navigation and web/native layouts/focus/accessibility. Keep credentials and applicant values local. Two detail workflows, dashboard period/binding and a future reference-ID bridge remain separate work. The new dialog and lifecycle are implemented and locally tested; live acceptance is still required. No production or real-record testing is authorized by these instructions.
