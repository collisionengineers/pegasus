# Post-implementation report — PR-045

## Change

- `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs`: added two real `ProcessQueuedIntake` caller tests plus test-only delegates/recorders. Live and completed replay both prove provider→MAIL-09→allocation ordering, allocation observes the existing associated Case, and later replay skips both association attempts.

No production code, policy, adapter, schema, migration, or external operation changed.

## Verification

- New caller tests: 2 passed.
- Full `QdosAllocationRecoveryTests`: 17 passed.
- Release build: passed, 0 warnings/errors.
- No live/external writes.

## Simplification

Reused the existing disposable SQL fixture, real processor, real association store/evidence query, and retained-message helper. One shared method covers both paths; the delegation is test-only and copies no business policy. No unapplied findings.
