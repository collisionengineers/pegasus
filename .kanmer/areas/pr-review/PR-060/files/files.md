# Files — PR-060

Surveyed before planning.

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260824123336_DropEvaHandoffTables.cs` | Correct the migration’s leading comments. Do not alter `Up()`, `Down()`, generated designer metadata, or schema shape. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/adr/0030-non-additive-schema-changes-before-cutover.md` | Accepted authority: before cutover, this direct removal is allowed and recovery past it is roll-forward, never back. |
| `src/Pegasus.Core/Eva/EvaBundleSchema.cs` | `ExportCaseBundleRequest` carries the operation key used for exact replay/history. |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | Shows the split: every distinct Export uses its operation key in `ActionHistory`; `EvaFirstHandoffProxies` records only the first-send case fact. |
| ENG-016 `post-implementation-report` | Already states roll-forward recovery and the replay-safe action-history contract; use it as the evidence wording to preserve. |
| PR #539 description | Already states direct pre-cutover removal, no rollback compatibility, and roll-forward recovery. |

## Ripple effects

- Review the final PR diff to prove only comments changed for this ticket.
- No compiled callers or tests change because the migration operations are untouched.
- A build is sufficient proportional verification; migration up/down/up behaviour is already unchanged.
- Re-read the PR description and ENG-016 report after the edit to ensure all four statements agree.

## Out of scope

- Changing Export replay implementation or concurrency handling; [[PR-055]] owns that.
- Changing migration operations, generating another migration, editing the model snapshot/designer, or adding compatibility/rollback machinery.
- Deleting proxy rows or modifying production data.
- Rewriting ADR-0030, the PR description, or ENG-016 report unless implementation work finds a concrete remaining contradiction.
