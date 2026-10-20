# Provide the missing API information

## Current approach: new Privacy Verification screen in the same app

The user chose a separate screen while retaining existing search. Both clients now implement the screen, so no additional pending-list response, backend source or ID-mapping answer is needed for this approach. The new screen uses the citizen ID already used in the successful collection requests. Keep that identifier and credentials local.

1. Run the updated app through the usual local development process and sign in using the existing configuration. The currently open desktop app was not restarted automatically. Estimated time: 1–2 minutes.
2. Open Privacy Verification, enter the approved test citizen ID and choose Select applicant. This does not send an API request. Estimated time: 30 seconds.
3. Reveal a section, choose Request code in the small dialog, enter the code from staff email and Verify. Only that authorized section is fetched. Estimated time: 1–2 minutes plus email delivery.
4. Try Hide, another section and leaving the screen. Locally confirm data is removed and a reopened screen starts hidden. Report a failure using only the action and generic message; do not send personal JSON, codes or tokens. Estimated time: 1–2 minutes.

Local validation: 128 core tests, four actual web-component render states and web/Windows/Android compilation pass. Live client/backend, native-device and accessibility acceptance remains pending. The replacement Privacy sign-in setting stays guarded; no configuration switch or production test is requested. Earlier steps below are historical and should not be repeated for this screen.

## Earlier step: identify the field used in the privacy URL — deferred bridge only

User clarified HTTP 401/business 2004 as no matching pending application and supplied GET /cardholder/pending as the pending list. They report citizenid is absent from that list. The earlier abstract id/citizenId question was confusing; ask about the exact URL instead.

1. Ask the API developer: Which field from GET /cardholder/pending should fill {citizenId} in GET /privacy/applicants/{citizenId}/bio? The list has no citizenid field. Please provide the field name and type, without applicant values. This resolves the required mapping without guessing a similarly named ID. Estimated time: 1–2 minutes plus response time.

The client now maps only the selected Privacy pending-detail HTTP 401/business 2004 to NotFound; 119 synthetic tests pass, including negative authorization/contract/attachment cases. Existing contracts and UI layouts remain unchanged. No full list/personal payload is needed. Dialog wiring, renewal compatibility and dashboard periods remain separate pending work.

## Earlier step: clarify lookup/status semantics — not-found meaning resolved

User confirmed HTTP 401 is paired with business code 2004/message Pending application not found for the original approved reference. The successful pending-detail attachment concerned a different applicant and exposes numeric id without citizenid. No canonical alias or definitive authentication/workflow diagnosis is established. The earlier generic-error developer draft should be revised to these current facts.

1. Give the API developer the route template GET `/obs/api/v1/cardholder/pending/{referenceNumber}`, preproduction environment, confirmed HTTP 401/business 2004/message and the fact that the original test applicant is accessible through the new applicant APIs. Keep all record identifiers/tokens private. Estimated time: 1–2 minutes.
2. Ask whether the pending endpoint is limited to specific application stages, which supported lookup resolves reference to citizenId across needed workflows, and whether the returned id is actually the canonical citizenId. Request sanitized contract guidance rather than source access or real payloads. Estimated time: 1 minute plus developer response time.

No further Postman request is needed for this diagnosis yet. Independently, shared privacy controller implementation and 118 synthetic tests are complete; dialogs/host wiring and full integration remain pending. Continue small steps when fresh evidence arrives.

## Earlier step: confirm the status accompanying the code-2004 response — pairing confirmed

The user prepared the original approved reference, sent the copied GET once, and reported HTTP 401. They then supplied a body with code 2004/message Pending application not found. Before directing a new login, confirm whether that exact body is paired with the earlier 401 or another status. HTTP status and business outcome must remain separate; neither the generic 401 nor this pending-specific message establishes a canonical citizen ID.

1. Read the HTTP status shown above this exact code-2004 response. Report only the displayed status. Do not Send again or paste more record data. Estimated time: 15–30 seconds.

