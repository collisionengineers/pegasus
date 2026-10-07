# FRD-10: MCP automation and actor boundary

> Owner capabilities: MCP-01 to MCP-04 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- MCP is a controlled entry point for one named Automation Actor. Ordinary
  staff never use it; they use the Web UI.
- Every tool calls the same Core command a staff member would. The Actor has
  its own identity and history and no management powers.
- Lists page by cursor, 50 by default and 100 at most. Documents are checked
  for permission before any content is read.
- A download hands a file back as content the client can show: a photograph
  as an image, a PDF as its page text, a small text file as text. Anything else
  comes back as metadata plus an authenticated URL for the original bytes.
  There are no public links.
- A write presented without a lease token holds the record's edit lease for
  that one command. The explicit lease tools remain for multi-step work.
- The Actor does the casework a staff member does: Unidentified items, Triage
  Cases, received items and their inspection address, direct Case creation,
  Case details and notes, findings, valuation, estimates, the Case's
  lifecycle acts, reports and their approval, report wording, image
  preparation, the Case's documents, AI jobs, mail dismiss and folder moves,
  and Work Centre dismiss. It sends reports to the Principal and e-mail under
  its own `automation.send` scope. It never puts an estimate in use, never
  runs a Glass's session and never touches Glass's credentials.
- A tool counts as delivered only after a real caller has proved success,
  authorisation failure, validation failure and history.

## Purpose

This document owns what automation may do through MCP, how it is identified,
and where its authority stops. Automation lists and resolves Unidentified
items by exact U-reference through the same Core command as staff
([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference)).

## Behaviour

### MCP automation and actor boundary

**One Actor.** MCP is a management and development controlled entry point for
one named, vendor-neutral Automation Actor. Ordinary staff have no MCP access.
The Actor calls only its approved list of ordinary Core actions, with its own
authentication, identity and permanent history. It has no Administrator,
configuration, credential, cloud, release, deletion or other management
authority.

**Casework parity.** Within casework the Actor may do anything a staff member
with `PerformCasework` can, with two exceptions: it never puts an estimate in
use (**Use repair spec**), and it never runs a Glass's session, which signs in
with the staff member's own Glass's account (operator, 7 October 2026;
[ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md)). The
`Manage*` rights stay with staff Administrators. The Case lifecycle, report
and document tools, report approval among them, the queue and job tools,
settling an inspection address, report wording, image preparation and
outward sending are delivered (operator, 7 October 2026). Sending has its own
scope, `automation.send`, so a grant can hold casework without sending.

**Grants.** Each connector grant has its own durable identity. History keeps
the grant identity, the shared client ID and the human approver separately.
An approved scope never grants human staff authority. Code exchange and refresh
keep the grant. A revoked, expired or wrong-audience credential fails before
any tool runs. Production signing and encryption use separate persistent
certificate purposes with a rotation overlap, so a restart or replica change
does not invalidate valid tokens. Missing production keys fail closed.

**Paging.** Lists use stable `(sort value, immutable ID)` continuations,
default 50 and maximum 100. A continuation is bound to the caller and the
filters; a malformed, oversize or foreign cursor fails. Case detail returns
bounded summaries with separate continuations for documents, history and
estimates rather than cutting silently. Case search and the per-Case document,
history and estimate lists use `CursorPage<T>`; protected tokens bind actor,
filters and order.

**Raw estimate import** is one Core command, `IImportRawEstimate`, run after
custody retention. The Case page and MCP both call it; there are no separate
import paths.

**Documents.** Permission and immutable metadata are checked before content
is read. A download (`pegasus_document_download`,
`pegasus_unidentified_source_download`, `pegasus_triage_source_download`)
returns the file as native MCP content, because an MCP client can show an
image block and read a text block but can do nothing with base64 text, and
Claude's hosted connector refuses a binary embedded resource
([ADR-0059](../adr/0059-native-mcp-file-content-and-consolidated-tool-inventory.md)):

