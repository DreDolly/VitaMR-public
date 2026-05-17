# FAQ

## Is VitaMR a medical advice tool?

No. VitaMR/Dolly is not medical advice and is not a clinician replacement.

## Is Dolly a doctor?

No. Dolly can help organize records and prepare questions, but a qualified medical professional should interpret information for care decisions.

## Can I use real medical records?

For public rebuild testing, no. Use synthetic data first. Only use real data after you have inspected, rebuilt, and trusted your local setup.

## Do I need an API key?

For safety inspection, no. For meaningful Dolly app testing, yes. Dolly needs a configured AI provider key to have her real external reasoning backend.

## Where do API keys go?

API keys should never go into Git, docs, screenshots, source files, prompts, logs, or chart files. The prototype includes local protected key storage for Gemini. Meaningful Dolly testing should use a configured provider key. See `docs/API_KEY_SETUP.md`.

## Which AI provider does VitaMR support?

The current prototype is Gemini-centered. The intended public direction is user choice: Gemini, OpenAI, Anthropic, xAI, and local model routes where appropriate. This multi-provider setup is not fully implemented yet.

## What if I skip setup?

You can return later. Open the Admin area and use `Run first-run setup` or `Repair setup`.

## What is Data Hunter?

Data Hunter is a guided workflow that gathers user-provided context about goals, history, records, care team, and missing evidence.

## What is Master Hunt?

Master Hunt turns clues from user memory into evidence targets. Progress requires accepted vault evidence.

## What counts as evidence?

Accepted records or accepted data in the vault. A memory, clue, or claim is context, not evidence.

## Why is the desktop the authority?

The desktop owns final chart writes, validation, scoring, and safety gates. The phone is a companion window, not the record authority.

## What is Healthspan Mode?

Healthspan Mode is an opt-in future-facing layer for healthspan support. It must remain separate from medical advice and verified chart facts.