The earlier sign-in suggestion was not executed by the assistant and no fresh login was reported. No source/tests changed; 106 tests remain the latest actual validation. After the pairing is known, decide whether authentication or pending-record lookup semantics require investigation. Continue one small step at a time.

## Earlier step: prepare the lookup for the original approved applicant — completed

The user confirmed the successful pending-detail response was for a different applicant from the privacy checks. Comparing those IDs would not establish a mapping. No more data from the other applicant is needed.

1. In the copied GET request, replace the reference number at the end of the URL with referencenumber from the original approved test applicant's Bio response. Keep GET and the remaining URL unchanged. Do not Send yet; report only that it is updated. Estimated time: 30–60 seconds.

After confirmation, guide Send once and collect status only, then a local same-record ID comparison without values if the response succeeds. Keep identifiers private; id/citizenId equivalence remains unestablished. The earlier developer message stays on hold. No source/test changes at this evidence-only checkpoint.

## Earlier step: compare the two IDs locally — different applicant confirmed

The user supplied an attached business-code-0 response and confirmed GET `/obs/api/v1/cardholder/pending/{referenceNumber}` after suspecting the earlier request's address/method. Hold the earlier developer message. Corrected HTTP status was not provided. The body uses the existing nested detail format and contains numeric id but no explicit citizenid. No source personal values were copied into evidence/fixtures.

1. For the same approved test applicant, compare the first id near the top of the corrected response with the local citizenId variable used for the successful Bio request. Report only match/different/different applicant; do not share values. This checks whether the observations even concern the same canonical record before making a mapping assumption. Estimated time: 30–60 seconds.

Even a matching sample does not prove a universal backend alias. Seek contract/source confirmation if an adapter must depend on id being citizenid. Current application source and tests are unchanged since the 106-test milestone. Continue one small step at a time.

## Earlier step: proposed developer error report — on hold after corrected response

The approved-record existing reference lookup returned user-reported HTTP 500 with code 99/message Internal server error. A generic error cannot establish whether the route is supported, the request matches the deployed identifier contract, or the server failed while processing it. The user has no backend source access; diagnosis now needs the API developer's route knowledge or logs. No additional personal response data is needed.

1. Give the API developer the method/path template GET `/obs/api/v1/cardholder/pending/{referenceNumber}`, preproduction environment, HTTP 500 and code 99/message Internal server error. Explain this route comes from the current application's reference search and was tested with the approved record. Share no tokens or personal identifiers in this guide/chat. Estimated time: 1–2 minutes.
2. Ask them to confirm whether this lookup is supported on the selected deployment and inspect server logs. If the route differs, request the supported method/path and sanitized response structure for resolving a reference number to the canonical citizen ID. This preserves the existing search instead of assuming identifiers are interchangeable. Estimated time: 1 minute plus developer response time.

The new collection's successful response observations remain valid as individual observations. Core mapping and tests remain at the 106-test milestone; no source change or repeated build is required for this evidence-only update. UI/controller work, renewal compatibility and dashboard periods remain incomplete separately.

## Earlier step: prepare the existing applicant lookup — completed with error

The user preferred one small step at a time. They completed the copied approved-record lookup with the PII header unchecked and reported HTTP 500, then code 99/message Internal server error. No canonical ID was established. The preparation steps below are historical recovery context, not instructions to repeat.

The response structures and exact PII/login cookie names have now been supplied. Core status/section/detail mappings pass synthetic tests. The current screen searches by reference number, while protected calls require a canonical citizen ID. This check tests the existing lookup using only the approved test record.

