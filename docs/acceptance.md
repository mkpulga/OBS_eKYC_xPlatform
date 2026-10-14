# Manual acceptance checklist

Every item below remains unchecked unless separately identified in automated tests. Run against an authorized non-production environment, using synthetic applicants/test staff. Record platform, SDK/app build, backend contract, role authorities, device/driver version, observed result, and date. Never save passwords, OTPs, bearer/refresh/XSRF values, or biometric data in test logs.

## Authentication and session

- [ ] Validate HTTPS URL/root/config before services; absent compatibility settings show diagnostics; API contract and feature set are independent.
- [ ] Request OTP with valid staff credentials; unknown email/wrong password show exactly "Invalid email or password.".
- [ ] Confirm locked/inactive mappings and approved wording with real backend responses.
- [ ] Refactored OTP accepts exactly six ASCII digits; wrong OTP stays correctable; Verify/Resend/Back work.
- [ ] Confirm 60-second local resend cooldown across midnight; authoritative throttling/no-pending-OTP errors are handled without automatic retries.
- [ ] First-login/expired-password require change; CPW denial returns safely to login; successful change requires re-login.
- [ ] Forgot password and change password use verified casing/header rules and backend validations.
- [ ] Refresh targets five seconds while foreground/visible; slow requests don't overlap; real runtime timing documented.
- [ ] Access/refresh/XSRF rotate consistently; exact cookie names/encoding/cookie-bearer requirements verified.
- [ ] Three consecutive transport/server failures terminate; success resets count; slow failures aren't promised to take exactly 15 seconds.
- [ ] Endpoint-specific code 3/400/401/403 rejection behavior verified. No takeover wording without reliable backend evidence.
- [ ] Logout while refresh is in-flight cannot restore session; clear login/OTP/session/form/document/dialog state.
- [ ] Voluntary logout asks "Are you sure you want to log out?"; Windows close asks "Are you sure you want to close the application?".
- [ ] Forced termination doesn't ask confirmation and closes protected UI. No second unsaved-change prompt.
- [ ] Browser hidden/tab-close/mobile OS termination/resume behavior documented; failed resume blocks protected work until a successful validation.
- [ ] Windows single instance works as a normal user without admin elevation.

## Permissions and workflows

- [ ] Separate staff circuits/accounts never share credentials, handlers, cookies, token state, or protected views.
- [ ] Confirm backend enforcement with allowed and denied role accounts, not only hidden buttons.
- [ ] Use actual module/action identifiers for navigation and every read/mutation; confirm audit and update action mappings.
- [ ] Stable record action identifiers survive search/sort/page changes; each detail/action targets the selected backend record.
- [ ] Paged grids provide loading/empty/error states and minimal permitted personal information.
- [ ] Wizard names/progress match the feature set; StandAlone excludes Education/Additional Requirements.
- [ ] Step navigation neither requires Save nor automatically saves. Editing/saving/submission are separately explained.
- [ ] Explicit section save persists via the existing API, reloads correctly, and updates persisted completion only.
- [ ] Required Civil Status, applicable Street/Address Line 1, address aggregate 90-character boundary, contact/document formats and verified lengths enforced.
- [ ] Region/Province/City/Barangay cascades use verified lookup identifiers and clear dependent invalid selections.
- [ ] Back ID required only for listed types; source/file-size/type rules established and tested.
- [ ] Submit explains missing saved requirements and remains disabled until the verified backend workflow is satisfied.
- [ ] Android/web can submit only with required previously persisted Windows fingerprint/captures; unsupported local capture doesn't waive requirements.
- [ ] Cardholder approve/return/reject and residence approve/revisit/reject/reasons use permitted transitions; Bacoor Skip Home Visit only where verified.
- [ ] Initial KYC result uploads, printing-result uploads, ready-for-printing tracking, and ID release details persist correctly without physical printing/encoding.
- [ ] View Details/documents obey permissions; raw fingerprint data isn't unnecessarily exposed.
- [ ] Role/User/Audit screens and actions enforce backend permissions; no applicant/My Applications features.
- [ ] Concurrency behavior follows existing contract; no invented version headers or automatic mutation retries.

## Hardware

- [ ] Windows FS88H vendor SDK/driver/template-image format and required fingers/counts/quality/duplicate matching verified end to end.
- [ ] Exact Topaz model/SDK/license/redistribution and backend signature format verified; Windows pad and mouse drawing both tested.
- [ ] Windows webcam and Android camera use the identified licensed engine and approved ICAO/quality rules; failed validation cannot persist as success.
- [ ] Android touchscreen signature encoding/nonblank checks and persistence tested.
- [ ] No Android fingerprint or web face/fingerprint/signature capture. Missing SDK shows a blocked capability, never simulated success.

## Reports and delivery

- [ ] Representative list and individual report proven on Windows, Android, and browser-side generation before committing exporter libraries.
- [ ] All required PDF/Word/Excel formats, preview and supported printing verified against RDLC references and API field mapping.
- [ ] Scope explicitly selected record/current page/all filtered; all-filtered result is complete, not mislabeled partial page.
- [ ] Every generated page has `<Module> – Generated by <operator email> | <date> | Page X of Y`; no disable switch.
- [ ] Asia/Manila UTC+08 timestamp and actual format-specific page totals verified; DOCX PAGE/NUMPAGES fields refresh accurately; XLSX print preview/pages show footer.
- [ ] Word/Excel remain editable; no tamper-proof claim. Required sensitive document inclusion is permission-controlled.
- [ ] Export requests/spool output are distinguished from prohibited local database/offline drafts; temporary files are cleaned where controlled.
- [ ] Accessible labels, keyboard/focus/screen-reader behavior, contrast, responsive layouts and 44x44 touch targets tested on actual clients.
- [ ] Source/config/log scans show no production secrets, plaintext password files, token/biometric/PII logs, or certificate-validation bypass.
- [ ] Normal builds/packages and installation/runtime checks pass for every claimed platform, including device/export/persistence acceptance.
- [ ] Exact dependency licenses/notices, redistribution, SDK/driver versions and remaining blockers are recorded; iOS remains labeled unimplemented/unverified.
