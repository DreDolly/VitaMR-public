# Laptop Rebuild Test

Use this protocol before public GitHub release.

1. Copy or clone the public staging repo to a separate laptop.
2. Confirm no private Git history exists.
3. Inspect files for secrets, PHI, images, screenshots, and local paths.
4. Build `VitaMR.csproj`.
5. Build `android/VitaMRCompanion`.
6. Run only with a blank synthetic Bruce Wayne patient.
7. Add only fake test data during the first workflow check.
8. Confirm no unexpected network calls occur.
9. Confirm no real data paths are required.
10. Confirm `.gitignore` blocks local runtime data.
11. Record findings in `docs/GITHUB_READINESS_AUDIT.md`.

Network and local-process checks:

1. Confirm the Android host is a configurable LAN desktop URL, not a required private address.
2. Confirm phone testing uses the desktop PC LAN address, not `127.0.0.1`.
3. Confirm local sidecar behavior is understood before running OCR/privacy flows: privacy on port `8001`, OCR on port `8000`.
4. Confirm local model behavior is understood before running local AI flows: Ollama on `localhost:11434`.
5. Confirm Android permissions match the prototype scope: LAN internet access, network state, biometric unlock, and cleartext LAN traffic.

Agent reconstruction check:

1. Give only the README and docs to a coding agent.
2. Ask whether the project can be reconstructed with synthetic data without blindly running repo code.
3. Compare the reconstructed plan against the reference repo.
4. Record any missing README details that forced the agent to rely too heavily on source inspection.
