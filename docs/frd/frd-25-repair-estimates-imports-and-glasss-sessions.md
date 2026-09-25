# FRD-25: Repair estimates, imports and Glass's sessions

> Owner capabilities: ENG-01, EXT-06, EXT-09, EXT-12 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A repair spec a staff member creates is the one in use at once: typed in,
  imported, or returned from Glass's. Each Case has at most one Current repair
  spec, and once an Inspection + Audit Case has its Audit, the Inspection and
  the Audit each have one.
- The Current repair spec stays editable while the Case is writable.
  **Use repair spec** switches back to another one.
- An imported spec takes the one enabled Pegasus labour-rate card. AI and
  automation material stays a Draft until a staff member presses
  **Use repair spec**.
- An import is keyed by Case plus source hash. The same hash replays the same
  live spec.
- Estimate PDFs need readable embedded text. An unreadable or ambiguous
  source refuses the whole import, with no OCR and no partial spec.
- A Glass's answer that was lost stays `Unknown` and holds the account until
  the owning staff member closes the session with a reason.

## Purpose

This document owns the repair estimate on a Case: the canonical repair
specification and its versions, where estimate figures may come from, the
retained PDF estimate import, and interrupted Glass's sessions. It serves the
PRD outcomes for source-labelled Case data and a definitive Engineer report.
The Engineer's findings, damage, valuation and settlement are owned by
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md). The
inspection address and vehicle data are owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md). Case states and edit
leases are owned by
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels) and
[FRD-14](frd-14-record-edit-leases.md#case-edit-lease).

## Behaviour

### Canonical repair specifications

**One Current spec.** A Case holds several live repair specifications and at
most one Current one, shared by all of the Case's report projections. A spec
a staff member types in, imports or brings back from Glass's becomes Current
when it is created (operator, 25 September 2026). The one it replaces stays
in the list, and **Use repair spec** switches back to it. An AI draft only
proposes: it stays a Draft until a staff member presses **Use repair spec**.
The Current spec stays editable while the Case is writable; each change marks
a generated report stale, and the numbered versions below keep the history.
Create audit copies every live estimate, with its lines and its Current
choice, into the Audit; discarded estimates and revision snapshots are not
copied
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
From then on the Inspection and the Audit each have their own Current spec,
feeding their own report. Every estimate edit, import, Glass's return and
Use repair spec acts on the Audit's estimates; the Inspection's stay as its
report was sent.

Each spec keeps its stable identity, ordered technical lines, source route,
source artifact identity, version and hash, the selected labour-rate card
version if one was selected, and its creating actor and time. Its totals are
calculated from its lines and header whenever they are read. Glass's,
Audatex PDF, JSON, an AI draft and manual entry are provenance routes, never
authorities. Obsolete development-state estimates need not be converted or
kept.

**Import is keyed by Case plus source hash.** A raw artifact imported through
either caller of the shared import command uses that key. The same Case with
the same hash is a replay that returns the existing live spec. A discarded
spec no longer holds its source, so importing that file again creates a new
one. A different artifact creates the next spec. The provider and parser are
detected from the registered types; an ambiguous artifact is refused, never
guessed.

**Authority is checked twice.** Before reading a replay or parsing the source,
the command proves the typed actor, the current persisted Case version, and
the edit lease holder, token and expiry. Both that check and the final save
need an assessment-writable state: Not ready, Review or With Engineer, where
With Engineer covers before and after the report
([FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels)). A file
occurrence must name the exact confirmed, non-removed document version; a
correctly paired historical version is still valid evidence. The new spec is
guarded again in the save transaction. A staff import becomes the Current
spec; an automation import through MCP stays a Draft.

**Glass's calculation PDFs.** These keep ordered Body, Auxiliary and Paint
rows, included-operation context, source guide codes, unambiguous
manufacturer part identities, notes and printed amounts. PDF labour hours are
already net of overlap and do not reuse the XML gross-time conversion. Parts
and position appendices are evidence for existing rows, never extra charges.
Repeated printed charges and visibly clipped text stay as printed. Whole-row,
section and document reconciliation is required. Section labour must equal
the printed rate × section hours as the source computes it. Missing or
ambiguous required evidence refuses the whole import. Source rates and VAT do
not select a Pegasus rate card or decide a repairer's VAT status.

**Labour rate on a new spec.** A new repair spec — imported, returned from
Glass's, or started with **New repair spec** — takes the one enabled Pegasus
labour-rate card. With no enabled card, or several, the rate stays blank and
report readiness asks for it. The staff member can change it. A source
document's own rate never picks the card.

**Readable text is required.** Estimate PDFs need readable embedded text.
Unreadable, scan-like or unsupported estimates are refused, with no OCR and
no partial spec. A staff upload stores the confirmed source through the normal
Case document flow, then parses it immediately in the same Import action.
After an interrupted or refused parse, retrying the same source reuses the
confirmed retained document under current Case authority without uploading it
again, and any file already confirmed in Case Files imports from there. Source attribution and complete arithmetic must agree before an import
succeeds. Replaying the original operation does not revive its old lease or
assume another Case-version increment.

**Engineer acts on a spec (v28, ruled 20 September 2026).** Target % of
value scales a spec down under the Engineer's hand: one factor lowers every
part price, every materials figure and the labour rate, each to its floor
(£50 an hour and 65 % of price unless the Engineer sets others); hours never
move. Apply saves the Case first (the one Save, which records the spec as
edited), then freezes the saved spec, saves the scaled specification and
freezes it again as the scaled version; Remove scaling likewise saves first
and returns the spec to the version frozen before. A contract repair's agreed sum is Case data the
Engineer records beside the specification; recording it sets the outcome to
Contract repair, and a different sum is a scaling target. Every import,
scale, removal, restore and sent report freezes a numbered version with how
it came about; Restore makes a frozen version the spec's content after
freezing the outgoing one; the version a sent report used is marked. Compare reads any
two of a Case's specifications line by line; a specification may name the
one it supplements, with a reason, and the composed supplementary statement
prints on the report when the Engineer says so. Send to AI proposes only.

**Corrections and projections.** A correction is an edit of the spec, Current
or not; an imported line keeps the values its source printed beside the
amendment, and the numbered versions keep what a scale, restore or sent
report replaced. There is no separate supersede step (operator,
25 September 2026). A Case with no Current spec cannot generate a report. The
specification uses one line vocabulary and one calculation basis. The three assessment-report lists (new parts, repairs
and additional operations) are one deterministic names-only projection of
those ordered lines, not a second renderer-owned specification.

**Replay in the estimate editor.** The editor has no save of its own: the
Case's one Save carries the specification with the rest of the Case
(operator, 23 September 2026), and a specification it leaves unchanged is not
rewritten. The Save keeps the Case version and line identities the staff
member submitted. Retrying the same operation keeps that
intent: source evidence and amendment timestamps are resolved only for a new
operation, not rebuilt before replay detection. A prior successful operation
returns the same estimate identity in its current state, even after later
edits, and never reapplies the older edit. Changed intent under the same
operation key is refused, as is a new operation against a stale Case
version.

### Glass's interrupted sessions

A Glass's launch records its callback and external account before contacting
the provider. Vehicle and estimate identities are kept as soon as they
arrive. Resume continues an interrupted preparation or a known vehicle that
has not started an estimate; an existing estimate reopens by its existing
identity. These actions sit in the Case estimate section and do not need
credentials reset.

Every Resume presents the current Case version and live edit lease, including
preparation, an active estimate and a waiting import. The current registration
(normalized for spacing and case) and whole-mile mileage must match the
protected launch facts. A mismatch refuses further provider work and leaves
the account held; staff restore the original facts or confirm external closure
before starting a new session. Valid Resume replaces the protected import
authority with the authority just proved. Credential generation must still
match the account used at launch.

Before selecting a vehicle or reopening an estimate, the provider detail form
must identify the expected vehicle ID, registration, mileage and NatCode, and
offer the configured repair profile. Missing or contradictory controls refuse
the action. Resume uses the positive estimate ID already recorded; uncertain
writes never restart at zero. A URL issued by the provider establishes no
claim that the hosted editor has initialized successfully.

Keeping a returned estimate's source files does not use up the staff member's
still-valid Case edit authority. The import uses that authority to land one
spec, which becomes Current. When that authority is no longer current — a
Case save while Glass's was open, or an expired or lost lease — the return
takes a fresh lease for the returning staff member and lands the spec, as long
as nobody holds the Case. While anyone holds it, the same staff member in
another window included, the retained result waits for **Resume**, so unsaved
edits are never overtaken. Callback replay creates neither another spec nor
another change.

**Unknown answers hold the account.** A provider write whose answer was lost
stays `Unknown` and keeps the account. It must not create another vehicle or
calculation, and must not release the account just because time passed. The
owning staff member can close any session that still holds the account, except
one in the middle of an import, only after confirming Glass's is closed and
no estimate is open, and with a reason. Stale versions and closure by another
staff member are refused. An account holds one live session: while it is held
from another Case, every Case the staff member opens names that Case instead of
offering a launch, and a second launch is refused before the provider is
contacted. Reopening an estimate may come back under a different provider
estimate id; every id a session was launched under is kept, and the provider
may return any of them. Checkpoints and explicit closure are audited
permanently without provider credentials, callback tokens or document
content.

### Inspection location and estimate sources

Every Collision Engineers assessment is a desktop inspection. A physical
inspection address is report data, not evidence that anyone attended. The
Principal setting picks a physical vehicle location or the literal
`Image Based Assessment`; a staff override needs a recorded reason and is
reversed the same way. Address suggestions may use Principal usage frequency,
accident location and image or vision evidence; a suggestion never becomes a
confirmed fact by itself. The full inspection address rules are in
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address).