1. Duplicate the GET Cardholder Update request in Postman and name the copy **Existing applicant lookup — test**. This keeps the supplied collection request intact. Estimated time: 30 seconds.
2. Set the copy's URL to `{{baseUrl}}/api/v1/cardholder/pending/{{testReference}}`. Set a local testReference variable to the approved record's reference number. This route comes from existing application code; availability on the new host is being checked, not assumed. Estimated time: 1 minute.
3. Keep staff Authorization and X-Obs-Token. Disable X-PII-Access-Token on this copy and Send once. This checks the existing reference lookup rather than a citizen-ID detail request. Estimated time: 30 seconds.
4. Report only HTTP status and whether data[0] contains citizenid, including its type (number/string/null/absent). Do not share its value or any personal fields. This determines whether the current search can obtain the required canonical ID. Estimated time: 30 seconds.

Total estimate: 2–3 minutes. No whole queue or real-record request is needed. Session renewal compatibility, privacy UI lifecycle and dashboard period semantics remain separate follow-up work. The earlier guide steps below are history.

## Earlier step: inspect privacy verification status — completed

The reported privacy response is HTTP 200/business 0 without verification metadata. The PII variable has a value, but freshness and exact cookie issuance are unconfirmed. Keep existing access checks unchanged until server status is known.

1. Keep the same local applicationId. Open **1. Privacy OTP → Verification status**. This checks the applicant-specific verification without repeating OTP mutations. Estimated time: 30 seconds.
2. In Headers, enable **X-PII-Access-Token** and retain the value **{{piiAccessToken}}**. Keep existing staff Authorization and configured headers. This exercises the documented separate PII-header fallback. Estimated time: 30 seconds.
3. Click Send once. Share HTTP status and sanitized JSON; retain field names/types/expiry/section metadata while removing credential and personal values. This provides the missing verification-state evidence. Estimated time: 1 minute.
4. On the earlier **Verify privacy OTP** response, check whether its Cookies tab includes the name **pii_access_token**. Report yes/no only; never copy the cookie value. This checks the intended request's credential issuance. Estimated time: 30 seconds.

Total estimate: 2–3 minutes. This step supersedes the older instructions below. No repeat OTP verification is requested.

## Current step: privacy OTP verification using the approved test record

The user supplied the dashboard response shape and confirmed an approved synthetic applicant is available. The dashboard core mapping is implemented and mock-tested; screens remain unchanged.

1. In collection Variables, set local `applicationId` to the approved synthetic applicant's citizenId, reference number or CID. Keep staff accessToken local and do not substitute a real person's identifier. This scopes verification to the intended fake record. Estimated time: 1 minute.
2. Open **1. Privacy OTP → Request privacy OTP** and Send once. Keep the existing headers/authentication; its POST path is `/obs/api/v1/privacy/otp/request`. This requests the separate privacy code in the staff account's email. Estimated time: 1 minute plus email delivery.
3. Enter that privacy email code into local variable `otp`. Open **1. Privacy OTP → Verify privacy OTP** and Send once. Its POST path is `/obs/api/v1/privacy/otp/verify`. This is separate from the earlier loginOtp step. Estimated time: 1 minute.
4. Share HTTP status and sanitized verification JSON preserving verified/expiresAt/authorizedSections and their types. Replace any OTP, token, email or personal value with a placeholder. Also report whether `piiAccessToken` now has a value, yes/no only. This establishes expiry/section/token-handling evidence for the approved dialog. Estimated time: 1–2 minutes.

Total estimate: 4–5 minutes plus email delivery. Run these requests individually, not the whole collection. Prior guide steps below are history.

## Current step: safely collect the dashboard response

The user reports completed test login: HTTP 200, business code 0, and an accessToken value. The response contains VIEWDASHBOARD permission. No identity or credential values are retained in this guide. Cookie/session behavior remains unverified.

