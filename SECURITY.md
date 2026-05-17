# Security Policy

VitaMR/Dolly is an early beta prototype. Treat it as inspectable source code, not trusted medical infrastructure.

## Supported Use

This public staging copy is intended for:

- source inspection
- synthetic rebuild testing
- agent-assisted safety review
- local-only experimentation with synthetic data

It is not intended for real medical use until a user has inspected, rebuilt, configured, and verified it locally.

## Sensitive Data Rules

Do not commit:

- real medical records or PHI
- uploaded photos or phone images
- screenshots containing private data
- API keys, tokens, secrets, passwords, or certificates
- logs containing private paths, prompts, model outputs, or health data
- local vaults, chart packets, raw sources, or OCR output
- machine-specific configuration

Use `.example` files for configuration patterns and keep real local config ignored.

## Medical Safety Boundary

VitaMR/Dolly is not medical advice and is not a clinician replacement.

Users should seek qualified medical professionals for medical decisions, symptoms, diagnoses, treatments, emergencies, medication questions, lab interpretation, imaging interpretation, and any health concern requiring professional judgment.

## Reporting Issues

For a private staging repo, record findings in `docs/GITHUB_READINESS_AUDIT.md`.

Before public release, add a public vulnerability reporting process and review this file with qualified legal/security support.