- an image (not SVG) as one JPEG image block, EXIF orientation applied, the
  longest edge at most 1568 px, re-encoded at the highest quality that fits
  the byte budget (`maxInlineBytes`, 100 KiB by default, 10 MiB at most);
- a PDF as its page text, one block per page within the same budget as
  characters, naming a page that needs OCR and a page cut at the budget
  (operator, 1 October 2026: PDF pages travel as text, not as images);
- a text file that already fits the budget as its text;
- anything else, or any file over 10 MiB, as metadata only.

Every result carries the file's identity, size, media type, SHA-256 and an
authenticated URL for the original bytes:
`/automation/documents/{occurrence}/versions/{version}` for a Case document
(Documents scope) and `/automation/intake-sources/{receipt}` for a retained
intake source (Intake scope). Each endpoint needs the same bearer audience and
its scope, rechecks Case or receipt authorisation, and answers with the exact
SHA-256 as ETag; the document route supports ranges. A metadata-only answer
fetches zero content bytes. There are no public signed links, no document
export archive, and no fetches of arbitrary URLs.

**Edit leases.** <a id="edit-leases"></a> A write tool presented without an
edit lease token claims the record's lease through the same Core port as
staff, runs its one command, and releases the lease afterwards; while another
editor holds the record, or the version is stale, the claim is refused and
nothing is written (operator, 1 October 2026). `pegasus_edit_begin`,
`pegasus_edit_renew` and `pegasus_edit_end` hold a Case or a Triage Case for
multi-step work, named by `recordKind`, under that record's own scope
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). With `takeOver`,
`pegasus_edit_begin` takes over a lease a staff member holds, as a staff
**Take over** does, and the takeover goes into the record's history. Staff
cannot take over an Automation lease; it lapses within five minutes. An
Automation lease that lapsed, with nobody claiming the record since, carries
on with the same token, as a staff lease does (operator, 7 October 2026).

**Assessment writes.** <a id="assessment-writes"></a>
`pegasus_assessment_update` writes the assessment fields a staff member
records, and so can change or clear, on the Case: the fields a Case section's
editor posts and those a section writes through its own typed member (damage
entries, mileage source, storage per day, recovery charge and report date).
Professional findings are among them (operator, 7 October 2026): the
outcome, roadworthiness (`assessment.legal_status`), the unroadworthy reason,
the salvage category and value, and the Retail, Trade and Engineer's values
([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction)).
As on the Case, a positive agreed contract sum makes the outcome a contract
repair. Any other field is refused and named, so every Automation value is
one staff can change or clear on its section (operator, 24 September 2026).
A value it writes is the Case's value, shown with its AI source tag until
staff change it; there is no per-field review (operator, 25 September 2026).
A system fill never overwrites it, as it never overwrites a staff value.
Case-owned facts, fields derived from damage entries and the facts the
DVLA/DVSA lookup alone records (engine, fuel, colour, tax and MOT expiry) are
refused. `pegasus_vocabulary_get` lists every assessment field path with its
type, accepted codes, staff label and whether the tool may write it, with the
estimate line types and evidence labels, the repairer VAT statuses and VAT
categories, the valuation sources and the shared image tags.

**Valuation.** `pegasus_valuation_list` returns the Case's valuation cards:
each guide card, the Engineer's Value card and any AI market research card.
`pegasus_valuation_save` records guide cards and, optionally, adopts the
valuation calculation against a basis card, as the Valuation section's Save
does; a card for the same source and guide month replaces the earlier one.

**Case details and notes.** Case facts, including the Inspection date the
report prints as the date the damage was assessed, change through
`pegasus_case_update_details`, which calls the staff Case save. An omitted
value is unchanged, an empty string clears a text or date value, and a value
the call does not name never changes. It edits the claimant, claim, contact,
accident, VAT status and repairer; the Principal, Claim source and Client
notes; the due by date and the Claim source contact; the vehicle's identity,
year and mileage; the inspection; and the Sign-off Engineer. As on the Case
page, it links the repairer to an active directory Repairer and records the
Claim source from the active Claim source records, each copied onto the Case
so a later directory edit never rewrites it; `pegasus_directory_search`
lists them.
`pegasus_assessment_get` returns Case facts under `caseOwned`. The Case's
Received date, which is its instruction date, is read-only:
`caseOwned.receivedDate` and the Case summary's `receivedAtUtc` carry it, and
no tool accepts an instruction date. `pegasus_case_note_add` adds an
append-only note to the Case timeline, attributed to the Actor, as staff add
one; it changes no Case value and needs no version or lease.