Repair cost figures come from external estimate imports (including Audatex
and Glass's), AI estimates returned through MCP, or staff file import. Manual
repair totals are never invented to get around the estimate contract. The
repairer's VAT status is recorded on each repair specification, the one
owner of that fact; an `Unknown` status never blocks **Use repair spec**, and
the specification's selected VAT categories govern its totals
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#estimate-vat-on-the-rendered-report)).
Report readiness asks no separate repairer VAT question. Supplied, observed,
derived and professionally accepted values keep their distinctions.

### Retained PDF estimate import

The estimate-import command accepts the supplied Glass's calculation and
Audatex full-report PDFs through their deterministic provider mappings. It
keeps the original document and its source hash before importing the spec;
the same Case and hash replay the same import. Printed totals, rates, line
structure and provider identity must agree. PDF net labour is not reduced
again by the XML-specific overlap rule.

Readable embedded text is required. An unusable font map, a scan-like page
or a parser failure gives an explicit refusal, never an OCR request
([ADR-0047](../adr/0047-scanned-instruction-ocr-only.md)). The staff Import
action accepts one supported file and stores the source through the existing
Case document upload mechanism before parsing it immediately. The confirmed
source appears in Case Files even when parsing refuses it; retrying the same
file uses that retained source, while a replay of the same operation returns
the same import. A staff Import makes the imported spec Current.

## States and transitions

| Thing | States |
| --- | --- |
| Repair specification | Draft (live) or Discarded. At most one live spec is Current: a staff-created spec is Current at once, and Use repair spec switches. Edits keep the spec; imports, scales, restores and sent reports freeze numbered versions |
| Glass's session | launched, `Unknown` (holds the account), resumed, closed by the owning staff member with a reason |

## Edge cases and fail-closed behaviour

- An ambiguous estimate artifact, an unreadable PDF, or a reconciliation
  mismatch refuses the whole import.
- A Case with no Current repair spec, or a Current spec with no lines or no
  labour rate, cannot generate a report.
- The Current repair spec cannot be discarded; switch to another one first.
- A lost Glass's answer stays `Unknown` and holds the account until the
  owning staff member closes it with a reason.

## Acceptance evidence

Core and integration evidence covers identity, current authority, account
exclusivity, uncertain writes, callback replay, custody and one import that
lands as the Current spec on the rate card.
Browser evidence covers save-before-launch, refusal without provider work,
fresh controls, stale Close and preservation of edits during return.

The hosted editor must also pass live acceptance on the deployed artifact:
three fresh launches across two vehicle models (cold and warm browser), three
positive-ID resumes including reload and host restart, deliberate estimate
changes followed by Save & Exit and an automatic import that lands as the
Current spec, replay producing one spec, expired-lease recovery, original-window closure, and a second Case
refused while the account is held. Chrome is primary; Edge also covers a fresh
launch and Resume. Manual export/import does not satisfy this integration's
acceptance. Supplier startup failures remain open until that journey passes
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `ENG-01`, `EXT-06`, `EXT-09`, `EXT-12` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md).
- Technical constraints:
  [ADR-0047](../adr/0047-scanned-instruction-ocr-only.md).
