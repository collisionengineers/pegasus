# Post-implementation report — MAIL-09

## Outcome

Implemented the accepted narrow automatic retained-mail association policy in the durable queued-intake live and replay paths. Core associates only one unique current Case selected by a system-wide normalized VRM or an exact mailbox/conversation thread; qualifying evidence must agree. Missing, zero, multiple, contradictory, or changed evidence abstains. Inbound Case/PO is absent from the matching contract.

## Implementation

- Extracted the existing Case registration normalization grammar for its second caller.
- Added one focused Core evidence record and `AssociateRetainedMailWithCase` use case.
- Reused `IAutomaticCaseAssociationStore` and `EfIntakeMutationStore`'s existing serializable, idempotent, append-only automatic association write.
- Added an optional expected-evidence fingerprint; Infrastructure re-reads exact current evidence inside the same transaction and refuses stale evidence before mutation. The precondition is deliberately excluded from stable request identity so the write's own receipt-version increment does not break replay.
- Added one shared current-association precedence query. Active manual/automatic rows win; an inactive staff-reversed row suppresses accepted-link fallback. Both thread evidence and retained Inbox projection use it.
- Wired live and completed-work replay calls after the existing provider-specific automatic association, only while no current association exists, with advisory failure and preserved cancellation semantics.
- Updated FRD-08, capabilities, and current architecture with local-only claims.

No EF schema/store/runtime/migration, generic matcher/framework, Web mutation, MCP surface, mailbox provider, permission, deployment, or external write was added.

## Acceptance traceability

- **Unique normalized VRM:** Core matrix and SQL evidence associate the one current non-archived Case before thread evidence exists.
- **Exact thread:** SQL evidence sees one current Case only for the same mailbox and conversation; the same conversation in another mailbox yields zero.
- **Zero/multiple/contradiction:** Core matrix covers zero/multiple VRM, multiple thread candidates, and disagreeing unique candidates; every case abstains without calling the store.
- **Stale evidence:** SQL changes receipt VRM after evidence read; the serializable store throws `IntakeAssociationConflictException` and writes no association/history, then fresh evidence succeeds.
- **Replay/history:** SQL repeats the same receipt-scoped operation after success and proves one association row and one immutable history row.
- **Current association/read surface:** retained detail resolves the automatic manual-association row through the shared current precedence and returns the Case.
- **Real caller:** both live and completed-replay branches of `ProcessQueuedIntake` call the focused use case after the landed provider-specific attempt; Release and architecture composition tests resolve the complete caller graph.
- **No external write:** all evidence uses Core fakes and disposable LocalDB; no Graph, Box, cloud, deployment, mailbox, or production operation ran.

## Verification

- `dotnet restore Pegasus.slnx --locked-mode` — passed.
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- Full `Pegasus.Core.Tests` — 860 passed.
- Full `Pegasus.ArchitectureTests` — 98 passed.
- Focused `AutomaticMailCaseAssociationTests` — 7 passed.
- Focused `CaseMatchIntegrationTests|RetainedMailPersistenceTests` — 33 passed.
- `git diff --check` — passed.

## Simplification

See the dated four-lens disposition in the plan. One idempotency finding was applied; no findings remain unapplied.
