# Editing the MAUI screens

## View Application

Search with **Application Number**, use **View Application** in the sidebar, or select a reference number in Cardholder Verification. Both clients use `IApplicationRecords` in OBS.Core, the existing `ApiTransport`, and the `PENDINGORS` authority. The web client also accepts `/applications` and `/applications/{number}` behind the existing sign-in/session checks.

The native layout is in `src/OBS.Maui/ViewApplicationView.cs`; the Blazor layout is in `src/OBS.Web/Components/ViewApplication.razor` and `wwwroot/application.css`. They reuse the dashboard shell, icon paths, city configuration, themes, and existing module navigation. Desktop has four columns; narrower views use two or one. The web quick links scroll and focus sections; native quick links scroll inside the parent screen. Eye controls show additional record fields. View Map opens the selected address in Google Maps; the inline map is a decorative illustration, without geocoding or a map-service key.

Records and attachments come from the original pending-detail routes. No sample applicant is supplied by production code. Missing fields show **Not provided**. The inspected source DTO does not supply an independent card status, graduation year, or school address, so these values are not inferred from workflow status or the applicant address. The status badge displays the source record's status as returned.

All details are read-only. Photo/section editing and attachment upload show unavailable controls because these workflows are not implemented in the migrated clients. Capture uses the existing `BlockedCaptureAdapter` availability rule. Staff with `VCARDHOLDERUPDATE` can navigate to the existing Cardholder Update module from Actions; that module remains unfinished. Audit appears as unavailable because its permission/route mapping is not finalized. PDF, PNG and JPEG attachments can be opened; unsupported content shows an availability message. Web previews use revocable blob URLs inside a dialog; native previews use temporary cache files cleared with the record/session.

Loading, no selection, missing records, invalid identifiers, permission denial, connection failure, malformed responses and attachment-only failures have distinct states. Requests superseded by a new search or an ended session cannot replace the active record. A failed attachment request leaves the applicant visible and offers Retry.

Validation: both production clients build; the dependency-free shared suite passes 53 checks. Browser checks use synthetic HTTP envelopes through the real record service at 1920, 1366, 1024, 768, 390 and 320 pixels. They cover direct application URLs behind sign-in, loading/empty/error states, field mapping, age, map URLs, unavailable controls, section dialogs, binary previews, quick-link focus, light/dark themes, overflow, record switching, stale-response cancellation, queue navigation, repeated actions, the authorized update route, dashboard navigation and logout. Screenshots and results are under `artifacts/application-check/`. Native runtime screenshots could not be completed: the preview host failed to initialize reliably, and its initial self-contained build also exceeded available disk space. MAUI compilation passes with the pre-existing `People.xaml` binding warning. Live backend and Windows/Android device acceptance remain outstanding.

`src/OBS.Maui/MainPage.xaml` contains the authentication and session screens and hosts the full-width `DashboardView.xaml`. Open these files in Visual Studio 2022 to edit the UI. `MainPage.xaml.cs` selects screens and forwards authentication actions; authentication, permissions, tokens, and refresh logic stay in OBS.Core.

## Design workflow

1. Open `OBS_eKYC.sln` and set OBS.Maui as the startup project.
2. Run Windows with F5. If an older OBS.Maui instance is still running, close it before rebuilding to avoid DLL locks.
3. Open `MainPage.xaml` in Solution Explorer and edit the relevant panel while it is visible in the running app.
4. Use XAML Hot Reload to review layout changes. Rebuild for changes that require it, such as code-behind/event-handler changes.

The named panels make each screen easy to find: `LoginPanel`, `ForgotPasswordPanel`, `OtpPanel`, `PasswordChangePanel`, and `DashboardPanel`. The configuration and session-paused panels handle startup/validation states. The session determines which panel is visible; changing visibility for a visual experiment does not authorize a backend action.

## Where to make changes

| Design change | XAML location |
|---|---|
| Spacing, page padding, form width | `PageContent` |
| Headings, labels, and control order | Each screen's VerticalStackLayout |
| Font sizes and heading appearance | Label / HeadingStyle resources |
| Button height, corners, and colors | PrimaryButtonStyle / SecondaryButtonStyle |
| Email/password/OTP field layout | Named Entry controls in the relevant panel |
| Dashboard shell, toolbar, summary, and chart layout | `DashboardView.xaml` |
| Dashboard navigation and metric cards | `DashboardView.xaml.cs`: `NavigationRow`, `CreateMetric`, `CreateQueue` |
| Native activity chart and release ring | `Controls/DashboardCharts.cs` |

Deployment colors and product/city names come from `theme.ini` at startup. Authentication screens use those deployment colors. The dashboard uses its own `Dash*` resources for the reference design's lavender surfaces, gradient header, and light/dark palettes; its city label still comes from deployment configuration. Edit `DashboardView.xaml` and `OnToggleTheme` together when changing dashboard colors.

Keep `x:Name` values, Clicked/Completed handlers, and permission-aware navigation wiring intact unless you also update the code-behind. Password entries must remain password fields. Shared core checks still enforce six-digit OTP, cooldown, CPW permissions, logout cleanup, and session validation.

Both clients open the dashboard after a validated sign-in. Web also accepts `/dashboard`, with the same sign-in requirements. Its layout is in `src/OBS.Web/Components/Dashboard.razor` and `wwwroot/dashboard.css`. Both clients use the sample figures, four activity periods, navigation labels, and icon paths in `src/OBS.Core/Dashboard.cs`. The visible **Sample data** label is intentional: no reporting endpoint is connected. Search, QR scanning, notifications, and unfinished modules show availability notices. No new backend permissions are invented to populate the sidebar.

The period selector changes the sample chart, the theme buttons switch light/dark appearance, and the menu button collapses navigation. Narrow layouts stack cards and keep navigation accessible through an expandable menu. Sign-out and password changes continue through the existing session controller.

Validation includes Windows and Android builds, the 37 existing shared regression checks, browser checks with synthetic authentication at desktop/tablet/phone widths, and Windows native view rendering in light/dark and narrow layouts. Actual backend authentication, Android device rendering, screen-reader use, and Hot Reload still need deployment/device checks. Rebuild and reinstall the Android APK to see these layouts on a phone.
