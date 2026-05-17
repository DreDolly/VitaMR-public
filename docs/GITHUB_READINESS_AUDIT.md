# GitHub Readiness Audit

Last updated: 2026-05-17

Checklist source: `docs/GITHUB_SAFETY_REVIEW_CHECKLIST.md`

## Summary Classification

Current status: `safe for public source inspection and synthetic-data build testing`, with `REVIEW` items to resolve before broader beta use.

No blocker was found in the current tracked file tree during the Day 17 safety pass. Inspection happened before build testing. No API keys were added, no external services were connected, no real medical records were used, and the app was not launched.

## Release Strategy

- Keep the original private repo private.
- Use this public staging folder as a separate Git repo with separate history and remote.
- Public source visibility is acceptable after the clean one-commit history rewrite and final safety scan.
- Use a blank synthetic Bruce Wayne test patient only.
- Promote new features from private to public deliberately, then rerun this audit before pushing or sharing.

## Allowed Exceptions

- Bruce Wayne blank synthetic patient/persona scaffolding is allowed. The public repo should not ship preloaded Bruce medical history.
- Styling/reference images, app icons, and UI mockup assets are allowed if they do not expose private data, PHI, secrets, screenshots, local paths, notifications, or machine details.

## Findings

| Classification | File path | Issue | Why it matters | Suggested fix | Blocks public release? | Blocks synthetic build? |
|---|---|---|---|---|---|---|
| OK | Current tracked tree | No real API keys, tokens, passwords, or credential-like strings found outside docs/checklists. | Secrets would require key rotation and must never be public. | Keep real keys outside Git. Continue using local DPAPI/user secrets patterns. | No | No |
| OK | Current tracked tree | No tracked databases, SQLite files, raw vaults, uploaded records, PDFs, HEIC/WebP images, `.env` files, keystores, PEM/key files, or Google/Firebase config files found. | These are common PHI/secret leak paths. | Keep `.gitignore` exclusions active and rerun before each release. | No | No |
| OK | `Resources/VitaMR_Heart.png`; Android launcher PNGs | Only tracked image files found are app icons/launcher assets. | Image files can hide screenshots, real photos, or PHI. These appear to be application assets. | Keep screenshots, photos, and real chart images out of Git. | No | No |
| OK | `.gitignore` | Build outputs, local data, secrets, logs, local Android files, `.dotnet-home`, vault/raw/OCR folders, PDFs, HEIC/WebP, and caches are ignored. | Prevents common private artifacts from being accidentally staged. | Keep this file reviewed whenever new storage folders are added. | No | No |
| REVIEW | Local working folder only: `bin/`, `obj/`, `android/VitaMRCompanion/.gradle/`, `android/VitaMRCompanion/app/build/` | Ignored build/generated folders exist on this machine but are not tracked by Git. | They should not matter for a Git clone, but they should not be included in ZIP exports or manual folder copies. | Before creating any ZIP/export, clean ignored build folders or export from a fresh clone. | No, if publishing via Git only | No |
| OK | Git history | Public staging history has been recreated as a single clean release commit. Earlier placeholder/demo-note history is not part of the public release branch. | Public source history should not expose old placeholders, deleted demo data, private repo history, PHI, secrets, screenshots, or logs. | Keep future public release branches clean and rerun history scans before release. | No | No |
| OK | Git history string scan | `sk-` history hits were task IDs such as `TASK-004`, not API key prefixes. No `AIza` history hits were found. | Secret-shaped history hits need verification before release. | Continue scanning history before every public release candidate. | No | No |
| OK | `sample-data/bruce-wayne/` | Bruce Wayne sample folder now contains no preloaded medical note or chart history. | A blank synthetic patient reduces leakage risk and lets testers build confidence by adding their own fake data locally. | Keep local fake test data untracked and out of public commits. | No | No |
| REVIEW | `android/VitaMRCompanion/app/src/main/java/com/dredolly/vitamr/MainActivity.kt` | Android default host is `http://192.168.1.100:5057`. | It is a generic LAN placeholder, not a credential, but the phone host must be configured for the user's PC and must not rely on `127.0.0.1` for PC access. | Keep documented in laptop rebuild docs; consider making first-run setup clearer before public release. | No | No |
| REVIEW | `android/VitaMRCompanion/app/src/main/AndroidManifest.xml` | Android declares `INTERNET`, `ACCESS_NETWORK_STATE`, `USE_BIOMETRIC`, and allows cleartext traffic. | These are understandable for a LAN companion prototype, but privacy-sensitive permissions and cleartext LAN behavior should be explained. | Document permissions and LAN-only cleartext rationale before public release. | No | No |
| REVIEW | `Services/*Gemini*.cs`; `Services/WindowsDpapiGeminiApiKeyStore.cs` | Gemini API integration exists and stores configured keys under local app data with Windows DPAPI protection. Backend processing defaults disabled in settings. | External model calls must stay explicit, scrubbed, and user-controlled. Safety inspection can happen without keys, but meaningful Dolly testing needs a configured provider. | Keep README warning: no keys for safety inspection; require provider setup before evaluating Dolly's real behavior. | No | No |
| OK | API setup UX | A local Provider Setup panel now supports provider selection, local protected key save, masked configured/not configured status, and clear key action. | Users need a safe route for provider setup while keeping keys out of Git and screenshots. | Keep testing with Gemini first; do not claim non-Gemini routing is implemented yet. | No | No |
| REVIEW | Multi-provider support | Current implementation is Gemini-centered. OpenAI, Anthropic, and xAI provider paths are not implemented yet. | Public positioning should not imply all provider routes are complete. | Add provider abstraction, local protected key storage per provider, and user-selected provider routing. Until then, keep docs clear that non-Gemini routing is planned. | No, if disclosed as current limitation | No |
| REVIEW | `Services/OllamaLocalModelWarmupService.cs`; `Services/LocalPythonSidecarManager.cs` | Code can start local Ollama and local Python sidecars when the app workflow requests them. | This affects local processes and can surprise cautious users if not documented. | Document local process startup behavior in `GETTING_STARTED` and laptop rebuild instructions. | No | No |
| REVIEW | `Services/GeminiBackendPipelineService.cs`; `ViewModels/MainWindowViewModel.cs` | Code includes file copy, chart archive/move, and temporary staging cleanup logic. | File-system effects are expected for a local record app, but should be understood before real use. | Keep synthetic-only first-run guidance; document that testing should use a throwaway chart folder. | No | No |
| OK | Repo root `LICENSE`; `README.md`; `VitaMR.csproj` | Apache License 2.0 added at repo root, README includes a License section, and the .NET project declares `Apache-2.0`. No `NOTICE` file was added because no project attribution or third-party notice requirement was identified. | Public users need explicit license terms before release. | Keep the license decision reviewed before public release. | No | No |

