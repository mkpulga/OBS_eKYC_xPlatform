# Observed privacy responses and core mapping

Date: 2026-10-20. Evidence came from the user's manual preproduction Postman checks for an approved test applicant. The assistant did not authenticate, use the pasted staff credential, or issue protected requests. This report records structures, never supplied personal/credential values. Examples in tests are invented.

## Verification

- Verify privacy OTP: user reported HTTP 200, business code 0/message Success, without `data`. The user confirmed the exact `pii_access_token` cookie was present and the local PII variable populated.
- Verification status: user reported HTTP 200, business code 0, `data` containing one object. Its `verified` is boolean; `applicationId` is a string; `expiresAt` is an ISO timestamp with UTC Z; `authorizedSections` is an array of strings.
- Observed claims are `canViewBioDetails`, `canViewAddress`, `canViewEmergencyContact`, and `canViewEducationalInformation`. These are section claims, not new staff-module names.
- The observation establishes a verification at the time of that check. It does not establish that the earlier token or expiry is still valid.

`PiiAccessToken.FromVerification` now accepts a successful bare verification envelope with the exact cookie. If `data` is present it must still contain exactly one `verified=true` record; false, null, malformed or ambiguous metadata is rejected. The token remains opaque and grants nothing by itself.

`GetVerificationStatusAsync` parses one status response after the request completes. It requires an exact application ID, literal boolean verified, an absolute zoned expiry, and an array of nonblank claim strings. It normalizes expiry to UTC and rejects expired results at the boundary. Zone-free timestamps and cultural date formats are invalid. Offset/fractional variants are supported by client validation, not independently observed server variants. Empty or unknown claims grant no sections; claim values use ordinal exact comparison.

## Protected sections

All supplied bodies use business code 0 with a one-object `data` array. Their HTTP statuses were requested but not supplied. Each child section is an array; every observed `citizenid` is numeric. For this test record the canonical numeric citizen ID corresponds to the string application ID in verification status. This single observation does not establish arbitrary reference/CID aliases.

| Endpoint | Child array | Retained display fields |
|---|---|---|
| Bio | `personal-info` | Names/suffix/displayname, raw birth-date text, gender/bloodtype/civilstatus/nationality/accounttype, birth-place text, tenurialstatus, separate status and citizenstatus |
| Address | `address` | Address type, specific/building/line values, barangay/city/province/region/country/zipcode; both address rows retained in server order |
| Emergency | `emergency` | Names, nullable suffix, relationship, contactnumber as text |
| Education | `other-info` | educationalattainment, school, course, department, nullable occupation/company, sourceofincome/natureofwork |

`ProtectedApplicant.cs` retains only these display values and canonical citizen IDs. Account numbers, public/row IDs, security answers, income amounts, identity-document numbers and unrelated flags are excluded from these section models. No source personal values are copied. Fields outside this table may be needed by a future separately authorized detail mapping; exclusion here does not delete them from any existing integration.

Required mapped properties must be present, unambiguous, and string or explicit null. Null, empty and whitespace remain distinct. Numeric IDs must be integral and match the requested canonical ID. Bio requires exactly one child row. Address/emergency/education preserve collection rows, including an explicit empty collection; no preferred row is invented. Any invalid row rejects the entire snapshot, avoiding partial mixed-applicant displays. These are conservative client acceptance rules based on the available sample, not a formal complete backend schema.

Birth dates remain text because slash-separated month/day order is unconfirmed. No age is computed. `status` and `citizenstatus` remain distinct; no workflow transition is inferred. Education does not invent an emergency address or other unavailable screen fields.

The four `Get*SnapshotAsync` helpers require matching unexpired status metadata and the specific claim before dispatch, then recheck expiry after receiving the response. Cancellation/error results carry no snapshot. Staff/session/configuration guards and separate staff Bearer/PII headers remain in effect. Both parallel Privacy Verification screens now call these through their shared controller; raw adapters alone remain insufficient as a screen authorization boundary.

## Detail observations and mapping

The later user-supplied Information Gathering body contains the same four child arrays plus `last-module-updated` (object with module/timestamp/citizenid), `account-classification` (array with type/citizenid), and `contact` (array with value/type/citizenid). It also has boolean `piiVerificationRequired` and an object `piiAuthorizedSections` containing the four boolean claim fields. The observed response is already authorized. HTTP status was not supplied.

The later Cardholder Update body uses the same outer layout with `piiVerificationRequired=true`, four false flags, and empty address/emergency/other-info arrays. Its personal-info row retains names/displayname, accounttype, citizenid, referencenumber, separate status/citizenstatus and additional identifiers not retained by the new model. Contact/classification/module data is still present. HTTP status and exact credential/cookie state were not reported, so the response proves a redacted shape, not why authorization was absent.

