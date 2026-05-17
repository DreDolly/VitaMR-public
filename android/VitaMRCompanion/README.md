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
