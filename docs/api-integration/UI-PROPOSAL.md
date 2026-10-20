# Approved parallel Privacy Verification screen

The user approved the small email OTP dialog, then explicitly chose a **new screen in the same app**, retaining the existing reference search. Both web and MAUI now have a Privacy Verification menu item. The new screen takes a citizen ID directly, avoiding the unresolved reference-to-citizen-ID mapping. The existing application screens and eye controls retain their behavior.

1. Sign in through the existing configured staff login. Open Privacy Verification.
2. Enter an approved test citizen ID and select the applicant. This sends no request and does not look up a reference number.
3. Reveal one of Bio Details, Address, Emergency Contact or Educational Information. The small dialog opens without automatically sending email.
4. Choose Request code, enter the emailed six digits locally, then Verify. Duplicate actions are blocked while pending. No undocumented resend cooldown or automatic retry is added.
5. The controller captures the separate PII credential, checks server status and then fetches only the requested authorized section. Other sections remain independently hidden. Each subsequent reveal checks status again.
6. Hide discards that section's fields. Applicant edits, navigation, cancellation, expiry, pause, logout, password change and replacement sign-in clear privacy data and pending work. Staff token rotation triggers status revalidation; failed refresh diagnostics cancel privacy without changing existing staff-session rules.

Web uses a native modal dialog with Escape cancellation; Windows uses a root overlay with Escape/focus handling and Android Back cancels the dialog. Physical-device/browser keyboard and accessibility acceptance remains pending. Core state tests and actual web-component HTML rendering are local synthetic evidence only.

PrivacyWorkspace preserves the exact existing configured HTTPS API root/header. It does not switch the deployment, login contract or reference lookup. The full replacement Privacy sign-in guard remains until authentication/renewal and broader workflow compatibility are accepted. This parallel screen does not integrate dashboard metrics or the two new detail endpoints into the old workflows.

Design: [parallel screen design](../plans/2026-10-20-parallel-privacy-workspace-design.md). No production deployment, real-record testing, commit or push was performed.

The user then requested the same appearance as View Application Details. Both clients now match its profile cover/avatar, badges/address/map area, personal cards, Biometrics/Documents column and Actions/Quick Links. Web reuses application.css; native matches the existing card dimensions, typography and responsive column thresholds. A compact citizen-ID selector remains above the record, and OTP authorization is preserved. Detail buttons expose full authorized field collections and close/clear on hide, revocation, selection/session changes and disposal. Missing photo/email/phone/card/document/capture data is not invented; related controls show unavailable states. Nine component-render states and three exact viewport sizes pass local synthetic checks; native-device/live acceptance remains pending.
