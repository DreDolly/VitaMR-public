# GitHub Safety Review Checklist

Use this checklist before any VitaMR/Dolly public release, laptop rebuild test, or feature promotion from the private working repo into the public staging repo.

## First Rules

- Do not run the project before inspection.
- Do not build before inspection.
- Do not add API keys.
- Do not connect external services.
- Do not use real medical records.
- Inspect files first, then decide whether synthetic build testing is appropriate.

## Final Classifications

- `safe to inspect only`: the repo can be read, but should not be built or run yet.
- `safe to build with synthetic data`: no blockers were found for a local synthetic-only build.
- `not safe yet / needs fixes first`: blockers must be fixed before build, run, sharing, or public release.

## Finding Labels

- `BLOCKER`: must fix before public release or synthetic build testing.
- `REVIEW`: needs human decision or clearer documentation.
- `OK`: expected, synthetic, design-only, or acceptable.

For each finding, record:

- File path
- What was found
- Why it matters
- Suggested fix
- Whether it blocks public release
- Whether it blocks synthetic build testing

## Allowed Exceptions

### Bruce Wayne Blank Synthetic Test Patient

Bruce Wayne is the approved synthetic test patient/persona for public repo preparation. The public repo should start Bruce with zero preloaded medical data.

Users may add fake Bruce Wayne chart data, fake records, fake Data Hunter answers, fake Master Hunt evidence, fake labs, fake visits, fake medications, and fake demo records during their own local testing if they are clearly synthetic and not copied from a real person. These generated local test files should not be committed back to the public repo.

Still review Bruce Wayne files for accidental real PHI, secrets, real addresses, real phone numbers, real portal data, real physician data, real facility data, and machine-specific details.

### Styling And Reference Images

Styling/reference images, icons, UI thumbnails, layout mockups, and visual design references are acceptable if they do not reveal private data, real medical data, API keys, private computer paths, real screenshots with sensitive content, or secrets.

Images are not automatic blockers. Classify them as `OK`, `REVIEW`, or `BLOCKER` based on content.

## Review Areas

### 1. Personal Or Private Data

