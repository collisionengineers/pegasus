# FRD-25: Repair estimates, imports and Glass's sessions

> Owner capabilities: ENG-01, EXT-06, EXT-09, EXT-12 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Every repair estimate is an immutable version. Each Case has exactly one
  current accepted version.
- Imported or AI material stays a Draft until an enabled human staff member accepts it with
  **Use estimate**. Importing never changes Current.
- An import is keyed by Case plus source hash. The same hash replays the same
  Draft.
- Estimate PDFs need readable embedded text. An unreadable or ambiguous
  source refuses the whole import, with no OCR and no partial Draft.
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

**One current version.** Every accepted repair specification is an
immutable, versioned Core aggregate. Each Case has exactly one current
accepted version, shared by all of the Case's report projections.

Each version keeps its stable identity, ordered technical lines, source
route, source artifact identity, version and hash, mapping evidence, raw
calculation basis and totals, the selected labour-rate card version if one
was selected, creating actor and time, and, when accepted, the named staff member
and acceptance time. Glass's, Audatex PDF, an approved AI proposal and manual
entry are provenance routes, never authorities. Imported or automated
material stays a Draft until an authorised human staff member accepts the exact source,
mapping, ordered lines and calculation basis. A Draft without the required
provenance cannot satisfy report readiness. Obsolete development-state
estimates need not be converted or kept.

**Import is keyed by Case plus source hash.** A raw artifact imported through
either caller of the shared import command uses that key. The same Case with
the same hash is a replay that returns the existing Draft. A different
artifact creates the next immutable Draft. The provider and parser are
detected from the registered types; an ambiguous artifact is refused, never
guessed.

**Authority is checked twice.** Before reading a replay or parsing the source,
the command proves the typed actor, the current persisted Case version, and
the edit lease holder, token and expiry. Both that check and the final save
need an assessment-writable state: Not ready, Review or With Engineer, where
With Engineer covers before and after the report
([FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels)). A file
occurrence must name the exact confirmed, non-removed document version; a
correctly paired historical version is still valid evidence. The new Draft is
guarded again in the save transaction. Importing never confirms rows or
changes Current, even when a staff member started it. The human staff
**Use estimate** action confirms and accepts the Draft once its source,
mapping, rows and calculation basis pass the normal acceptance rules.

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

**Readable text is required.** Estimate PDFs need readable embedded text.
Unreadable, scan-like or unsupported estimates are refused, with no OCR and
no partial Draft. A staff upload stores the confirmed source through the normal
Case document flow, then parses it immediately in the same Import action.
After an interrupted or refused parse, retrying the same source reuses the
confirmed retained document under current Case authority without uploading it
again. Source attribution and complete arithmetic must agree before an import
succeeds. Replaying the original operation does not revive its old lease or
assume another Case-version increment.

**Engineer acts on a Draft (v28, ruled 20 September 2026).** Target % of
value scales a Draft down under the Engineer's hand: one factor lowers every
part price, every materials figure and the labour rate, each to its floor
(£50 an hour and 65 % of price unless the Engineer sets others); hours never
move. Apply saves the Case first (the one Save, which records the Draft as
edited), then freezes the saved draft, saves the scaled specification and
freezes it again as the scaled version; Remove scaling likewise saves first
and returns the Draft to the version frozen before. A contract repair's agreed sum is Case data the
Engineer records beside the specification; recording it sets the outcome to
Contract repair, and a different sum is a scaling target. Every import,
scale, removal, restore and sent report freezes a numbered version with how
it came about; Restore makes a frozen version the Draft after freezing the
outgoing one; the version a sent report used is marked. Compare reads any
two of a Case's specifications line by line; a specification may name the
one it supplements, with a reason, and the composed supplementary statement
prints on the report when the Engineer says so. Send to AI proposes only.

**Corrections and projections.** A correction creates a new reasoned version
that keeps and supersedes the earlier accepted one. Accepted rows and their
evidence are never edited in place. A Case with no unambiguous current
accepted version fails closed. The specification uses one line vocabulary and
one calculation basis. The three assessment-report lists (new parts, repairs
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

Keeping a returned estimate's source files does not use up the staff member's
still-valid Case edit authority. The import uses that authority to land one
Draft. A genuine Case edit in between, or an expired or lost lease, leaves
the retained result waiting until the staff member regains authority. Callback
replay creates neither another Draft nor another change.

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
repair totals are never invented to get around the estimate contract. An
unknown repairer VAT status needs an explicit status or category before
totals are accepted. Supplied, observed, derived and professionally accepted
values keep their distinctions.

### Retained PDF estimate import

The estimate-import command accepts the supplied Glass's calculation and
Audatex full-report PDFs through their deterministic provider mappings. It
keeps the original document and its source hash before importing a Draft;
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
the same import. Import never selects a Current estimate.

## States and transitions

| Thing | States |
| --- | --- |
| Repair specification | Draft, then accepted (Current) by Use estimate; a correction makes a new version that supersedes the old |
| Glass's session | launched, `Unknown` (holds the account), resumed, closed by the owning staff member with a reason |

## Edge cases and fail-closed behaviour

- An ambiguous estimate artifact, an unreadable PDF, or a reconciliation
  mismatch refuses the whole import.
- A Case with no unambiguous current accepted specification fails closed.
- A lost Glass's answer stays `Unknown` and holds the account until the
  owning staff member closes it with a reason.

## Acceptance evidence

Core tests cover the import replay key. Integration tests cover the estimate
import command under a lease, Use estimate, and Glass's session closure. Live
Glass's evidence is a separate tier
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
