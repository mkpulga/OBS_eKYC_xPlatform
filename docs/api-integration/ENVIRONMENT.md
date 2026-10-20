# Environment and configuration guide

## Current availability

Current follow-up: the user selected a new screen in the same app while preserving existing reference search. Privacy Verification is now wired in both clients using the existing staff login and configured API root. Enter a citizen ID directly; no pending-list field mapping is required for this flow. Existing settings are not edited or switched. Request/verify/status/four sections are wired and synthetic-tested; live UI/server acceptance, renewal compatibility, dashboard binding and two detail workflows remain pending. Earlier discovery notes below are historical.

Most recent follow-up: the browser confirmed `/obs` is a “Page not found” screen, while an unauthenticated HEAD to `/obs/api/v1/dashboard` returned HTTP 401 and JSON content type. The API address exposes an authentication boundary; actual route behavior and schemas remain unverified until approved test login. Use the supplied `/obs` context in the collection's baseUrl. No authenticated request or OTP was sent.

Later update on 2026-10-20: the user supplied a preproduction HTTPS address with `/obs` context. An unauthenticated GET returned HTTP 403 HTML, so API deployment/reachability remains unverified. No login or OTP was submitted. Check normal browser/company-network access before testing authenticated requests. For this collection, a base URL retaining `/obs` composes `/obs/api/v1/...`; deployment confirmation is still required. The user has no backend-source access, so response evidence must come from an approved test service or developer-supplied examples.

The collection defaults to HTTP loopback port 8080. A read-only local listener check on 2026-10-20 found no API listening on that port. No HTTP request was sent to it. No matching privacy backend project was found in the checked `C:\mkpulga` project paths; this does not prove the backend is absent elsewhere.

At initial discovery there was no available approved test server/account or fixture. That availability has since changed as described above. Do not copy populated credentials/identifiers into settings, fixtures or logs. Production and real-record testing remain unauthorized.

## Settings

| Setting | Privacy contract | Existing deployment behavior |
|---|---|---|
| BaseUrl / baseUrl | Approved HTTPS origin or explicit full API root | Existing URL retained; no automatic host/environment selection |
| ApiRootPath / apiRootPath | `/api/v1`, explicitly required | Defaults to `/obs/api/v1` |
| ApiContract / apiContract | `Privacy`, explicit opt-in core contract | Existing `Refactored`/`Legacy` defaults and enum values retained |
| TokenHeaderKey / tokenHeaderKey | `X-Obs-Token` | Existing configurable header mechanism retained |
| UserAgent / userAgent | Deployment-supplied configured header value | Despite its name, this is the configured token-header value, not a separate HTTP User-Agent |
| FeatureSet / featureSet | Existing selected feature set, e.g. StandAlone | No automatic business-rule/feature switch |

Replacement Privacy sign-in mode is still **Blocked in both client hosts**, even with valid settings, pending renewal and complete retained-workflow compatibility. The new parallel screen is available alongside a valid existing Refactored/Legacy staff login. PrivacyWorkspace preserves the exact existing configured API root and header; it changes only its internal service contract. Do not switch to Privacy or remove the startup guard to use the new menu. The settings table/example describe the guarded replacement mode, not a required change for the parallel screen.

Use different protected local configuration for test and production, with no credential sharing. The code makes no fallback to production or another contract. HTTPS validation, no redirects, existing handler cookie policy, and owned-client timeout conventions remain unchanged. HTTP loopback is not enabled: the collection's sample URL cannot be used directly without a separately approved development-only change or HTTPS test endpoint.

## Web configuration example

The existing ignored file is `src/OBS.Web/appsettings.Local.json`. The following is a placeholder-only example, not a runnable deployment or permission to test a real server:

```json
{
  "Backend": {
    "BaseUrl": "https://approved-test-backend.example.invalid",
    "ApiRootPath": "/api/v1",
    "ApiContract": "Privacy",
    "FeatureSet": "StandAlone",
    "TokenHeaderKey": "X-Obs-Token",
    "UserAgent": "<deployment-supplied-header-value>"
  }
}
```

Obtain the header value through the deployment owner's protected mechanism. Staff passwords, login OTPs, access/refresh tokens and PII tokens are transient runtime values, not configuration settings. Existing production Data Protection/TLS/hosting setup remains the owner's responsibility; no deployment changes were made.

## Native configuration

[config/privacy-api.ini.example](../../config/privacy-api.ini.example) shows the new core INI keys using placeholders. Native composition still reads the embedded `src/OBS.Maui/app.ini`; an external INI loader is not implemented. Do not place real secrets into that tracked file or commit packaged credentials. The approved native deployment/configuration mechanism needs review before rollout. This task did not modify embedded settings, manifests, permissions, signing, or release configuration.

## Required backend evidence, in plain terms

Ask the API developer for a test service that can return responses for:

1. Login OTP completion and any refresh/session cookies used by the existing client.
2. Privacy OTP request/verify and verification status, including expired/rejected cases.
3. Bio, address, emergency and education sections for one synthetic applicant.
4. Redacted and verified detail responses, with the same synthetic applicant's citizen ID, reference number and CID.
5. Dashboard response for a test staff role with VIEWDASHBOARD and a denied role.

The developer may instead provide the backend controller/DTO sources or sanitized example responses with sensitive values removed but field names, types, nulls and nesting retained. The user does not need to create JSON schemas. These responses must establish expiry format, authorized-section values, ID mapping, metric names/period semantics, and failure behavior before UI mapping is written.
