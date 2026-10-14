# Editing the MAUI screens

`src/OBS.Maui/MainPage.xaml` now contains the complete visual layouts for configuration errors, staff Login, Forgot Password, OTP Verification, Change Password, session revalidation, and Dashboard. Open it in Visual Studio 2022 to edit the UI. Its nested `MainPage.xaml.cs` contains event handlers and visible-screen selection; authentication, permissions, tokens, and refresh logic stay in OBS.Core.

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
| Dashboard module buttons or record-card appearance | `ModuleList` BindableLayout.ItemTemplate |

Deployment colors and product/city names come from `theme.ini` at startup. The XAML color resources provide defaults and live edit points; for branding that persists across launches, update the theme configuration and rebuild its embedded resource. Every relevant label/control uses these theme resources.

Keep `x:Name` values, Clicked/Completed handler names, module CommandParameter binding, and permission-aware item source wiring intact unless you also update the code-behind. Password entries must remain password fields. Shared core checks still enforce six-digit OTP, cooldown, CPW permissions, logout cleanup, and session validation.

Dashboard modules remain unfinished workflow notices, not complete screens. The conversion doesn't add backend workflows or capture/report integrations. Web design remains in `.razor`/CSS files, independent of MAUI XAML.

Windows XAML/code compilation was verified after conversion. Actual Hot Reload behavior, screen-reader use, focus navigation, login/logout transitions, and device runtime behavior still need manual checks. After changing Android layouts, rebuild the standalone APK before expecting those changes on a phone; the previously supplied APK predates this XAML conversion.
