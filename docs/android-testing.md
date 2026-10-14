# Android test build

The Android client is an authentication/session migration foundation. Enrollment/workflow screens, hardware capture, and report exports are not complete. A successful APK build is not device runtime acceptance.

The project targets .NET 9 Android, MAUI Controls 9.0.51, and minimum Android API 24 (Android 7.0). The verified APK includes ARM64 (`arm64-v8a`, for phones) and x64 (`x86_64`, for emulators) native/managed payloads; it does not target 32-bit phones. The owner-requested .NET/MAUI 9 development baseline is out of MAUI support; production rollout still requires a supported toolchain and acceptance tests.

## Standalone phone installation

Android Debug builds now set `EmbedAssembliesIntoApk=true`, so the managed app/core code is included instead of requiring Visual Studio fast deployment. The signed Debug APK can be copied to a phone and installed independently of Visual Studio.

1. Copy `artifacts/android/OBS_eKYC-android-debug.apk` to the Android phone.
2. Open it in the phone's file manager. If Android prompts, allow that source to install the APK.
3. Open OBS_eKYC and test login/OTP, permitted navigation, session refresh, logout, and background/resume behavior.
4. The phone must be able to reach the configured HTTPS backend. Backend URL/header settings are embedded from `src/OBS.Maui/app.ini`; changing them requires rebuilding the APK. Use an authorized test environment/account.

The signed APK uses the standard development key in ignored workspace artifacts. It is not a production release/signing setup. No device installation or runtime behavior is claimed verified by packaging alone.

The standalone Debug build completed with zero warnings/errors. Both `OBS.Maui` and `OBS.Core` managed payloads were verified inside each packaged architecture, and the resulting signed APK was copied to `artifacts/android/OBS_eKYC-android-debug.apk` (117,834,994 bytes, approximately 112 MiB). It contains the latest case-insensitive session parser fix. This is a full Debug package, not an optimized release package.

## Visual Studio 2022

To expose Android in Visual Studio, add `<EnableAndroidTarget>true</EnableAndroidTarget>` in the first PropertyGroup of `src/OBS.Maui/OBS.Maui.csproj`, **before the TargetFrameworks lines**, then reload the project. Keep `OBS.Maui` as the startup project, select the Android framework/device/emulator, and press F5. Android SDK/JDK and an authorized USB-debugging connection or emulator are required. The default project still shows Windows only unless the Android option is enabled.

## CLI rebuild

Configure the embedded app/theme settings before packaging. From the repository root:

```powershell
dotnet restore src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true
dotnet build src/OBS.Maui/OBS.Maui.csproj -p:EnableAndroidTarget=true -f net9.0-android -p:_ApkDebugKeyStore=C:\mkpulga\obs_ekyc\obs_eKYC\artifacts\android\debug.keystore
```

The generated signed file is `src/OBS.Maui/bin/Debug/net9.0-android/com.ibayad.obsekyc-Signed.apk`. The friendly APK in `artifacts/android` is a copy made after verification. ADB wasn't able to query connected targets from the agent session; phone/emulator testing must still be performed by the owner.
