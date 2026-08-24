---
id: ADR-0030
status: accepted
date: 2026-08-24
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: []
tags: [deployment, schema, migrations, cutover]
---
# ADR-0030: Non-additive schema changes before cutover

- Status: Accepted
- Date: 2026-08-24
- Supersedes: ADR-0002 expand-and-contract schema clause, before cutover only

## Status

Accepted. This ADR supersedes the expand-and-contract requirement in ADR-0002
for releases made **before** the QDOS cutover, and only for that window.
ADR-0002 remains accepted and its body is not edited; from cutover its clause
governs again unchanged.

## Context

ADR-0002 requires that schema changes use expand-and-contract deployment and
that a release "must not require the old and new code to disagree about a
destructive schema change during deployment or rollback". That rule exists to
protect live case work across a rollback.

Pegasus has not cut over. Step 7 of the ordered critical path in
[open decisions](../open-decisions.md) — all new QDOS instructions entering
Pegasus — has not happened. Production has carried only alpha cases, and the
operator has twice approved a selective wipe of them, which is the recorded
position that pre-cutover case data is disposable while identity, principal,
mailbox cursor and the sequence tables are preserved so no reference is reused.

Staging an expand/contract pair for a column that never carried live data
costs two releases and a migration that exists only to satisfy a rule whose
purpose is not yet engaged. The requirement is also stated in `runbook.md`,
which is downstream of this ADR and cannot relax it on its own authority — the
reason this decision is recorded here rather than only there.

## Decision

Before cutover, a migration may drop a dead column, table, or constraint
outright rather than staging an expand/contract pair.

Three obligations attach, and they are the whole of the exemption:

1. **Name the affected capability** in that release's record in
   `operations.md`. A non-additive migration makes the retained previous
   artifact fail wherever it writes the removed shape, and — because migrations
   are applied before the new packages are activated — makes the *currently
   running* revision fail for the length of that window too. That consequence
   is accepted, not absent, so it is written down rather than discovered.
2. **Roll forward, never back**, past such a migration. Recovering case data is
   the operator-approved selective wipe already recorded in `operations.md`,
   never an unqualified rebuild.
3. **Re-establish compatibility before cutover.** Ending the exemption at the
   cutover date does not repair a migration deployed before it. Schema
   compatibility between the live schema and the retained rollback artifact is
   a prerequisite of step 7, checked at that point, not a rule that merely
   starts applying to later releases.

From cutover, ADR-0002's clause binds again with no exemption.

## Consequences

- ENG-014 may drop the three dead `EvaHandoffRevisions` columns in one
  migration. EVA hand-off generation and download are the named affected
  capability; the table is empty and the hand-off is switched off in
  production, so nothing can reach the broken paths.
- Every pre-cutover release using this exemption leaves a rollback gap that is
  recorded rather than mitigated. The gap is bounded by obligation 3.
- Cutover gains a prerequisite check it did not have: the retained artifact
  must run against the live schema before live work is accepted.
- After cutover this ADR has no further effect and needs no supersession.

## Options considered

- **Keep expand-and-contract universally.** Correct after cutover, but before
  it, it spends two releases per dead column to protect data the operator has
  twice approved discarding.
- **Relax the rule in `runbook.md` alone.** What PLAT-042 first attempted. The
  runbook is downstream of ADRs in the authority chain, so this left release
  engineers with a lower-authority document contradicting an accepted ADR.
- **Exempt by verified-empty data rather than by the cutover milestone.**
  Sound, but it makes every release re-argue whether a table is live. The
  cutover milestone is a single, observable, already-tracked event, and
  obligation 1 still forces the per-release naming that the data check was
  meant to produce.

## Links

- [ADR-0002](0002-dotnet-modular-monolith-on-azure.md) — the superseded clause
- [`runbook.md` rollback step 3](../runbook.md#previous-artifact-rollback-web-and-worker)
- [`open-decisions.md`](../open-decisions.md) — the ordered critical path
