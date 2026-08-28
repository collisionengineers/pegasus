# Post-implementation report — MAIL-017

## Delivered

- `src/Pegasus.Infrastructure/Persistence/Migrations/20260827100901_ReactivateBoundApprovedMailboxes.cs` (+ Designer): raw-SQL `UPDATE` restoring `ActivatedAtUtc` on Approved, identity-bound mailboxes whose activation was nulled; empty `Down`.
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`: migration-head assertion extended.
- `docs/operations.md`: release-33 entry records the de-activation defect and this repair.
- Model snapshot unchanged (no model change); `dotnet ef migrations add` produced no snapshot diff.

Branch `task/mail-017-reactivate-mailbox`, commit `bd34b1a0`, worktree `../pegasus-worktrees/mail-017-reactivate-mailbox`.

## Verification (Windows, PowerShell 7)

| Command | Result |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | OK |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | Build succeeded, 0 warnings |
| `dotnet test … --filter "FullyQualifiedName~IntakePersistenceIntegrationTests"` | 10/10 passed |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` | Core 1001/1001, Architecture 100/100, Integration 987/987, exit 0 (log `artifacts/mail-017/test-full.log`, ignored) |

## Live state (read-only, prod)

The operator re-saved the mailbox at 10:20:33Z (interim action); the 10:25Z tick created the Graph subscription (`Active`, expires 2026-09-02) and polled; a later re-forward arrived through the webhook in 7 s. On deploy this migration's `UPDATE` will match nothing in prod and is retained for any database walking the release-33 chain. See `scratch/live-evidence.md`.

## Deviations / follow-ups

None to the plan. Follow-up tickets: MAIL-018, MAIL-019, MAIL-020 (App Insights cap), MAIL-021, INTK-044 (failed Audit allocation, no staff recovery route).
