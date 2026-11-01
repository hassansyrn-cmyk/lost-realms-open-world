# Local Android toolchain — 30 October 2026

Codex, Unity 6000.6.0f1. User authorized toolchain repair, local APK build and deployment of the separate development app to the connected HONOR.

## Installed tools
Unity's official release manifest for revision f7f8ed4d1e24 supplied all download URLs and expected byte counts. Packages are from download.unity3d.com / dl.google.com, extracted under ignored `Tools/downloaded_models/toolchain/UnityAndroid6000.6.0f1`:
- OpenJDK Temurin 17.0.18+8; java -version verified.
- Android NDK r27c, revision 27.2.12479018; source.properties verified.
- SDK platforms Android 34 and 36, build-tools 36.0.0, platform-tools 36.0.0.
- Command-line tools 16.0 and CMake 3.22.1.
- SDK license setup completed with sdkmanager. Archive byte counts match Unity manifest; no separate cryptographic integrity verification claimed.

`Assets/Editor/AndroidToolchainSetup.cs` configures JDK/SDK/NDK using Unity's AndroidExternalToolsSettings API. Previous path values are backed up once in the ignored tools folder. These paths are machine-local Editor preferences, not portable dependencies committed to Git. No system-wide Java/PATH changes or Program Files modifications were needed.

The helper's BuildInEditor wrapper writes a dated RUNNING/FAIL result and calls the existing build function while keeping the interactive Editor open; the batch Run entry would exit it. The existing Lost Realms OW > Build > Android Development APK menu uses the configured external tools too.

## Input configuration blocker
First repaired-toolchain build passed Android content preparation/Addressables (15.5 s) but failed on Active Input Handling = Both. Project gameplay uses UnityEngine.Input and the touch HUD creates StandaloneInputModule. Set activeInputHandler to 0 (legacy Input Manager) through SerializedObject; only that ProjectSettings line changed. No touch/gameplay code changes. A temporary helper compilation error from using an unavailable API was corrected; compile at 16:19:00 has zero errors. Second build started 16:19:48 Dubai.

## Build/device result
IN PROGRESS. Read Docs/android-build-result.txt for the current attempt and verify the actual APK, not an old report.
Windows detects HONOR Magic7 Pro as WPD/file transfer, but ADB device list is empty as of 16:18 Dubai. User asked to enable USB debugging and accept computer authorization. No device installation claimed yet.

## Reuse
Do not remove/move the local tools folder while Unity preferences refer to it. On another PC install the corresponding Unity Android submodules or provision the official manifest versions, then configure that machine's paths. Do not commit downloaded toolchains. No vendor/Photon/sibling modifications were made by this setup.

Official manifest: https://services.api.unity.com/unity/editor/release/v1/releases?version=6000.6.0f1
Unity tools API: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/android/androidexternaltoolssettings