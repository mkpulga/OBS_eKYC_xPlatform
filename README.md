# OBS_eKYC

This repository contains a migration foundation, **not a completed first release**. It implements shared configuration, source-backed authentication requests/parsing, session control, and permission navigation, with Blazor and MAUI client source. Enrollment screens, administrative workflows, hardware integration, reporting exporters, and live backend acceptance are unfinished.

The original source is read from `C:\mkpulga\ccs-obs-stand-alone-without-git\ccs-obs-stand-alone`. It has not been modified. Original configuration values, credentials, applicant data, and vendor binaries have not been copied into application defaults.

| Project | Purpose | Verification |
|---|---|---|
| `src/OBS.Core` | Configuration, HTTP results, authentication, session safety, permissions, wizard rules, report footer contract | Built; synthetic tests run |
| `src/OBS.Web` | ASP.NET Core Blazor Interactive Server authentication UI | Normal .NET 9 build passes; page and framework script HTTP smoke checks pass; live login untested |
| `src/OBS.Maui` | Windows/Android installed authentication client source | Windows and Android .NET 9 Debug builds pass; installed runtime acceptance remains untested |
| `tests/OBS.Core.Tests` | Dependency-free executable regression suite | Run with the pinned SDK |

Use **Visual Studio 2022 17.14** and [.NET SDK 9.0.301](global.json), matching this machine's installed toolchain. The projects now target .NET 9 and MAUI Controls 9.0.51. Windows is the default native target; enable Android with `-p:EnableAndroidTarget=true`. No test framework packages are necessary:

```powershell
dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj
```

For a normal web build:

```powershell
dotnet build src/OBS.Web/OBS.Web.csproj
dotnet run --project src/OBS.Web/OBS.Web.csproj -- --environment Development --urls http://localhost:5187
```

Configure `src/OBS.Web/appsettings.Local.json` (ignored by version control), using the same `Backend`/`Theme` sections as `appsettings.json`. Fill in the authorized backend URL and configured header value through deployment configuration. An empty URL displays a configuration error. Authentication has no demo mode. Don't put staff passwords, OTPs, or tokens in settings.

For installed clients, set `src/OBS.Maui/app.ini` and `theme.ini` before packaging. A desktop external INI loader and an Android deployment settings mechanism are still to be added; the current files are embedded resources. Keep sensitive header values out of browser assets and source control.

See [architecture](docs/architecture.md), [API evidence](docs/api-mapping.md), [setup and packaging](docs/setup.md), [hardware and reporting findings](docs/feasibility.md), [acceptance checklist](docs/acceptance.md), and [delivery status](docs/status.md). Generated inventories contain field names and hashes, without request body values.

For manual UI design, edit [MainPage.xaml](src/OBS.Maui/MainPage.xaml) for authentication and [DashboardView.xaml](src/OBS.Maui/DashboardView.xaml) for the native dashboard. The web dashboard is in [Dashboard.razor](src/OBS.Web/Components/Dashboard.razor) and [dashboard.css](src/OBS.Web/wwwroot/dashboard.css). Both dashboards appear after sign-in and show explicitly labeled sample metrics, an interactive activity chart, release progress, responsive navigation, and light/dark themes. [The screen design guide](docs/screen-design.md) explains the shared sample data, styles, and remaining integration work.