`ApplicantDetailApi.cs` maps these observed layouts through both typed detail helpers. Baseline identity/contact/classification/module values are retained without inferring eligibility, primary contact or timezone. Protected values require both unexpired applicant-specific status metadata and a true corresponding server flag. Without that intersection, protected snapshot properties are null (withheld), distinct from authorized empty collections. A token or response flag alone is insufficient. Contradictory required/allowed flags, malformed fields, mixed applicant IDs and ambiguous rows reject the snapshot. A late-expiring response is discarded. No public/row IDs, account numbers or identity-document numbers are retained.

Object-shaped detail flags differ from the status array. They cannot replace verification status. Canonical reference/CID resolution remains unverified; existing pending code exposes a citizenid field but its behavior on this deployment still needs a focused approved-record lookup check. That check must not request or share a whole real applicant queue.

Lookup history: user first reported HTTP 500/business 99/message Internal server error. They later suspected an incorrect address/method, supplied a successful-business-envelope detail attachment and confirmed GET /obs/api/v1/cardholder/pending/{referenceNumber}. The earlier failure cannot establish route incompatibility; the developer error message is on hold. Corrected HTTP status was not supplied.

The corrected attachment has one data row with numeric id, no explicit citizenid, and the existing nested personalinformation/address/emergencycontact/educationalinformation layout. It also contains sensitive extras excluded from existing display models; only field names/types were inspected in tool output, with no source values copied to evidence or fixtures. This response supplies no documented id-to-citizenid alias. Do not substitute id silently or assume the same applicant as earlier observations. A local same-record comparison was requested without sharing values. This retained route is outside the selected 16-request collection; no additional endpoint was invented and no assistant-authenticated call was made.

## Login follow-up

Record clarification: the user subsequently confirmed the successful pending-detail attachment concerns a different applicant from the earlier privacy checks. Its numeric id cannot be compared with the earlier citizenId to establish a mapping. Guide only the original approved test record's reference lookup; request no further personal data from the other applicant. Numeric id remains excluded as an assumed canonical alias.

The user confirmed both exact login-response cookie names `refresh_token` and `XSRF-TOKEN`. Combined with the earlier successful login body, this supplies the fields required by the existing source-backed session parser. One synthetic complete-session test now covers that combination. Actual renewal/rotation, restoration and logout/invalidation semantics remain unverified; cookie presence does not prove those behaviors.

## Remaining integration

Current approach supersedes lookup as a prerequisite: the user selected a new screen in the same app, preserving the old search. Both new screens accept citizen ID directly and implement the approved OTP dialog, seven request workflows, per-screen lifetime, UI-context updates, field/input clearing and navigation disposal. Nine new shared screen/workspace tests bring the total to 128; four actual web-component render states also pass. Replacement Privacy sign-in remains guarded pending renewal/full retained-workflow acceptance. Live browser/native-device and server-enforcement acceptance, dashboard binding and two detail integrations remain pending. The lookup/controller paragraphs below are historical milestones and are not instructions to repeat ID checks or delay the parallel screen.

Original-record lookup follow-up: user confirmed HTTP 401 is paired with code 2004/message Pending application not found for the original approved reference. No automatic login, ID alias, successful original-record lookup or definitive diagnosis is claimed. A missing pending application is not evidence that the applicant is absent from other workflow stages. Ask the developer to clarify pending lookup scope, this status pairing and the supported canonical reference lookup.

Subsequent user clarification resolves that outcome as no matching pending application. The client now maps exactly Privacy contract + specific pending-detail route + HTTP 401/business 2004 to ApplicationOutcome.NotFound, retaining raw HTTP/business evidence and all other authorization rules. A new parameterized regression brings the suite to 119 passing cases; it covers negative statuses/codes, old contracts and attachment isolation. User identified GET /cardholder/pending as the pending list and reports citizenid absent. The source model's optional citizenid member is retained for legacy compatibility, but cannot establish this deployment's canonical mapping. Ask which actual list field/type fills {citizenId} in the protected Bio URL; no ID alias is inferred from a similarly named field or a different applicant.

ApplicantPrivacyController now implements core staff/applicant binding, duplicate-action guards, status-before-reveal, independent hide/discard, explicit revocation revalidation, absolute expiry clearing, cancellation/disposal and stale-response protection. Its opaque staff identity survives ordinary token rotation and changes on sign-in/logout. Twelve additional synthetic controller tests bring the total to 118. Approved dialogs, controller registration, UI-copy clearing/disposal/foreground hooks, canonical lookup and complete live acceptance remain pending. Both hosts retain the Privacy startup guard.

Synthetic tests cover the observed four section shapes, status/cookie handling, nullable values, strict claims, wrong IDs, malformed/duplicate fields, expiry before/after asynchronous requests, cancellation and failures. They establish local core behavior, not actual server claim enforcement or completed UI acceptance. See TESTING.md for current counts and commands.
