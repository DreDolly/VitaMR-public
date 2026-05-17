# VitaMR Android Companion

First native Android slice for VitaMR mobile companion.

## Current scope

- Connect to VitaMR desktop mobile API on LAN, default port `5057`.
- Pair phone using desktop pairing code.
- Fetch chart roster.
- Chat with Dolly through desktop `/mobile/chat`.
- Use Android voice-to-text and place transcript into the chat box.
- Choose a photo from gallery and send it to `/mobile/capture`.
- Take a camera photo and send it to `/mobile/capture`.
- Manager-approved Android face/fingerprint unlock gate after pairing.
- Keep Saved Packets as a placeholder until desktop packet builder is implemented.

## Manager biometric gate

The Android app uses the phone's operating-system biometric prompt. That can be face unlock, fingerprint, or device credential depending on what the phone supports and what the user enrolled.

VitaMR does **not** store face images, face templates, or biometric identifiers. The phone only tells the app whether Android authentication succeeded.

## Build Prerequisites

The Android companion uses:

- Kotlin / Jetpack Compose.
- `compileSdk 35`.
- `targetSdk 35`.
- `minSdk 26` / Android 8.0+.
- Java 17.

Do not attempt the Android build until the Android toolchain is installed and configured. If Java, Android SDK, or environment variables are missing, stop before building and report the missing component.

Before building, confirm in PowerShell:

```powershell
java -version
$env:JAVA_HOME
$env:ANDROID_HOME
$env:ANDROID_SDK_ROOT
```

CMD-compatible environment checks:

```cmd
echo %JAVA_HOME%
echo %ANDROID_HOME%
echo %ANDROID_SDK_ROOT%
```

Install or configure:

- Android Studio or Android command-line tools.
- JDK 17.
- Android SDK Platform 35.
- Android SDK Build-Tools.
- Android SDK Platform-Tools.
- `JAVA_HOME`.
- `ANDROID_HOME` or `ANDROID_SDK_ROOT`.

If Android tooling is missing, agents should stop and report the missing component. Install Android Studio, JDK 17, or Android SDK components only with explicit user approval.

## Android Toolchain Installation Approval Gate

Full Android companion build testing requires the Android toolchain before the build can run.

Required Android tooling:

- JDK 17.
- Android Studio or Android command-line tools.
- Android SDK Platform 35.
- Android SDK Build-Tools.
- Android SDK Platform-Tools.
- `JAVA_HOME`.
- `ANDROID_HOME` or `ANDROID_SDK_ROOT`.

Before attempting Android setup, Codex, Claude, or any other coding agent must ask the user for explicit approval.

Suggested agent prompt:

```text
Android build tooling is missing or incomplete.

To build the VitaMR Android companion, I need your approval to install/configure:
- JDK 17
- Android command-line tools or Android Studio
- Android SDK Platform 35
- Android SDK Build-Tools
- Android SDK Platform-Tools
- JAVA_HOME
- ANDROID_HOME or ANDROID_SDK_ROOT

This will download developer tools, use disk space, and may require accepting Android SDK licenses.

Do you approve installing/configuring the Android build toolchain on this machine?
```

If the user approves, the agent may proceed with the safest available setup path.

Preferred agent setup path:

1. Prefer Android command-line tools for automated setup.
2. Use Android Studio if the user prefers a GUI install.
3. Install JDK 17.
4. Install Android SDK command-line tools.
5. Install:
   - `platforms;android-35`
   - `build-tools;<latest compatible 35.x>`
   - `platform-tools`
6. Accept Android SDK licenses only after user approval.
7. Set:
   - `JAVA_HOME`
   - `ANDROID_HOME`
   - `ANDROID_SDK_ROOT`
8. Open a new shell/session or refresh environment.
9. Verify:

   ```powershell
   java -version
   $env:JAVA_HOME
   $env:ANDROID_HOME
   $env:ANDROID_SDK_ROOT
   ```

