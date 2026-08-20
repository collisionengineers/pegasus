# Independent review — PR #486 at 33aa2dfb — 2026-08-20

## Changes

- `docs/frd/frd-08-email-mailbox-and-background-processing.md` canonically adds the accepted unique system-wide normalized-VRM / exact mailbox-thread policy, agreement rule, stale-evidence abstention, no Case/PO key, and ordinary association/reversal semantics.
- `docs/capabilities.md` and `docs/current-architecture.md` record the local implementation and current caller/read shape.
- `CaseQueries.cs` exposes the existing registration normalizer for its second concrete caller.
- `AutomaticMailCaseAssociation.cs` adds one focused Core evidence contract/query port and selection use case.
- `CaseMatchContracts.cs` adds only an optional expected-evidence fingerprint to the existing automatic-association request.
- `DurableIntake.cs` invokes the focused use case after provider matching in live and completed-replay paths while the receipt is unassociated.
- `DependencyInjection.cs` shares one scoped `EfIntakeMutationStore` instance across its existing and new ports and registers the use case.
- `CurrentIntakeAssociations.cs` centralizes active manual/automatic-over-accepted-link precedence, including inactive-row suppression of accepted fallback.
- `EfIntakeMutationStore.cs` gathers current Case/retained-thread facts and revalidates their fingerprint inside the existing serializable, idempotent, append-only write transaction.
- `EfRetainedMailboxMessageStore.cs` consumes the shared current-association projection.
- The Core and SQL tests cover the decision matrix, exact mailbox scope, stale evidence, replay/history, and resulting retained detail.

## Comments and disposition

- **Pass:** One Core policy owner chooses/abstains. Infrastructure gathers and rechecks facts. The existing `IAutomaticCaseAssociationStore` / `EfIntakeMutationStore`, `IntakeManualAssociations`, and `IntakeMutationHistory` remain the sole write/history owner. No duplicate matcher, table, normalizer grammar, generic framework, provider adapter, or external operation was added.
- **Pass:** A present VRM with zero/multiple candidates abstains; unique thread evidence is mailbox-scoped; both qualifying evidence sources must agree; inbound Case/PO is absent from the contract.
- **Pass:** The expected fingerprint is a narrow freshness precondition excluded from stable operation identity, so transaction replay remains idempotent after the write increments receipt version. Existing provider/QDOS callers retain their prior request hash.
- **Pass:** Current-association projection gives any manual/automatic row precedence over accepted links, and an inactive staff-reversed row suppresses fallback. The existing automatic store guard prevents silent relink.
- **Blocking:** The PIR claims the real queued caller, but changed tests invoke Core/EF directly and provide no executable live/completed-replay caller proof. Filed as [[PR-045]], which blocks [[TICK-051]].
- **Blocking:** The MAIL-09 capabilities edit erases the pre-existing QDOS-direct Now / 0.1.0-alpha.1 allocation and ADR-0020 link while adding the general local implementation. Filed as [[PR-046]], which blocks [[TICK-051]].
- **Blocking:** The PIR does not inventory every final-diff path with a rationale, so report-to-diff reconciliation is incomplete. Filed as [[PR-047]], which blocks [[TICK-051]].
- **Non-blocking:** No live production association was performed. That separately approval-gated acceptance remains for post-merge verification and does not authorize a write in this review.

## Verdict

Needs changes. Reviewed the complete ticket and both epic contexts, resolved gates/open questions, governing FRD and scheduling authority, exact PR head/diff, Core/Infrastructure ownership, transaction/replay/reversal behavior, runtime grants, tests, and the dated four-lens simplification disposition. PR #486 must not merge until [[PR-045]], [[PR-046]], and [[PR-047]] are resolved and a full replacement CI run is green.

## CI state at needs-changes handoff

Initial exact-head run 32408607514 for `33aa2dfb` had unit, changes, documentation, reference-data and local-development-scripts green; SQL shards 1–3 and browser were still in progress when the implementer began the blocker fixes. This run is superseded by any blocker commit and cannot satisfy the required replacement-head gate. Re-review must wait for the full replacement run on the final head.
