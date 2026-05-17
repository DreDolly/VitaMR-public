# VitaMR / Dolly

<p align="center">
  <img src="Resources/VitaMR_Heart.png" alt="VitaMR heart icon" width="96" />
</p>

Open-code beta longevity medical-record infrastructure.

VitaMR/Dolly is a WPF desktop prototype with an Android companion app. Its first job is helping a person build a living, source-protected health record. Its deeper direction is healthspan optimization infrastructure: clinician preparation, missing-evidence hunting, and long-term personal health intelligence.

This repository is a public-safe staging copy intended for inspection, synthetic rebuild, and laptop verification before any public GitHub release.

The long-term public goal is unusual on purpose: a cautious user should be able to give this README to Codex, Claude, or a similar coding agent and ask that agent to reconstruct a local VitaMR/Dolly prototype with little or no direct execution of the repository code. The repository should serve as the reference implementation and verification source. Brave users can build the repo directly; cautious users can use the README and docs as the reconstruction blueprint.

## Do Not Blindly Trust This Repo

Do not blindly run unknown code from GitHub, including this project.

Recommended trust path:

1. Read this README first.
2. Ask your own coding agent to review the repository.
3. Build only with synthetic data.
4. Verify local file, network, credential, and OS behavior.
5. Use real data only after trust is earned.

The project is designed so a cautious user can inspect the source, rebuild locally, and test with a blank synthetic Bruce Wayne patient before considering any private use.

## What VitaMR/Dolly Is

- A local-first medical-record organization prototype.
- A source-protected chart workflow where raw files are treated as sacred.
- A desktop authority with a phone companion front end/cache.
- A place to separate user memory, accepted records, and future healthspan planning.
- A beta-stage open-code project for careful local inspection and synthetic testing.

## What VitaMR/Dolly Is Not

VitaMR/Dolly is not a medical device, clinician, doctor, emergency service, diagnosis engine, treatment engine, prescribing tool, test-ordering tool, triage system, or clinician replacement.

Any use as medical advice is outside the project intention.

Users should seek qualified medical professionals for medical decisions, symptoms, diagnoses, treatments, emergencies, medication questions, lab interpretation, imaging interpretation, and any health concern requiring professional judgment.

Before public release, this disclaimer and intended-use boundary should be reviewed by a qualified legal professional.

## Agent Safety Review Prompt

Copy this prompt into Codex, Claude, or another coding agent before running this project:

```text
Please inspect this VitaMR/Dolly repository before I run anything.

Do not run code yet.
Do not use real medical records.
Do not add API keys yet.
Do not connect to external services unless I explicitly approve it.

Review the repository for:
- possible computer impact
- file-system impact
- OS impact
- network behavior
- credentials/API keys
- external services
- local servers
- scripts
- dependencies
- hardcoded paths
- PHI risk
- secrets
- machine-specific assumptions
- uploaded photos or screenshots
- whether the project can be rebuilt safely from README + repo using synthetic data only

Return one of:
- safe to inspect only
- safe to build with synthetic data
- not safe yet / needs fixes first

Also list any risks, unclear code paths, commands I should avoid, and files that should not be public.
```

## System Topology

- Desktop WPF remains the chart authority.
- Android companion remains a phone-first companion/cache/front end.
- C# owns routing, validation, persistence, scoring, safety gates, and final chart writes.
- AI assists but does not own the record.
- Local privacy/OCR sidecars are bootstrapped by the C# app.
- AI provider and local model integrations must be reviewed before use and configured only after the user chooses a provider.

## Evidence Philosophy

- Raw source files remain sacred and should not be modified after capture.
- User memory provides context.
- Accepted records provide evidence.
- A clue is not evidence.
- A claim is not evidence.
- Master Hunt evidence points require accepted vault evidence.
- Best-practice research must remain separate from verified chart facts.

## Synthetic Demo First

Use only synthetic data during public rebuild testing.

This staging repo includes a blank synthetic test-patient area:

```text
sample-data/bruce-wayne/
```

Bruce Wayne is the intended public test patient, but the public repo should not ship preloaded medical history. The recommended first exercise is to create a blank Bruce Wayne chart, add fake records or fake Data Hunter answers, and watch how VitaMR/Dolly separates user-provided context from accepted evidence.

Do not use real screenshots, uploaded photos, portal exports, medical records, or personal health data during this first test.

