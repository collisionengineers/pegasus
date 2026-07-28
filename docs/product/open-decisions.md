# Open decisions

Status: **Active material-ambiguity and dependency register**

Most product decisions reviewed through 2026-07-25 are preserved in the
historical [questionnaire](../history/product/project-discovery-questionnaire.md)
and reconciled into the canonical product areas. Allocation is owned by the
[capability inventory](capabilities.md), current release gaps by the
[QDOS-alpha gap](qdos-alpha-gap.md), and dependency intent by the
[roadmap](../roadmap.md). Deliberately deferred capabilities, including
conditional and `Unclear` source rows, are not current-scope questions; this
register records the exact evidence that blocks their activation.

## Mailbox categorisation and matching evidence

Instruction interpretation architecture is settled: direct-provider and
intermediary routes are separate Core-owned, code-versioned policies. The
applicable route owns provider, instruction type, case association and
precedence. Staff forwards retain outer transport provenance while the proved
original sender drives route identification.

Each activated route still needs genuine examples, exact predicates, ambiguity,
correction/reversal and holdout evidence. Until accepted, retain stable source
identity, expose uncertainty through the settled review outcome, and add no
generic rule engine or transport-specific second classifier.

QDOS direct sender identity is the exact `@qdosassist.co.uk` suffix. It does not
classify message type, associate a case or apply to an identified intermediary
without the remaining route policy.

The supplied [Mapped Principals spreadsheet](../reference/imp-docs/requirementsdocs/provider-extra-info/Mapped%20Principals.xlsx)
identifies additional principals and route candidates beyond QDOS. Preserve
every listed candidate as evidence; each still needs its exact sender/intermediary
identity, predicates, precedence, genuine examples and holdout accepted before
activation.

## EVA manual handoff mapping

The two observed examples fix the key order to `Work Provider`, `VRM`, `Vehicle
Model`, `Claimant Name`, `Reference`, `Incident Date`, `Instruction Date`,
`Inspection Date`, `Inspection Address`, `Accident Circumstances`, `VAT Status`,
`Mileage`, `Mileage Unit`.

Operator acceptance must still prove every source-field mapping—especially
whether `Reference` maps to EVA Claim No rather than Case/PO—plus null/empty
rules, date and mileage normalization, image selection/naming/order and a real
drag-and-drop run. Until then, generation remains review-gated and no guessed
mapping may create or alter EVA work.

## EVA API availability

Direct EVA API use remains blocked until the EVA development team supplies a
usable operation. A separate change must accept the exact operation, contract,
caller, coexistence/migration, idempotency, recovery and live evidence. The
manual handoff remains supported if no usable API appears, until each EVA
function is replaced independently.

## Engineering dependency contracts

The following are independent blockers, not one integration decision:

- Glass's direct repair-estimate licensing, API/embedded access and cost;
- CAP, Glass's and Cazana direct valuation access and terms;
- provider submission/delivery API formats and identities;
- Audatex PDF variants and accepted mapping evidence;
- the provider/vehicle-history mandatory-check contract; and
- report wording still owed for salvage Categories N, A, B and N/A, recovery
  and storage, final statement of truth, and named qualifications.

EVA-observed `VEHICLE DATA`, Parkers and AutoTrader remain evidence rather than
selected adapters.

## Send-to-AI transport experiment

`AI-09` preserves one Core-owned work-request/proposal/review contract. A later
activation compares:

1. attended Claude Code, Cowork or Desktop chat consuming scoped MCP work;
2. supported scheduled Claude Desktop automation polling the MCP queue; and
3. a future Collision AI Centre harness polling it.

The experiment must prove actual client/tool support, OAuth/actor identity,
attended versus unattended operation, lease/cancel/recovery, proposal return
and cost. If a Claude surface cannot satisfy the contract, discard it rather
than weakening the queue. Direct Anthropic or other model API integration is
not an assumed candidate or fallback.

## Operator shell

Operations-first is selected for the QDOS-alpha shell. The retained
Worklist-first and Case-first directions remain comparison evidence only and do
not override the complete design requirements. Later UI capabilities re-enter
the complete design route rather than inheriting raster details.

Azure ownership and retirement remain separate exact-target decisions under
the Azure retirement plan. They require fresh inventory and explicit approval
before cloud mutation.
