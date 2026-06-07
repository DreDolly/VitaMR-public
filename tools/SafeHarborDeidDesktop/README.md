# Safe Harbor Cleaner

**Beta version:** `1.3-beta`  
**License:** Apache License 2.0  
**Platform:** Windows desktop  
**App type:** Local-first WPF desktop app  
**Default mode:** deterministic, non-AI de-identification attempt

Safe Harbor Cleaner is a small local Windows app for producing **HIPAA Safe Harbor candidate** de-identification from clinical text and supported document files.

It is built for privacy-first local use. Files are opened on your computer, text is extracted locally, and the default non-AI cleaning path does not call any cloud service.

This tool is an implementation aid. It does **not** prove HIPAA compliance, create legal certification, replace privacy/compliance review, or make content automatically safe to publish or share.

Use wording such as:

- `Safe Harbor candidate`
- `attempted Safe Harbor de-identification`
- `needs human review`

Avoid wording such as:

- `HIPAA compliant`
- `legally de-identified`
- `safe to publish`

## Quick Download

After this repository is pushed to GitHub, the public download zip is intended to live here:

```text
https://github.com/DreDolly/VitaMR-public/raw/master/downloads/SafeHarborCleaner-v1.3-beta-win-x64.zip
```

Download, unzip, then run:

```text
SafeHarborCleaner.exe
```

If Windows SmartScreen warns you, that is expected for an unsigned beta app. Only run it if you trust the source, have reviewed the repository, or have asked your own coding agent/security reviewer to inspect it first.

## Build From Source

Source folder:

```text
tools/SafeHarborDeidDesktop
```

Project file:

```text
tools/SafeHarborDeidDesktop/SafeHarborDeidDesktop.csproj
```

Build command from the repository root:

```powershell
dotnet build tools\SafeHarborDeidDesktop\SafeHarborDeidDesktop.csproj
```

Run from source:

```powershell
dotnet run --project tools\SafeHarborDeidDesktop\SafeHarborDeidDesktop.csproj
```

Publish a framework-dependent Windows build:

```powershell
dotnet publish tools\SafeHarborDeidDesktop\SafeHarborDeidDesktop.csproj -c Release -o tools\SafeHarborDeidDesktop\publish
```

Publish a self-contained Windows x64 build:

```powershell
dotnet publish tools\SafeHarborDeidDesktop\SafeHarborDeidDesktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o publish\SafeHarborCleaner-v1.3-beta-win-x64
```

## Requirements

### To Use The Direct Download, Non-AI Mode

Recommended:

- Windows 10 or Windows 11, 64-bit
- No API key
- No cloud account
- No Ollama/local model server
- No Python
- No Tesseract/OCR setup

The release zip is intended to be self-contained for Windows x64, so ordinary users should be able to unzip and run `SafeHarborCleaner.exe`.

If you build a framework-dependent version instead, install:

- .NET 9 Desktop Runtime for Windows

### To Build From Source

Install:

- Windows 10 or Windows 11
- .NET 9 SDK or newer compatible SDK
- Git, if cloning from GitHub
- Internet access for NuGet package restore on first build

NuGet package used for PDF text extraction:

- `PdfPig` version `0.1.14`

### To Use AI Mode

AI Mode is optional and experimental. The app works without it.

AI Mode requires:

- The non-AI requirements above
- A local Ollama-compatible server listening at:

```text
http://127.0.0.1:11434
```

Default model names in code:

```text
Privacy model: llama3.2:3b
Preservation model: gemma4:e4b
```

The model names are currently configured in `MainWindow.xaml.cs`:

```csharp
private const string AiEndpoint = "http://127.0.0.1:11434";
private const string PrivacyModel = "llama3.2:3b";
private const string PreservationModel = "gemma4:e4b";
```

If you use different local model names, update those constants and rebuild.

AI Mode behavior:

1. The deterministic Safe Harbor scrub runs first.
2. The local privacy model reviews the scrubbed result for possible missed identifiers.
3. The preservation model checks whether important clinical content may have been over-redacted.
4. The result still needs human review.

AI Mode does not send data to OpenAI, Gemini, Anthropic, or any cloud model by default. It talks to the local endpoint above. You are responsible for verifying your local model server and network setup before using real data.

## Supported Inputs

Reliable in `1.3-beta`:

- Pasted text
- `.txt`
- `.md`
- `.log`
- `.csv`
- `.tsv`
- `.json`
- `.xml`
- `.html`
- `.htm`
- `.docx` text extraction
- Selectable text `.pdf` extraction

Blocked or later-phase:

- Scanned/image-only PDFs
- Images
- Audio/video
- Legacy `.doc`

PDF support is for selectable text PDFs only. Scanned pages, signatures, embedded images, and image-only pages are not OCR-reviewed in this version.

