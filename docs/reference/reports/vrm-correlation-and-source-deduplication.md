# VRM correlation and source deduplication

**Operator decision:** Dealt with on 2026-07-24. Current v2 already covers the required principle; this report records the differences rather than adding another requirement or plan.

**Legacy sources dealt with:** ADR-0002 (`../dealt-with/accepted/0002-vrm-open-case-correlation.md`) and its directly related ADR-0010 (`../dealt-with/accepted/0010-dedup-reference-disambiguated-no-time-window.md`).

## Current v2 position

### Requirements

- A readable vehicle registration is the provisional identifier for image-led work until the principal is known and a formal Case/PO can be allocated.
- Instruction-led and image-led records may be linked automatically only for a definitive match. Uncertain associations remain in `Needs sorting`; staff may link them manually and reverse a mistaken merge with permanent action history.
- The original intake source and provenance remain available after matching or merging.
- Replaying the same source occurrence is idempotent. Equal bytes received as distinct permitted occurrences remain distinct evidence.

These rules are already recorded in the [questionnaire](../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#5-case-information), [remaining requirements](../../product/v1-gap.md#3-complete-intake-formats-and-paths), and accepted [multi-format intake ADR](../../architecture/decisions/ADR-0005-multiformat-intake-assets.md).

### Plan

The [lifecycle and work-management plan](../../history/plans/remainder-delivery/casework/lifecycle-and-work-management.md) already assigns definitive matching, `Needs sorting`, association history, manual linking, and merge reversal to one planned Core policy. It does not need a duplicate task derived from these legacy ADRs.

### Implementation and verified evidence

The current `/Intake/Upload` caller only extracts, normalises, and persists a registration in a pre-case draft through [`ProcessIntake`](../../../src/CollisionSpike.Core/Intake/ProcessIntake.cs). Source-occurrence replay and equal-content occurrence retention are covered by the current intake persistence path.

There is no implemented case-matching, merge, reversal, candidate search, or mutable staff caller yet. The requirement and plan exist; end-to-end matching behaviour is not implemented or verified.

## Differences from the legacy ADRs

| Legacy decision | Current v2 treatment |
| --- | --- |
| Search only compatible open cases | Not adopted as an exact candidate boundary. Closed/reopened-case treatment and the definitive compatibility predicate are not settled by current authority. |
| Use `-002`/`-003` suffixes for concurrent image-first work | Not planned. Stable source occurrence identity prevents storage collision without making a suffixed VRM a business identity. |
| Adopt images through the predecessor archive-holding path | Not adopted. v2 preserves source identity/provenance and uses the planned case association and custody boundaries. |
| Provider reference, principal, and incident-date rules are fixed eliminators | Not yet adopted as an exact matching contract. They may be useful evidence, but the approved definitive predicate must be settled and tested through the current Core owner. |
| Same incident date plus corroboration may attach | Not an implemented or accepted automatic rule. A date match alone is correctly insufficient. |
| Arrival-time proximity may never merge cases | Aligned with v2's requirement for a definitive match; time proximity alone is not definitive evidence. |
| Exact payload-hash repeat is dropped | Rejected for distinct occurrences. Current v2 groups equal hashes for review but retains every occurrence and its provenance. Only replay of the same channel occurrence is idempotent. |
| Matching Case/PO or provider reference forms fixed deduplication rungs | Not adopted as a generic ladder. Provider-scoped identities, application Case/PO, and source occurrence identity remain distinct concepts. |
| Merges are logged and reversible | Already required and planned, but not implemented. |

## Real caller and evidence still required

The planned authorised staff case-detail/review action must call the single Core matching policy. Worker, provider API, MCP, mailbox, and manual-upload routes must supply evidence to that owner rather than deciding matches themselves.

Implementation evidence must eventually include:

- one registration with multiple legitimate cases and no automatic merge;
- one and multiple candidate outcomes;
- uncertain association retained in `Needs sorting`;
- same source occurrence replayed without a second effect;
- identical bytes under different source identities retained separately;
- manual link and mistaken-merge reversal preserving both origins and permanent history; and
- the real staff caller, persistence transaction, concurrency behaviour, and operator-visible failure outcome.

## Deferred-capability impact

Future VRM OCR/VLM, AI/vision, guided capture, WhatsApp ingestion, and additional mailboxes may provide candidate evidence only; none may own or bypass matching policy. Provider API and staff MCP callers must use the same Core action. Later Diminution and Commercial cases may share registrations and therefore must retain stable case and source identities. No external accounts, malware scanning, EVA replacement, estimating/valuation, or infrastructure capability is introduced or constrained by this comparison.
