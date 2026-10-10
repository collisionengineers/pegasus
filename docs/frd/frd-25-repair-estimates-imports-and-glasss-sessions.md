# FRD-25: Repair estimates, imports and Glass's sessions

> Owner capabilities: ENG-01, EXT-06, EXT-09, EXT-12 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A repair spec a staff member creates is the one in use at once: typed in,
  imported, or returned from Glass's. Each Case has at most one Current repair
  spec, and once an Inspection + Audit Case has its Audit, the Inspection and
  the Audit each have one.
- The Current repair spec stays editable while the Case is writable.
  **Use repair spec** switches back to another one.
- A Glass's estimate belongs to its repair spec. **Glass's** on that spec
  reopens the same estimate, and its saved return updates that spec. On any
  other spec it starts a new estimate.
- An imported spec takes the one enabled Pegasus labour-rate card. AI and
  automation material stays a Draft until a staff member presses
  **Use repair spec**.
- An import is keyed by Case plus source hash. The same hash replays the same
  live spec.
- Estimate PDFs need readable embedded text. An unreadable or ambiguous
  source refuses the whole import, with no OCR and no partial spec.
- A Glass's answer that was lost stays `Unknown` and holds the account until
  the owning staff member closes the session with a reason.
- Glass's launch and return run in the background. The Glass's window waits,
  then opens the estimator or reports the outcome on the Case.

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
The Automation Actor may edit any live spec in place, the Current one
included, but never puts a spec in use (operator, 7 October 2026;
[FRD-10](frd-10-mcp-automation-and-actor-boundary.md#ai-job-and-estimate-tools)).
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

**A Glass's estimate belongs to its repair spec (operator, 6 October 2026).**
The return that makes a spec records on it the Glass's stock vehicle its
estimate stands on, with the estimate id and the vehicle facts it was proved
against. A later return for that vehicle updates the same spec. Its lines and
source are replaced. Its name, labour-rate card, VAT, discounts and
supplementary statement are kept. Edits made in Pegasus since the last return
are overwritten, and nothing is frozen before the update; the updated content
is frozen as an imported version, as a new import is. The updated spec is
Current. A spec discarded since no longer takes the return, which lands as a
new spec that belongs to the same estimate. Create audit copies the tie, so
the Audit's copy belongs to the same Glass's estimate. A duplicated spec
belongs to none. A spec made before this rule carries no tie and starts a new
estimate.

Each spec keeps its stable identity, ordered technical lines, source route,
source artifact identity, version and hash, the selected labour-rate card
version if one was selected, and its creating actor and time. Its totals are
calculated from its lines and header whenever they are read. Glass's,
Audatex PDF, JSON, an AI draft and manual entry are provenance routes, never
authorities. Obsolete development-state estimates need not be converted or
kept.

**Import is keyed by Case plus source hash.** A raw artifact imported through
either caller of the shared import command uses that key. The same Case with
the same hash is a replay that returns the existing live spec and leaves the
spec in use as it was; the page reports that the file was already imported
and shows that spec, and a Glass's return reports its estimate as recorded
rather than as the spec in use. A discarded spec no longer holds its source,
so importing that file again creates a new one. A different artifact creates
the next spec. A Glass's return that updates its spec answers only for that
spec: the same export replays to it, and another spec holding that hash is
not it. The provider and parser are detected from the registered types; an
ambiguous artifact is refused, never guessed.

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

**A Glass's import fills an empty VIN.** The VIN a Glass's export or
calculation sheet names fills the Case's VIN in the import's transaction,
only where the Case holds none
([FRD-06](frd-06-vehicle-and-engineering-evidence.md); operator, 7 October
2026).

**Glass's calculation PDFs.** These keep ordered rows from the sheet's
operation sections (Body, Mechanical, Electrical, Auxiliary work,
Upholstery, Dent repair, Mechatronics and Special work; at least one),
Paint and Additional costs, included-operation context, source guide codes,
unambiguous manufacturer part identities, notes and printed amounts. PDF
labour hours are already net of overlap and do not reuse the XML gross-time
conversion. The sheet's labour time unit, `1 Hour` or N work units to the
hour, converts every printed time to hours, and the XML's `TimeUnit` does the
same (60 is the Hour option). Parts and position appendices are evidence for existing rows,
never extra charges. Repeated printed charges and visibly clipped text stay
as printed. Whole-row, section and document reconciliation is required.
Section labour must equal the printed rate × section hours as the source
computes it; Additional costs prints no rate, so its labour is checked at
the Body rate, and its hours stay out of the Total Labour hours while its
cost is in the Total Labour cost. The document's Total Labour is the total
hours at the one rate rounded once, plus Additional costs, and may differ
from the sum of the section labours by a penny. A paint level the one table below does
not name refuses the import. Missing or ambiguous required evidence refuses
the whole import. Source rates and VAT do not select a Pegasus rate card or
decide a repairer's VAT status.

**Glass's rows, both routes (operator, 29 September 2026).** The Glass's XML
export and the calculation PDF land the same spec:

- An additional operation (the XML's `Extra costs`, which includes Glass's
  own set-up time; the PDF's Auxiliary work or Additional costs `EC` row) is
  a Specialist line. With hours it is priced by work units
  at the spec's rate; with none, its amount is a fixed Specialist sum.
- An included operation is a no-charge Other line, with neither hours nor a
  price, noted as included in its row. It is never a new part. The grid draws
  it as included: indented under the row it belongs to, its note across the
  figure columns, with no figure boxes while reading (operator, 9 October 2026).
- A part's side prints after its description, `(L)` or `(R)`, as Glass's
  sheet prints it.
- A guide time or price the engineer changed in Glass's, Glass's reason for
  it, and a row's annotations and selected criteria are the line's note.
- A paint row's level is its line type, from one table both routes share. New
  paint: `I`, `K1R`, `K1N`, `K1G` (the XML's material kind and level B 3,
  K 2, K 3, K 4). Blend: `B` (B 4, K 5). Repair: `II` inner surface (the
  operator's reading, 5 October 2026), `III`, `IV`, `SP`, `K2` (B 0, B 1,
  B 2, B 6, K 0). A row with no level (preparation, colour mixing, sample,
  the clear-coat surcharge) is preparation. Glass's marked-up paint
  material (the sheet's `Z`, the XML's `PaintMatExtraAppl`) is the note
  "Markup material".
- A discount, surcharge, small-material, sourcing, disposal or
  environmental-fee value the XML prints as non-zero (operator, 5 October
  2026) is imported and noted on the first line with room, beside Glass's
  net. It changes no line and no total.

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
move. The slider starts at the spec's own share of the Engineer's Value,
above 100 % where the spec costs more, and goes no higher, so scaling only
lowers prices; a contract repair's agreed sum below that share is the
starting target instead (operator, 8 October 2026). Moving the slider previews (P34; operator, 28 September 2026): Core
scales and totals the spec as the editor holds it, read on the save's own
terms, the changed cells show its figures in amber and cannot be typed in,
and the rollup and the readout follow. The readout, previewed and recorded
alike, ends `labour at floor` when the labour rate stops at its floor.
Nothing is recorded until Apply, and any save records the spec as
edited, not as previewed. Apply waits for a change not yet sent to land
(every save records the spec as edited, [FRD-16](frd-16-case-record-workspace.md#case-workspace)),
then freezes the saved spec, saves the scaled specification and freezes it
again as the scaled version; Remove scaling likewise waits and returns the
spec to the version frozen before. A contract repair's agreed sum is Case data the
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
those ordered lines, not a second renderer-owned specification. Repairs lists
repair and R&I lines, as the Case page does.

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

### Glass's launch and return

**One button (operator, 6 October 2026).** The Estimate section offers
**Glass's** and no Resume. What it does depends on where it is pressed:

- While the staff member's own session on the Case still holds the account,
  it continues that session: an interrupted preparation carries on, an open
  estimate reopens, and a held return lands.
- On a repair spec that belongs to a Glass's estimate, it reopens that
  estimate in a new session, under the pressing staff member's own account.
  No vehicle is made.
- On any other spec, on **New repair spec**, or with no spec, it starts a new
  estimate on a new stock vehicle. This is how a Case comes to hold a second
  Glass's spec.

A fresh estimate for a spec comes from clearing its lines inside the
estimator, or from discarding the spec. The portal's own Reset Repair Estimate
is not supported: the vehicle then shows no estimate and the reopen is refused
at `glass.details.profile`.

**Any staff member who holds the Case edit may reopen a spec's estimate.**
While a colleague holds the edit, nobody else can press Glass's. After a
take-over, or once the colleague's edit has ended, their session may still be
live on the same estimate. The reopen ends that session and frees its account.
While that session is landing a return, the reopen is refused and can be
retried. Work the colleague later saves in Glass's stays in the estimate and
arrives with the next return.

**Glass's runs in the background (operator, 28 September 2026).** A launch, a
reopen and a continued session check the Case authority, the account and the
vehicle, record the session, and answer at once. The provider work then runs in the background
([ADR-0058](../adr/0058-glass-provider-work-in-the-web-host.md)). The Glass's
window says "Preparing Glass's…" while it runs. When the estimator is ready,
the window refreshes the Case's Glass's controls and opens the estimator. If
the launch did not open, the window hands the outcome back to the Case.

The provider's return is the same. It is checked and claimed at once: a
stranger is refused before the one-use token is spent. The window then says
"Bringing the estimate back…" while the relay, export, retention and import
run. The finished outcome is reported on the Case as before. The same return
delivered again while the import runs waits on the same work.

Only the owning staff member sees the window or its progress. The estimator
address is never shown on the page or in its progress answer.

While a session's work runs it cannot be closed. The Close control is absent,
and a Close posted anyway is refused with a reason, so nothing the work creates
at the provider is left without a session. A second click of the same Launch
waits on the first click's work. If the queue is full, the work runs in the
staff member's own request, as it did before, and is never dropped.

**A plate Glass's does not know (operator, 2 October 2026).** The launch's
lookup is Glass's own plate search, made once; the portal does not repeat
it. An answer without a type number is Glass's "vehicle details have not been
found" and settles nothing. The launch then inserts a placeholder vehicle into
the account's stock list — the portal's own "Add Unqualified Vehicle", with
the Case registration as its model text so the stock list names the Case —
one per launch and never reused, proves it, selects it and opens the
estimator as usual. The Engineer identifies the real vehicle inside the
estimator with its Alternative vehicle selection. The Case page shows
nothing new: the session line reads as for any launch, and the host log says
the launch is on a placeholder. Get valuation on such a plate answers the
card's information sentence, as Glass's vehicle-age and Cazana's no-data
answers do, because nothing is broken (operator, 10 October 2026); a
placeholder has no value
([ADR-0062](../adr/0062-glass-placeholder-vehicle-for-unknown-plates.md)).

The return of a placeholder session is checked as any other, except for the
vehicle's identity: the export may name no plate and no mileage, or the
Case's own, and any other vehicle's is refused; the type number it names is
the Engineer's choice and is recorded on the session, not compared. This rule
stands until the first live return has been read.

### Glass's interrupted sessions

A Glass's launch records its callback and external account before contacting
the provider. Vehicle and estimate identities are kept as soon as they
arrive. Pressing **Glass's** again continues an interrupted preparation or
a known vehicle that has not started an estimate; an existing estimate
reopens through its vehicle, as the portal does, and must answer one of the
session's own estimate ids. A start whose answer was lost is asked again on
the recorded vehicle: the portal answers a start on a vehicle with the
estimate that vehicle holds, so no second vehicle or estimate is made
(captures of 6 October 2026). These actions sit in the Case estimate section
and do not need credentials reset.

**Interrupted background work.** Work can stop part-way: the host restarts,
or the work runs past its time limit. A session found waiting with no work
running for it is settled when the window next asks:

- Prepared never reached the provider. It stays Prepared and can be
  continued, and the Case says the estimate did not start.
- Launching or a claimed import may have reached the provider. It becomes
  `Unknown`, keeps the account, and follows the Unknown rule below.
- A claimed import keeps the provider's return with its claim. If the relay
  back to Glass's had not begun, continuing the session makes it once. If
  it had begun, continuing looks the export up again and never relays again.

**A return that cannot be brought in (issue 916, operator ruling).** Three
things follow when the return is accepted but the estimate does not land:

- If Pegasus's own reader refuses the export, the session is `Failed` with
  `glass.export.unreadable`, and the downloaded XML is kept on the Case first,
  as a rejected Glass's export. It is not a Draft's source. The PDF sheet is
  inside that XML. The host log names the position or field the reader refused,
  in position numbers, field names and short code values only, never the
  registration, a token or a URL. Once the reader accepts the file, staff can
  import the kept XML from Files. An export refused for another vehicle's
  registration, mileage or type number, or for carrying no estimate, is kept
  the same way, so work an Engineer corrected inside Glass's is not lost; it
  is not offered **Fetch again**, because the export is not this session's
  vehicle.
- A session that failed only because the export could not be read, or could
  not be fetched after the relay succeeded (the export grid or the download
  refused, offered more than one export or one off Glass's own origin, or
  exceeded the size cap), offers **Fetch again** beside Glass's in the Case's
  Glass's controls. It signs in, selects the recorded vehicle and fetches the
  export for the same estimate. It makes no vehicle and starts no estimate. It
  is refused while the account holds another live session. A relay answered by
  a redirect (the sign-in page) may have been acted on, so that session is
  `Unknown`, and continuing it looks the export up without relaying again.
- If custody throws while storing the export, the session is `Unknown` with
  `glass.custody.failed`, holds the account and can be continued. Continuing
  fetches the export again and does not relay Save & Exit again. It is never
  reported as interrupted. If custody instead answers that an artifact failed,
  the session is `Failed` with the same code and cannot be continued.

A session has no lifetime (operator, 9 October 2026). A saved return lands,
and a press continues the staff member's own session, however long ago it was
launched; only a return under a replaced or disabled credential is `Expired`.

Every continued session presents the current Case version and live edit
lease, including preparation, an active estimate and a waiting import. The
current registration (normalized for spacing and case) must match the one the
stock vehicle was made with. A mismatch refuses further provider work and
leaves the account held; staff restore the original registration or confirm
external closure before starting a new session. A corrected mileage does not
stop it: the vehicle keeps the mileage it was made with, and is proved and its
export read against that. A placeholder holds no registration or mileage at
Glass's, so it continues whatever the Case now records, and its return is read
against the Case's current facts (operator, 9 October 2026). A valid
continuation replaces the protected import authority with the authority just
proved. Credential generation must still match the account used at launch.

A reopen of a spec's estimate follows the same rule against what the spec
recorded: on a real stock vehicle the Case must still record the registration
the estimate was started for. A changed registration is refused before
Glass's is contacted and before a session is recorded, and holds nothing. A
corrected mileage, or any change on a placeholder, reopens the estimate. The
session is new and uses the pressing staff member's current credential.

Before selecting a vehicle or reopening an estimate, the provider detail form
must identify the expected vehicle ID, registration, mileage and NatCode, and
show the configured repair profile. A placeholder's form must identify the
recorded vehicle ID, a numeric type number (the one the launch recorded, once
it has) and an empty registration, and show the profile; it has no mileage
to prove. Missing or contradictory controls refuse the action. A launch reads
the form of the vehicle it has just created once more, 500 ms later, when the
first reading does not identify it, and settles on the second refusal; no
other proof is read again.

The configured profile is one deployment setting because Glass's keeps
repair profiles for the organisation, so every staff login offers the same one
under the same id (operator, 9 October 2026). What "show the profile" means
depends on whether the provider has started an estimate on the vehicle. Before a start, the repair-profile control is enabled
and offers the configured profile. Once a start has allocated an estimate the
provider locks the control and marks the profile that started it selected, so
a continued session, a reopen or Fetch again requires exactly one disabled
repair-profile control whose selected option is the configured profile
(operator, 5 October 2026). A vehicle whose first start went unanswered may
show either, and is proved as whichever it shows.
The identity controls are checked the same way in both phases.

Reopening follows the portal: after the vehicle is proved, it starts with
estimate ID 0, as the portal does, and accepts only an answer naming one of
the estimate IDs the session recorded, or the one the spec recorded. Any other
answer leaves the session `Unknown` without opening the estimator: it is the
wrong vehicle, or an estimate reset at the portal. A URL issued by the provider
establishes no claim that the hosted editor has initialized successfully.

**What stops a reopen (operator, 6 October 2026).** A session reopening its
spec's estimate makes nothing at Glass's: the vehicle and the estimate were
recorded before it began. Whatever stops it short of another estimate's answer
is a plain `Failed` that frees the account, and the next press reopens the
estimate again. A session continuing its own first launch keeps the earlier
rule: what stops its reopen is `Unknown` and holds the account.

Keeping a returned estimate's source files does not use up the staff member's
still-valid Case edit authority. The import uses that authority to land one
spec, which becomes Current. When that authority is no longer current — a
Case save, a hand-off or a fresh edit session while Glass's was open, or an
expired or lost lease — the return lands the spec under the returning staff
member's own session: the live lease they hold, or a fresh lease taken for
them when nobody holds the Case (operator, 6 October 2026). The landing
advances the Case without ending their session, and a page they keep open
catches up on it as on any system write. While another staff member holds
the Case, the retained result waits for its owner to press **Glass's**, so
nobody's session is overtaken; the host log says why a held estimate waits. Callback replay
creates neither another spec nor another change.

**Unknown answers hold the account.** A provider write whose answer was lost
stays `Unknown` and keeps the account. It must not create another vehicle or
calculation, and must not release the account just because time passed. A
vehicle whose creation went unanswered is never made again. A start that went
unanswered on a recorded vehicle is asked again there when **Glass's** is
pressed, because it answers the estimate that vehicle holds. The
owning staff member can close any session that still holds the account, except
one in the middle of an import, only after confirming Glass's is closed and
no estimate is open, and with a reason. Stale versions and closure by another
staff member are refused. An account holds one live session: while it is held
from another Case, every Case the staff member opens names that Case instead of
offering a launch, and a second launch is refused before the provider is
contacted. Every estimate id a session was launched under is kept, and the
provider may return any of them. Checkpoints and explicit closure are audited
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
An `Unknown` status does block the report, and so does a hand-picked set of
VAT categories the report has no wording for (operator, 27 September 2026).
Report readiness reads the status from the Current repair specification; the
Case records it nowhere else. Supplied, observed, derived and professionally
accepted values keep their distinctions.

### Retained PDF estimate import

The estimate-import command accepts the supplied Glass's calculation and
Audatex full-report PDFs through their deterministic provider mappings. It
keeps the original document and its source hash before importing the spec;
the same Case and hash replay the same import. Printed totals, rates, line
structure and provider identity must agree. PDF net labour is not reduced
again by the XML-specific overlap rule.

Each Audatex LABOUR and PAINT WORK section prints its own time basis,
`Time Basis N WU = 1 HR`; the basis can differ between the two sections of
one report. The section's work units reconcile to its printed total and are
then divided by that basis, so every line carries hours, and paint time is
paint hours. A section without a readable basis refuses the import. The
printed Total Paint And Material Cost lands on the first paint line, as
materials sit on lines. Extras keep their printed price.

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
| Glass's session | prepared (work running in the background), launched, `Unknown` (holds the account), continued, closed by the owning staff member with a reason, or ended when a colleague holding the Case reopens the same estimate. Interrupted work settles Prepared as resumable and anything past it as `Unknown`. A `Failed` session whose export was unreadable, or could not be fetched after a successful relay, can fetch that estimate again |

## Edge cases and fail-closed behaviour

- An ambiguous estimate artifact, an unreadable PDF, or a reconciliation
  mismatch refuses the whole import.
- A Case with no Current repair spec, or a Current spec with no lines or no
  labour rate, cannot generate a report.
- The Current repair spec cannot be discarded; switch to another one first.
- A lost Glass's answer stays `Unknown` and holds the account until the
  owning staff member closes it with a reason.
- A **Glass's** press against a Case that changed since the page rendered, or
  whose edit lease ended, is refused with "The Case changed. Reload it and
  retry." It is not an error page.
- A reopen of a spec's estimate after the Case registration changed is refused
  before Glass's is contacted. A corrected mileage, or a corrected plate on a
  placeholder, reopens it.
- An estimate reset with the portal's own Reset Repair Estimate cannot be
  reopened from Pegasus.

## Acceptance evidence

Core and integration evidence covers identity, current authority, account
exclusivity, uncertain writes, callback replay, custody and one import that
lands as the Current spec on the rate card.
Browser evidence covers save-before-launch, refusal without provider work,
waiting in the Glass's window, fresh controls, stale Close and preservation of
edits during return.

The hosted editor must also pass live acceptance on the deployed artifact:
three fresh launches across two vehicle models (cold and warm browser), three
reopens (before and after a save) including reload and host restart,
deliberate estimate changes followed by Save & Exit and an automatic import
that lands as the Current spec, a reopen of the returned spec whose second
Save & Exit updates that spec and leaves one stock vehicle at Glass's, a
second Glass's spec started from **New repair spec**, a reopen by a second
staff member after a take-over, a reopen from an Audit's copy, replay
producing one spec, expired-lease recovery, original-window closure, and a
second Case refused while the account is held. Chrome is primary; Edge also
covers a fresh launch and a reopen. Manual export/import does not satisfy this integration's
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
