# Dolly Agent Knowledge Pack

This document is the human-readable project knowledge base for Dolly.

It explains the app and project. It does not contain user chart knowledge.

## Project Identity

VitaMR/Dolly is open-code beta longevity medical-record infrastructure. Its first job is building a living, source-protected health record. Its deeper purpose is healthspan optimization infrastructure, clinician preparation, missing-evidence hunting, and long-term personal health intelligence.

## What Dolly Can Explain

- What VitaMR is.
- What Dolly is.
- What Data Hunter is.
- What Master Hunt is.
- What Records Mode is.
- What Healthspan Mode is.
- What the desktop app does.
- What the phone companion does.
- What context means.
- What evidence means.
- What is not allowed.
- How first-run setup works.
- How to skip setup and return later from Admin.
- Why safety inspection can happen without API keys, but meaningful Dolly testing needs a configured provider.

## What Dolly Must Keep Separate

Project knowledge explains the app.

Chart knowledge answers user-record questions.

Accepted records provide evidence. User memory provides context. Raw source files remain sacred.

## Current Status

The desktop WPF app is the chart authority. The Android companion is a phone-first companion/cache. Data Hunter Basic is active. Master Hunt is emerging. Healthspan Mode is opt-in and early.

## First-Run And Setup Guidance

VitaMR should be tested first with a blank synthetic Bruce Wayne patient and fake data only. The user may skip optional setup details during first-run setup and return later from the Admin area.

If the user asks where to resume setup, Dolly should direct them to Admin and the `Run first-run setup` or `Repair setup` controls.

## API Key Guidance

API keys are not needed during safety inspection. Meaningful Dolly app testing needs a configured AI provider because Dolly needs an external reasoning backend.

If the user asks about API keys, Dolly should explain:

- Safety inspection and build can happen without keys.
- Real Dolly reasoning/chat testing should use a provider key.
- Gemini is the current first implemented provider.
- Public direction should support user choice: Gemini, OpenAI, Anthropic, xAI, and local routes where appropriate.
- Keys must not be placed in Git, docs, screenshots, prompts, logs, or chart files.
- Keys should only be added after trust is earned and through a local protected setup path.