## Current PDF Behavior

- **Open File** supports selectable text PDFs.
- Drag/drop supports selectable text PDFs.
- Open File and drag/drop use the same loader path.
- Extracted PDF text appears in the Input pane as plain text.
- The app does not create cleaned PDFs yet.
- Saving creates clean text plus an audit JSON report.

If no selectable text is found, the app shows:

```text
No selectable PDF text was found. This may be a scanned or image-based PDF, which is still blocked.
```

If text is extracted, the app shows:

```text
Text PDF loaded. Embedded images, signatures, and scanned page content are not reviewed by this version.
```

Verified local test during development:

```text
Synthetic Bruce Wayne PCP follow-up PDF generated with ReportLab
```

That ReportLab-generated PDF previously failed with the old parser. In `1.3-beta`, it loads readable note text before cleaning.

## How To Use

1. Open `SafeHarborCleaner.exe`.
2. Paste text, choose **Open File**, or drag a supported file onto the app.
3. Review the extracted Input text.
4. Choose **Clean Data**.
5. Review the Clean Result.
6. Use **Copy** to copy the result, or **Save Result** to save:

```text
safe-harbor-redacted.txt
safe-harbor-redacted.audit.json
```

The audit JSON contains replacement counts, findings, warnings, and blocked reasons.

## What Gets Removed Or Flagged

The deterministic scrubber attempts to remove or flag common HIPAA Safe Harbor identifier categories, including:

1. Names when detected by supported patterns
2. Geographic subdivisions smaller than state when detected by supported patterns
3. Dates related to an individual, except years where appropriate
4. Ages over 89
5. Telephone and fax numbers
6. Email addresses
7. Social Security numbers
8. Medical record numbers
9. Health plan beneficiary numbers
10. Account and claim numbers
11. Certificate and license numbers
12. Vehicle identifiers and license plates
13. Device identifiers and serial numbers
14. URLs
15. IP addresses
16. Biometric identifier terms
17. Visual/media file risks
18. Other unique codes when detected by supported patterns

Free text can contain unusual jobs, events, locations, family structures, school/team details, rare diagnoses, or local clues that identify someone even after obvious identifiers are removed. Treat `safe_harbor_candidate` as a candidate status, not a legal conclusion.

## Privacy Model

Default non-AI mode:

- No cloud API call
- No API key required
- File text extraction happens locally
- Cleaning happens locally in C#
- Results stay on your machine unless you copy, save, share, or upload them yourself

AI Mode:

- Optional
- Local-only by default
- Requires a local Ollama-compatible endpoint
- Still needs user review

## CLI-Enabled AI Build Prompt

You can give this prompt to Codex, Claude, or another local coding agent:

```text
Please inspect and build Safe Harbor Cleaner from this repository.

Do not use real medical records.
Do not call cloud APIs.
Do not enable AI Mode unless I explicitly approve it.

Tasks:
1. Review tools/SafeHarborDeidDesktop for file access, network behavior, and dependencies.
2. Confirm the project is a Windows WPF app targeting .NET 9.
3. Confirm the license is Apache-2.0.
4. Build with: dotnet build tools/SafeHarborDeidDesktop/SafeHarborDeidDesktop.csproj
5. Optionally publish a self-contained win-x64 build.
6. Test with synthetic text only.
7. Explain what files are read, written, and saved by the app.
8. Explain the difference between non-AI mode and AI Mode.
```

## Known Limits

- This beta is not a legal compliance product.
- The app does not OCR scanned PDFs yet.
- The app does not inspect embedded images, signatures, faces, or screenshots inside PDFs.
- The app does not clean audio or video.
- The app does not preserve original document formatting in the cleaned output.
- The app does not generate redacted PDFs yet.
- Deterministic detection can miss identifiers in unusual free text.
- Local AI review is experimental and depends on your local model quality.

## Roadmap

Near-term polish:

- Add a visible inline PDF caution after PDF load, not only a status line.
- Add a zoom/text-size control.
- Add a small public synthetic test fixture set.
- Add a reviewer panel for accepting/rejecting AI suggestions.

Later:

- OCR for scanned PDFs with explicit visual/biometric review warnings.
- Better document parsing and formatting preservation.
- Stronger synthetic benchmark suite.
- Signed Windows builds if the project moves beyond beta.

## License

Safe Harbor Cleaner is released under the Apache License 2.0 as part of this public repository. See the repository-level `LICENSE` file.

Third-party package:

- PdfPig, used for selectable PDF text extraction. See its NuGet package metadata and upstream license for details.

## Medical And Legal Disclaimer

Safe Harbor Cleaner is not medical advice, legal advice, a medical device, a HIPAA compliance guarantee, or a substitute for qualified privacy/compliance review.

For real clinical, legal, regulatory, or publication decisions, consult qualified professionals.



