# Files — TICK-044

## Where the change lands

| Path | Why |
|---|---|
| `docs/operator-notes.md` | Record the protected operator confirmation of the exact mapping without altering the already-settled taxonomy; changing meaning requires the user's explicit answer first. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Add the canonical MAIL-02 behavior matrix, fail-closed rules, correction/history behavior, and acceptance evidence after operator confirmation. |
| `docs/capabilities.md` | Refresh MAIL-02 implementation/evidence status when delivered; keep MAIL-23 and UI-14 allocations distinct. |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` or a sibling under `src/Pegasus.Core/Intake/Classification/` | Add the separate Core-owned operational-destination contract/policy without putting queue, Triage, or Outlook facts on `MailCategory`. Exact file choice should follow the one-owner convention during planning. |
| `tests/Pegasus.Core.Tests/Intake/Classification/` | Prove the exhaustive confirmed mapping, reply behavior, Other handling, ambiguity/unclassified fail-closed outcomes, and separation from folder routing. |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Expose the operational destination to the existing retained-mail read contract if the confirmed caller requires it. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Project the Core-owned destination for retained-message callers; avoid a second mapping in SQL/infrastructure. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` and relevant integration tests | Verify the real read-model caller receives the Core result and does not infer destinations independently. |
| `docs/current-architecture.md` / `docs/operations.md` | Refresh as-built and evidence state if implementation or deployment changes reality; do not claim activation from local verification alone. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `AGENTS.md` | Core is the sole business-policy owner; operator notes are protected; local alpha must not mutate Outlook; activation requires real caller evidence. |
| `docs/index.md` | FRD owns behavior, ADR only a durable technical decision, and capabilities owns allocation/status. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Sole taxonomy/correction owner and the explicit classification/queue/Triage/folder separation invariant. |
| `docs/frd/frd-03-triage.md` | Triage is distinct pre-case work; missing VRM and unaccepted routing remain Needs sorting. |
| `docs/open-decisions.md` | Exact route predicates, multi-match precedence, activation roles, cohort/holdout thresholds, and Graph scope remain gated. |
| `docs/design/README.md` | Receiving work, Queries, and Other are Next / 0.3.0 surfaces; operator-facing UI must not invent policy or expose queue mechanics as jargon. |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | Existing taxonomy and the test-backed rule that category carries no queue/Triage/folder destination. |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` | Existing route-owned classification precedent and explicit Ambiguous/Unclassified fail-closed behavior. |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Existing application read boundary currently exposes classification outcome and route disposition only. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Current real workspace detail caller is read-only and explicitly says classification/link/folder mutations are unlanded. |
| `tests/Pegasus.Core.Tests/Intake/Classification/MailTaxonomyTests.cs` | Exact taxonomy contract and architectural guard against mixing classification with routing/destination. |
| EPIC-006 `context.md` | All workspace callers must reuse one Core implementation; Outlook writes require explicit confirmation and live-operation approval. |
| TICK-064 / MAIL-23 | Downstream folder mapping depends on this operational mapping and must not be folded into MAIL-02. |

## Ripple effects

The result feeds retained-mail detail and later UI-14 queue filters, Automation Actor callers, and MAIL-23 folder recommendations. Correction/reclassification must deterministically update dependent projections while preserving original decision history. Tests must cover every confirmed category/subtype and no-match/ambiguity behavior. Any deployment or live caller activation requires the separate evidence and current-state documentation gates.

## Out of scope

Outlook/Graph folder mutation; arbitrary folder selection; UI-14 visual implementation; bulk actions; additional-provider predicate invention; QDOS Triage matcher activation without accepted predicates; confidence scores or precedence; generic rule engines; taxonomy changes; MAIL-23 folder design; deployment or cloud writes.