## Development Environment

Expected baseline:

- Windows development machine
- Visual Studio or .NET SDK capable of building the WPF project
- Android Studio / Android SDK for the companion app
- Java runtime compatible with the Gradle wrapper
- Optional local model/OCR/privacy dependencies only after inspection

## Get The Source

The current public branch is `master`.

Preferred clone command:

```powershell
git clone https://github.com/DreDolly/VitaMR-public.git
cd VitaMR-public
```

If downloading a ZIP from GitHub, use the branch shown by GitHub's **Code** button. Do not assume a `main.zip` URL unless the default branch is changed to `main`.

## Supported Platforms And Recommended Specs

Current official build target:

- Windows desktop app plus Android companion app.
- The desktop app is WPF on `.NET net10.0-windows`, so it is Windows-only in the current prototype.
- The Android companion is Kotlin/Jetpack Compose with `compileSdk 35`, `targetSdk 35`, and `minSdk 26`/Android 8.0+.

Minimum practical laptop specs for synthetic build testing:

- Windows 11 preferred; Windows 10 may work if the required .NET and Android tooling is installed.
- x64 CPU.
- 8 GB RAM minimum; 16 GB recommended.
- SSD strongly recommended.
- 20-40 GB free disk space if installing Visual Studio, Android Studio, and Android SDK components.
- .NET SDK capable of building `net10.0-windows`.
- Visual Studio or compatible .NET build tools with WPF support.
- Android Studio or Android SDK with SDK 35.
- Java 17 for the Android Gradle build.

Android phone/emulator target:

- Android 8.0+ device or emulator.
- The phone must connect to the desktop mobile API using the desktop PC LAN address, not `127.0.0.1`.

Meaningful Dolly testing:

- Safety inspection and synthetic build testing do not require provider keys.
- Meaningful Dolly chat/workflow testing requires a user-configured AI provider key.
- Gemini is the first implemented external provider today.
- OpenAI, Anthropic, xAI, and local provider routing are planned but not fully implemented.

Optional local helper specs:

- Local OCR/privacy sidecars use Python services on localhost.
- Local model workflows may use Ollama on `localhost:11434`.
- For local model work, 16 GB RAM is a practical minimum; 32 GB RAM is more comfortable.
- A modern GPU can help local model performance but is not required for basic synthetic build testing.

Mac and Apple platform status:

- A Mac can inspect the repository and can usually build the Android companion if Android Studio, Java 17, and SDK 35 are installed.
- A Mac cannot natively build or run the current WPF desktop app because WPF is Windows-only.
- To build the current desktop app from a Mac, use Windows in a VM, a remote Windows machine, Windows CI, or another Windows build environment.
- A future Apple-native desktop path would require a separate port, such as Avalonia, .NET MAUI, Electron/Tauri, or another cross-platform UI shell.

iPhone status:

- There is no current VitaMR iPhone companion app in this repo.
- iPhone sideloading is not a dependable default distribution plan for this project. Apple allows alternative iOS app distribution mainly in the EU under specific marketplace/Web Distribution, notarization, and developer-account rules.
- For general users, a future iPhone companion should assume App Store/TestFlight distribution or a web/PWA companion path unless the project deliberately builds an EU alternative-distribution track.

Desktop project:

```text
VitaMR.csproj
```

Android project:

```text
android/VitaMRCompanion/
```

## Prerequisite Checks

Do not attempt a build until the required toolchain for that build path is installed and the checks below pass. For a full Windows desktop + Android companion test, install and configure all required desktop and Android tooling first.

Run these checks before building in PowerShell:

