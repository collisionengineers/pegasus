# Plan — PR-038

## Approach

1. Add a filtered unique index for one pending/uncertain operation per retained message while retaining unique operation keys.
2. Keep terminal rows outside the active constraint so a new key after failure remains valid.
3. Add LocalDB concurrency evidence with a gated mover so both requests overlap at the claim boundary.
4. Run focused persistence and migration checks, then update reports and traceability.

## Governing docs

`docs/frd/frd-08-email-mailbox-and-background-processing.md` requires explicit retry without duplicate movement; database serialization enforces that behavior. No ADR is needed because this strengthens the existing dedicated operation boundary.

## Risks

SQL/provider split remains; uncertain operations deliberately retain the active slot until same-key recovery resolves them.

## Simplification pass — 2026-08-20

- **Reuse:** Extended the existing dedicated operation table and EF claim path.
- **Simplification:** A single filtered unique index is the only new serialization mechanism; no lock service or generic operation framework.
- **Efficiency:** The database rejects overlapping active claims and the reserved operation probes exact current location before any provider move.
- **Altitude:** SQL owns concurrency; the provider adapter remains unaware of claims.
- **Unapplied findings:** none.
