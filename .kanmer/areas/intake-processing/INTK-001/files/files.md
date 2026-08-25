# Files — INTK-001

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Extend the bounded queued-status contract/state mapping so retry-scheduled work is truthful and its due time can drive presentation. Risk: this is shared Core policy and currently overlaps unmerged [[INTK-040]] work. |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedIntakeStatusQueries.cs` | Project work state/due time and resolve the current Case using manual-association precedence as well as accepted Case links. Risk: an inactive association must suppress, not fall back to, an accepted link. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml.cs` | Derive the exact heading and bounded refresh delay from the corrected status contract. Remove message/lede behavior that no longer belongs on the surface. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml` | Remove lede narration and emit the corrected optional refresh delay while retaining file facts, outcome, and actions. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Suspend the existing shared automatic-reload behavior while `document.hidden`, then resume safely when visible. Risk: grouped Upload Status is a second concrete caller of this helper. |
| `tests/Pegasus.IntegrationTests/RecoveryTests.cs` | Replace the assertion that RetryScheduled appears as Received with the truthful public state/due-time contract. |
| `tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs` | Prove retry presentation/refresh, no lede, allocation link remains correct, and a Case reached only through an active manual/automatic association produces “Open case”; cover inactive-association precedence if the fixture supports reversal. |
| browser or focused JavaScript test under `tests/Pegasus.IntegrationTests/Browser/` | Prove a hidden tab does not reload and visible state resumes the bounded refresh through the real shared script. Reuse the existing Playwright/browser-test convention; do not invent a JavaScript test stack. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Align the required staff-visible status/retry/current-Case behavior with [[INTK-041]]. Risk: this file currently overlaps [[INTK-040]] and must be edited only after that work is integrated. |
| `docs/design/README.md` | Replace the obsolete four-state/fixed-two-second Upload Status row with the settled truthful state and page-economy behavior. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | `IntakeReceipt.CurrentCaseId` is the authority for accepted-vs-manual association precedence; manual-association version existence matters even when inactive. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | Shows how the receipt projection supplies accepted and manual association inputs to the Core-owned derivation. |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` | Contains an existing private persistence helper mirroring the same current-Case rule; search here before introducing a third copy. |
| `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs` and `UploadGroupStatus.cshtml` | The second current `data-auto-refresh` caller; shared script changes must not stop grouped submissions from progressing when visible. |
| `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs` | Existing association/current-Case fixtures and outcome behavior that can be reused for the focused status regression. |
| `docs/design/README.md` | No-explanatory-copy/page-economy authority and the current obsolete fixed-refresh component statement. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Governs durable receive, Worker-only processing, public queued state, current association, and fail-closed Case creation. |
| [[INTK-041]] research and docs | Settles truthful Processing for retry/large work and the near-real-time performance contract. |
| [[INTK-042]] | Owns immediate after-commit queue publication; INTK-001 observes states but must not implement dispatch. |
| [[SIMPLI-008]] and [[SIMPLI-009]] research/proof | Records why the status page exists, its existing evidence, and the exact review defects from which this ticket arose. |
| active [[INTK-040]] ticket/worktree | Owns mailbox image-intake changes in overlapping Core, FRD, and tests; do not touch or copy its uncommitted work. |

## Ripple effects

- Every constructor or test factory creating `QueuedIntakeStatus` must be updated if the bounded contract gains state/due fields; `UploadOutcomeQueriesTests` is a known direct constructor surface.
- The shared `site.js` behavior affects both single and grouped upload status pages and every future element opting into `data-auto-refresh`; verification must cover the existing group caller.
- The current design row and FRD state language must change with the implementation so code and canonical behavior do not describe different state vocabularies.
- No schema migration is expected: `IntakeWorkItems.State`, `DueAtUtc`, `CaseIntakeLinks`, and `IntakeManualAssociations` already hold the required facts.

## Out of scope

- Immediate queue publication or reconciliation schedules ([[INTK-003]], [[INTK-042]]).
- Graph mailbox notifications, sender derivation, or mailbox polling ([[MAIL-013]]).
- Source-reader/classification/extraction performance ([[INTK-043]]).
- Any new queue, background worker, cache, endpoint, database table, migration, feature flag, or compatibility path.
- Editing, committing, or rebasing the active [[INTK-040]] worktree.
- Deployment, Azure configuration, mailbox mutation, or production proof.
