# Plan — PR-054

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: invalid/stale context fails closed, while valid selected context remains preserved.

## Steps

1. Extract the already-used folder-plus-queue checks into one private `TryParseListContext` helper.
2. Call it after actor resolution and before validation, exact-message reads, lease operations, or mutations in all six POST handlers; reuse it for GET/reload.
3. Add authenticated HTTP proofs for both forged contexts across every handler, with exact database/fake-mover no-effect assertions and valid-context regression coverage.
4. Run focused/proportional verification and update TICK-057 traceability/PIR.

No background work, framework, parser copy, or policy redesign.
