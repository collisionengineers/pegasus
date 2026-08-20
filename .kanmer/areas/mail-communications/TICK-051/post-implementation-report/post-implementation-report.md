# Post-implementation report — MAIL-09

## Outcome

Implemented the accepted narrow automatic retained-mail association policy in the durable queued-intake live and completed-replay paths. Core associates only one unique current Case selected by a system-wide normalized VRM or an exact mailbox/conversation thread; qualifying evidence must agree. Missing, zero, multiple, contradictory, or changed evidence abstains. Inbound Case/PO is absent from the matching contract.

## Exact final-diff inventory

| Path | Rationale |
| --- | --- |
| `docs/capabilities.md` | Records general local MAIL-09 evidence while preserving the QDOS-direct `Now / 0.1.0-alpha.1` allocation and ADR-0020 link; retains the row's general `Next / 0.3.0` schedule and no-live-write boundary. |
| `docs/current-architecture.md` | Describes the as-built queued caller, current evidence query, existing transaction, and retained current-association projection. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Owns the accepted unique normalized-VRM / exact mailbox-thread behavior, agreement, abstention, no Case/PO key, history, reversal precedence, and no mailbox mutation. |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | Exposes the existing Case registration normalization grammar for its second concrete caller instead of duplicating it. |
| `src/Pegasus.Core/Intake/CaseMatching/AutomaticMailCaseAssociation.cs` | Adds the focused Core evidence contract and one choose-or-abstain MAIL-09 use case. |
| `src/Pegasus.Core/Intake/CaseMatching/CaseMatchContracts.cs` | Adds the optional expected-evidence fingerprint to the existing automatic association request. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Invokes MAIL-09 after provider matching in both live and completed-replay paths, refreshes a successful association before allocation, skips associated receipts, treats faults as advisory, and preserves cancellation. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Shares one scoped EF mutation store across existing/new ports and registers the focused use case. |
| `src/Pegasus.Infrastructure/Persistence/CurrentIntakeAssociations.cs` | Provides one current-association precedence query for active manual/automatic rows, inactive reversal suppression, and accepted-link fallback. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | Gathers current Case/retained-thread evidence and revalidates its fingerprint inside the existing serializable, idempotent, append-only association transaction. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Uses the shared current-association precedence for retained list/detail Case projection. |
| `tests/Pegasus.Core.Tests/Intake/CaseMatching/AutomaticMailCaseAssociationTests.cs` | Proves the unique/agreement decision matrix and no-store-call abstention cases with fakes. |
| `tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs` | Proves system-wide current VRM, exact mailbox scope, stale refusal, stable replay/history, and retained-detail projection in disposable LocalDB. |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Proves the real queued processor's live and completed-replay calls, provider→MAIL-09→allocation order, refreshed Case visibility, and subsequent associated-replay skip. |

## Acceptance traceability

- **Unique normalized VRM:** Core matrix and SQL evidence associate the one current non-archived Case before thread evidence exists.
- **Exact thread:** SQL evidence sees one current Case only for the same mailbox and conversation; the same conversation in another mailbox yields zero.
- **Zero/multiple/contradiction:** Core matrix covers zero/multiple VRM, multiple thread candidates, and disagreeing unique candidates; every case abstains without calling the store.
- **Stale evidence:** SQL changes receipt VRM after evidence read; the serializable store throws `IntakeAssociationConflictException` and writes no association/history, then fresh evidence succeeds.
- **Replay/history:** SQL repeats the same receipt-scoped operation after success and proves one association row and one immutable history row.
- **Current association/read surface:** retained detail resolves the automatic row through the shared current precedence and returns the Case.
- **Real live/replay caller:** two LocalDB tests enter the real `ProcessQueuedIntake` first-pass and completed-work branches. Each records provider→MAIL-09→allocation, proves allocation reads the associated existing Case, and proves a later replay skips both association attempts.
- **Schedule:** the capability registry preserves QDOS-direct `Now / 0.1.0-alpha.1` under ADR-0020 while the general MAIL-09 row remains `Next / 0.3.0`.
- **No external write:** all evidence uses Core fakes and disposable LocalDB; no Graph, Box, cloud, deployment, mailbox, or production operation ran.

## Verification

- `dotnet restore Pegasus.slnx --locked-mode` — passed at initial implementation head; blocker changes add only tests/docs and no dependency files.
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — passed after blockers, 0 warnings/errors.
- Full `Pegasus.Core.Tests` — 860 passed at initial implementation head; blocker changes do not touch Core or these tests.
- Full `Pegasus.ArchitectureTests` — 98 passed at initial implementation head; blocker changes do not touch production composition.
- Focused new queued caller tests — 2 passed after blockers.
- Full `QdosAllocationRecoveryTests` — 17 passed after blockers.
- Focused `CaseMatchIntegrationTests|RetainedMailPersistenceTests` — 33 passed at initial implementation head; blocker changes do not touch their production paths.
- `scripts/Test-DocumentationLinks.ps1` — passed after blockers, 192 files checked.
- `git diff --check` — passed after blockers.

## Simplification

The initial dated four-lens disposition remains in the plan. The blocker pass reused the existing QDOS recovery fixture and retained-message helper, shared the two scenarios through one test method, and added only test-specific delegates/recorders needed to enter real live/replay paths. No production abstraction or behavior changed. No findings remain unapplied.

## Governing documents and boundaries

The implementation meets linked FRD-08's conservative association and no-mailbox-mutation contract. `docs/capabilities.md` remains the schedule authority and ADR-0020 remains the QDOS-direct decision link. No deployment/live evidence, policy redesign, migration, external permission, or production write is claimed.
