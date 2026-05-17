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

## Rebuild Plan

1. Inspect the repo and docs.
2. Confirm no real PHI, screenshots, API keys, or private logs are present.
3. Build the WPF desktop app.
4. Build the Android companion app.
5. Start with a blank synthetic Bruce Wayne patient and add only fake test data.
6. Confirm local server, file, and network behavior before adding any private data.

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
