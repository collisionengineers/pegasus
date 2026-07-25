# Validation evidence

Use one concise Markdown package per validation pass. Keep commands and result summaries sufficient to reproduce the conclusion without copying secrets, message bodies, or corpus contents.

## Suggested artifact shape

`risk-matrix.md`:

| Risk | Proof | Procedure | Pass condition |
| --- | --- | --- | --- |
| Incorrect caller wiring | Real entry-point exercise | Named command or manual route | Policy owner receives the expected request |
| Regression | Targeted tests | Exact command | All named tests pass |

`validation-results.md`:

1. Repository consistency — commands, statuses, and scope.
2. Product behavior — real caller, inputs, observed output, negative paths.
3. Skipped or blocked checks — reason, risk, and next owner.
4. Readiness — ready, ready with stated limits, or remediation required.

`remediation-NNN.md`:

- Reproduction and direct facts.
- Expected and actual behavior.
- Broken acceptance criterion and affected owner.
- Required change and revalidation evidence.

Do not force a fixed number of tests, browser sessions, or artifacts. The matrix must be proportionate to the change and its failure modes.
