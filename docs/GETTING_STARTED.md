# Getting Started

Start with `README.md`.

Recommended first run for direct repo users:

1. Inspect the source.
2. Run the agent safety review prompt from the README.
3. Build desktop locally.
4. Build Android companion locally.
5. Start with the blank Bruce Wayne synthetic test patient.
6. Add only fake records, fake notes, or fake Data Hunter answers.
7. Do not add API keys or real medical records until the project is trusted.
8. Before meaningful Dolly chat/workflow testing, choose and configure an AI provider through a local protected setup path.

If setup is skipped, open the Admin area later and use `Run first-run setup` or `Repair setup`.

API keys are not needed for safety inspection, but they are needed for meaningful Dolly testing. See `docs/API_KEY_SETUP.md` before adding any external service key.

Local helper behavior:

- VitaMR may start local-only privacy/OCR sidecars on `127.0.0.1` ports `8001` and `8000` when those workflows request them.
- VitaMR may warm or call a local Ollama endpoint on `localhost:11434` when local model workflows request it.
- The Android companion talks to the desktop mobile API on port `5057`. A real phone must use the desktop PC LAN address, not `127.0.0.1`.

Android permissions:

- `INTERNET` and `ACCESS_NETWORK_STATE` support the LAN companion connection.
- `USE_BIOMETRIC` supports local device unlock flows.
- Cleartext traffic is enabled for the current LAN prototype and should be revisited before broader release.

Recommended first run for cautious users:

1. Give `README.md` to a coding agent.
2. Ask it to inspect the repo without running code.
3. Ask it to reconstruct a synthetic-only local build plan from the README and docs.
4. Use the repo only as a reference implementation until trust is earned.

Desktop entry point:

```text
VitaMR.csproj
```

Android entry point:

```text
android/VitaMRCompanion/
```
