# MCP assessment toolset (Automation Actor tranche 2)

Status: planning stage, for operator review. Nothing in this document is
implemented, claimed, or activated by its existence. Implementation starts
only after the decisions in [Open decisions](#open-decisions-for-the-operator)
are made; several slices are additionally gated on capability allocation as
stated in [Sequencing](#sequencing-and-slices).

## Goal and scope boundary

Close out the remaining Automation Actor MCP toolset work left open by the
merged MCP-01–04 ingress (PR #327): the two inventory candidates the shipped
plan deliberately excluded pending exactly this scoping —
`pegasus_case_update_details` and an EVA/report-bundle generation tool — and
the assessment/estimate surface that the design-only Engineers screens
(`src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` and
`Suggestions.cshtml`, PR #326) now define field by field.

The operator requirement this plan implements (restated 2026-08-03): the AI
provider modifies the details of an assessment directly in order to prepare
a report. Human review is the existing case workflow itself — every case is
manually assigned to an engineer, and that assignment is where automated
detail is reviewed. Automations are designed and safeguarded outside Pegasus
(Claude Desktop; skills, prompts, and tasks built in Cowork and run on
Automations). Pegasus's obligations are exactly two: a fully comprehensive
toolset, and clear logging of every automation action with the same rigor as
any human action. Outward sending and the confirmation of professional
findings remain human acts.

In scope:

- the write model for Automation Actor assessment access (decided below);
- the Core assessment/estimate domain model the screens imply, mapped from
  the authoritative field inventory
  (`docs/reference/rendererref1/report_data_schema.json` plus the markup);
- the proposed MCP tool inventory extension, scopes, idempotency, lease
  interaction, and activity attribution;
- store/migration impact, test plan, docs impact, and sequencing with honest
  activation preconditions.

Out of scope (see [Non-goals](#non-goals)): wiring the assessment or
Suggestions screens, any `Send to AI` transport, report rendering, valuation
or estimating adapters, fee/invoicing, production activation, and any tier-5
evidence claim.

## Governing authority already settled

This plan operates inside constraints that are already decided; it reopens
none of them.

- **ADR-0011 / ADR-0013 clause 10.** MCP is a management/development-controlled
  ingress for one named vendor-neutral Automation Actor invoking only its
  approved inventory of ordinary operational Core use cases. Any authority
  expansion needs a new accepted decision.
- **The recovered scope note** (deleted `docs/temp-plans/mcp-automation-actor.md`
  § 1.6, commit `6b538fb`): ordinary case-detail editing and manual EVA bundle
  generation are inventory candidates; "issuing or altering the professional
  findings", report approval/sending, and report generation itself are
  excluded regardless of inventory.
- **Permanent boundary** (`docs/requirements.md`): "no model, skill, prompt,
  or external source issuing an accepted case, engineering, economic, legal,
  or report outcome."
- **AI-proposal shape** (`docs/requirements.md` § Targeted sending and
  reviewed AI proposals) currently reads "a scoped worker may lease it and
  return only a proposal" with named-Engineer accept/amend/reject required.
  The operator decision of 2026-08-03 (direct writes, review at engineer
  assignment) supersedes the proposal-only reading for the Automation Actor;
  the queued Automation Actor ADR must revise the AI-09 contract wording in
  `docs/requirements.md` and `docs/capabilities.md` under product authority
  before this tranche's write tools ship. Until that text lands the conflict
  is recorded here, not silently ignored.
- **Core precedent.** `Pegasus.Core` already carries: the
  Fact/Suggestion/Confirmed value model (`CaseDataValueKind`,
  `CaseField<T>` in `Cases/CaseDataContracts.cs`); a staff
  accept/correct/reject suggestion command under lease
  (`AcceptVehicleSuggestionCommand`, `Vehicle/VehicleWorkflow.cs`); and a
  professional-finding policy that fails closed unless the actor is an
  authenticated staff Engineer (`EngineerFindingPolicy.ValidateRequest`,
  `Cases/CaseContracts.cs`). The Automation Actor structurally cannot record
  a finding today; under this plan it may *record* finding field values as
  unconfirmed working data, while confirmation stays staff-Engineer-only
  (see the write-model section).
- **Actor rights.** `StaffAuthorization` grants `ActorKind.Automation`
  exactly `PerformCasework`; this plan adds no right.
- **Allocations.** UI-15 (Engineer workbench) is `Later / 1.0.0`; AI-09
  (`Send to AI` work transport) is `Later / 1.3.0`; ENG-01/ENG-02, EXT-09,
  EXT-10, EXT-12, EXT-13 (`Later / 1.0.0`), EXT-11 (`1.2.0`), RPT-01/EXT-08
  (`1.1.0`). `design/README.md` § Deferred casework requires every deferred
  UI capability to re-enter specification and review before implementation.
  The assessment screens exist only as unlinked design markup under the
  operator's 2026-08-03 widening; this plan does not treat that as
  implementation authority.

## Coordination with live claims (`NOW.md` on `origin/dev`, 2026-08-03)

- **task/ui-alpha-design-pass** (live, PR #326) owns the UI-15/AI-09 design
  route this plan's field inventory is mapped from — and with it the
  ENG-01/ENG-02 surface (estimate lines, valuation, outcome/salvage): the
  operator treats ENG-01/ENG-02 as in progress through that UI work, so the
  Core assessment model here continues in-progress work rather than opening
  a new front. Slice 4's wiring work does not start until that task merges
  and the re-entry review happens against its output. AI-09 implementation
  is assigned to the channels task (companion plan).
- **task/report-renderer-integration** (live) is planning the renderer seam
  (RPT-01–05, EXT-08), the `docs/reference/rendererref1` blueprint and
  report-template intake, and "promotion of the renderer's pre-existing MCP
  server as the replacement for the current `.mcpb` packaging (MCP-01–04
  follow-ups)". Two touch points: the report-data projection candidate tool
  must land on whatever Core render seam that task locates, and any MCP
  packaging change it proposes must be reconciled with this plan's tool
  inventory before slice 3. Neither plan implements ahead of the other; the
  shared ADR resolves precedence.
- **`NOW.md` Next queue** already holds the two items this plan depends on:
  the Automation Actor ADR promotion (the vehicle for the direct-write
  model and the AI-09 rewording) and the tier-5 MCP evidence run (slice 6).

## Assessment field inventory

Extracted from `Index.cshtml` (962 lines; field `name` attributes follow the
report job definition in `docs/reference/rendererref1/report_data_schema.json`)
and reconciled against `Pegasus.Core` as merged. "Exists" means the field is
already owned by `CaseDataProjection` (`Cases/CaseDataContracts.cs`) with
Fact/Suggestion/Confirmed provenance.

### Case identity (read-only header)

Case/PO, Principal, Registration, Case type, Workflow state, Due by — all
already served by existing Core queries (`ISearchCases`, `IGetCase`). No new
model.

### Vehicle (`vehicle.*`)

| Field | Markup/schema contract | Core today |
| --- | --- | --- |
| `vehicle.registration` | text, required | exists (`CaseVehicleData.Registration`) |
| `vehicle.vehicle_type` | enum `car, van, motorcycle, scooter, bicycle, trailer, caravan, other`, required | new |
| `vehicle.make` | text, required | exists |
| `vehicle.model` | text, required | exists |
| `vehicle.year` | string, required | new |
| `vehicle.vin` | optional, "No format is enforced" | new |
| `vehicle.engine_cc` | integer ≥ 0, optional | new |
| `vehicle.fuel` | text, optional | new |
| `vehicle.odometer_miles` | integer ≥ 0; required unless mileage source is `tbc` | overlaps `CaseVehicleData.Mileage` + `MileageUnit` (D4) |
| `vehicle.mileage_source` | enum `online_data, owner, repairer, principal, average, tbc`, required; drives the composed mileage sentence | new; interacts with ADR-0012 mileage tiers (D4) |
| `vehicle.condition` | enum `poor, below_average, average, good, excellent`, required | new |

### Incident and impact (`incident.*`, `assessment.impact_*`, `narrative.nature_of_incident`)

| Field | Contract | Core today |
| --- | --- | --- |
| `incident.date` | date, optional | exists (`CaseAccidentData.IncidentDate`) |
| `incident.instructions_received` | date, required | overlaps `CaseInstructionData.InstructionDate` (D4) |
| `incident.assessed` | date, required | new |
| `assessment.impact_severity` | enum `light, light_to_moderate, moderate, moderate_to_heavy, heavy`, required | new |
| `assessment.impact_location` | 14-value enum (`front` … `multiple`), required | new |
| `narrative.nature_of_incident` | optional override of the composed severity+location sentence | new (distinct from `CaseAccidentData.Circumstances`) |

### Inspection (`assessment.method`, `assessment.location_address`)

Both exist: `CaseInspectionData.Mode` (`CaseInspectionMode`) with the
principal default from ADR-0018 (`ProviderInspectionModePolicy`), and
`CaseInspectionData.Address` with `Ext18InspectionAddressPolicy` enforcing
the exact `Image Based Assessment` value pairing. The screen's rule
("changing it on this case needs a reason") is already Core policy via
`SaveCaseRequest.Reason`. No new model; the assessment projection reads them.

### Valuation

| Field | Contract | Core today |
| --- | --- | --- |
| Guide evidence: CAP retail/trade, Glass's retail/trade, Cazana retail/trade | read-only display with source caption; "Guide figures stay on this screen" — they never reach the report | no store; EXT-10/EXT-13 territory (D8) |
| `assessment.values.retail` | number > 0, required, chosen from guide evidence | new — professional decision layer |
| `assessment.values.trade` | number > 0, required, chosen from guide evidence | new — professional decision layer |
| `assessment.values.engineer` | number > 0, required, engineer's opinion ("usually the retail value; it may differ") | new — professional decision layer |

### Estimate

Rate card (`rates.*`): `rates.card` (select from published rate cards, each
carrying its own dates and caveat — explicitly "a guide to charges, not a
fixed tariff", and per `AGENTS.md` an external skill package never becomes an
application policy owner), `rates.class` (`standard, prestige, van`),
`rates.manufacturer_approved` (bool), `rates.regional_uplift` (bool); derived
display of labour and paint rate in use. **No rate-card model exists anywhere
in Core** — ownership is open decision D2.

Repair lines (`operations[n].*`) — an ordered collection ("Add a repair
line"; the three markup rows are placeholders):

| Column | Contract |
| --- | --- |
| `type` | enum `rnr, repair, new_part, check_labour, paint_new, paint_repair, paint_blend, paint_prep, specialist_fixed, specialist_wu` |
| `guide` | text "Code" (semantics unstated — D8) |
| `desc` | text |
| `wu` | number, 0.1 step (work units; a line is worth WU/10 × rate) |
| `price` | number, 0.01 step, plus `unpriced` "To be confirmed" checkbox |
| `part_num` | text |
| `bet` | text "Betterment" (type and semantics unstated — D8) |
| `status` | enum `confirmed, estimated, provisional` |
| `evidence_label` | enum `official, reference, case, judgement` ("Where this came from") |
| `justification` | text ("Why this line") |

Charges and VAT (`costs.*`): `recovery_charge` (optional; presence generates
the Recovery & Storage paragraph), `storage_charge` (optional),
`repairer_vat_registered` (required two-way choice, deliberately no default;
`true` = 20% on the whole repair cost, `false` = 20% on parts and paint
materials only).

Derived, never typed in (the markup prints the working beside each row):
body labour (WU on repair/RnR/check ÷ 10 × labour rate), paint labour (WU on
paint lines ÷ 10 × paint rate), parts (new-part prices + the card's sundry
parts percentage), paint materials (the card's material band + sundry and
pre-paint charges), specialist and extras, repair cost before VAT, VAT,
repair cost. The report schema's `costs` inputs (`labour_hours`,
`hourly_rate`, `parts`, `paint_materials`, `specialist_other`) and the three
report worklists (`new_parts`, `repairs`, `operations`; RnR lines are never
named — their labour is carried in the hours) are all projections over the
lines and the card. Computed once, in Core, per the duplicate-truth-owner
prohibition — but the formulas require accepted authority (EXT-09 note), so
derivation is gated by D2.

### Findings (`assessment.*`)

| Field | Contract |
| --- | --- |
| `outcome` | enum `total_loss, repairable, cash_in_lieu, contract_repair`, required |
| `legal_status` | enum `roadworthy, unroadworthy`, required |
| `unroadworthy_reason` | required when unroadworthy; composed into Engineer's comments |
| `category` | `A, B, S, N, N/A` (schema also allows `""` — D8); required when total loss |
| `salvage_value` | number ≥ 0; required when total loss |

Screen rule carried into Core policy: a correction keeps the earlier finding
(versioned, reasoned) and never implies a fee or invoice change (EXT-11
boundary). These are professional findings: staff-Engineer-only, always.

### Report content (`narrative.*`, `engineer.*`, `fee.*`, `statement_of_truth`)

| Field | Contract | Notes |
| --- | --- | --- |
| `narrative.history_check` | required; pass-through of the vehicle-history provider result | provider adapter out of scope |
| `narrative.engineers_comments` | optional, appended to composed comments | |
| `engineer.name` / `engineer.qualifications` | required | |
| `engineer.signature` | select from approved signatories (schema enumerates three) | signatory-list ownership D8; signature images are provenance-sensitive document assets |
| `fee.agreed_fee` | required, > 0 | EXT-11 is `1.2.0` — inclusion now is D8 |
| `fee.description_lines` | optional, one line per row | |
| `statement_of_truth` | optional override; standard CPR wording by default | |

Composed by the generator, never stored as inputs (schema `not` clauses):
introduction, desktop-assessment section, mileage sentence, pre-incident
condition, settlement and salvage paragraphs, and the `refs.matter` line.
`refs.*` comes from the case; `photos`/`impact_diagram` belong to the
excluded report-image selection sub-surface.

### Readiness and Suggestions surfaces

Readiness rail: a derived projection (requirement, source, why outstanding,
how to resolve) — read model only, no stored fields. The Suggestions screen
was designed as a blocking accept/amend/reject gate; the operator's
2026-08-03 direct-write decision supersedes that role — see D10 for its
candidate repurposing as a read-only review of automation changes driven by
action history.

## Write model decision: direct writes with logging parity (operator-decided)

Two candidate models were weighed; the operator decided on 2026-08-03.

**Staged suggestions** (a suggestion store plus a staff apply gate) was
recommended by the first draft of this plan and **rejected by the
operator**: every case is already human-review based, because every case
must be manually assigned to an engineer — that assignment is where
automated detail is reviewed. A second in-app review gate in front of
automation writes duplicates a review the workflow already guarantees.
Automations themselves are designed and safeguarded outside Pegasus (Claude
Desktop; skills, prompts, and tasks built in Cowork and run on Automations);
Pegasus is not where those safeguards live.

**Decision (D1, decided): direct writes.** Automation Actor assessment
writes go through the same Core commands, validation, edit lease, and
version guards as a staff save, attributed to the automation identity. What
Pegasus owes in exchange is logging parity: every automation write lands in
permanent action history with actor identity, operation key, correlation
id, and per-field change evidence, exactly as a human action does, and is
visible in `/Administration/Automation/Activity`. The review point is the
engineer the case is assigned to; the assessment surface shows recorded
values with their provenance, and automation-recorded values are visibly
unconfirmed until staff review.

Boundaries retained inside the direct-write model:

- **Findings are recordable, not confirmable.** The Automation Actor may
  record `assessment.outcome`, `assessment.legal_status`, salvage category
  and value, and `assessment.values.*` as unconfirmed working values.
  Confirmation — the act that makes a finding the accepted professional
  outcome — remains staff-Engineer-only (the `EngineerFindingPolicy`
  precedent), performed at review. This preserves the permanent
  `docs/requirements.md` boundary (no external source *issues an accepted*
  outcome) while giving the automation the full field surface.
- **No outward send, ever.** No tool dispatches anything to a customer,
  principal, or external party; report approval and sending remain human.
  This operator requirement is unchanged.

Consequences for the two previously excluded candidates, both now resolved
in favour of a comprehensive toolset:

- **`pegasus_case_update_details` is reinstated.** With no suggestion gate
  to bypass, ordinary case-detail editing (registration, make, model,
  mileage, incident date, inspection fields) is an approved-inventory
  direct write under the existing edit lease, wrapping the existing
  `ISaveCase` path with full history attribution.
- **`pegasus_eva_bundle_generate` is reinstated as a mutating tool.**
  Generation hands the case to an engineer for review — it pushes work
  *into* the human review point, not around it. Core's `IGenerateEvaHandoff`
  is already lease-guarded and idempotent; the CASE-21 `First sent to
  Engineer` proxy event it may establish is recorded in history exactly as
  a staff-triggered generation records it, and regeneration of an existing
  revision runs under the same guards and logging. The read-only
  `pegasus_eva_handoff_status` companion ships as well.

## Proposed Core model, ports, and commands

New Core area `src/Pegasus.Core/Assessment/` (a directory in the existing
project — no new project, store, or migration stream, so no ADR trigger):

- **`AssessmentContracts.cs`** — the typed sections and enums from the
  inventory above (`VehicleType`, `MileageSource`, `PreIncidentCondition`,
  `ImpactSeverity`, `ImpactLocation`, `EstimateLineType`,
  `EstimateLineStatus`, `EstimateEvidenceLabel`, `AssessmentOutcome`,
  `Roadworthiness`, `SalvageCategory`), `CaseAssessmentProjection`
  (referencing, not copying, the case-data-owned fields per D4), estimate
  lines as an ordered collection, and `IGetCaseAssessment` /
  `ICaseAssessmentStore` ports. Field vocabulary is closed: the exact
  `name` paths from the markup are the wire vocabulary
  (`vehicle.condition`, `operations[3].wu`, …); unknown paths fail closed.
- **`AssessmentCommands.cs`** — `SaveAssessmentRequest : CaseMutationRequest`
  (per-section or whole-surface save: actor, `mcp:` operation key for
  automation callers, edit lease, expected case version, optional
  work-request binding per D3, and field values keyed by path). One
  command, two callers: the staff app (the screen's per-section save
  buttons, wired later by the UI-15 activation task) and the Automation
  Actor over MCP. Values written by an `ActorKind.Automation` caller are
  stored with automation provenance and an unconfirmed mark; a staff save
  or explicit staff confirmation of the same field confirms it.
  Finding-field confirmation validates staff-Engineer identity
  (`EngineerFindingPolicy` precedent); an automation caller writing a
  finding field can only produce an unconfirmed value, never a confirmed
  finding. `StaffAuthorization` is untouched.
- **`AssessmentPolicy.cs`** — validation (required-when rules exactly as the
  hints state: odometer required unless `tbc`; unroadworthy reason required
  when unroadworthy; category and salvage value required when total loss;
  physical inspection requires an address; VAT answer required with no
  default) plus, gated on D2, the single-owner derivation of estimate totals
  and the three report worklists.
- **Staff callers** stay unwired in this tranche — the built-but-unwired
  rule in engineering.md is honoured by keeping this tranche's callers
  MCP + tests only, and by not linking the screens.

Infrastructure (`src/Pegasus.Infrastructure/Persistence/`): a
`CaseAssessment*` entity set following the existing per-field snapshot
pattern (`CaseDataSnapshotEntity`/`CaseDataFieldEntity`), carrying per-field
provenance and the confirmed/unconfirmed mark, and a `CaseEstimateLine`
table (ordered, case-scoped). All in the existing `PegasusDbContext`
migration stream.

## Proposed tool inventory

New per-area scope: `automation.assessment` (added to
`AutomationMcp.Scopes`; the seeded registration in
`AutomationClientRegistry` grants it; the endpoint policy and rate-limit
policy already cover any registered scope). Tool copy stays vendor-neutral —
"automation" — per ADR-0011; `Send to Claude` remains a UI-label sanction
only.

| Tool | Wraps | Kind | Idempotency | Lease | Attribution |
| --- | --- | --- | --- | --- | --- |
| `pegasus_assessment_get` | `IGetCaseAssessment` (+ readiness projection) | read-only | n/a | none | action history, correlation = trace id |
| `pegasus_assessment_update` | `SaveAssessment` | mutating (direct write; automation values stored unconfirmed) | `mcp:` operation key; replay returns the original result with `IsReplay` | required (`pegasus_case_edit_begin`/`end`) plus expected case version | action history per field, correlation = operation key |
| `pegasus_case_update_details` | `ISaveCase` | mutating (direct write to the overlapping case-data fields) | `mcp:` operation key | required | action history per field, correlation = operation key |
| `pegasus_eva_bundle_generate` | `IGenerateEvaHandoff` | mutating (handoff revision; may establish the CASE-21 proxy event) | Core-idempotent per revision plus `mcp:` operation key | required | action history, correlation = operation key |
| `pegasus_eva_handoff_status` | `IEvaHandoffQueries.GetPreparationAsync` | read-only | n/a | none | action history, correlation = trace id |

Explicitly absent, structurally: any finding-confirmation tool; any report
approval tool; any tool that dispatches anything to a customer, principal,
or external party. `pegasus_assessment_update` validates every target path
against the closed vocabulary, bounds batch size, and fails closed on
unknown paths, a stale version, or a missing lease. The existing nine tools
are unchanged.

A report-data projection tool (`pegasus_assessment_report_data_get`,
returning the validated report-job JSON shape as data, no rendering) is
recorded as a *candidate* for the slice after D2/D9 — it is a projection,
not a report, but it is deferred until the assessment record exists and is
listed here only so the renderer boundary question is not smuggled in later.

## Migration and store impact

- One migration in the existing stream (`src/Pegasus.Infrastructure/
  Persistence/Migrations/`), no new store, no new project — no ADR trigger.
- The exact applied-migrations assertion
  `CommittedMigrationCreatesTheSqlServerSchema` in
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
  enumerates every committed migration by name and bit this team last time
  (fixed as follow-up commit `47b4516`): the new migration name must be
  appended to that array in the same change, with table-existence assertions
  for the new tables.
- Principal and reference remain immutable; no assessment table carries a
  mutable reference. Field history is append-only snapshot evidence — an
  automation write never erases the prior value from history. Nothing
  deletes a case.

## Test plan

Unit (`tests/Pegasus.Core.Tests`):

- vocabulary and validation policy: unknown field path fails closed; every
  required-when rule from the hints (odometer/`tbc`, unroadworthy reason,
  total-loss category and salvage value, physical-address pairing, VAT
  two-way answer); enum round-trips for every code in the markup;
- write lifecycle: automation save requires lease and expected version and
  fails closed on staleness; automation-written values carry unconfirmed
  provenance; staff confirmation flips them; automation callers cannot
  produce a confirmed finding; operation-key replay returns the original
  result;
- derivation tests for totals/worklists only if D2 accepts the formulas.

SQL integration (`tests/Pegasus.IntegrationTests`, each class
`[Trait("Category", "SqlServer")]`, **no collection fixtures** — the
parallel disposable-LocalDB pattern via `LocalDbTestDatabase`/template):

- assessment store round-trip and per-field provenance;
- automation write under a real lease with operation-key replay and
  action-history proof of the attributed per-field change (logging parity
  asserted side by side with an equivalent staff save);
- the migration assertion update above.

Ingress (`AutomationMcpIngressTests`): extend the `ExpectedTools` array;
token without `automation.assessment` denied with an
`automation_scope_denied` security event; `pegasus_assessment_update` over
real HTTP mutates under a real lease and writes attributed per-field
history; `pegasus_eva_bundle_generate` records the handoff revision and its
history entry; kill switch (Admin disable, ~5 s registration cache) still
refuses the new tools; gate-off still exposes no route.

Architecture (`tests/Pegasus.ArchitectureTests`): no change expected —
`DependencyDirectionTests` already forbids Core referencing `OpenIddict` and
`ModelContextProtocol` and must stay green; no new packages are introduced.

## Docs impact

- `docs/architecture.md` § Provider API and Automation MCP: tool count and
  the direct-write model sentence.
- `docs/operations.md` § Automation MCP: the fourth scope, the
  logging-parity statement ("every automation action is recorded exactly as
  a human action is"), and the local-evidence statement.
- `docs/capabilities.md`: new row (proposed `MCP-06`, Automation Actor
  assessment actions) or a widened MCP-02 note — D7.
- The ADR already queued in `NOW.md` ("promote the Automation Actor contract
  to an ADR") must record: the direct-write model with logging parity, the
  reinstatement of both previously excluded candidates, the
  recordable-not-confirmable finding boundary, the structural absence of
  confirmation/send tools, and the required rewording of the AI-09 contract
  in `docs/requirements.md`/`docs/capabilities.md` (proposal-only worker →
  direct-writing worker reviewed at assignment). This plan's decisions feed
  that ADR; they are not authoritative until it is accepted.
- `docs/open-decisions.md`: entries for whatever remains open after review.
- No change to `design/README.md` or any screen — the screens stay unlinked.

## Non-goals

- Wiring the Assessment or Suggestions screens, linking them from
  navigation, or any UI-15 activation (separate task; requires the
  design/README.md re-entry review).
- Any `Send to AI` transport, worker, or durable work-request lifecycle
  beyond the minimal send-binding record if D3 accepts it (AI-09 remains
  `Later / 1.3.0`).
- Report rendering or renderer activation (RPT-01/EXT-08); the renderer
  workspace remains a non-runtime import.
- Valuation guide adapters (EXT-10/EXT-13), estimate ingestion (EXT-12),
  fee/invoice work (EXT-11), vehicle-history provider calls.
- Any new access right for the Automation Actor; any lifecycle-transition,
  archive, or hold tool; any deletion of anything.
- Production activation, deployment, or external-client evidence claims.

## Open decisions for the operator

Decided 2026-08-03 (recorded, no longer open): **D1** — direct writes with
logging parity; staged suggestions rejected. **D5** —
`pegasus_case_update_details` reinstated. **D6** — EVA bundle generation
reinstated as a mutating tool, regeneration allowed under the same guards.
**D9** — no fresh pull-forward needed: ENG-01/ENG-02 and the UI-15/AI-09
design route are in progress (PR #326) and AI-09 implementation is assigned
to the channels task, so the Core assessment model continues in-progress
work; the `NOW.md` claim line at take-up is workflow mechanics, not a
pending approval.

Still open:

- **D2 — Rate cards and formulas.** Who owns the rate-card data (Core
  reference data vs a versioned published-card artifact), and acceptance of
  the derivation formulas (WU÷10×rate, sundry percentage, material bands,
  the VAT rule) as Core policy — EXT-09 says formulas require accepted
  authority. Blocks estimate derivation; raw line writes can precede it.
- **D3 — Work-request binding.** Must an automation assessment write
  reference an open Send-to-AI work request, or are free-standing writes
  from externally designed Automations equally acceptable (consistent with
  the comprehensive-toolset direction)? Recommended: optional binding —
  carried for correlation when the session was initiated by a Send to
  Claude hand-off, never required.
- **D4 — Field overlap.** Single-owner treatment of the fields the
  assessment surface shares with `CaseDataProjection` (registration, make,
  model, mileage/odometer, incident date, instruction date, inspection):
  recommended — the assessment projection reads them and
  `pegasus_case_update_details` writes them through the existing `ISaveCase`
  path. Includes the odometer-miles vs mileage+unit mapping and how
  `mileage_source` relates to the ADR-0012 tiers.
- **D7 — Capability row.** New MCP-06 row vs widening MCP-02's note.
- **D8 — Markup ambiguities**, recorded rather than guessed: betterment
  type/semantics; the `operations[n].guide` "Code" meaning; approved
  signatory list ownership and its relationship to staff identity; whether
  fee fields join the assessment record now given EXT-11 is `1.2.0`;
  salvage category `""` vs `N/A`; where guide valuation figures are stored
  and when (now also the landing place for the planned external
  valuation-service figures — see the channels plan).
- **D10 — Suggestions screen fate.** With the apply gate superseded, the
  built Suggestions markup is a candidate for repurposing as a read-only
  "what the automation changed" review view driven by action history, or
  for retirement. Design decision at the UI-15 re-entry review.

## Sequencing and slices

1. **Slice 1 — can start once the ADR text is agreed** (no new Core model,
   no migration): `pegasus_eva_handoff_status` and
   `pegasus_eva_bundle_generate` over the existing `IEvaHandoffQueries` /
   `IGenerateEvaHandoff`; `pegasus_case_update_details` over the existing
   `ISaveCase`; ingress tests; docs. Independently shippable.
2. **Slice 2 — after D3/D4** (D2's formulas may lag): Core assessment
   contracts, save command, store, one migration, unit + SQL tests. No UI,
   no linked route.
3. **Slice 3 — with or immediately after slice 2**: the
   `automation.assessment` scope, `pegasus_assessment_get`,
   `pegasus_assessment_update`, ingress tests, docs, and the ADR text.
4. **Slice 4 — separate task, after UI-15 re-entry review**: wiring the
   workbench's per-section saves, the review presentation of unconfirmed
   automation values, and D10's Suggestions-screen decision. Not this
   tranche.
5. **Slice 5 — after D2**: estimate derivation in Core and the report-data
   projection candidate.
6. **Tier-5 evidence run** (already queued in `NOW.md`): one run covering
   the full toolset — existing nine plus this tranche — so the external-client
   evidence is recorded once, per operations.md, before any activation claim.

## Evidence-tier claims

Per operations.md § Required evidence tiers, and stated in advance so no
stronger claim is inferred: a tool registration or schema is not proof of
anything; a green build with the unit suites is tier 1–2; the SQL
persistence tests are tier 4-adjacent local evidence; the ingress HTTP tests
are the repeatable tier-4 caller equivalent the merged MCP work already
established (tier 2–4 recorded in operations.md). This plan, fully
implemented, still claims no tier-5 evidence (that is the separately queued
real-client run), no deployment, no live activation, and no operator
acceptance. The whole surface remains composition-gated off outside
DevelopmentOffline evidence runs.
