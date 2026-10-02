# FRD-11: Reports and correspondence

> Owner capabilities: CASE-23, CASE-31, EXT-08, EXT-11, RPT-01 to RPT-07 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Core picks one of four report outcomes from the Engineer's accepted
  finding and computes every figure once. A renderer never chooses or
  reinterprets an outcome.
- An Audit report uses the same template and layout as an Inspection report.
  Only its reference and provenance differ.
- Generating a report is not approving, sending or receiving it. Each is a
  separate recorded event.
- An issued report is immutable. A correction is a new version; the old one
  is kept.
- Report sent starts post-report work. It does not close the Case.

## Purpose

This document owns how reports are produced and corrected. Targeted sending,
reviewed AI proposals and the AI Job List are in
[FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md). Reports come from accepted Case facts and
source-labelled evidence through the integrated renderer
([ADR-0025](../adr/0025-integrate-renderer-and-extractor-into-the-application.md),
[ADR-0050](../adr/0050-questpdf-report-renderer.md)). Infrastructure owns
the renderer source; the retired imports are not callers.

## Behaviour

### Assessment-report outcomes

There are exactly four outcomes: `total_loss`, `repairable`, `cash_in_lieu`
and `contract_repair`. Contract repair is its own outcome, not a variant of
repairable. Every outcome uses the same bundle: outcome and findings, vehicle
data and the repair-cost calculation, the itemised repair specification, the
marked damage diagram
([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#damage-record)),
selected vehicle images, the statement, the sign-off Engineer tuple, and the
fee note.

| Outcome | Title and badge | Headline figures | Settlement meaning | Value box label |
| --- | --- | --- | --- | --- |
| `total_loss` | `TOTAL LOSS REPORT`; `TOTAL LOSS — CATEGORY x`, or `TOTAL LOSS` for Category N/A | Pre-accident value, repair cost including VAT, salvage value, recommended settlement | Recommended settlement is the accepted Engineer value less the accepted salvage value. The accepted category and salvage value are required. The report prints the recorded category's accepted wording (operator, 26 September 2026). Category N/A prints no category and no salvage paragraph. | `Recommended equitable settlement (pre-accident value less salvage)` |
| `repairable` | `REPAIRABLE REPORT`; `REPAIRABLE` | Pre-accident value, labour hours, repair cost including VAT | Recommended settlement is the calculated repair cost for the Engineer's repairable finding. | `Recommended settlement (calculated repair cost)` |
| `cash_in_lieu` | `CASH IN LIEU REPORT`; `CASH IN LIEU` | Pre-accident value, labour hours, cash-in-lieu settlement | The recommended cash-in-lieu settlement is the calculated repair cost. | `Recommended cash in lieu settlement (estimated repair cost)` |
| `contract_repair` | `CONTRACT REPAIR REPORT`; `CONTRACT REPAIR` | Pre-accident value, labour hours, repair cost including VAT | The agreed contract sum the Engineer recorded (v28 P35) is the contract-repair cap and cannot increase; the report prints it beside the Core-computed VAT-inclusive repair total. | `Agreed contract repair (including VAT)` |

Labour hours on the report are panel hours and paint hours together. The
headline figure and the Labour Hours row of the repair cost table print the
same number, and the Case page shows it too. The value box under the
settlement sentence carries the template's full label (operator, 27 September
2026).

Core selects the outcome from the accepted Engineer finding and computes
each figure once from accepted, source-labelled inputs. A caller or renderer
cannot pick an outcome, supply a ready-made settlement, or read one outcome
as another. Missing, unknown, conflicting or incomplete outcome data fails
before any report is rendered. Data that affects the document is required:
category and salvage for total loss, and the raw cost components that Core
uses for the contract-repair cap.

The operator has supplied the report and correspondence templates. Pegasus
uses those. Supplied templates, schemas, wording, designs and samples are
evidence for this contract, not a second rule owner. Any wording that has not
been accepted, such as a category treatment, storage paragraph,
qualification or signature, is unavailable. The renderer never fills a gap
with a placeholder or a guess.

The statement of truth is the accepted wording `Pegasus.Core` owns, printed
in its accepted order. Its Glass's guide sentence prints only when Disclose
guide source is on and a Glass's valuation guide was used; no sentence stands
in for another guide. No Case edits the statement: its only per-Case inputs
are that switch and the Sign-off Engineer who signs below it, and the Case's
Report section shows the same paragraphs read-only
([FRD-16](frd-16-case-record-workspace.md#report)). The Vehicle Details table
prints no VIN-checked or fault-code row (operator, 24 September 2026).

The report prints only what the template prints (operator, 27 September
2026). Vehicle Details holds the template's eight facts: make, registration,
model, VIN, odometer, engine and fuel, pre-incident condition and impact
magnitude. The Settlement section holds the settlement sentence and its value
box. These facts stay recorded on the Case and are not printed:

- tyres, seat belts and airbags;
- the unrelated-damage deduction and paint or material transfer;
- temporary repairs;
- the vehicle's transmission, colour, body, tax expiry and MOT expiry;
- the settlement facts beyond the outcome's own figures, such as excess,
  betterment, reserve, equity, delays, storage, recovery, hire, diminution
  and salvage logistics.

Changing one of them does not make a generated report stale.

### Report wording blocks

The report's narrative is composed from the Engineer's wording blocks, in
their order (v28 P30). The blocks are Nature of Incident, Engineer's
Comments, Supplementary damage, PAV commentary, Unrelated damage, Vehicle
History Check, Pre-Incident Condition, Settlement and Salvage, and that is
their order until the Engineer moves one. Each prints under that title until
the Engineer renames it; on a contract repair the Settlement block is titled
Contract Repair. A paragraph the Engineer adds is titled New paragraph until
the Engineer names it (operator, 27 September 2026).

The report prints no Damage table and no Tyres table. The marked damage
diagram prints on page 1, beside the Close-up image (operator, 27 September
2026). It is the Case page's plan, turned on its side with the front pointing
left. When no damage names a plan area it prints with no marks.

The Damage section's Incident narrative reads the Nature of Incident block
from the same owner: the Engineer's wording when written, else the composed
sentence. It is available once the Engineer has written that wording or the
damage record has a headline impact, in every lifecycle state and before a
report can be projected, whether or not the report carries the block; Damage
keeps no narrative of its own (operator, 24 September 2026).

A block tracks the Case's own facts until the Engineer writes wording in its
place. The Engineer may rename a block, move it, take it off the report, put
it back, and add a paragraph of their own. Wording that reads the same as the
composed sentence is no change, so the block keeps tracking its fields. The
Engineer's changes are held per Case, separately for the Inspection and the
Audit once an Audit exists, and written by the one Case save.

Report wording comes from two sources only (operator, 27 September 2026):
the supplied template, `reference/rendererref1`, and the old manager's case
page,
`design/planning-and-old-designs/v27_planning/pegasus_case_dashboard_2026-09-15.html`.
Every heading, label and composed sentence uses one source's own words.
Wording found in neither does not print. The one ruled exception is the
`TOTAL LOSS` badge of a total loss with no category (operator, 26 September
2026). What the Engineer writes in a block is the Engineer's own.

The composed sentences remain the accepted report wording and nothing else:
the mileage statement by its recorded source, the salvage paragraph by the
recorded category, the settlement paragraph by the recorded outcome. A
recorded value the wording does not cover, such as an unrecognised mileage
source, fails before rendering rather than printing around it. The
Supplementary damage block is the Current repair specification's own
statement, and the Vehicle History Check block is the recorded check
verbatim.

- **Small letters inside a sentence.** Severity, location and condition print
  in small letters: "The vehicle has suffered moderate collision/impact
  damage to the right rear." and "The vehicle is considered to be in below
  average condition for its age and type."
- **Damage to several areas.** The sentence ends "to the following areas:"
  and the areas follow, one to a line, in the order they were recorded.
- **Mileage.** The online data sentence is the template's. The sentences for
  owner, repairer, principal, average and a mileage still to be confirmed are
  the old manager's case page's, for example "The mileage was advised by the
  owner."
- **Salvage.** The Category S paragraph is the template's. The Category A, B
  and N paragraphs are the old manager's case page's, exactly as it has them.
  Category N/A prints no salvage paragraph.

A generation freezes the Engineer's changes with the rest of its facts, so an
issued report renders the same way again and a later edit changes nothing
already issued. A block the Engineer never wrote composes from the facts that
generation froze, so the narrative can never contradict the figures printed
beside it. A generation frozen before the blocks existed holds no changes and
composes every block.

### Audit report parity

Audit and Inspection + Audit are active. An Audit report uses the same
approved contract, template, wording, layout and presentation as the
matching Inspection report. What differs is the Case's provenance and its
reference:

- A standalone Audit Case's Case/PO is the `a.` reference itself, for
  example `a.QDOS26002`.
- For Inspection + Audit, the Inspection report carries the Case/PO
  `QDOS26001` and the Audit report, once Create audit has run, the Audit
  reference `a.QDOS26001`.

The assessment outcome never changes the reference. The assessment is a
separate Case fact
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
Those identity facts travel through the shared Core report contract; they do
not create a separate report family.

**A report per work.** An Inspection + Audit Case produces the Inspection
report from the Inspection's values and, after Create audit, the Audit report
from the Audit's values, on the same Case. Each report is generated,
approved and sent on its own: current, superseded and stale are decided for
each separately, and an Audit edit never makes the Inspection report stale.
The Audit report's reference is its Our Ref, its file name (for example
`A_QDOS26001_assessment.pdf`) and its email subject, and its re-sends are
counted among Audit sends only. It has its own fee note and fee, counted
separately. Report image choices are shared, because they belong to the
Case's files. Once the Audit exists, an Inspection report already sent can be
opened and downloaded but never generated again or sent again. An Inspection
report not yet sent at Create audit is still generated, prepared, sent and
marked sent from the Inspection view, on the Inspection's own work and
without changing the Case's state, which is the Audit's (operator,
1 October 2026); a delivery prepared for it before Create audit is refused at
send, because Create audit changed the Case. The Audit report never
overwrites or reissues the Inspection report.

Audit outcome or reference evidence that is missing, conflicting, ambiguous,
stale or from another Case fails before rendering. Audit adds no second
template, wording, layout, report model, conservative/maximised pair, or
money or percentage uplift.

### Initial renderer activation

The active renderer uses the `rendererref1` assessment and fee note for
Inspection, Audit and Inspection + Audit. Diminution, addendum,
valuation-evidence and generic-letter families are not activated. There is
no caller-selectable template or density. Core takes an immutable,
source-labelled snapshot, checks readiness and the sign-off tuple, computes
the figures once, and selects one outcome. Infrastructure renders that
selection with the governed layout, embedded fonts, logo and supplied
signature image.

**Page style.** The renderer follows the template, `reference/rendererref1`
Design I (operator, 27 September 2026). Every page carries the header, which
is the logo and the company block, and the footer. The margins, type and
colours are the template's. The Repair Spec printout takes the same page
style.

**Report For.** The report is addressed to the principal: its name, then its
address line by line, then its postcode. A postcode the address already ends
with is not printed twice. A principal with no address prints its name alone
(operator, 27 September 2026).

**Sign-off on the report.** The snapshot carries the Case's sign-off tuple:
printed name, qualifications and signature image, read from the Sign-off
Engineer account. That profile may belong to any enabled staff role. The name and signature image are required. Qualifications
are optional; without them the name prints alone. Who is offered as Sign-off
Engineer and the default are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#sign-off-engineer). The
account flag and its data are in
[FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts). Typed
Engineer identity alone is never the signatory. Missing or unsupported
signatory content, source version, custody reference or required value
fails. No custom signature path, local attachment path, placeholder or
unaccepted wording is allowed.

**Generation is one step of several.** Generation is deterministic,
versioned, retained and gated on readiness. Generation, approval, issue,
sending, external receipt and Case closure are separate recorded events.
Generation returns draft assessment and fee-note files with bytes, hashes,
page counts, template version and engine version. It is not approval, issue,
sending, receipt, reference allocation or correction custody. A human must
approve before issue.

**What a generation freezes.** The Case version, signatory account and
signature digest, Current estimate identity, version and breakdown, the
Engineer's Value with its retail and trade values, the applied valuation
identity when one exists, content switches, report date or override, the
Engineer's changes to the report's wording — headings, wording, order and
what is off the report — fee, source documents with their Box identities,
and how each image in the report prints, its order, rotation and crop.

### Companion documents and what a delivery attaches

A generation may also hold two companion documents (v28 P22), each generated
on its own request against a generation that is already confirmed, each a
separately addressable artifact in custody:

- the **Repair Spec**, which is the specification document the Case already
  prints, rendered from the estimate that generation pinned. An estimate that
  has moved on since, or one that cannot be printed, fails the artifact closed
  rather than attaching a specification the report never priced;
- the **images**, which are the images the report prints, alone, in the
  report's own grid of six to a page with a Full page image on a page of its
  own, in the report's page style. A Case whose report uses no image has no
  pack to generate.

A delivery attaches the documents the operator chose. Without a choice every
artifact the generation holds attaches and a partly confirmed generation
yields nothing; with a choice, exactly the chosen documents must be present
and confirmed, so a companion still being filed never silently drops out of a
delivery that asked for it and never blocks one that did not. The report is
always attached. The preparation pins each chosen attachment by document,
version, hash and length, and the send re-checks that every pinned attachment
is still a confirmed artifact, byte for byte.

**What a delivery is called (v28 P23).** The attached report is named for the
people who read it — the Case's reference, the vehicle's registration and the
outcome — and one dot is added for each report of this Case already sent, so
a re-issue is distinguishable at a glance. Companion documents keep the names
custody gave them. The delivery carries one covering message, which starts as
the Case report delivery template
([FRD-17](frd-17-administration-workspace.md#e-mail-templates)) rendered for
this Case: a first report has no "supersedes" line; a later one says plainly
that it supersedes the report dated the day the superseded generation carried.
Before Prepare delivery, staff read the message in an editable box and may
change it. Prepare delivery freezes the text they submit, which cannot be blank
or longer than 5000 characters. The report's name and that message are frozen
with the preparation, so what was reviewed is what is sent. Send uses only the
frozen message, and custody keeps its own name for the same bytes. A send is a
staff send that actually left the approved mailbox; a prepared-but-unsent
delivery is not one. The subject stays the Case or Audit reference. A send the
mailbox provider refuses is shown as a failed send; that preparation is spent,
and Prepare delivery is offered again so a fresh preparation can be sent.

**Report and fee note.** They are separately addressable files in custody.
The operator generating the report chooses whether the fee note is a separate
document or the report's final pages, with the Include fee note choice beside
Generate report (issue 912). The snapshot records that choice, so a
combined report is one file under the report's name and reproduces the same
way. A later request for a separate fee note names the current confirmed,
non-stale generation and adds the fee note from that generation's frozen
date and fee facts. It does not re-freeze the report. The request is refused
when there is no current generation, the generation is stale, or the report
already contains its fee note. Fee facts, readiness and accepted fee terms
are the same either way.

**Staleness.** One Core rule over normalised effective values marks a
generation stale only when an accepted report fact changes. Notes, no-op
saves and recipient edits do not. A ready generation records
`case_report_generation_ready` in history. Stale generations cannot be
prepared or sent.

**Views and downloads.** A preview creates no file and no Sent evidence.
Viewing one records `case_report_draft_previewed`, distinct from generation
and download. Reopening a confirmed file's bytes records
`case_report_artifact_downloaded`, at most once per Case or file, staff
member and London day. Opening the stored report in the viewer reads the same
bytes, so it records the same event, and Case history shows it as Report
downloaded. That includes the report opening by itself after Generate report.

**Version checks.** Snapshot assembly reads the Case version first and
refuses a changed version before freezing. Generation and preparation
commands carry the version shown in the browser and refuse a stale one
rather than re-reading. An operation key replays only the same file kind,
packaging choice and target generation; reusing it for a different command
is a conflict. The signatory tuple is rechecked in the freeze transaction.
Confirming or removing source evidence, or changing the signatory's
eligibility, name, qualifications or signature, invalidates affected current
generations in the same transaction. Report outputs are not report inputs
and do not invalidate their own generation. Other retained files stay source
evidence whatever their transport label.

**Recipients.** A delivery preparation needs current addressing. Principal
recipient settings can include the original instruction sender and any
number of extra addresses. The original sender comes from the originating
instruction, never the latest reply; an unresolved sender adds no invented
address. Claim Source is never copied implicitly. Recipients are frozen in
the preparation, but the preparing human staff member may edit To and Cc before that freeze.
Every delivery still needs a staff-controlled send. Default report dates and
displayed times use Europe/London.

### Report generation entry point

The Report section of the Case record offers **Generate report** to every
enabled human staff role whenever the Case is writable and nothing blocks,
in or out of edit mode (operator, 26 September 2026). Outside edit mode the
handler claims the Case's edit lease for the one generation and releases it;
a colleague's live lease refuses it. In edit mode Generate saves the Case
first. It uses the accepted saved facts and the snapshot above, and retains
versioned report and fee-note files, their custody outcome and history. A
generated file is not approval, sending or receipt. Generate for a companion
document of the confirmed generation — the separate fee note, the Repair Spec
and the images — is offered the same way, in or out of edit mode, with the
same one-off lease (operator, 28 September 2026, issue 912).

**After Generate report** (operator, 27 September 2026). A report that was
generated and stored opens in the Case page's document viewer by itself,
once. The Report section then says where the report stands, in plain words:

| Words | Meaning |
| --- | --- |
| Stored | The report's file is in Box. **Open report** opens it in the viewer |
| Storing | The file was drawn and is on its way to Box |
| Storage failed | Storing the file failed |
| Not confirmed | What became of the file is not known |
| Not generated | The report was never drawn |

A report still being filed is a warning, never a confirmation. The notice
reads "The report is still being filed to Box."

**A report filed after its request.** A request can end while its file is
still on its way to Box. Once the file is filed, the Worker records the
report as stored. Nobody presses Generate report again. The Worker leaves
alone a report generated, or a file recorded, in the last three minutes, so
it never competes with the request still producing it.

**Retry.** A retry asks what was filed before it draws anything. It never
draws again a file that is already held. A file that is filed is recorded as
stored. A file still being filed, or one that failed, is recorded as that.

**Failures.** Staff read one plain sentence. It names the document: the
report, the fee note, the Repair Spec or the images.

| What happened | Sentence for the report |
| --- | --- |
| The generation ran out of time | "The report took too long to generate." |
| Box could not be reached or written to | "The report could not be stored in Box just now." |
| The report was refused | The refusal's own reason |
| Storing failed, or any other fault | "The report could not be generated." |

A fee-note preview shows the recorded fee and description without saving
anything. Native Hand to Engineer opens engineering work without an EVA
export; EVA is optional and never gates report readiness.

The report prints its images six to a page, two across and three down, in
the order the Engineer set (operator, 27 September 2026). The Close-up prints
on page 1 only, beside the marked damage diagram, and Full page has no effect
on it. The Overview leads the image pages. An image flagged Full page prints
on a page of its own (v28 P41). An image fills its frame: it is trimmed to
the frame's shape after the Engineer's own crop and rotation. Every image
the Engineer includes prints, whatever their number or source file size,
each as a print-resolution copy; the retained source is unchanged (operator,
24 September 2026). The renderer opens one source image at a time, when it
prints that image, and lets it go before the next, so a long report does not
hold every source at once. A large PNG is read a row at a time and averaged
down, so it is never held whole at full size. An image is never refused for its
size. A render that outlives its caller is told to stop and keeps its place in
the renderer's admission until it has, so abandoned renders cannot add to the
queue (issue 850).

A report generated without an overridden report date is dated the day it was
generated, and that date is written into the Case's own record so the screen
and the document agree (v28 P40). A date already recorded is never
overwritten.

The Repair Spec section offers **Print Repair Spec**, an unretained Estimate
document in the report's page style for any saved estimate version from
`EstimateTotals`. Viewing it records `case_estimate_document_previewed`. It
is not a report, approval, delivery or correspondence.

Settlement and Report editors use the Case's one workspace Save with its
version and edit lease
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)); a save needs no
reason. Report records Engineer comments, agreed fee, description lines, the
report-date override and the **valuation commentary** text, up to 4,000
characters; the Sign-off Engineer is chosen on the Case card in Case details,
and the three content switches under **On the report** in Valuation (v28
P38). With the valuation commentary switch on, the report prints that text if
recorded, otherwise the applied valuation's reason, and readiness accepts
either. The Case save's own message, "Engineer's Value applied.", never
counts as commentary: it does not print and does not satisfy readiness
(operator, 27 September 2026). With the switch off, neither prints. Vehicle
History is edited once, in Vehicle. Values not submitted stay unchanged;
explicit clears and false values count as submitted. Turning off the date
override does not clear an unsubmitted recorded date. Validation and
concurrency refusals keep the current and proposed values for comparison.

Engineer sections stay viewable in other states; edits follow
[FRD-13](frd-13-case-lifecycle-and-workflow.md#actions).

### Report readiness

Readiness names every fact the report prints and cannot print without, and
nothing else (operator, 24 September 2026). Each is one blocker: what is
missing, where it comes from, why, what clears it, and a link to the section
that records it
([FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)). The
Case page and Generate evaluate the whole list before a generation is
recorded. The preview refuses on the same printed facts, sign-off Engineer,
Current repair spec, labour rate and repairer VAT status before anything is
projected; it does not wait for the report images or the valuation
commentary and unrelated damage the On the report switches ask for. A fact
Review already checked is named again only when it is missing and the report
prints it. Missing accepted state is never invented.

Each fact is recorded in one section of the Case record
([FRD-16](frd-16-case-record-workspace.md#case-workspace)):

| Fact | Recorded in |
| --- | --- |
| Claimant name | Claim |
| Claim reference, incident date | Case details |
| A Sign-off Engineer chosen for the Case | Case details |
| The Sign-off Engineer's name and signature on file | Accounts |
| Registration, vehicle type, pre-incident condition, vehicle history check | Vehicle |
| Inspection type; the inspection address for a physical location; the Inspection date, printed as the date the damage was assessed | Inspection details |
| Impact location and severity derived from the damage record ([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#damage-record)); unrelated damage when its switch is on | Damage |
| The Engineer's Value with its retail and trade values ([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)) | Valuation |
| A Current repair spec with lines, a labour rate and a repairer VAT status the report can word ([Estimate VAT on the rendered report](#estimate-vat-on-the-rendered-report)) | Repair Spec |
| Outcome and roadworthiness; the unroadworthy reason; on a total loss the salvage category and salvage value; on a contract repair the agreed contract sum | Decisions |
| Agreed fee; the report date when overridden; valuation commentary when its switch is on | Report |
| An image in the report tagged Close-up and one tagged Overview, matching their confirmed sources | Files |

The Sign-off Engineer blocker has three cases (operator, 26 September
2026). Where accounts are offered and the Case has none chosen, it says
"Choose the Sign-off Engineer on Case details" and links there. Where no
account is eligible, or the chosen account has no name or signature, it says
an Administrator sets a name and signature on the account in Accounts. That
one links to Accounts for an Administrator and to nothing for anyone else,
since no Case section clears it.

The date the report says instructions were received is the Case's Received
date (operator, 24 September 2026); every Case has one, so it is never a
blocker. On an Inspection + Audit Case each report prints its own work's
Inspection date, and changing it makes the current generation stale. A
recorded value is the Case's value whoever recorded it (operator, 25
September 2026): there is no per-field review, so no blocker names a value
because a lookup, an extraction or the Automation actor recorded it. Where a
value came from is its source tag
([FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md#field-provenance-and-value-kinds)),
and Hand to Engineer is the only review
([FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)).
VAT comes only from the
Current repair spec
([Estimate VAT on the rendered report](#estimate-vat-on-the-rendered-report)).
Readiness reads that spec's repairer VAT status. A status of `Unknown` blocks
the report, and so does a hand-picked set of VAT categories the report has no
wording for (operator, 27 September 2026).

### Report correction, finality, and post-report work

An issued report has an immutable file and version identity and hash. A
correction or addendum creates a new reasoned version and keeps every
earlier file, fact, actor, time and source. It never silently overwrites the
issued report. Later report changes follow
[FRD-13](frd-13-case-lifecycle-and-workflow.md#actions) and keep the
correction history.

Report sent is the exact approved-mailbox Sent-item evidence in
[FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence).
Report sent starts post-report work; it does not close the Case.

Post-report queries, disputes, amendment requests and replies stay Case
correspondence with source and reply-chain identity and permanent history.
The Engineer answers them. The Completed → Query → Completed cycle is in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#completed-and-query). A
mailbox adapter calls that shared transition; it never allocates a Case or a
reference.

Requirements:

- deterministic template and payload versioning;
- preserved document and source provenance;
- authorised human review and approval of facts and content before issue,
  with no separate pre-send review gate in the Case lifecycle;
- immutable issued file identity and hash;
- correction or addendum, never silent overwrite;
- exact delivery evidence where the workflow requires it;
- accessible status, validation and failure display that never implies an
  unproved delivery.

### Estimate VAT on the rendered report

Each estimate has its own VAT percentage, default 20, and selected VAT
categories. An `Unknown` repairer VAT status does not block Use repair spec;
the selected VAT categories still govern the calculation.

An `Unknown` status does block the report (operator, 27 September 2026). With
no status recorded, the report would understate the repair cost. The blocker
is named Repairer VAT status. Staff clear it on Repair Spec by choosing
Registered or Not registered. VAT categories nobody chose by hand follow the
status, so choosing one charges what that status charges (operator, 28
September 2026). One edge is accepted: an operator who changes the status and
then ticks the categories back to exactly the saved set is read as having
left them alone, so on Save they follow the new status.

The report words its VAT row as the template does. The percentage shown is
the estimate's own.

| VAT is charged on | VAT row |
| --- | --- |
| Labour, Parts, Materials and Specialist, as for a registered repairer | `VAT (20%)` |
| Parts and Materials only, as for a repairer who is not registered | `VAT (20% — parts & paint only)` |

Any other hand-picked set of VAT categories blocks the report, because no
accepted wording exists for it. Staff clear it on Repair Spec with Reset to
repairer status.

Where the viewer can edit the Repair Spec, both blockers' links in the Next
action's blocker list open it for editing on the Current
spec and put the focus on the control that clears the blocker: Repairer VAT
status for an unknown status, Reset to repairer status for a hand-picked set
(operator, 28 September 2026). Otherwise they jump to the section.

On a rendered report, VAT is `Taxable × VatPercent / 100`, where Taxable is
the selected discounted Labour, Parts, Materials and Specialist categories.
Core computes each printed component on its own. Printed Net is their sum;
printed Gross is printed Net plus printed VAT. No residual penny moves
between components.

| Figure | Rule |
| --- | --- |
| Parts | Explicit part prices × quantity |
| Labour | Panel, paint and Specialist work-unit hours × the selected labour-rate-card rate; hours on a fixed-price Specialist line are kept, shown and not priced |
| Parts, materials and specialist | Explicit estimate amounts, discounted where selected |
| Taxable | Selected discounted Labour, Parts, Materials and Specialist categories |
| VAT | Taxable × VAT % |
| Net / Gross | Sum of independently rounded printed components / Net + printed VAT |

The repair-specification editor may compare two saved specifications line by
line. It does not compute or present a provider-versus-assessed savings claim
as a report outcome. Normalised provider and manual
estimates are editable records; their raw source evidence and hashes are
immutable. Every change follows the same lease, version, attribution, reason
and history rules.

Signatures in rendered documents are provenance-sensitive document assets,
not decorative images. The signatory is the Case's Sign-off Engineer.

## States and transitions

A report generation is `ready`, then `stale` when an accepted fact changes.
An issued report is immutable; a correction is a new version. AI job states
are in
[FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list).
The Case's own states are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels).

## Edge cases and fail-closed behaviour

- Any missing, unknown or conflicting outcome data stops rendering.
- A stale version in the browser is refused, never replaced.
- A stale generation cannot be prepared or sent.
- A fee-note request against a report that already contains one is refused.
- A missing printed fact, such as a total loss with no salvage category, is
  a named readiness item: preview and Generate refuse before any generation
  is recorded, never at render.
- An `Unknown` repairer VAT status, or a hand-picked set of VAT categories
  the report has no wording for, is a named readiness item too.
- A report still being filed never reads as stored, and a retry never draws
  again a file that is already held.
- No generated file, preview, draft or export counts as Report sent.

## Acceptance evidence

Core tests cover outcome selection, the figure calculations and VAT table,
snapshot freezing and staleness, and the fee-note rules. Core tests also
cover that the Case's Incident narrative and Statement of truth read the
owners the report prints. Integration tests cover Generate report, previews
and downloads with their history events.
A conformance test renders the template's four sample jobs and compares each
with the template's PDF: page order, headings, margins, header, footer and
image frames. It checks structure and measurements, not page images.
Deployment and live acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `CASE-23`, `CASE-31`, `EXT-08`, `EXT-11`, `RPT-01`–`RPT-07` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md),
  [FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md),
  [FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md).
- Technical constraints:
  [ADR-0025](../adr/0025-integrate-renderer-and-extractor-into-the-application.md),
  [ADR-0050](../adr/0050-questpdf-report-renderer.md).
