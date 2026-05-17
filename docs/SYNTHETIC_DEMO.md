# Synthetic Demo

The public demo path uses Bruce Wayne as a clearly synthetic blank test patient.

Use:

```text
sample-data/bruce-wayne/
```

The public repo should not ship Bruce Wayne medical history, fake labs, fake medication lists, fake diagnoses, or fake visit summaries. The safer demo is a zero-data patient that the tester fills with their own fake records.

Recommended first exercise:

1. Create or open a blank Bruce Wayne chart.
2. Add one fake note, fake record, or fake Data Hunter answer.
3. Confirm Dolly treats the answer as user-provided context.
4. Accept a fake uploaded record only after review.
5. Confirm accepted evidence and user-provided context remain separate.

Rules:

- No real PHI.
- No real uploaded photos.
- No real screenshots.
- No real portal records.
- No real labs, imaging, or medication records.
- Any tester-created demo records must be clearly synthetic.

Synthetic data should demonstrate workflows without resembling a real person's private medical history.
