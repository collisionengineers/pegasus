# Files — TICK-045: MAIL-03 shared mailbox classification policy

*The files document. Not the research — this is the **surface area** of the change, not the findings behind it.*

Surveyed BEFORE planning. Two tables, and the second is the one that earns its keep.

## Where the change lands

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` and/or the existing MAIL-04 Core classification command | Reuse the single taxonomy/result owner and express only the cross-mailbox invariant or minimal external-boundary contract actually required by the real exact-message caller. High risk: duplicating taxonomy or adding a callerless wrapper violates repository simplicity rules. |
| Focused Core/Web/integration tests beside the MAIL-04 exact-message classification tests | Prove two distinct approved mailbox identities use the same Core validation and decision owner, and unsupported/stale/ambiguous inputs fail closed. Prefer extending the existing test fixture rather than a second test fake. |
| `docs/capabilities.md` | After implementation evidence exists, update MAIL-03's activation note without claiming mailbox deployment or live Outlook verification. |

The implementation plan must re-check [[TICK-046]] after its branch lands. If its Core command and tests already prove this capability completely, MAIL-03 should reuse/extend those files and keep its own diff to the missing cross-mailbox evidence rather than creating parallel production code.

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Binding shared taxonomy, approved-mailbox scope, evidence/history requirements, and separation of classification from queue/folder actions. |
| `docs/adr/0008-separate-direct-provider-and-intermediary-email-policies.md` | Automated predicates remain route-owned and versioned; no universal rules engine or unrelated-policy coupling. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Existing automatic caller selects `IMailClassificationPolicy` by accepted `WorkProviderCode`, not mailbox identity. |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` | Delivered QDOS automatic policy and evidence/version convention; do not generalize its predicates to all mailboxes. |
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | Approved mailbox identity and transport scopes; there is intentionally no per-mailbox classification list. |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Exact retained-message read contract and the current classification projection used by the workspace. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Existing cross-mailbox read projection and mailbox-scoped exact-message lookup. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | The real exact-message workspace caller that MAIL-04 extends; avoid a second action path. |
| `tests/Pegasus.Core.Tests/Intake/Classification/MailTaxonomyTests.cs` | Canonical taxonomy tests and category/destination separation; do not copy their category list elsewhere. |
| [[TICK-009]] and [[TICK-010]] research/proof | MAIL-21/22 already delivered QDOS automation and full taxonomy persistence; MAIL-03 must build on, not repeat, them. |
| EPIC-006 `context.md` | Every UI/Core/infrastructure/MCP caller must share one business implementation; mailbox writes remain separately approval-gated. |

## Ripple effects

- MAIL-04 consumes the same Core owner for exact-message correction/history; implement these tickets serially in one conflict lane.
- MAIL-02/23 and MAIL-05–07 consume the current classification but own queue/folder mapping and Outlook movement.
- MCP-05 later calls the same Core action; it must not own category validation.
- No migration is necessarily owned by MAIL-03. Any append-only classification-history schema belongs with MAIL-04 unless the final joint plan proves otherwise.
- Local tests may use multiple mailbox identities. No Graph, Outlook, Azure, or corpus mutation is needed.

## Out of scope

- New automatic predicates, confidence thresholds, precedence, generic rule engine, or per-mailbox policy configuration.
- Duplicating or renaming the settled taxonomy.
- Correction/audit-history mechanics beyond the shared contract owned by [[TICK-046]].
- Queue/Triage mapping, Outlook folder recommendation or move, case association, bulk classification, and Automation Actor tools.
- Real mailbox/cloud writes, deployment, live verification, or operator holdout acceptance.