## Network And External Service Notes

- Gemini service classes call `https://generativelanguage.googleapis.com/...` only when API keys are configured and the related backend path is enabled.
- OpenAI, Anthropic, and xAI API routes are not implemented yet.
- Local Ollama defaults use `http://localhost:11434`.
- Local privacy/OCR sidecars use localhost ports `8001` and `8000`.
- The desktop mobile API uses port `5057`.
- The Android companion default host is a generic LAN placeholder: `http://192.168.1.100:5057`.
- The phone should use the desktop PC LAN address, not `127.0.0.1`, for real device testing.

## Day 17 Synthetic Build Verification

- Desktop WPF build passed for `VitaMR.csproj` with 0 warnings and 0 errors.
- Android companion `assembleDebug` passed after setting `ANDROID_HOME` to the local Android SDK path for this machine.
- Laptop README test found the public repo downloadable from `master`, classified it as `safe to build with synthetic data`, and confirmed blank Bruce plus expected project shape.
- Laptop desktop build passed with `dotnet build VitaMR.csproj --configuration Debug`.
- Laptop Android build did not run because Java/Android SDK tooling was missing; README and Android README now document JDK 17, Android SDK 35, `JAVA_HOME`, `ANDROID_HOME`, and `ANDROID_SDK_ROOT` setup.
- Laptop desktop build initially hit a user NuGet config permission issue; README now documents the symptom and a workspace-local `APPDATA`/`LOCALAPPDATA` workaround.
- The app was not launched.
- No provider keys were added.
- No real chart data or medical records were used.

## Git History Notes

The public staging repo has been recreated as a one-commit public release branch and does not include the original private repo history.

Clean release commit: the one-commit public release branch named `Initial public release`.

## Recommended Next Steps Before Synthetic Build Testing

1. Build only from the current tracked Git tree or a fresh clone.
2. Start with a blank Bruce Wayne synthetic patient.
3. Add only fake test data locally.
4. Do not add API keys.
5. Do not connect external services.
6. Do not use real medical records.
7. Confirm local output folders are ignored and not staged after build.

## Recommended Next Steps Before Laptop Rebuild Testing

1. Give the laptop agent the public GitHub URL.
2. Let the laptop agent inspect first using `README.md`, `docs/GITHUB_SAFETY_REVIEW_CHECKLIST.md`, and `docs/GITHUB_READINESS_AUDIT.md`.
3. Build only after the laptop agent agrees the repo is safe for synthetic build testing.
4. Record any laptop-specific findings in this audit.

## Recommended Next Steps Before Public GitHub Release

1. Add provider-choice routing for OpenAI, Anthropic, xAI, and local routes.
2. Rerun this checklist from a fresh clone after each release-prep change.
3. Consider legal review of README/SECURITY medical disclaimer language.
4. Record laptop-specific findings after public URL testing.

## Current Decision

Current tracked HEAD is safe for public source inspection and synthetic-only build testing. Desktop and Android synthetic builds passed on this machine before the clean history rewrite. The app was not launched, no provider keys were added, and no real medical records were used.