1. Because the pasted reply included an access credential, use **0. Auth → Terminate (logout)** to end that test session, then repeat login locally to obtain a fresh session. Keep all new passwords, OTPs, tokens and cookies private. This limits further use of the exposed credential; backend token revocation behavior is not established. Estimated time: 2–5 minutes plus email delivery.
2. Open **4. Dashboard → Get Dashboard**. Confirm GET to the intended preproduction host with path `/obs/api/v1/dashboard`, using the collection's existing authorization/header settings. This requests the dashboard schema needed for the existing cards/charts. Estimated time: 1 minute.
3. Click **Send once**. Report HTTP status and sanitized JSON response. Preserve field names, nesting, value types and nulls; replace personal values with synthetic placeholders and remove all password/token/cookie values. This supplies mapping evidence without retaining sensitive payloads. Estimated time: 1–2 minutes.

Total estimate: 4–8 minutes plus email delivery. The older steps below are historical.

## Current step: verify the login OTP

The user reports HTTP 200 and receipt of the login OTP email from the preceding manual preproduction request. This is user-reported live evidence for the first login step; no credentials or OTP were shared with the assistant. Full login, cookies/session behavior, and privacy/dashboard workflows remain unverified.

1. In the collection Variables tab, set the local `loginOtp` value to the code received by email. Keep the code private. This supplies the documented second-step field. Estimated time: 30 seconds.
2. Open **0. Auth → Authenticate (submit login OTP)**. Keep the existing username/password settings and supplied request headers. This selects the collection's login-completion request. Estimated time: 30 seconds.
3. Click **Send once**. This submits the documented username/password/otp body to the same authentication route. Estimated time: 30 seconds plus server response time.
4. Report only HTTP status, the response JSON's `code` value if present, and whether collection variable `accessToken` was populated. Do not send the OTP, token, cookies or full response. This checks login completion while protecting credentials. Estimated time: 30 seconds.

Total estimate: 2 minutes plus server response time. The request and earlier discovery notes below are history; this verification step is the current action.

## Current step: request the login OTP in Postman

The user confirmed they have a staff account approved for the preproduction server. No credentials were supplied to the assistant and no login/OTP request has been executed by the assistant.

1. Open the imported **OBS - Privacy PII OTP & Dashboard** collection in Postman and select **Variables**. This prepares the provided requests to use the correct test environment. Estimated time: 1 minute.
2. Set `baseUrl` to the user-provided HTTPS preproduction address ending in `/obs`. Enter the approved test account in the `username` and `password` variables using local values. Do not share those values to the workspace or paste them in chat. This supplies the documented request placeholders. Estimated time: 1 minute.
3. Open **0. Auth → Authenticate (request login OTP)**. Check the resolved request is POST to the intended preproduction host with path `/obs/api/v1/session/authenticate`. Retain its supplied headers and No Auth setting. This verifies the destination before credentials are sent. Estimated time: 1 minute.
4. Confirm Body contains username/password placeholders and no login OTP for this first request. Click **Send once** to request the email code. This is the first step of the documented login flow. Estimated time: 1 minute, plus email delivery time.
5. Report only the HTTP status and whether the OTP email arrived. Keep passwords, tokens, cookies and OTP codes private. This lets the assistant guide the next step without exposing credentials. Estimated time: 1 minute.

Total estimate: 5 minutes plus email delivery time. Do not run the entire collection; several later requests depend on verified login/privacy state and approved synthetic records.

Postman variable guidance was checked against the official documentation: https://learning.postman.com/docs/use/send-requests/variables/variables/ . Local collection/environment values are distinct from shared values; use local values for this manual test.

The notes below are earlier discovery history; the current step above supersedes their earlier access/account questions.

Latest follow-up: `/obs` displays a webpage-not-found screen, but an unauthenticated HEAD to `/obs/api/v1/dashboard` returned 401 Unauthorized. Continue with an approved staff test account in Postman; API response schemas remain unverified. Do not share credentials in chat. This supersedes the browser-access troubleshooting step immediately below.

Current update: backend source is unavailable. Use the test-API route below. A preproduction HTTPS address ending in `/obs` has now been supplied, but an unauthenticated check returned HTTP 403.

The immediate step is to open that address in your normal browser using your usual company network/VPN and report whether you see a login screen or the same 403 error. This takes about 1–2 minutes and establishes whether an access restriction needs the API developer's help. No password or token needs to be shared in chat.

