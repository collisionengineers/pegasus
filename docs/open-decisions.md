# Open decisions

This is the sole register of material unresolved decisions. Most product decisions reviewed through 2026-07-25 are not reopened here. The [requirements](requirements.md) and [capability inventory](capabilities.md) own scope context; deliberately deferred, conditional, and `Unclear` capabilities are not current-scope questions merely because their activation evidence is recorded here.

Intended, implemented, caller-proved, deployed, and accepted are separate evidence states. A selected design or recommended default is not proof of implementation; implementation without a real caller is not caller proof; caller proof is not deployment; and deployment is not acceptance. No stronger state is inferred below.

Accepted decisions should move to the appropriate [decision](decisions/README.md) or [change](changes/README.md) record. Delivery status does not belong in this register.

## Mailbox route activation and confidence display

The classification architecture is fixed:

- Direct-provider and intermediary routes are separate Core-owned, code-versioned policies.
- The applicable route owns provider, instruction type, case association, and precedence.
- For staff forwards, outer transport provenance is retained while the proved original sender drives route identification.
- Stable source identity must be retained and uncertainty exposed through the established review outcome.
- No generic rule engine or transport-specific second classifier is to be added.
- QDOS direct sender identity is the exact `@qdosassist.co.uk` suffix. That suffix alone does not classify message type, associate a case, or apply to an identified intermediary.
- The Mapped Principals spreadsheet at the opaque source citation `../reference/imp-docs/requirementsdocs/provider-extra-info/Mapped%20Principals.xlsx` identifies additional principals and route candidates beyond QDOS. Every listed candidate remains evidence, not an activated route.

The available evidence establishes review-visible uncertainty, but not an accepted numeric confidence score, threshold, or alternative confidence display. None should be inferred.

The first additional-provider route cohort is allocated to `0.2.0`; the broader
classified-email workspace and email MCP cohort is allocated to `0.3.0`.
Neither target closes this evidence gate.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| For each proposed route: genuine examples; exact sender or intermediary identity; exact predicates; provider and instruction taxonomy; case-association and precedence rules; ambiguity handling; correction and reversal behaviour; and accepted holdout evidence. | Premature activation could misidentify the provider, instruction type, case, or controlling route and could conceal uncertainty from review. | Keep any route without accepted evidence inactive. Preserve source identity and route uncertain outcomes to review without introducing another classifier. | Has the proposed direct-provider or intermediary route supplied enough accepted policy and holdout evidence to be activated? |

## EVA manual handoff activation

Two observed examples establish this key order:

1. `Work Provider`
2. `VRM`
3. `Vehicle Model`
4. `Claimant Name`
5. `Reference`
6. `Incident Date`
7. `Instruction Date`
8. `Inspection Date`
9. `Inspection Address`
10. `Accident Circumstances`
11. `VAT Status`
12. `Mileage`
13. `Mileage Unit`

The examples establish the presence and order of `VRM`, but do not by themselves prove its source-field mapping, a VRM-specific confidence rule, or permission to create or alter EVA work.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Operator acceptance of every source-field mapping, especially whether `Reference` maps to EVA Claim No rather than Case/PO; null and empty handling; date and mileage normalization; image selection, naming, and order; treatment of uncertain VRM values; and a real drag-and-drop run. | An incorrect or guessed mapping could create or alter EVA work with the wrong claim, vehicle, dates, mileage, or images. | Keep generation review-gated. Do not allow a guessed mapping, including a guessed VRM mapping, to create or alter EVA work. | Has an operator accepted every mapping and normalization rule through a real drag-and-drop run? |

## EVA API activation (`0.7.0` / `EXT-04`)

Direct EVA API use is allocated only as an optional, non-blocking `0.7.0`
branch and remains blocked because no usable EVA operation has been supplied by
the EVA development team.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| A separate accepted change defining the exact operation, contract, real caller, coexistence or migration approach, idempotency, recovery, and live evidence. | An assumed API could duplicate, lose, or corrupt EVA work and could prematurely remove a working manual path. | Continue supporting the manual handoff if no usable API appears. Replace each EVA function independently only after its API path is accepted. | Has a usable EVA operation been caller-proved and accepted with coexistence, idempotency, recovery, and live evidence? |

## External data, submission, and report contracts

These are independent blockers, not one integration decision. `VEHICLE DATA` observed in EVA, Parkers, and AutoTrader remain evidence rather than selected adapters.