Look for real names, emails, phone numbers, addresses, usernames, personal notes, private project journals, contact lists, calendar exports, browser profile paths, and local user folders such as `C:\Users\`, `OneDrive`, `Desktop`, and `Downloads`.

Classify real private data as `BLOCKER`. Documentation examples may be `REVIEW`. Synthetic placeholders may be `OK`.

### 2. Medical / PHI / Health Data

Look for real patient names, DOBs, MRNs, addresses, phone numbers, labs, medications, diagnoses, allergies, imaging reports, vaccines, visits, discharge summaries, physician names tied to real care, hospital/clinic data, pharmacy data, insurance data, portal screenshots, OCR output, chart summaries, and logs containing chart data.

Blank Bruce Wayne test scaffolding may be `OK`. Preloaded Bruce Wayne health data in the public repo should be `REVIEW` unless explicitly approved. Real PHI is a `BLOCKER`. Unclear health data is `REVIEW`.

### 3. Photos, Images, Screenshots, And PDFs

Inspect `.png`, `.jpg`, `.jpeg`, `.webp`, `.gif`, `.bmp`, `.tiff`, `.heic`, `.svg`, and PDFs. Look for real people, profile photos, portal screenshots, desktop screenshots, file explorer screenshots, usernames, paths, emails, API keys, terminal output, phone notifications, private repo names, and chart data.

Safe icons/mockups may be `OK`. Unclear images are `REVIEW`. Sensitive images are `BLOCKER`.

### 4. API Keys, Secrets, Tokens, And Credentials

Search for OpenAI, Gemini, Google, Firebase, Anthropic, GitHub, Supabase, Azure, AWS, database, SMTP, JWT, OAuth, signing, keystore, certificate, and encryption secrets.

Review likely files such as `.env`, `appsettings*.json`, `secrets.json`, `google-services.json`, `credentials.json`, `service-account.json`, `launchSettings.json`, `local.settings.json`, `*.keystore`, `*.jks`, `*.pfx`, `*.pem`, and `*.key`.

Real secrets are `BLOCKER`; rotate anything ever committed.

### 5. Computer / Machine-Specific Data

Look for absolute paths, private Windows usernames, hardcoded repo paths, hardcoded vault paths, machine names, local IPs, MAC addresses, device IDs, Android device IDs, emulator IDs, browser profile paths, and Visual Studio local paths.

Phone rule: the Android companion should not rely on `127.0.0.1` for PC access. LAN host settings should be configurable or documented.

### 6. Logs, Cache, Build Artifacts, And Generated Files

Look for `logs/`, `traces/`, `telemetry/`, `cache/`, `temp/`, `exports/`, `backups/`, `dumps/`, crash reports, `bin/`, `obj/`, `.vs/`, `.idea/`, `.gradle/`, `build/`, `dist/`, `out/`, `.dotnet-home/`, and `node_modules/`.

Tracked generated/build/cache folders should usually be removed and ignored. Logs containing PHI, prompts, secrets, paths, OCR output, or screenshots are `BLOCKER`.

### 7. Databases, Vaults, And Local Storage

Inspect `.db`, `.sqlite`, `.sqlite3`, `.db-shm`, `.db-wal`, `.mdb`, `.accdb`, JSON vault files, local storage exports, chart folders, raw source folders, OCR output folders, embeddings, and vector DB files.

Blank Bruce Wayne test scaffolding may be `OK`. Real chart/vault data is `BLOCKER`.

### 8. LLM / Agent Prompt Data

Review system prompts, developer prompts, model logs, prompt templates, prompt history, raw model responses, conversation transcripts, user memory files, context packets, chart packets, RAG chunks, vector store text, and embeddings source text.

Prompts containing real PHI, secrets, or unsafe medical behavior are `BLOCKER`.

### 9. Network Behavior And External Services

Inspect code for `HttpClient`, `WebClient`, sockets, WebSocket, SignalR, gRPC, Firebase, Google APIs, OpenAI, Gemini, Anthropic, cloud storage, telemetry, update checkers, analytics, local servers, LAN servers, mobile bridge endpoints, and background sync.

Raw PHI sent externally without explicit approval is `BLOCKER`. Document all ports, endpoints, and startup behavior.

### 10. Scripts And Computer-Impacting Commands

Inspect `.bat`, `.cmd`, `.ps1`, `.sh`, `.py`, `.js`, `.ts`, `.csx`, Makefiles, Dockerfiles, setup scripts, migration scripts, cleanup scripts, postinstall scripts, Gradle scripts, and MSBuild targets.

Look for file deletion, moving, PATH changes, registry edits, service installation, firewall changes, permission changes, executable downloads, remote script execution, background processes, and access outside the repo.

### 11. Android-Specific Safety

Check for `google-services.json`, Firebase credentials, signing keys, keystores, `local.properties`, hardcoded LAN IPs, desktop host defaults, debug/release APKs, personal device IDs, Android logs, and permissions.

Review `INTERNET`, `CAMERA`, media/storage, microphone, location, and package-query permissions.

### 12. Medical Safety / Product Claims

Review docs, UI text, prompts, and code for claims that VitaMR/Dolly diagnoses, recommends treatment, tells users what medication to take, tells users to stop/start medication, orders tests, triages symptoms, escalates emergencies, replaces clinicians, guarantees longevity, or promises lifespan extension.

The project should state that VitaMR/Dolly is not medical advice, diagnosis, treatment, triage, prescribing, test ordering, emergency guidance, or clinician replacement.

### 13. Git History Risk

Review whether any commit in the public staging repo ever contained secrets, PHI, screenshots, config files, logs, databases, or raw records. Because public staging should be a fresh repo, any history risk should be small, but still check before making it public.

### 14. Public Release Documentation

Check for `README.md`, `LICENSE`, `SECURITY.md`, `CONTRIBUTING.md`, `ROADMAP.md`, `.gitignore`, example config files, synthetic sample data, architecture docs, getting started docs, synthetic demo docs, laptop rebuild docs, safety boundary docs, and readiness audit docs.

Missing docs may be `REVIEW`, not always a blocker for private staging.

### 15. Synthetic Demo Data

Confirm the public repo starts with a blank approved synthetic persona: Bruce Wayne.

Flag preloaded medical data, other patient/persona names, or unclear health data as `REVIEW` unless explicitly approved. Real or copied health data is `BLOCKER`.

## Required Audit Output

Update `docs/GITHUB_READINESS_AUDIT.md` with:

1. Summary classification.
2. Findings table.
3. Allowed exceptions.
4. Next steps before synthetic build testing.
5. Next steps before laptop rebuild testing.
6. Next steps before public GitHub release.

Do not delete, move, or rewrite risky files without first reporting what was found and why it matters.
