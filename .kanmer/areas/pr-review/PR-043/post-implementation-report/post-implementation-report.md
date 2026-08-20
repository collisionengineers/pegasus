# Post-implementation report — PR-043

## Outcome

Matching replay of a Pending folder move is now refused as still processing without probing or mutating the operation. The Pending row therefore retains the filtered active slot and blocks different keys while the original provider call is in flight. If the original provider call throws, its row is durably changed to Uncertain before the existing recovery probe, so only genuinely recoverable Uncertain operations use that path.

## Files

- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs`: separated Pending refusal from Uncertain replay recovery and persisted Uncertain after provider exception before probing.
- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`: added deterministic overlapping same-key/new-key proof and parent-probe count to the existing blocking mover.

## Governing docs and simplicity

This satisfies FRD-08’s duplicate-safe, explicit and recoverable move behavior. The four-lens disposition is recorded in the plan. No Core/Web contract, state taxonomy, lease, worker, framework, provider adapter, migration, permission, live mailbox or deployment scope was added.

## Verification

- Exact same-key/different-key plus genuine Uncertain recovery set — 5/5 passed.
- Provider failure/freshness/reclassification regression set — 3/3 passed.
- Full `RetainedMailPersistenceTests` — 24/24 passed.
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- `git diff --check` and staged diff check — passed.
- All evidence used local SQL and fake provider behavior; no external write occurred.

## Traceability

- Commit: `83293162`
- Pull request: https://github.com/collisionengineers/pegasus/pull/477
- Handoff: Review for independent `kanmer-review`; no self-review or merge.