**Case lifecycle.** <a id="case-lifecycle"></a> `pegasus_case_action` takes
one Actions-menu act through the same Core command as the staff Case page:
place on Hold, with an optional review date, and release it; return to
Review; assign the Engineer; return to Engineer; close with a named outcome;
complete; reopen to a named destination; archive; create the linked
replacement of a Case created in error; Create audit; request the vehicle
lookup; and record a manual chase. Each needs the reason staff give for it.
Core alone decides which act the Case's state allows
([FRD-13](frd-13-case-lifecycle-and-workflow.md)). A Case is never deleted.

**Reports.** <a id="reports"></a> `pegasus_report_list` returns a work's
report generations with their artifacts, filing state and SHA-256, the
recorded report approval, the linked report-Sent evidence and the retained
Sent evidence available to link. `pegasus_report_action` generates the report,
which goes on to make its separate fee note, and the Repair Spec and images
documents of a confirmed generation; records report approval of one stored
artifact; and links or unlinks report-Sent evidence, each through the staff
command ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point)).
Report approval is by staff or the Automation actor (operator, 7 October
2026). A report that is not ready answers with its readiness reasons and
records nothing. An approval, archive or evidence link the Actor records is
attributed to it and reads back as the Automation actor.

**Document acts.** `pegasus_document_action` tags and untags an image, puts
it in the report or takes it out, marks the Audit's original report, removes
a document occurrence while custody content and history are kept, retries
failed custody, and adds a word to the shared image-tag vocabulary, through
the staff Custody commands.

**Report wording and images.** <a id="report-wording-and-images"></a>
`pegasus_report_wording_get` reads a work's report wording blocks as the
Report section offers them, each with the sentence composed from the Case's
fields; `pegasus_report_wording_save` renames, rewords, moves, takes off or
puts back a block, or adds or deletes a paragraph of the Actor's own, through
the Case save's Report wording section. A block it does not name keeps its
heading, wording, place and presence, and wording equal to the composed
sentence is no change, by the same rule as the Case page's Save
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md)). While a
Case's report cannot yet be projected there are no blocks, and a save is
refused. `pegasus_image_preparation_get` reads each image's place, role,
rotation, crop and page of its own with its preparation version;
`pegasus_image_prepare` crops, rotates, orders and sets a page of its own
through the Case save's Image preparation section, each image guarded by its
preparation version. Whether an image is in the report stays
`pegasus_document_action` `set_in_report`.

Estimates go through the named estimate tools, with the same actor, lease,
version and replay checks as the Case UI
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). Unidentified reason
codes on the wire are the Core list under `unidentified`. No tool exposes
Glass's credentials or runs a Glass's session.

**Sending.** <a id="sending"></a> The Actor sends as staff do, under
`automation.send` (operator, 7 October 2026;
[ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md)). EVA
submission is not part of it.

- `pegasus_report_send` is the Case page's **Send report**, through the same
  Core command ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md)):
  the work's current confirmed generation, the reviewed To and Cc (omitted,
  the Principal's suggested recipients), the companion documents chosen
  (omitted, every confirmed document the generation holds, the fee note
  included) and the covering message as written. It holds the Case's lease
  for its one send unless a lease token is presented.
