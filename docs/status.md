# Delivery status — 2026-10-15

**First release is incomplete. No platform is claimed complete.** The deliverable is a source-backed authentication/session migration foundation and evidence inventory.

## Implemented

- Core HTTPS/API-root validation, separate API/feature settings, INI fallback diagnostics and theme parsing.
- Typed transport result with HTTP/business/body status separation, exact cookie names/values, one request per action, and no certificate bypass or sensitive console logging.
- Source-backed request OTP, verify, resend, refresh, terminate, forgot/change password adapter and conservative response handling.
- Credential clearing, generic credential errors, source-mapped locked/inactive outcomes, six-digit OTP and 60-second cooldown.
- Required password-change gating, `CPW` checks, role password lengths, and return-to-login behavior.
- Five-second active session scheduler, no overlapping refreshes, coordinated token updates, rejection/three-failure termination, generation invalidation, and resume validation gating.
- Backend-authority navigation registry, Blazor Interactive Server authentication UI and scoped sessions, MAUI Windows/Android authentication UI source and lifecycle hooks.
- MAUI screen layouts are now declared in MainPage.xaml, with action handlers in MainPage.xaml.cs. Login/OTP/recovery/password-change/dashboard layouts can be edited directly, including the permission-fed module item template. Shared authentication/session logic is unchanged; the Windows build and Android Compile target pass after conversion with zero warnings/errors. A new Android APK was not packaged for this conversion. Runtime/Hot Reload UI checks remain pending.
- Read-only Cardholder Verification pending queue (GET `cardholder/pending`) shared by web and MAUI: `PENDINGORS` gating, oldest-first numbered rows, refresh, loading/empty/error states, stale-reply discard after refresh/logout, and an unavailable fallback for invalid configuration. Web shows a table that stacks into cards on phones; MAUI shows one card per cardholder. OBS.Core, OBS.Web, Windows OBS.Maui, and the Android Compile target build with zero errors after this slice. Neither UI has been runtime-checked, and no live backend response has been seen.
- Shared wizard step/ID/address rules and saved-completion submission gate; these are not wired into record editing/persistence.
- Mandatory report footer/scope metadata contract; this is not a report exporter.
- Explicit blocked capture adapters and sanitized inventories for 99 collection requests and 10 RDLC templates.
- Windows FS88H Start/Stop live preview in the biometric overlay: background native reads, latest-frame display, transient retries, error alerts, and cancellation with device cleanup after the read exits. Frames stay in memory and do not mark tiles as captured. Validation, matching, saving and real-device acceptance remain pending; Reset is still a placeholder. The owner supplies the ignored x64 vendor DLL locally.

## Built and tested

- The current solution targets .NET 9 with SDK 9.0.301 and MAUI Controls 9.0.51 for Visual Studio 2022 17.14 compatibility. Windows OBS.Maui builds as x64; the existing People.xaml compiled-binding warning XC0025 remains.
- The opt-in Android Debug build also passes with no warnings/errors, using a workspace-local debug signing key. No emulator/device runtime acceptance was performed.
- A standalone Android Debug APK with managed assemblies embedded is available under artifacts/android/OBS_eKYC-android-debug.apk. ARM64/x64 payloads for both app and core were verified in the signed APK. It includes the latest session parser fix; physical installation and runtime checks remain unverified.
- The full default solution build passes; the existing People.xaml compiled-binding warning XC0025 remains. Visual Studio UI loading/debugging has not been directly tested.
- All **71 synthetic regression tests passed**. They cover authentication/session/permission rules, pending queues and applicant reveal/details, report contracts, grayscale BMP headers/palettes/row order/padding, and scanner Start/Stop, latest-frame retention, transient/fatal errors, cancellation, retries and exactly-once device cleanup. Scanner fakes exist only in tests; physical FS88H behavior is unverified.
- Session troubleshooting: module selection does not log out. A refresh rejection independently returns the UI to Login. Debug builds now emit only fixed failure categories and numeric HTTP/business codes under `OBS session refresh` in Visual Studio's Debug Output. Raw backend messages, identities, credentials, and token values are not logged. Unsupported refresh payloads have a separate user-facing explanation. Actual backend rejection causes still require runtime codes.
- OBS.Web now builds normally without the old .NET 10 assets override.
- Development host smoke: root/CSS/lifecycle script/framework JS all return HTTP 200. Browser login was **not** exercised.
- The source configuration and API paths were inspected without publishing deployment secrets. No authenticated live API requests were sent.

## Blocked by environment or integrations

- The earlier .NET 10 web/workload build blockers are resolved for local development by retargeting to the installed .NET 9 SDK/packages. Windows is the default target; Android is opt-in with EnableAndroidTarget.
- MAUI 9 is out of support. The owner-requested VS 2022 compatibility baseline requires an upgrade to a supported MAUI toolchain before production release; see docs/setup.md.
- Backend network probe fails with SocketException. No live CORS/cookie/session/permission/persistence verification is available.
- Hardware: physical FS88H and Topaz model/SDK/driver/license tests; source face engine/license/ICAO rules and Android engine support.
- Reports: package/platform/browser exporter proof, real viewers/printing tests, and approved field/layout mapping.

## Unimplemented / unfinished

- Enrollment/update forms, remote section saves/reloads and final submission; searchable paged record grids; read-only details/documents and permitted actions.
- Cardholder verification approve/reject/return actions, specific record view, reasons, and reports; residence verification queues/actions/reasons; initial KYC queues/result uploads; printing result tracking/uploads; issued/unclaimed/unprinted release workflows.
- Role Management, User Access, Audit Logs, About screen content and administrative API actions. Permission menu entries alone are not working screens.
- Camera/signature implementations; fingerprint validation, template extraction/matching and persistence; vendor adapter/device acceptance; file capture validation and temporary-file cleanup.
- Representative report preview/printing and PDF/DOCX/XLSX exporters with real footers/pagination across all three platforms.
- External desktop INI deployment, native responsive sidebar/multi-column forms, package signing/installers, complete accessibility/browser testing.
- Comprehensive workflow API contracts and acceptance verification. Existing source material provides more evidence to migrate these, but those migrations have not been completed here.

## Deferred / excluded

iOS, DigitalPersona, applicant accounts/My Applications, macOS/Linux, applicant self-service, offline drafts/synchronization, local database, advanced analytics, payments, physical card printing/drivers/encoding. No backend changes or new reporting endpoints were made.

Live test evidence, actual builds and runtime acceptance on Windows/Android/web, documented device tests, verified exports, and backend persistence are still required before release acceptance. Code generation, source comments, and synthetic fixtures don't satisfy those checks.
