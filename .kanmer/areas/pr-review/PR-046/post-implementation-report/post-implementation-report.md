# Post-implementation report — PR-046

## Change

- `docs/capabilities.md`: reconciled the MAIL-09 note so the general local implementation evidence coexists with the pre-existing QDOS-direct `Now / 0.1.0-alpha.1` allocation and ADR-0020 link. The row remains `Next / 0.3.0`; no live/deployed claim was added.

## Verification

- Documentation links: 192 files passed.
- Release build: passed, 0 warnings/errors.
- Diff inspection confirms only the MAIL-09 row changed for this blocker.

## Simplification

One sentence restores the lost scheduling fact; no other schedule, policy, or document changed. No unapplied findings.
