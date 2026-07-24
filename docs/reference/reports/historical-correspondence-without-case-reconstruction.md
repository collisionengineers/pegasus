# Historical correspondence without case reconstruction

**Operator decision:** ADR-0022 was rejected as a v2 case-creation and migration workflow, and dealt with on 2026-07-24.

**Legacy source dealt with:** [ADR-0022](../dealt-with/rejected/0022-retroactive-case-reconstruction.md).

No legacy finding from ADR-0022 was accepted. This report records the current-v2 boundary exposed by the comparison; it does not approve the predecessor reconstruction ladder or create a new implementation requirement.

## Current v2 position

### Current authority

- The [discovery questionnaire](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md) says v2 starts fresh: previous application cases, users, audit records and application state are not migrated. Historical documents and operational records stay in Box, EVA, Outlook, spreadsheets or the network drive, and the previous application is shut down after cutover.
- A case and principal reference arise only from accepted definitive instructions or usable image-led intake. A missing or ambiguous input remains pre-case; it is not converted into a case merely because historical material exists.
- Related email may be associated automatically only when it definitively matches an existing v2 case. An uncertain match is operator-visible in `Needs sorting`.
- [`Held`](../../../../AGENTS.md#product-language-and-invariants) is a reasoned pause on an existing case. It is not a pre-case state or a fallback case constructor.
- Post-report queries and disputes are required operational work, but the exact predicates for `Queries`, `Other`, and `Needs sorting` remain unresolved in the [mailbox categorisation decision](../../../plans/open-decisions.md#mailbox-categorisation-and-correction).

### Decision boundary for new correspondence about historical work

1. Receive and retain the inbound item through the approved current mailbox scope.
2. If it definitively identifies an existing v2 case, use the normal related-correspondence association path.
3. If there is no v2 case, or the association is uncertain, keep the item visible for staff resolution under the eventual mailbox categorisation policy.
4. Staff may consult the historical records that remain in Box, EVA or Outlook to answer the correspondence.
5. Do not import a predecessor case, adopt its Case/PO as a v2 reference, or create a `Held` anchor solely to attach that correspondence.

The eventual mailbox caller must audit any staff association or resolution it performs. Until that caller exists, the exact persisted action and category are not claimed as implemented.

## Differences from ADR-0022

| Legacy decision | Current v2 treatment |
| --- | --- |
| Link to exactly one existing case before reconstruction | Keep only the ordinary definitive-association principle for an existing v2 case. It does not activate reconstruction. |
| Search Archive roots and approved mailboxes for an original instruction | Rejected as an automatic reconstruction ladder. Historical systems remain available for staff consultation, not as migration inputs that create v2 cases. |
| Create a minimal `Held` anchor from an Archive folder | Rejected. `Held` can pause an existing case; it cannot supply missing case-creation authority. |
| Discover or adopt the predecessor Case/PO | Rejected. v2 allocates references under its current principal/year rules and explicitly imports no predecessor cases or application state. |
| Use provider reference, VRM, or claimant name to reconstruct | These may help staff identify correspondence, but they do not independently authorise a new case or reference. Ambiguity remains visible rather than guessed. |
| Search all approved mailboxes, including Deleted Items | Rejected for the current scope. First-MVP automatic ingestion is limited to `instructions@collisionengineers.co.uk`; other mailboxes are deferred. |
| Backfill every related email and fill empty case fields from weaker correspondence | Rejected. Definitive related-mail association is planned, but weaker correspondence cannot silently construct or mutate a case. |
| Ship reconstruction and related-ingest behavior behind `RETRO_*` flags | Rejected. v2 does not add dormant predecessor services, tables, flags, or orchestration without a current caller and approved requirement. |
| Record failure and leave the item for staff | The operator-visible outcome is aligned in principle, but the exact `Queries` / `Other` / `Needs sorting` rule is still an open current-v2 decision. |

## Current implementation and real caller

The only current intake caller is the Development-only path:

`POST /Intake/Qdos` -> [`QdosModel.OnPostAsync`](../../../../src/CollisionSpike.Web/Pages/Intake/Qdos.cshtml.cs) -> [`ProcessQdosIntake`](../../../../src/CollisionSpike.Core/Intake/Qdos/ProcessQdosIntake.cs).

It processes new QDOS input; it does not search historical Box/Outlook records or reconstruct predecessor cases. The planned mailbox Worker, broader email management and related-correspondence caller are not implemented. The current dashboard's [`Queries` tile](../../../../src/CollisionSpike.Web/Pages/Index.cshtml) is static and is not evidence of a query workflow.

The [Outlook delivery plan](../../../plans/remainder-delivery/integrations/outlook-and-background-processing.md) likewise routes uncertain associations to `Needs sorting` and excludes broader mailbox coverage from its current slice. A targeted repository search on 2026-07-24 found no production caller for retroactive case reconstruction.

## Evidence still required

A future authorised mailbox caller must prove, through the real entry point, that it retains the inbound source, associates only a definitive existing-v2 match, leaves uncertain or historical-only correspondence visible to staff, and never allocates or adopts a case reference from historical evidence alone. It must also record the staff action, actor, time and outcome without treating mailbox receipt as proof that an external response was sent.

That evidence would prove current correspondence handling. It would not prove migration, Archive reconstruction, full-mailbox search, predecessor Case/PO adoption, related-email backfill, or operator acceptance of any such capability.

## Deferred-capability impact

Broader mailbox coverage and a future read-only historical-search aid remain possible as separately approved capabilities. Preserved source identity, explicit association outcomes and narrow Outlook/Box adapters keep those options open without importing old case state.

No predecessor case migration, reference adoption, reconstruction orchestrator, broad Deleted Items search, retroactive field fill, dormant `RETRO_*` feature flag, queue, service, table or endpoint is introduced by this decision. Any later assisted historical lookup must define its operator workflow, licence and privacy boundaries, real caller, audit contract and proof without turning historical evidence into a v2 case automatically.