- `pegasus_mail_send` is the staff mail send with a `mode`: `new` is the
  Inbox composer from the default approved mailbox (`chaser` records it as
  the Case's chaser); `reply`, `reply_all` and `forward` answer one retained
  message from the approved mailbox that holds it, with the recipients the
  message page gives them; `triage_reply` is the Triage Case's reply to its
  origin e-mail, the chaser until the outcome is recorded and the outcome
  reply once Completed ([FRD-03](frd-03-triage.md)). Every mode files the
  mail against a Case and checks the version the caller observed.
  Attachments are Case files by document version, or the origin item's files
  by asset id for `triage_reply`.

Both are the one staff send operation of
[FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence):
the same approved mailbox, operation-key replay, Submitted-is-not-Sent rule
and Sent evidence. The operation, its history and the Case's Notes line
record the Automation Actor as the sender, never a member of staff. The
Actor's current-sender check is its client registration's kill switch, read
at each step a staff send re-reads the staff account, so a send in flight
when the client is stopped fails as `staff_send_authorization_lost`.

**Network-drive scanning.** An externally scheduled client may scan an
approved network-drive scope and submit immutable source occurrences through
the approved MCP document actions. Claude Desktop may supply the first
accepted client evidence without owning the Actor identity or the Core
action. The client, its schedule and the filesystem stay outside Pegasus.
Custody starts only with an authenticated accepted MCP submission. Each
occurrence follows the ordinary source-occurrence, idempotency, matching,
classification and history rules. Scanning never associates material and
never allocates a Case or reference.

**Proof.** Registration, a tool schema or an endpoint file proves nothing.
Each tool tranche needs a real caller exercised for success, authorisation
failure, validation failure, and its history line.

**Background automation** follows the same rule. Queues and timers carry
stable work identities; Core owns transitions and idempotency
([FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels)). Poison
work stays recoverable and visible. No AI proposal or workspace service can
change Case state directly.

### Triage automation contract

Triage is ordinary `PerformCasework`. The Actor may list and inspect a
Triage, retrieve its retained origin source, mark it Awaiting information,
record or supersede a finding, link or unlink exact response evidence,
complete, cancel or reopen it, and link or unlink a Case under the normal
Case edit lease and version guards. The tools identify a Triage by its Case
id (`caseId`) and return its `t.` Case/PO. Each action calls the same Core
query or command staff use, supplies the resolved Automation identity rather
than caller-provided actor data, and keeps Triage distinct from Unidentified.

`pegasus_triage_complete` and `pegasus_triage_await_information` take no
`reason`: completion and Awaiting information write their own history text,
as they do for staff. Findings, response evidence, cancel, reopen and Case
links keep their reasons. `pegasus_triage_record_finding` supersedes an
earlier finding when it names `supersedesFindingId`;
`pegasus_triage_response_evidence` and `pegasus_triage_case_link` take an
`action` of Link or Unlink. Each change holds the Triage for its one command,
as a staff change does; `pegasus_edit_begin` with `recordKind` Triage lets a
session hold it for a multi-step change, and staff actions on it are refused
while it does.

Assignment names a selected staff assignee, separate from the acting principal.
`pegasus_triage_assign` takes an `action` of Assign, naming an enabled member
of staff from the roster the Triage page offers, or Unassign; neither takes a
reason. `pegasus_triage_note_add` appends a note to the Triage history, as
staff add one. Both hold the Triage for their one command, as the other
Triage changes do.

### Queue and intake tools

The queue acts a staff member performs on received items, Unidentified
items, mail and the Work Centre (operator, 7 October 2026;
[ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md)). Each calls
the same Core command as the staff page, records the Actor's own history and
replays by its operation key.

- `pegasus_intake_get` reads one received item as the Create case page
  reviews it: version, decision, existing Case, classified case type, draft,
  missing identity-critical fields, allocation attempt, the inspection
  address state, suggestion and fingerprint, and who settled it, and the
  item's files, which a Triage reply attaches by asset id.
- `pegasus_intake_action` with `resolve_inspection_address` settles the
  inspection address as **Create case** does: it accepts the suggested
  address, corrects it, or, where nothing was suggested, supplies one
  ([FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address)).
  Accepting or correcting names the suggestion's fingerprint. The settlement
  records the Actor as its settler, by actor kind and identity, and the
  Case's address carries an Automation label, never a staff one.
- With `accept` it turns a received item into a Case as **Create case**
  does: the reviewed draft is recorded (named draft fields replace the
  item's), then the Case is allocated for the Principal and case type.
  `accept` is refused while the address is unsettled; a Principal that
  inspects by images needs no settling. An Audit is accepted only for an
  item classified as one. With `allocate` it retries a failed allocation with
  a reason, naming the attempt, as the Intake log's **Retry allocation** does.
- `pegasus_case_create` creates a Case directly, as staff **Add case** does
  ([FRD-02](frd-02-intake-and-source-identity.md#ways-intake-starts)): an
  Inspection or Inspection and Audit Case with its identity-critical facts,
  or a Triage Case from its Principal and registration. An Audit is never
  created by hand.
- `pegasus_unidentified_resolve` with `targetKind` Closed is **Close with
  reason**: it names no destination. `pegasus_unidentified_reopen` withdraws
  a resolution or closure with a reason.
- `pegasus_mail_action` with `move_folder` confirms the move to the folder
  the classification recommends, with the classification, recommendation
  and mailbox versions `pegasus_mail_get` returns; `dismiss` and `restore`
  change the message's Pegasus scope only
  ([FRD-20](frd-20-mailbox-workspace.md#dismiss)).
- `pegasus_work_centre_dismiss` dismisses a Case, Unidentified item or AI
  job from the Work Centre for everyone, as a row's **Dismiss** does
  ([FRD-15](frd-15-work-centre-queues-and-search.md#work-centre)).

### AI job and estimate tools

The Actor's inventory includes the AI job ledger tools from
[ADR-0035](../adr/0035-ai-job-ledger.md). Job kinds and states are owned by
[FRD-27 AI Job List](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list).
Every tool calls the same Core command as the staff application, supplies the
resolved Automation identity and the connecting client's name, needs an
operation key and expected version on every change, and records permanent
history. The ADR-0031 kill switch and attribution rules still apply: a
stopped automation client is refused before any tool runs.

| Tool | Scope | Action |
| --- | --- | --- |
| `pegasus_ai_job_list` | `automation.jobs` | List jobs by state and kind; a client sees every queued job and its own taken jobs |
| `pegasus_ai_job_create` | `automation.jobs` | Create a job of any kind with the subject the staff action names: Estimate (a Case, optional direction and target), MarketResearch (a Case and guide month; a pending job for the Case is returned instead), QueryResponse (a retained post-report query linked to its Case), UnidentifiedResolution (an open U-reference) or UnidentifiedQueuePass (an instruction). Jobs are how external agents on automated runs hand work to each other (operator, 7 October 2026) |
| `pegasus_ai_job_transition` | `automation.jobs` | One tool with an `action`: Take claims a queued job under a bounded lease held by the client's name (refused when the job is not queued or the kill switch is on); Progress renews the lease and records a short note (refused after cancellation, lease expiry or while the kill switch is on); Complete marks a non-MarketResearch job `Draft ready`, naming its result kind; Fail marks it `Failed` with a reason; Release returns a taken job to `Queued` before the lease ends. Those five act only on the client's own job. Cancel stops any queued, taken or `Draft ready` job with a reason, as an Administrator's Stop does; Confirm marks a `Draft ready` QueryResponse or UnidentifiedQueuePass `Completed`, as the Work Centre's **Complete job** does |
| `pegasus_ai_job_complete_market_research` | `automation.jobs` | File one findings document and one AI market research card and mark the client's MarketResearch job `Draft ready`. It takes no Case edit lease and no Case version: a source card is not a Case field edit, and the Engineer who asked is usually still editing, so it never waits on or ends their session. The Case history records the attachment at the Case's current version. Refused for an archived or completed Case (operator, 28 September 2026) |
| `pegasus_estimate_save` | `automation.assessment` | Create an estimate on a Case, which lands as an AI-draft `Draft`, or edit any live estimate in place, the Current one included; citing the Estimate job it fulfils is optional |
| `pegasus_estimate_list` | `automation.assessment` | List a Case's estimates with their state and source |
| `pegasus_estimate_get` | `automation.assessment` | One estimate in full: its header, every line with its `lineId` and evidence, its totals and the snapshots it can be restored to |
| `pegasus_estimate_act` | `automation.assessment` | One Repair Spec act with an `action`: duplicate or discard (each with a reason; the estimate in use cannot be discarded), scale to a percentage of the Engineer's Value, remove_scaling, or restore a snapshot |
| `pegasus_estimate_import` | `automation.assessment` | Import one retained raw estimate through the canonical Core command using its name, Case and document occurrence/version identities, SHA-256, typed actor, expected Case version, edit lease and operation key; return the estimate identity or the same structured refusal as the Case caller |

`pegasus_estimate_import` and **Import estimate** on the Repair Spec section
are two callers of one Core command. Both use the same parser types, the same
fail-closed provider detection, the same provider-plus-sequence naming, the
same labour-rate card and the same replay rule. The caller does not choose a trusted provider
route. Even a source-hash replay needs the current actor, version and lease
authority and the exact retained source tuple. An unsupported estimate
document is refused without OCR or partial rows. A staff Import on the Case
is the Current repair spec at once; the MCP import runs as the Automation
actor, so it stays a Draft until a staff member uses it. These contracts do
not prove live provider acceptance.

**Estimate saves.** `pegasus_estimate_save` saves through the same Core
command as the Case's Repair Spec editor (operator, 7 October 2026). A new
estimate lands as an AI-draft `Draft`. An edit changes any live estimate in
place; editing the Current one makes a generated report stale. Each kept line
sends its `lineId` so it keeps its source evidence. The header carries the
labour rates, the repairer's VAT status, the VAT categories, discounts and
regional uplift; a header value the call omits keeps its recorded value. An
`Unknown` VAT status, the default, charges no VAT until the status is known.
A line marked To be confirmed that carries a price is saved priced: the
price wins, as on the Case page. Each line carries one evidence label, and
its source text goes in its justification:

| Label | Meaning |
| --- | --- |
| `official` | Manufacturer or official repair or price data |
| `reference` | A published reference or guide (Glass's or Audatex times, ABP) |
| `case` | Evidence on this Case: photographs, documents, the repairer's estimate |
| `judgement` | The assessor's professional judgement |

Only a staff member puts an estimate in use, with **Use repair spec**; no tool
makes an estimate Current. An AI job is how an external client picks up
work, not the authority to save, so citing one is optional.

**Scopes.** `automation.jobs` is its own scope with a consent description on
the Administrator consent page; a token without it cannot see the ledger.
`automation.send` is its own scope for the same reason: a token without it
sends nothing, whatever casework it may do. The estimate tools stay under `automation.assessment` because they write
assessment values. `pegasus_estimate_import` names a retained PDF, XML or
JSON file for shared extraction. Every scope has a consent
description on the Administrator consent page. Every tool is proven under the
tranche rule above.

### Tool inventory

The Actor's whole inventory, 61 tools, by scope. "One-command lease" means
the tool takes `expectedVersion` and `operationKey`, accepts an
`editLeaseToken` from `pegasus_edit_begin`, and holds the record's lease for
its one command when none is given.

| Scope | Tool | Action | Lease |
| --- | --- | --- | --- |
| `automation.cases` | `pegasus_case_search` | Search Cases by text, reference, registration, claimant, claim number, Principal, state; cursor page | none |
| `automation.cases` | `pegasus_case_get` | One Case with its paged documents (occurrence and version ids for download) and history | none |
| `automation.cases` | `pegasus_case_update_details` | Case-detail edit through the staff Case save; an omitted value is unchanged, an empty string clears | one-command lease |
| `automation.cases` | `pegasus_case_note_add` | Add an append-only note to the Case timeline | key only |
| `automation.cases` | `pegasus_case_action` | One Case lifecycle act with an `action`: hold, release_hold, return_to_review, assign_engineer, return_to_engineer, close, complete, reopen, archive, create_linked_replacement, create_audit, vehicle_lookup, manual_chase | one-command lease |
| `automation.cases` | `pegasus_directory_search` | The active directory Repairers or Claim sources a Case links to | none |
| `automation.cases` | `pegasus_vocabulary_get` | The vocabularies the write tools accept: assessment field paths, codes, labels and writability; estimate line types and evidence labels; VAT statuses and categories; valuation sources; image tags | none |
| `automation.cases` | `pegasus_work_centre_dismiss` | Dismiss a Case, Unidentified item or AI job from the Work Centre | key only |
| `automation.cases` / `automation.intake` | `pegasus_edit_begin`, `pegasus_edit_renew`, `pegasus_edit_end` | Hold a Case (`automation.cases`) or a Triage Case (`automation.intake`) for multi-step work; `takeOver` takes a lease a staff member holds | explicit lease |
| `automation.intake` | `pegasus_intake_queue_list` | List intake receipts by decision and allocation | none |
| `automation.intake` | `pegasus_intake_submit` | Submit one immutable source on the automation channel | none |
| `automation.intake` | `pegasus_intake_get` | One received item as the Create case page reviews it | none |
| `automation.intake` | `pegasus_intake_action` | `resolve_inspection_address` (accept, correct or supply it), `accept` a received item as a Case, or `allocate` (retry) a failed allocation | item version and key |
| `automation.intake` | `pegasus_case_create` | Create a Case or Triage Case directly | key only |
| `automation.intake` | `pegasus_unidentified_list`, `pegasus_unidentified_get` | The open Unidentified queue; one item by U-reference with its sources and history | none |
| `automation.intake` | `pegasus_unidentified_source_download` | A retained source as native content | none |
| `automation.intake` | `pegasus_unidentified_resolve` | Resolve an item to a destination, or Close with reason | version and key |
| `automation.intake` | `pegasus_unidentified_reopen` | Reopen a resolved or closed item with a reason | version and key |
| `automation.intake` | `pegasus_triage_list`, `pegasus_triage_get` | Triage records; one Triage with findings, evidence, candidates and history | none |
| `automation.intake` | `pegasus_triage_source_download` | The Triage's retained origin source as native content | none |
| `automation.intake` | `pegasus_triage_await_information`, `pegasus_triage_record_finding`, `pegasus_triage_response_evidence`, `pegasus_triage_complete`, `pegasus_triage_cancel`, `pegasus_triage_reopen` | The Triage contract above | one-command lease |
| `automation.intake` | `pegasus_triage_case_link` | Link or unlink the Triage and an instruction Case; both records' versions and leases | one-command lease on each record |
| `automation.intake` | `pegasus_triage_assign`, `pegasus_triage_note_add` | Assign or unassign the Triage; add a Triage note | one-command lease |
| `automation.documents` | `pegasus_document_add` | Retain one document in Case custody, Automation-sourced | one-command lease |
| `automation.documents` | `pegasus_document_download` | One exact document version as native content | none |
| `automation.documents` | `pegasus_document_action` | One document act with an `action`: tag, untag, set_in_report, mark_original_report, remove, retry_custody; create_image_tag changes no Case and takes no version | one-command lease |
| `automation.documents` | `pegasus_report_list` | A work's report generations and artifacts, its report approval and its report-Sent evidence | none |
| `automation.documents` | `pegasus_report_action` | Generate the report, Repair Spec or images; record report approval; link or unlink report-Sent evidence | one-command lease |
| `automation.documents` | `pegasus_report_wording_get`, `pegasus_image_preparation_get` | A work's report wording blocks with their composed sentences; each image's report place, role, rotation, crop, page of its own and preparation version | none |
| `automation.documents` | `pegasus_report_wording_save`, `pegasus_image_prepare` | Change wording blocks through the Case save; crop, rotate, order and set a page of its own for images through the Case save | one-command lease |
| `automation.assessment` | `pegasus_assessment_get`, `pegasus_estimate_list`, `pegasus_estimate_get`, `pegasus_valuation_list` | The recorded assessment surface; a Case's estimate headers; one estimate in full; the valuation cards | none |
| `automation.assessment` | `pegasus_assessment_update`, `pegasus_valuation_save`, `pegasus_estimate_save`, `pegasus_estimate_act`, `pegasus_estimate_import` | The assessment, valuation and estimate writes above | one-command lease |
| `automation.mail` | `pegasus_mail_list`, `pegasus_mail_get` | The retained mail workspace; one message with classification and history | none |
| `automation.mail` | `pegasus_mail_correct_classification` | Correct a classification through the staff command | version and key |
| `automation.mail` | `pegasus_mail_action` | `move_folder` to the recommended Outlook folder; `dismiss` or `restore` in Pegasus | versions and key for a move; key for dismiss and restore |
| `automation.jobs` | `pegasus_ai_job_list`, `pegasus_ai_job_create`, `pegasus_ai_job_transition`, `pegasus_ai_job_complete_market_research` | The AI job ledger above | job version and key |
| `automation.send` | `pegasus_report_send` | Send a generated report to the Principal, as **Send report** does | one-command lease |
| `automation.send` | `pegasus_mail_send` | Send e-mail with a `mode`: `new` (or a chaser), `reply`, `reply_all`, `forward`, `triage_reply` | Case or Triage version and key |

## States and transitions

The Actor changes no state of its own. Each tool moves the record it acts on
through that record's owner: Unidentified and intake in FRD-02, Triage in
FRD-03, Cases in FRD-13, AI jobs in FRD-11.

## Edge cases and fail-closed behaviour

- A revoked, expired or wrong-scope token fails before the tool runs.
- A foreign or oversize cursor or document request gets a non-disclosing
  failure.
- A download whose file cannot be rendered inline (not an image, PDF or
  text file; over 10 MiB; an image no quality fits in the budget; bytes that
  are not a readable PDF) returns metadata and the authenticated content URL,
  never a fabricated rendering.
- A write without a lease token is refused, writing nothing, while another
  editor holds the record or the version is stale, exactly as its claim would
  be.
- A stopped automation client is refused by the kill switch.
- A generic assessment update that names a Case-owned or derived field, a
  fact only the vehicle lookup records, or a field no Case section records is
  refused, naming the field, and writes nothing.
- No tool puts an estimate in use or runs a Glass's session.
- A lifecycle, report or document act the Case's state does not allow is
  refused with Core's reason and writes nothing; an act missing an input it
  needs is refused before any lease is claimed.
- A report that is not ready is not generated: the answer names each
  readiness reason and nothing is recorded.
- Recording report approval sends nothing and claims nothing was sent.
- A send tool without `automation.send` is refused before it runs. A send is
  Submitted, not Sent, until its Sent item is observed; an Unknown send is
  never resent, its operation key is replayed.
- A reply or reply all takes no recipients of its own; a new mail or forward
  without a To recipient, or with an address that is not a plain e-mail
  address, is refused and nothing is sent.
- Mail is filed against an instruction Case; a Triage Case is answered only
  with `triage_reply`, and a cancelled Triage takes no reply.
- `pegasus_intake_action accept` is refused while the item's inspection
  address is unsettled; `resolve_inspection_address` settles it first. A
  stale item version or suggestion fingerprint is refused and writes nothing.
- `pegasus_report_wording_save` is refused while the Case's report cannot
  be projected, since there is no composed sentence to compare with.
- Confirm is refused for a job that is not a `Draft ready` QueryResponse or
  UnidentifiedQueuePass; those others complete through their record's own
  act.
- A subject field a job kind does not take is refused, not ignored.
- Staff cannot take over an Automation lease; it lapses within five minutes.
- Missing production signing or encryption keys fail closed.

## Acceptance evidence

Each tool tranche is proven with a real caller: success, authorisation
failure, validation failure and the resulting history line. Integration
tests cover the automation ingress over real HTTP. Deployment and live
acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MCP-01`–`MCP-04` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-03](frd-03-triage.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).
- Technical constraints:
  [ADR-0011](../adr/0011-restrict-mcp-to-automation-actor.md),
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md),
  [ADR-0035](../adr/0035-ai-job-ledger.md),
  [ADR-0059](../adr/0059-native-mcp-file-content-and-consolidated-tool-inventory.md),
  [ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md).