| Decision | Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|---|
| Glass’s direct repair-estimate access | Accepted licensing, API or embedded-access terms, technical access, and cost. | Repair-estimate integration and its commercial viability cannot be established. | Do not select or represent Glass’s as an available direct estimate adapter. | Are Glass’s licensing, access mode, technical contract, and cost accepted for direct repair estimates? |
| Direct valuation access | Accepted direct-access contracts and terms for CAP, Glass’s, and Cazana, including the basis for selecting any adapter. | Valuation sourcing, permissions, and cost remain uncertain. | Treat all three as candidates only; do not imply that any valuation adapter is selected. | Is there an accepted direct-access and commercial contract for a selected valuation source? |
| Provider submission and delivery | Exact provider API formats, delivery contracts, and provider identities. | Reports or work could be sent in an unsupported format or to an unproved identity. | Keep provider delivery behind review or existing supported procedures until each provider contract is accepted. | Has the exact format and identity contract been accepted for the provider being activated? |
| Audatex PDF ingestion | Representative PDF variants and accepted field-mapping evidence. | Variant layouts could produce incomplete or incorrect extraction. | Do not activate generic Audatex PDF mapping from unrepresentative examples. | Have the supported Audatex PDF variants and their mappings been accepted from representative evidence? |
| Mandatory provider and vehicle-history checks | An exact contract defining which checks are mandatory, for which provider or route, when they run, and how failures or unavailable results are handled. | Cases could proceed without required checks or be blocked by checks that were never mandated. | Do not infer a universal mandatory-check policy. Keep activation gated on an exact contract. | Has the provider-specific mandatory-check contract, including vehicle-history handling, been accepted? |
| Report wording | Accepted wording for salvage Categories N, A, B, and N/A; recovery and storage; the final statement of truth; and named qualifications. | Reports could contain incomplete, unauthorized, or inconsistent statements. | Keep the affected wording review-gated and do not invent missing text or qualifications. | Has the complete wording and qualification set been accepted for report generation? |

## Send-to-AI transport experiment (`1.3.0` / `AI-09`)

`AI-09` is allocated to `1.3.0` and preserves one Core-owned work-request,
proposal, and review contract. The target does not activate a transport; any
transport must conform to that contract rather than weakening the queue.

A later experiment may compare:

1. attended Claude Code, Cowork, or Desktop chat consuming scoped MCP work;
2. supported scheduled Claude Desktop automation polling the MCP queue; and
3. a future Collision AI Centre harness polling the queue.

Direct Anthropic or other model API integration is neither an assumed candidate nor a fallback.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Actual client and tool support; OAuth and actor identity; attended versus unattended behaviour; leasing, cancellation, and recovery; proposal return; and cost. | An unsupported client could weaken actor accountability, queue recovery, or proposal review and could create an unintended direct-model dependency. | Run the experiment without changing the Core contract. Discard any Claude surface that cannot satisfy it. | Which candidate, if any, proves the complete Core queue contract with acceptable identity, recovery, proposal return, and cost? |

## Later operator UI capabilities

Operations-first is selected for the QDOS-alpha shell. Worklist-first and Case-first directions are retained only as comparison evidence and do not override the complete design requirements.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Completion of the full design route for each later UI capability, using the canonical [design process](../design/README.md) rather than inheriting raster details. | Treating comparison material or raster details as requirements could constrain later capabilities to an unaccepted interaction model. | Keep the operations-first alpha shell. Require later UI capabilities to re-enter complete design before activation. | Has the later UI capability completed the full design route without treating comparison evidence or raster details as accepted requirements? |

## Azure ownership and retirement targets

Azure ownership changes and retirement are separate exact-target decisions. The available evidence does not provide accepted target names. Each requires fresh inventory and explicit approval before any cloud mutation; see the canonical [Azure guidance](azure/README.md) and [operations guidance](operations.md).

| Decision | Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|---|
| Azure ownership change | Fresh inventory establishing the exact current target identities and names, current ownership, proposed ownership, and explicit approval for those targets. | An ownership mutation against an assumed or stale target could affect the wrong Azure resource. | Make no ownership mutation until the exact freshly inventoried targets are named and approved. | Which freshly inventoried and exactly named Azure targets have explicit approval for an ownership change? |
| Azure retirement | Fresh inventory establishing the exact target identities and names, dependencies, retirement scope, and explicit approval for those targets. | Retiring an assumed or stale target could remove a required service or leave dependent resources unmanaged. | Retire nothing until the exact freshly inventoried targets are named and approved. | Which freshly inventoried and exactly named Azure targets have explicit approval for retirement? |