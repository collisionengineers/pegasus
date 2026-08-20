# Files — PR-013

- `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs` — update tracked children in place, remove absent keys, add new keys.
- `tests/Pegasus.IntegrationTests/AdministrationPolicyPersistenceTests.cs` — relational refresh coverage for unchanged, changed, removed, and added bindings in one transaction.

Exact overlap with TICK-064 is intentional on PR #468. No migration, Core, Web, or permission change.
