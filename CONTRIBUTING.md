# Contributing

This project is not ready for broad public contribution yet. This file exists so early reviewers and coding agents understand the expected safety posture.

## Contribution Principles

- Use synthetic data only.
- Do not add real PHI.
- Do not add real screenshots or uploaded photos.
- Do not add hardcoded API keys, tokens, or local machine paths.
- Keep chart evidence separate from user-provided context.
- Keep best-practice research separate from verified chart facts.
- Do not add medical advice, diagnosis, treatment, triage, prescribing, test ordering, or emergency guidance behavior.

## Before Opening A Change

Run a local safety review:

- Build desktop.
- Build Android companion if touched.
- Check for secrets and private paths.
- Check for generated files.
- Update docs if behavior changes.