```powershell
dotnet --info
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

Expected:

- `.NET` SDK can build `net10.0-windows`.
- `Microsoft.WindowsDesktop.App` is present for the WPF desktop build.
- `java -version` reports Java 17 for Android builds.
- `JAVA_HOME` points to a JDK 17 install.
- `ANDROID_HOME` or `ANDROID_SDK_ROOT` points to the Android SDK.
- Android SDK includes Platform 35, Build-Tools, and Platform-Tools.

If any required tool is missing, stop before building. Codex, Claude, or any other coding agent may help install or configure missing tooling only after the user explicitly approves the machine-level change.

## Setup Order For Cautious Builders

1. Inspect first.
   - Read `README.md`.
   - Read `docs/GITHUB_SAFETY_REVIEW_CHECKLIST.md`.
   - Read `docs/GITHUB_READINESS_AUDIT.md`.
   - Read `android/VitaMRCompanion/README.md` before Android build work.

2. Check prerequisites.

   ```powershell
   dotnet --info
   java -version
   $env:JAVA_HOME
   $env:ANDROID_HOME
   $env:ANDROID_SDK_ROOT
   ```

3. Install or configure missing tools before building.
   - For desktop-only testing, the required tools are `.NET SDK` and `Microsoft.WindowsDesktop.App` for WPF.
   - For Android testing, the required tools are JDK 17, Android Studio or Android command-line tools, Android SDK Platform 35, Build-Tools, Platform-Tools, `JAVA_HOME`, and `ANDROID_HOME` or `ANDROID_SDK_ROOT`.
   - If these are missing, do not try the build yet.
   - Agents must clearly tell the user what is missing and ask permission before installing or changing machine-level configuration.

4. Desktop path.
   - The Windows desktop app needs .NET/WPF tooling.
   - If `.NET SDK` and `Microsoft.WindowsDesktop.App` are present, build:

     ```powershell
     dotnet build VitaMR.csproj --configuration Debug
     ```

   - If the user-level NuGet config permission issue appears, use the documented workspace-local workaround below.
   - Desktop can be built without Android tooling.

5. Android path.
   - The Android companion cannot build until Android tooling is installed and configured.
   - Required tooling:
     - JDK 17.
     - Android Studio or Android command-line tools.
     - Android SDK Platform 35.
     - Android SDK Build-Tools.
     - Android SDK Platform-Tools.
     - `JAVA_HOME` set to the JDK 17 folder.
     - `ANDROID_HOME` or `ANDROID_SDK_ROOT` set to the Android SDK folder.
   - Missing Java or Android SDK configuration is a local machine setup gap, not a repo defect.
   - If `java -version` fails or the Android environment variables are blank, stop and report the missing tooling.
   - Do not install Android Studio, JDK 17, or Android SDK components without explicit user approval.

6. User role.
   - Approve or perform machine-level setup.
   - Install .NET SDK / Windows Desktop workload if desktop tooling is missing.
   - Install JDK 17.
   - Install Android Studio or Android command-line tools.
   - Install SDK Platform 35, Build-Tools, and Platform-Tools.
   - Accept Android SDK licenses.
   - Set environment variables.
   - Decide whether to run the app after build.

7. Agent role.
   - Inspect docs first.
   - Classify safety before build.
   - Run prerequisite checks.
   - Do not try to build a path until that path's required tools are installed and configured.
   - Clearly report missing tools and ask for approval before installing .NET, Android Studio, JDK 17, Android SDK components, or changing environment variables.
   - Build desktop if safe.
   - Use the NuGet workaround if needed.
   - Stop and report missing Android tooling.
   - Build Android only after tooling exists or the user explicitly approves setup.
   - Do not run the app, add keys, connect services, or use real data unless explicitly approved.

8. Android build after setup.

   ```powershell
   java -version
   $env:JAVA_HOME
   $env:ANDROID_HOME
   $env:ANDROID_SDK_ROOT

   cd android/VitaMRCompanion
   .\gradlew.bat assembleDebug
   ```

Android tooling is required before Android build or phone runtime testing. It is not required before the desktop build. Physical phone testing uses the desktop PC LAN IP, not `127.0.0.1`. Keep `sample-data/bruce-wayne/` blank except for its README and `.gitkeep` placeholder unless synthetic sample records are intentionally approved later.

## Rebuild Plan

1. Inspect the repo and docs.
2. Confirm no real PHI, screenshots, API keys, or private logs are present.
3. Build the WPF desktop app.
4. Build the Android companion app if Java/Android SDK tooling is installed.
5. Start with a blank synthetic Bruce Wayne patient and add only fake test data.
6. Confirm local server, file, and network behavior before adding any private data.

Desktop build command:

```powershell
dotnet build VitaMR.csproj --configuration Debug
```

Android build commands:

```powershell
cd android/VitaMRCompanion
.\gradlew.bat assembleDebug
```

Do not run the app until after inspection and synthetic build testing are complete.

## Android Tooling Setup

The Android companion uses:

- Kotlin / Jetpack Compose.
- `compileSdk 35`.
- `targetSdk 35`.
- `minSdk 26` / Android 8.0+.
- Java 17.

If Android tooling is missing:

1. Install Android Studio, or install Android command-line tools.
2. Install JDK 17.
3. Install Android SDK Platform 35.
4. Install Android SDK Build-Tools.
5. Install Android SDK Platform-Tools.
6. Set `JAVA_HOME` to the JDK 17 folder.
7. Set `ANDROID_HOME` or `ANDROID_SDK_ROOT` to the Android SDK folder.
8. Open a new terminal and rerun the prerequisite checks.

If Android tooling is missing, agents should stop and report the missing component. Install Android Studio, JDK 17, or Android SDK components only with explicit user approval.

Physical phone testing uses the desktop PC LAN IP, not `127.0.0.1`. Example:

```text
http://192.168.1.100:5057
```

Replace the example address with the actual desktop PC LAN address.

## Troubleshooting

### NuGet.Config Permission Error

Symptom:

```text
Access to the path 'C:\Users\<user>\AppData\Roaming\NuGet\NuGet.Config' is denied.
```

Likely cause:

- The .NET SDK is trying to read a user-level NuGet config that the current terminal, agent, or sandbox cannot access.

Safe fixes:

1. Run the build from a normal user terminal that has access to the user's NuGet config.
2. Repair file permissions on the user NuGet config if they are wrong.
3. Use a workspace-local NuGet config for synthetic build testing.

Workspace-local workaround:

```powershell
mkdir .local-appdata
mkdir .local-roaming
mkdir .nuget

