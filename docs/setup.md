# Build, run, and packaging

## Versions and host

The current solution is retargeted for **Visual Studio 2022 17.14**, using installed .NET SDK **9.0.301**, .NET/ASP.NET Core runtime/reference packs **9.0.6**, and MAUI Controls **9.0.51**. The host is Windows 11 build 22631 x64. `global.json` pins SDK 9.0.301 with roll-forward disabled. Normal web and Windows MAUI builds succeed; all 37 shared regression tests pass. Windows App SDK **1.6.250205002** and Windows SDK .NET reference package **10.0.19041.57** are restored native dependencies.

Under SDK 9.0.301, the installed workloads report maui-windows **9.0.51** and Android **35.0.78**, associated with VS **17.14.36212.18**. SDK selection matters: the earlier .NET 10 build selected inconsistent workload manifests. No system workload installation or Visual Studio modification was performed during this retarget.

Open `OBS_eKYC.sln` in Visual Studio 2022 17.14. The installed .NET MAUI development tools are required to edit/run the native project. Right-click OBS.Maui and choose Set as Startup Project for Windows, or select OBS.Web for the browser client. CLI builds have been verified; interactive Visual Studio loading/debugging has not been exercised here. See [Microsoft's SDK/VS compatibility table](https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs). Windows MAUI builds require Windows. iOS is still unimplemented/unverified.

This retarget is for compatibility with the owner's installed development environment. **MAUI 9 support ended May 12, 2026**; it is not a supported production release baseline on the current date. A production release needs migration to a supported MAUI/toolchain and renewed platform tests. See the [official support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/maui).

`Directory.Build.props` uses an absolute workspace-local `.nuget/packages` cache, ignored by version control. This fixes the machine's relative NuGet package-root setting, which previously prevented package build imports from resolving. With restricted network access, restore from the existing cache without changing machine settings:

```powershell
dotnet restore OBS_eKYC.sln --source "$env:USERPROFILE\.nuget\packages" -p:NuGetAudit=false
dotnet build OBS_eKYC.sln --no-restore
```

`NuGetAudit=false` is only the explicit offline command; auditing isn't disabled in project settings. Run normal restore/auditing when network access is available.

## Shared tests

```powershell
dotnet build src/OBS.Core/OBS.Core.csproj
dotnet run --project tests/OBS.Core.Tests/OBS.Core.Tests.csproj
```

This executable test suite deliberately uses synthetic handlers and fixtures. Exit code zero means all listed regressions passed, not that a backend deployment or hardware integration passed. It does not participate in `dotnet test`; run it with `dotnet run` as shown.

## Web

Populate an ignored `src/OBS.Web/appsettings.Local.json` with authorized settings, or supply `Backend__BaseUrl`, `Backend__TokenHeaderKey`, `Backend__UserAgent`, `Backend__ApiContract`, `Backend__FeatureSet` through secure deployment configuration. Keep the API root and branding independent. Do not configure a secret as a client static asset. Review `AllowedHosts` for the deployment hostname; default is localhost. Bind deployment configuration before running. Development HTTP is only a loopback host; backend calls still require HTTPS.

```powershell
dotnet build src/OBS.Web/OBS.Web.csproj
dotnet run --project src/OBS.Web/OBS.Web.csproj -- --environment Development --urls http://localhost:5187
dotnet publish src/OBS.Web/OBS.Web.csproj -c Release -o artifacts/web
```

Production needs HTTPS termination and a protected Data Protection key store for the service identity. Development uses memory-only keys; restarting invalidates circuits. Enable WebSockets and configure the proxy for SignalR. Serve framework assets from the built/published output. No production deployment or package was generated here.

The original .NET 10 build required an unavailable internal assets package and only compiled with an offline override. That workaround is no longer needed after retargeting. The current normal .NET 9 build serves `/`, `/app.css`, `/lifecycle.js`, and `/_framework/blazor.web.js` with HTTP 200. These HTTP smoke checks do not verify browser interaction or live backend authentication.

## Windows / Android

Windows is the default MAUI target so Visual Studio 2022 can load/build with the installed Windows workload. Android remains available explicitly:

```powershell
dotnet build src/OBS.Maui/OBS.Maui.csproj -f net9.0-windows10.0.19041.0
dotnet build src/OBS.Maui/OBS.Maui.csproj -t:Run -f net9.0-windows10.0.19041.0
dotnet restore src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true
dotnet build src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true -f net9.0-android
dotnet build src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true -t:Run -f net9.0-android
dotnet publish src/OBS.Maui/OBS.Maui.csproj -f net9.0-windows10.0.19041.0 -c Release
dotnet publish src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true -f net9.0-android -c Release
```

Windows and Android Debug builds have succeeded on this host, both with zero warnings/errors. Android compiled and produced its debug APK; no emulator/device was used. The sandbox couldn't write the usual user-profile debug signing key, so the verified Android command supplied a workspace-local key path:

```powershell
New-Item -ItemType Directory -Path artifacts/android -Force
dotnet build src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true -f net9.0-android --no-restore -p:_ApkDebugKeyStore=C:\mkpulga\obs_ekyc\obs_eKYC\artifacts\android\debug.keystore
```

That key is only the standard Android debug key, stored under ignored artifacts; it is not a production signing key. Run/publish/device acceptance steps are not claimed complete. Android Run requires a configured emulator/device; release publishing requires owner-controlled signing keys. Windows targets unpackaged normal-user operation (`WindowsPackageType=None`, asInvoker manifest); installer/MSIX signing/distribution is unfinished. Single-instance suppression and close/logout confirmation are implemented in source but remain runtime untested. No signed release installation artifacts are claimed.

Set native embedded `app.ini`/`theme.ini` for authorized deployments before packaging. External desktop INI selection and mobile configuration distribution remain unfinished. Supply device drivers/vendor SDKs only under their applicable license and architecture constraints. Installation/device setup may require elevated privileges; the app itself doesn't request elevation.

## Evidence inventory

`scripts/Inspect-Evidence.ps1` regenerates sanitized field/header/route inventories and report/source hashes. It reads the supplied source and collection without modifying them. Run it only under the owner's approved PowerShell script policy; this work didn't change the machine execution policy. The generated JSON is already supplied in `docs/evidence`.
