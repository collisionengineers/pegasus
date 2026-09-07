# Proof — <ticket id>

*The proof. Not the report — this is **evidence from the configured integration branch after review and merge**, not a description of what was built.*

When the requirement names a flavour (`proof:visual`, `proof:test`), use the
matching template instead: `proof-visual-template.md`, `proof-test-template.md`.

Verification evidence gathered on **the configured integration branch after review and merge** (not the feature branch) by
`kanmer-verify` — the **Verifying → Done** gate. Real output only: paste what
actually ran, not what should have.

## What was verified

- The behaviour/requirement, how, and the exact integration commit it was verified on.

## Evidence

```
test/command output pasted here
```

## Not covered

- Anything knowingly unverified, and why that's acceptable (or a follow-up
  ticket id).

## Verification identity and attempts

- Integration branch: resolve from get_status.delivery.integrationBranch.
- Exact SHA, environment, command exit codes and evidence paths.
- Retain every failed or inconclusive attempt and its disposition.
- Deployment and live operator acceptance are separate proof.
- PASS is required for ordinary Done; this template grants no waiver.
