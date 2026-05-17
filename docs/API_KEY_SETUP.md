# API Key Setup

API keys are not needed for first safety inspection. They are needed for meaningful Dolly app testing because Dolly needs an AI provider to act as her external reasoning backend.

## Default Public Test Path

For a cautious public rebuild:

1. Inspect the repo first.
2. Confirm no secrets, PHI, private screenshots, or unsafe scripts are present.
3. Build only after inspection passes.
4. Choose an AI provider.
5. Add the provider key only through a local protected setup path.
6. Use the blank Bruce Wayne synthetic test patient.
7. Add only fake test data.
8. Confirm local file and network behavior.

## What Keys Are For

The current prototype includes Gemini-backed services for chart Q&A, document classification, extraction, and sterile wiki-writing workflows.

Gemini is the first implemented provider because it is inexpensive for testing. Public VitaMR/Dolly should eventually support provider choice, including Gemini, OpenAI, Anthropic, xAI, and local model routes where appropriate.

C# remains responsible for routing, privacy gates, validation, persistence, and final chart writes. AI assists but does not own the record.

## Current Prototype Status

The code currently includes a local Windows DPAPI-backed Gemini key store:

```text
Services/WindowsDpapiGeminiApiKeyStore.cs
```

The key store writes protected local key material under the current Windows user's local app data folder, not into the Git repo.

Current Provider Setup behavior:

1. The user can inspect/build without keys.
2. Dolly explains that meaningful app testing needs a chosen provider key.
3. The user can choose Gemini, OpenAI, Anthropic, xAI, or Local in the Provider Setup panel.
4. The app can save or clear a local protected key for each provider slot.
5. Gemini is the active implemented external provider today.
6. OpenAI, Anthropic, xAI, and Local are visible provider slots, but request routing still needs implementation.
7. A clear Admin control lets the user return to setup later.

At this stage, laptop safety inspection can happen without API keys. Meaningful Dolly workflow testing should wait until the user deliberately configures a provider key.

## Guidance For Codex / Claude Setup Agents

When helping a user set up VitaMR/Dolly:

- Do not ask for API keys during the safety review.
- Do not put API keys into source files.
- Do not commit API keys.
- Do not write API keys to `.env`, `appsettings.json`, docs, prompts, logs, screenshots, or chat transcripts.
- Do not connect external services until the user explicitly approves.
- First inspect and build the project safely.
- Before meaningful Dolly testing, help the user choose an AI provider.
- If a polished key-entry UI is not available, stop and report that API setup needs a local-only UI before public guidance should tell users to paste keys.
- Do not force Gemini. Gemini is the first implemented provider, but public direction is user-selected provider support.

## Dolly Guidance

If a user asks Dolly about API keys, Dolly should say:

```text
You can inspect and build VitaMR without API keys, but meaningful Dolly testing needs an AI provider key. Gemini is currently the first implemented provider; future public setup should also support OpenAI, Anthropic, xAI, and local routes. Do not put keys into Git, docs, screenshots, prompts, logs, or chart files.
```

If a user asks why Gemini is unavailable, Dolly should say:

```text
The external AI backend is not configured yet. VitaMR can still be inspected and built, but Dolly's real reasoning/chat behavior will be limited until you choose and configure a provider key.
```

## Public Release Requirement

Before public release, finish provider routing beyond Gemini. The setup workflow should continue to:

- support provider choice rather than forcing Gemini,
- keep keys out of Git,
- store keys only in local protected storage,
- show whether keys are configured without displaying the full key,
- allow clearing/replacing keys,
- keep inspection/build possible without keys while making clear that meaningful Dolly testing needs a configured provider.
