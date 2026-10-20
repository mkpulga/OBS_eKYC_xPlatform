# Privacy PII OTP & Dashboard integration handover

**Citizen ID search update:** both dashboard search dropdowns now append IG CID and CU CID, visible only with VINFORMATIONGATHERING and VCARDHOLDERUPDATE respectively. The validated typed IG/CU response opens the existing View Application layout directly; its Citizen ID, reference number and source remain distinct. Refresh uses the same source endpoint. Protected sections reuse the existing OTP/controller/status/expiry flow, and missing ordinary fields/photos/documents show “Not available.” The existing application/reference searches retain their routing and permissions. See the [search design and validation scope](../plans/2026-10-20-citizen-id-dashboard-search-design.md). The earlier counts and remaining two-detail-workflow notes below are historical; both detail readers are now wired. Dashboard metric binding and live acceptance remain outstanding.

2026-10-20, Asia/Manila. The requested **parallel Privacy Verification screen is implemented** in web and MAUI. The broader selected-collection integration remains **incomplete**.

The new menu uses the existing staff session, takes a citizen ID directly, and opens the approved email-code dialog before fetching Bio Details, Address, Emergency Contact or Educational Information. At the user's request, it now matches View Application Details: profile cover/avatar/badges/address, personal-detail cards, Biometrics/Documents column and Actions/Quick Links sidebar, using the same web styling and native dimensions/breakpoints. Full authorized fields remain available through section detail buttons. Unavailable data/actions are labelled and disabled. Existing reference search and applicant screens retain their behavior; no reference/id/cid alias is assumed.

PrivacyWorkspace owns a dedicated cookie-free, redirect-free client and preserves the exact existing configured HTTPS API root/header. The replacement Privacy sign-in mode retains its startup guard pending session/renewal and full workflow acceptance. Do not switch configuration to Privacy merely to use this screen.

## Coverage and validation

| Category | Requests | Meaning |
|---|---:|---|
| Already supported | 2 | Existing login request and OTP formats; user supplied body/cookie evidence |
| Implemented | 11 | Core logout, privacy, detail and dashboard adapters |
| Deferred | 3 | Legacy OTP facades; selected collection prefers modern privacy flow |
| Total reviewed | 16 | All requests classified with sanitized provenance |

Request verification: **13 Mock-tested, 3 Not tested, 0 assistant-authenticated Live-verified**. Of ten new privacy/detail/dashboard request workflows, **seven are wired** into the parallel screen; dashboard and two detail integrations remain pending. Wiring and user-reported Postman samples do not establish live client acceptance.

- **128/128 synthetic core tests pass**, including the original 71 and nine new workspace/screen cases covering exact-root preservation, explicit OTP actions, mapping, independent hiding, cancellation, expiry, session changes and late replies.
- **9/9 actual web-component render states pass**, including protected detail-dialog clearing on hide and server denial. The real reference component is rendered with synthetic data to compare layout structure. Headless previews at 390/1000/1680 pixels confirm one/two/four columns without horizontal overflow; light/dark/modal images were inspected. These are local synthetic checks, with no production route or added dependency.
- Web/Windows solution build passes with the existing People.xaml XC0025 warning. Android Compile passes with zero warnings/errors after restore. No new APK, device test or installed-app restart was performed.
- No database, dependency version, SDK, manifest, existing deployment settings, commit, push or deployment change. The running desktop app was left open; builds used isolated output.

## Handover

- [Approved screen interaction](UI-PROPOSAL.md) and [design](../plans/2026-10-20-parallel-privacy-workspace-design.md)
- [Technical implementation](TECHNICAL.md), [environment/configuration](ENVIRONMENT.md), [security/compatibility](SECURITY.md)
- [Endpoint mapping](ENDPOINT-MAPPING.md) and [structured inventory](endpoint-inventory.json)
- [Dashboard evidence](DASHBOARD-EVIDENCE.md) and [privacy evidence](PRIVACY-EVIDENCE.md)
- [Testing/results/limits](TESTING.md) and [machine-readable results](verification-results.json)
- [Changed-file inventory](CHANGE-INVENTORY.md) and [progress/recovery](PROGRESS.md)

## Remaining acceptance

Test the new screen locally against the approved synthetic record using a fresh staff session, keeping passwords, identifiers, OTPs and tokens local. Confirm server denial/revocation/expiry, staff renewal compatibility and web/native layout, focus, keyboard and lifecycle behavior. The assistant has used no supplied credentials or authenticated API requests.

Dashboard period semantics/live binding, two detail-screen integrations and any future bridge from reference search to citizen ID remain separate work. No more personal response bodies or pending lists are needed for implementing this parallel screen. The source collection and its populated credentials were never copied into the repository; fixtures contain invented values.
