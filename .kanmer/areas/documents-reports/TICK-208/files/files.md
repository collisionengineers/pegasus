# Files — TICK-208

## Where the change lands

| Path | Why |
|---|---|
| docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Clarify per-issued-version binding between immutable artifact/approval and exact final Sent evidence, and distinguish correction from reassociation. |
| src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs | Extend the single current approval/evidence projection with a Core-owned issued-report version and version-specific evidence contract. |
| src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs | Add the append-only relationship/history joining report versions, approvals, and Sent evidence without clearing prior links. |
| src/Pegasus.Infrastructure/Persistence/CaseWorkflowModelConfiguration.cs | Configure keys, uniqueness, chronology/association constraints, and non-destructive relationships. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs | Record versions and approvals, link/reassociate exact evidence to a named version, preserve old associations, project history, and retain idempotency/concurrency behavior. |
| src/Pegasus.Infrastructure/Persistence/Migrations/(new migration) | Evolve the one-slot schema and preserve/backfill current approval/evidence safely. |
| src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs | Reflect the durable model. |
| tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs | Prove original version and Sent evidence survive correction/addendum, a new version starts unsent, second evidence binds only to it, and prior rows remain queryable. |
| tests/Pegasus.Core.Tests/Workflow/ApprovedMailboxReportSentEvidenceTests.cs | Extend policy coverage for version-specific binding while retaining exact-source validation. |
| tests/Pegasus.Core.Tests/Workflow/PollSentEvidenceTests.cs | Prove auto-link cannot overwrite prior final evidence or assign evidence ambiguously. |

## Context files

| Path | What it tells the implementer |
|---|---|
| docs/frd/frd-08-email-mailbox-and-background-processing.md | Exact Sent evidence fields, finality, proof limits, and permanent reasoned association history. |
| docs/frd/frd-01-case-identity-and-lifecycle.md | Report sent enters PostReport; reasoned reopen rules and boundary against inventing closure behavior. |
| docs/open-decisions.md | CASE-23 transitions/correction interaction remain unresolved. |
| docs/capabilities.md | MAIL-14/MAIL-15 evidence capabilities and CASE-23/RPT-05 allocation boundaries. |
| src/Pegasus.Core/Workflow/ApprovedMailboxReportSentEvidence.cs | Reusable exact retained-source validation and retention operation. |
| src/Pegasus.Core/Workflow/PollSentEvidence.cs | Reusable Worker discovery, retention, and guarded auto-link flow. |
| src/Pegasus.Infrastructure/Persistence/EfCaseReportSentEvidenceStore.cs | Existing immutable source-item retention and unlinked-candidate queries. |
| src/Pegasus.Infrastructure/Persistence/CaseWorkflowModelConfiguration.cs | Current unique one-slot FKs and evidence indexes constraining migration. |
| tests/Pegasus.IntegrationTests/LocalDurableApprovedSentSourceTests.cs | Durable exact-source identity and replay precedent. |
| tests/Pegasus.IntegrationTests/SentEvidencePollPersistenceTests.cs | Poll outcome and auto-link persistence precedent. |
| tests/Pegasus.IntegrationTests/CaseTasksWebTests.cs | Existing staff UI mutation path displaying and linking report-Sent evidence. |
| EPIC-004/context.md | Renderer integration supplies immutable version/reference/hash; Infrastructure must not own policy. |

## Ripple effects

- Case/detail/dashboard projections must show the current version while retaining navigable issued-version history.
- SIMPLI-014 rendering/approval must create or reference immutable report version identity before Sent evidence binds.
- Worker auto-linking may require a stronger artifact/version match; without authoritative version identity the item remains unlinked.
- Migration must preserve current approvals/evidence without fabricating an artifact-version match; uncertain legacy associations need explicit legacy provenance.
- API/Web mutations and authorization tests need version identity and stale-version conflict coverage.
- Operations/current-architecture docs change only when implementation/deployment lands.

## Out of scope

- CASE-23 query/dispute states, due/chaser rules, response workflow, or completion transitions (TICK-055).
- Sending mail, mutating Outlook, proving delivery/read, or Azure/cloud writes.
- Renderer integration mechanics (SIMPLI-014), except consuming immutable artifact/version identity.
- Fee-note financial correction behavior beyond retaining artifact relationship.
- Treating Box artifacts, uploads, drafts, queue results, or staff assertions as Sent evidence.