10. Build:

    ```powershell
    cd android/VitaMRCompanion
    .\gradlew.bat assembleDebug
    ```

If the user does not approve:

- Do not install anything.
- Report that Android build is blocked until the user installs/configures the Android toolchain.
- Report that desktop-only build can proceed from the repo root if .NET/WPF tooling is available.

Missing Java or Android SDK is a local setup gap, not a repo defect. Desktop-only build does not require Android tooling. Full project build verification does require Android tooling. Do not add API keys, use real medical records, connect external services, or run the app unless the user explicitly asks.

## Setup Order

1. Inspect first.
   - Read the root `README.md`.
   - Read `docs/GITHUB_SAFETY_REVIEW_CHECKLIST.md`.
   - Read `docs/GITHUB_READINESS_AUDIT.md`.
   - Read this Android README before Android build work.

2. Check Android prerequisites before building.

   ```powershell
   java -version
   $env:JAVA_HOME
   $env:ANDROID_HOME
   $env:ANDROID_SDK_ROOT
   ```

3. Install or configure missing tools before building.
   - If `java -version` fails, JDK 17 is missing or not on `PATH`.
   - If `JAVA_HOME` is blank, set it to the JDK 17 folder after JDK setup.
   - If `ANDROID_HOME` and `ANDROID_SDK_ROOT` are blank, Android SDK tooling is not configured.
   - Missing Java or Android SDK configuration is a local machine setup gap, not a repo defect.
   - Do not run `.\gradlew.bat assembleDebug` until Java 17 and Android SDK tooling are available.
   - Do not install Android Studio, JDK 17, or Android SDK components without explicit user approval.

4. User role.
   - Approve or perform Android tooling setup.
   - Install JDK 17.
   - Install Android Studio or Android command-line tools.
   - Install Android SDK Platform 35, Build-Tools, and Platform-Tools.
   - Accept Android SDK licenses.
   - Set `JAVA_HOME` and `ANDROID_HOME` or `ANDROID_SDK_ROOT`.

5. Agent role.
   - Inspect docs first.
   - Report missing tooling instead of silently installing it.
   - Ask for explicit user approval before installing JDK 17, Android Studio, Android SDK components, or changing environment variables.
   - Build Android only after tooling exists or the user explicitly approves setup.
   - Do not run the app, add keys, connect services, or use real data unless explicitly approved.

6. Build after setup.

   ```powershell
   java -version
   $env:JAVA_HOME
   $env:ANDROID_HOME
   $env:ANDROID_SDK_ROOT

   .\gradlew.bat assembleDebug
   ```

Physical phone testing happens after build and uses the desktop PC LAN IP, not `127.0.0.1`.

## Build

From this folder:

```powershell
.\gradlew.bat assembleDebug
```

If Java is missing, Gradle may fail with:

```text
ERROR: JAVA_HOME is not set and no 'java' command could be found in your PATH.
```

Install JDK 17, set `JAVA_HOME`, open a new terminal, and retry.

## Runtime Desktop Requirements

Open VitaMR desktop first. It should report:

```powershell
Invoke-RestMethod http://127.0.0.1:5057/mobile/health
Invoke-RestMethod http://127.0.0.1:8001/health
Invoke-RestMethod http://127.0.0.1:8000/health
```

All should return `ready` while VitaMR is open.

These runtime checks are for phone testing after inspection/build. They are not required for a source-only safety inspection.

## Android Studio Run

Open this folder in Android Studio:

```text
android/VitaMRCompanion
```

Build and run the `app` configuration. For a physical phone, set the desktop URL in Settings to the desktop LAN IP:

```text
http://<desktop-ip>:5057
```

Do not use `127.0.0.1` from a physical phone. On a phone, `127.0.0.1` points back to the phone, not the desktop PC.

The app currently allows cleartext HTTP because v1 is LAN-only. Before any remote use, add HTTPS or an encrypted local tunnel.