You do not need to write schemas or change application code. Choose the source-code route first. The test-server route is an alternative when source code is unavailable. Source code can unblock implementation; a working test service will still be needed later for live verification.

Time estimates below cover your coordination and file-location work. Waiting for the API developer or setting up their server may take longer.

## Preferred route: provide the backend source

1. **Ask whoever maintains the API for its backend project.** Ask specifically for the version implementing the Privacy PII OTP & Dashboard Postman collection. The backend is the server program that handles those requests. **Why:** its code can establish the missing response fields, expiry rules, and applicant identifiers. **Your time: 2–5 minutes.**

2. **Provide its location.** If the project is already on this computer, open its folder in File Explorer, click the address bar, copy the full folder path, and send that path in this chat. If it is in a repository, send the repository URL and branch/version instead. If you are unsure whether it is the right project, send the folder path and I can inspect it. **Why:** this gives me the actual files to review. **Your time: 1–3 minutes.**

3. **Pause here once the source location is supplied.** I can check whether it contains the required APIs and report any remaining gap. You do not need to collect response examples first. **Why:** avoids asking you to do technical work that the code can answer. **Your time: 1 minute.**

**Total coordination estimate: 4–9 minutes, plus any wait for the developer.**

## Alternative route: provide an approved test API

1. **Ask the API developer for a running test-server address.** Request the HTTPS address and confirmation that it is a development/staging environment with fake applicant records. The collection currently points to localhost:8080; nothing was listening there during the earlier check. If their service runs locally, ask them for its startup instructions. **Why:** I need a reachable, approved environment to observe responses. **Your time: 2–5 minutes.**

2. **Arrange a temporary staff test account and email OTP access.** Request access to dashboard, Information Gathering, Cardholder Update, and privacy OTP. The developer should also identify any required X-Obs-Token header setting. Receive passwords/header values through your usual secure channel and enter them locally when needed; do not paste them into this chat or add them to tracked project files. **Why:** these APIs require staff login, permissions, and emailed codes. **Your time: 3–5 minutes; account provisioning varies.**

3. **Ask for one fake applicant's identifiers.** Request its citizenId, reference number, and CID, and confirmation that all three refer to the same synthetic applicant. **Why:** the existing screen searches by reference, while several new routes use citizenId. I must map them correctly. **Your time: 2–3 minutes.**

4. **Send only the non-secret details in this chat.** Send the test-server address, confirmation that it is a test environment, confirmation that the account/email access is ready, and the fake applicant identifiers. State that I may test login, email OTP request/verification, and read-only API calls against those synthetic records. We can then arrange local credential entry. **Why:** this establishes the correct target, permitted actions, and test record without exposing credentials. **Your time: 2 minutes.**

**Total coordination estimate: 9–15 minutes, excluding server/account setup and developer waiting time.**

If neither source nor a test server can be provided, ask the API developer for sanitized example responses for login completion, privacy verification/status including expiry, the four protected sections, both applicant-detail routes, and dashboard. They should preserve field names/types/nesting while replacing personal values and token/cookie values. They should explain the expiry format and identifier mapping. You do not need to create those examples yourself.

## Message you can copy to the API developer

Please help me finish the OBS_eKYC integration using the “OBS - Privacy PII OTP & Dashboard” Postman collection.

First preference: please provide the backend source project location or repository and branch for this API version.

If source code cannot be shared, please provide a running HTTPS test API, a temporary staff account with email OTP access and the necessary module permissions, the required header configuration through our secure channel, and one fake applicant's citizenId, reference number and CID.

We need to confirm the actual response fields for dashboard, privacy verification/status/expiry, protected applicant sections and applicant details. Please use synthetic records. If neither source nor test access is possible, sanitized response examples and explanations of expiry and ID mapping would help.

Please keep passwords, tokens and real applicant data out of chat exports and source control.
