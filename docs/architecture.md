# Architecture and capabilities

The primary contract is Refactored and the primary feature set is StandAlone. These remain separate enums/configuration values. INI compatibility supports the original missing-feature city fallback and missing-contract feature fallback with diagnostics. Explicit invalid enum values fail startup validation. Legacy authentication is currently blocked rather than claimed compatible; it needs its own response fixtures and acceptance tests. Bacoor/Makati rule verification is incomplete.

`OBS.Core` has no local database, storage service, platform UI, or backend replacement. `ApiTransport` sends one request per action, retains actual HTTP status separately from the business code and Spring body status/error, and rejects unknown success shapes. Its result contains sensitive response material: do not log or serialize it. Request security/session objects redact `ToString()`. HTTPS certificate validation is the system default. Redirects and automatic cookie handling are disabled in the owned client gateways. No automatic authentication/mutation retries are installed.

`SourceAuthenticationGateway` derives its DTO fields from `Models/userData.cs` and the request/response flow from `Controllers/sessionController.cs`. It requires an explicit successful envelope, complete token state, permissions, user email, and password flags before protected navigation. The backend password string is not mapped into its DTO. This is source-backed behavior, not live contract verification.

`SessionController` owns transient login credentials, session state, cancellation, generation invalidation, refresh serialization, five-second scheduling, and the three-consecutive-connection-failure counter. Logout invalidates state before remote termination. Refresh success swaps access/refresh/XSRF together. Resume disables protected work until validation succeeds. Generic rejection never claims another-device takeover. An unexpected contract shape closes the session safely.

`Navigation` uses the exact authority strings consumed by the old Dashboard. Permissions are matched ordinally and default to denial. No new module/action identifiers are invented. Backend enforcement remains to be demonstrated with allowed/denied test accounts. Administrative and workflow action implementations remain unfinished.

The web host uses **Blazor Interactive Server**, with prerendering disabled for its authentication component. Each circuit receives its own gateway, HttpClient handler, and SessionController; credentials and backend cookies never become static/singleton web state. The browser sends UI events to the host; the host calls the existing backend directly. This avoids relying on JavaScript reading `Set-Cookie` or third-party cookie delivery. It doesn't create new backend or reporting API endpoints. A new browser circuit requires sign-in; disconnected circuits have zero retention and are disposed. Browser/mobile termination cannot guarantee a network logout request.

Direct WebAssembly backend authentication is not established: CORS preflight, allowed origins/headers, credentials support, cookie SameSite/Secure/path/domain rules, and XSRF encoding are not verified. Browser JavaScript cannot directly read `Set-Cookie`, and HttpOnly refresh cookies cannot be copied into bearer headers by JavaScript. The source's bearer-refresh flow is a reason to select the server host provisionally. See [Microsoft's Blazor security guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0).

Deploy the web host as an HTTPS ASP.NET Core process with backend network access, WebSockets/SignalR proxy support, an explicit public host allowlist, and protected production Data Protection keys. Development uses process-memory keys and has no persistent application data. Multi-node circuit routing/key management has not been implemented or tested. Tokens stay in the circuit, not in browser configuration or browser storage. Only deliberate exports may later deliver authorized record data to a browser exporter.

| Capability | Windows target | Android target | Web target | Current implementation |
|---|---|---|---|---|
| Login/OTP/session/permission UI | Yes | Yes | Yes | Shared source-backed service; client/device runtime acceptance pending |
| Face capture and validated checks | Webcam | Camera | Excluded | Explicit blocked adapter pending engine/rules/license/device proof |
| Fingerprints | FS88H/vendor SDK | Excluded | Excluded | Explicit blocked adapter; backend format unresolved |
| Signature | Topaz or mouse | Touch drawing | Excluded | Adapter boundary only; capture/render/persistence unfinished |
| Enrollment and update | Yes | Saved capture rules apply | Saved capture rules apply | Shared step rules only; section UI/persistence unfinished |
| Workflow queues/actions | Yes | Yes | Yes | Permission navigation only; queues/actions unfinished |
| Preview/PDF/Word/Excel | Required | Required | Browser generation required | Report metadata/footer contract only; exporters unimplemented |
| Physical card printing/encoding/payment | Excluded | Excluded | Excluded | Not implemented |

Blazor Server is an authentication/workflow host, not approval for server report generation. The planned reporting boundary must run generation in the installed clients and browser, potentially via a separate browser-only WASM exporter. No exporter or reporting server has been added. iOS can later consume the shared core/adapters; no iOS target is included or verified.
