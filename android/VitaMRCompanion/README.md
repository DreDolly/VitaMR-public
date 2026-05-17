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

## Desktop requirements

Open VitaMR desktop first. It should report:

```powershell
Invoke-RestMethod http://127.0.0.1:5057/mobile/health
Invoke-RestMethod http://127.0.0.1:8001/health
Invoke-RestMethod http://127.0.0.1:8000/health
```

All should return `ready` while VitaMR is open.

## Android Studio

Open this folder in Android Studio:

```text
android/VitaMRCompanion
```

Build and run the `app` configuration. For a physical phone, set the desktop URL in Settings to the desktop LAN IP:

```text
http://<desktop-ip>:5057
```

The app currently allows cleartext HTTP because v1 is LAN-only. Before any remote use, add HTTPS or an encrypted local tunnel.