$env:LOCALAPPDATA = (Resolve-Path .local-appdata).Path
$env:APPDATA = (Resolve-Path .local-roaming).Path

@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -Path .nuget\NuGet.Config -Encoding UTF8

dotnet build VitaMR.csproj --configuration Debug --configfile .nuget\NuGet.Config
```

Do not put API keys or private paths in NuGet config files.

### Android Java Not Found

Symptom:

```text
ERROR: JAVA_HOME is not set and no 'java' command could be found in your PATH.
```

Fix:

- Install JDK 17.
- Set `JAVA_HOME` to the JDK 17 folder.
- Open a new terminal.
- Confirm `java -version` works.

## Agent Reconstruction Goal

The README is intended to become the Gold README: the canonical rebuild blueprint for coding agents.

Agent reconstruction target:

- Use the README and linked docs as the primary build instructions.
- Use the repository as a reference implementation, not as blind executable trust.
- Recreate the desktop WPF project structure.
- Recreate the Android companion structure.
- Recreate safety boundaries, synthetic demo rules, and project knowledge docs.
- Verify behavior using a blank synthetic Bruce Wayne patient and fake data only.
- Avoid API keys, real medical records, uploaded photos, screenshots, and external services unless explicitly approved.

The desired trust model:

```text
README first.
Agent review second.
Synthetic reconstruction third.
Local verification fourth.
Direct repo execution only after trust is earned.
```

## Dolly Agent Knowledge Pack

Runtime/project knowledge is separated from rebuild docs:

```text
docs/AGENT_KNOWLEDGE_PACK.md
agent-knowledge/
```

README rebuilds. Markdown explains. JSON controls.

Project knowledge explains the app. Chart knowledge answers user-record questions. The two must not be mixed.

## API Keys And Provider Choice

Do not add API keys during the safety inspection step.

After inspection, a meaningful Dolly app test should use an AI provider key. Without a configured provider, the app can still be inspected, built, and checked for local behavior, but Dolly will not have her real external reasoning backend.

The current prototype is Gemini-centered because Gemini is inexpensive for testing. Public VitaMR/Dolly should not force Gemini long term. The intended public direction is provider choice:

- Gemini
- OpenAI
- Anthropic
- xAI
- local model routes where appropriate

Provider keys must stay local and must never be committed to Git, docs, screenshots, prompts, logs, or chart files.

See:

```text
docs/API_KEY_SETUP.md
```

## GitHub Readiness

Before any public release, review:

```text
docs/GITHUB_READINESS_AUDIT.md
SECURITY.md
```

The public repo should contain no real PHI, no real uploaded photos, no private screenshots, no API keys, no private logs, and no old private Git history.

## License

This project is licensed under the Apache License 2.0. See the LICENSE file for details.
