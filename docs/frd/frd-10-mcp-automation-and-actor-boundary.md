# FRD-10: MCP automation and actor boundary

> Owner capabilities: MCP-01 to MCP-05 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- MCP is a controlled entry point for one named Automation Actor. Ordinary
  staff never use it; they use the Web UI.
- Every tool calls the same Core command a staff member would. The Actor has
  its own identity and history and no management powers.
- The tools are served by one MCP server, `pegasus-automation`, at `/mcp`. A
  connector is added once, and the Administrator ticks the scopes its grant
  holds.
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
  preparation, the Case's documents, AI jobs, mail correction, linking mail
  to a Case, dismiss and restore, Work Centre dismiss and photo links. It
  sends reports to the Principal, e-mail and photo links under its own
  `automation.send` scope. It runs Glass's sessions of its own under its
  own `automation.glass` scope, and a finished one lands as a Draft. It
  acts in a staff member's open Glass's session only while that staff
  member has turned help on for it, and never saves or closes that session.
  It never puts an estimate in use and never reads Glass's credentials.

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
authentication, identity and permanent history for casework, lease claims and
releases, takeovers and refusals; routine lease renewal, job polling and
progress heartbeats are telemetry only
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). It has no
Administrator, configuration, credential, cloud, release, deletion or other
management authority. The MCP is on in every deployed estate and is stopped by
the Automation client's Stop switch (R1046).

**Casework parity.** Within casework the Actor may do anything a staff member
with `PerformCasework` can, with one exception: it never puts an estimate in
use (**Use repair spec**) (operator, 7 October 2026;
[ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md)). It runs
Glass's sessions of its own under the Glass's login the deployment holds for
it: the server signs in, the Actor never reads the login, and no staff
member's stored login is read for it (18 November 2026; R1177;
[Glass's sessions](#glasss-sessions)). It acts in a staff member's open
Glass's session only while that staff member has turned help on for it,
never saves or closes it, and never reads the staff member's Glass's login
(18 November 2026; R1170, R1184, R1185). No tool exposes Glass's credentials;
Get valuation uses Pegasus's own valuation account and is not a session. The
`Manage*` rights stay with staff Administrators. Sending a report, an e-mail
or a photo link has its own scope, `automation.send`, so a grant can hold
casework without sending; every other photo link act is `automation.cases`
(R1039, R1040). The Actor's Glass's sessions have their own scope,
`automation.glass`, so a grant can hold casework without acting in Glass's
(R1179).

**Grants.** Each connector grant has its own durable identity. History keeps
the grant identity, the shared client ID and the human approver separately.
The consent page lists each requested scope with a tick box, ticked by
default; the grant holds the scopes left ticked, and approving none is the
refusal the Refuse button records (R548). An approved scope never grants
human staff authority. Code exchange and refresh keep the grant. An expired
or wrong-audience credential fails before any tool runs; an issued token is
not revoked but lapses at the end of its own lifetime, and stopping the
Automation client is the emergency control (R1045). Production signing and
encryption use separate persistent certificate purposes with a rotation
overlap, so a restart or replica change does not invalidate valid tokens.
Missing production keys fail closed.

**Paging.** Lists use stable `(sort value, immutable ID)` continuations,
default 50 and maximum 100. A continuation is bound to the caller, the
filters and the order; a malformed, oversize or foreign cursor fails. Case
detail returns bounded summaries with separate continuations for documents,
history and estimates rather than cutting silently. Case search and the
per-Case document, history and estimate lists use `CursorPage<T>`.

**Documents.** Permission and immutable metadata are checked before content
is read. A download (`pegasus_document_download`,
`pegasus_document_download_batch`, `pegasus_unidentified_source_download`,
`pegasus_triage_source_download`) returns the file as native MCP content
([ADR-0059](../adr/0059-native-mcp-file-content-and-consolidated-tool-inventory.md)):

- an image (not SVG) as one JPEG image block, EXIF orientation applied, the
  longest edge at most 1568 px, re-encoded at the highest quality that fits
  the byte budget (`maxInlineBytes`, 100 KiB by default, 10 MiB at most);
- a PDF as its page text, one block per page within the same budget as
  characters, naming a page that needs OCR and a page cut at the budget
  (the batch withholds whole and the cut stays the single download's: a
  whole file either fits or is withheld with its reason) (operator,
  1 October 2026: PDF pages travel as text, not as images);
- a Word document (.doc or .docx), from `pegasus_document_download`, as the
  page text of the PDF rendering staff view
  ([FRD-05](frd-05-documents-extraction-and-custody.md)), within
  the same budget; a rendering still being made answers "call again", the
  rendering is not a download of the document, and the batch returns a Word
  document's metadata (R1043);
- a text file that already fits the budget as its text;
- anything else, any file over 10 MiB, an image no quality fits in the
  budget, or bytes that are not a readable PDF, as metadata only, never a
  fabricated rendering.

`pegasus_document_download_batch` downloads 1 to 25 exact Case document
versions in one call, failing closed before reading blob bytes if any item is
invalid, duplicated, unconfirmed, or removed (operator, 19 October 2026). It
charges `maxInlineBytes` at the encoded size, items in request order, and
stops at the first item that does not fit whole: one owner
(`AutomationFileContent`) counts what is sent, and a whole file either fits
or is withheld with its reason (operator, 22 October 2026). Only "no quality
fits" stops it at an image; an undecodable or oversized image is withheld
with its reason and the batch continues (operator, 22 October 2026). Every
result carries the file's identity, size, media type, SHA-256 and an
authenticated URL for the original bytes:
`/automation/documents/{occurrence}/versions/{version}` for a Case document
(Documents scope) and `/automation/intake-sources/{receipt}` for a retained
intake source (Intake scope). Each endpoint needs the same bearer audience and
its scope, checks the casework right, and answers with the exact SHA-256 as
ETag. The document route resolves the document inside the Case it names; the
intake-source route rechecks no Case or receipt membership. The document route
supports ranges. A metadata-only answer fetches zero content bytes, except a
PDF, an image or a text file read once to learn that it does not fit whole or
cannot be read. There are no public signed links, no document export archive,
and no fetches of arbitrary URLs.

**Large documents.** A document too large for one tool argument is staged in
parts (operator, 22 October 2026). `pegasus_upload_begin` declares its size,
at most 10 MiB, and its SHA-256 under an operation key and returns an
`uploadId`; the same key with the same declaration returns the same upload
and the parts already received. `pegasus_upload_part` sends one part of
49,152 bytes, the last shorter, with the part's own SHA-256: a damaged part
is refused by its hash before it is stored, and the same bytes sent again
change nothing. `pegasus_document_add` and
`pegasus_ai_job_complete_market_research` take the `uploadId` instead of
`contentBase64`. Only that call writes custody, once the staged document
matches its declared size and SHA-256, and its replay returns the original
record. Staging writes no Case record, and no history row unless it is
refused. An upload lives 24 hours. `pegasus_intake_submit` takes no
`uploadId` (operator, 22 October 2026).

**Edit leases.** <a id="edit-leases"></a> A write tool presented without an
edit lease token claims the record's lease through the same Core port as
staff, runs its one command, and releases the lease afterwards; while another
editor holds the record, or the expected version is newer than the record's,
the claim is refused and nothing is written (operator, 1 October 2026;
R516). `pegasus_edit_begin`, `pegasus_edit_renew` and `pegasus_edit_end`
hold a Case, a Triage Case or an Image Intake record for multi-step work,
named by `recordKind`, under that record's own scope; the lease rules
themselves are [FRD-14](frd-14-record-edit-leases.md#case-edit-lease)'s.
With `takeOver` on a Case, `pegasus_edit_begin` asks the member of staff
holding the lease and waits inside the call, as
[FRD-14](frd-14-record-edit-leases.md#take-over) describes; the Automation
Actor never forces a hand-over (R554). The Glass's help tools take no Case
lease: the Engineer helped keeps it (18 November 2026; R1170). A Case's lease-conflict refusal names
that request, and the tool, its `takeOver` parameter and the server's
instructions describe it as the way to get a Case a member of staff holds
(R901). On a Triage Case or an Image Intake record `takeOver` takes over a
staff scope at once, and the takeover goes into the record's history (R556).
Staff take over an Automation Case lease or Triage scope at once, and only
staff may: the Automation Actor never takes over an Automation holder (R517;
[ADR-0075](../adr/0075-staff-take-over-of-an-automation-lease.md)).

`pegasus_edit_begin` replayed with the same operation key and details
returns the same token while that lease is live, for a Triage Case as for a
Case (R519). A write made under an Automation lease advances the record's
version and keeps the lease, so one token from `pegasus_edit_begin` carries
every write of the work until `pegasus_edit_end` releases it (operator,
8 October 2026). `pegasus_edit_end` on a lease that has already been
released answers `released` false; one that lapsed with nobody claiming the
record since is released as a live one is (R515). `pegasus_edit_renew` is a
heartbeat, for a Case as for a Triage Case, and takes no version (R524); a
routine renewal is telemetry only and records permanent history only when
refused (operator, 14 October 2026). Stopping the automation client releases
every Case lease and Triage or Image Intake scope Automation holds, so staff
need not wait for the lapse; the Stop's own history entry records what was
released, and no record gains history of its own (operator, 10 October
2026).

**Assessment writes.** <a id="assessment-writes"></a>
`pegasus_assessment_update` writes the assessment fields a staff member
records, and so can change or clear, on the Case: the fields a Case section's
editor posts and those a section writes through its own typed member (damage
entries, storage per day, recovery charge and report date).
Professional findings are among them (operator, 7 October 2026): the
outcome, roadworthiness (`assessment.legal_status`), the unroadworthy reason,
the salvage category and value, and the Engineer's Value
([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#professional-engineering-findings-and-correction)).
The report's Retail and Trade are the chosen guide card's figures (operator,
8 October 2026): `pegasus_assessment_update` refuses them, and
`pegasus_valuation_save` with a calculation writes them from its basis card.
As on the Case, a positive agreed contract sum makes the outcome a contract
repair. Any other field is refused and named, so every Automation value is
one staff can change or clear on its section (operator, 24 September 2026).
A value it writes is the Case's value, shown with its AI source tag until
staff change it; there is no per-field review (operator, 25 September 2026).
A system fill never overwrites it, as it never overwrites a staff value.
Case-owned facts and the facts the DVLA/DVSA lookup alone records (engine,
fuel, colour, tax and MOT expiry) are refused. The impacts staff enter
(`assessment.impacts`) and the damage markers (`damage.impacts`) are written
through `pegasus_assessment_update` like any staff field; there is no impact
tool of its own (R646). Skip impacts (`assessment.impacts_skipped`) is
written the same way, and a write that would leave it ticked beside an
impact row is refused as it is for staff (R649). `pegasus_vocabulary_get`
lists every assessment field path with its type, accepted codes, the format
a structured value takes (`assessment.impacts` and `damage.impacts`), staff
label and whether the tool may write it, with the estimate line types and
evidence labels, the repairer VAT statuses and VAT categories, the valuation
sources and the shared image tags. It also lists the enabled staff by id and
username, whose ids the assign actions take, and the eligible Sign-off
Engineers by id and printed name, whose id `signOffEngineerId` takes (R593),
and the unroadworthy wordings bank, the one list offered on every Case
(R952).
`pegasus_assessment_update` with `saveUnroadworthyReasonToBank` also saves
the unroadworthy reason it records to that bank, as **Save this wording**
does; a wording the bank already offers is not saved twice.

**Valuation.** `pegasus_valuation_list` returns the Case's valuation cards:
each guide card, the Engineer's Value card and any AI market research card.
`pegasus_valuation_save` records guide cards and, optionally, adopts the
valuation calculation against a basis card, as the Valuation section's Save
does; a card for the same source and guide month replaces the earlier one.
An Other increase counts only with a name and an amount above zero and is
otherwise left out, as on the Case page (R934).
`pegasus_valuation_get` presses a guide card's **Get valuation** and records
that card with the figures, as the section's Save would (operator, 8 October
2026): Pegasus asks the source's connected provider with its own valuation
credential and, where the provider offers them, files its report and fills an
empty VIN, exactly as for staff. An unavailable source, a vehicle the source
does not value (`VehicleAge`) or a registration it holds no data for
(`NoVehicleData`) records nothing, and so does a source already answered for
that guide month at the Case's registration and mileage (`AlreadyAsked`; R900;
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)).
Adopting a basis card, which writes the report's Retail and Trade, stays
with `pegasus_valuation_save`; the Engineer's Value stays with
`pegasus_assessment_update`.

**Case details and notes.** Case facts, including the Inspection date the
report prints as the date the damage was assessed, change through
`pegasus_case_update_details`, which calls the staff Case save. An omitted
value is unchanged, an empty string clears a text or date value, and a value
the call does not name never changes. It edits the claimant, claim, contact,
accident, VAT status and repairer; the Principal, Claim source and Client
notes; the due by date and the Claim source contact (its name the Case's
own, [R1048](../rulings.md)); the vehicle's identity,
year and mileage; the inspection; and the Sign-off Engineer. As on the Case
page, it links the repairer to an active directory Repairer and records the
Claim source from the active Claim source records, each copied onto the Case
so a later directory edit never rewrites it; `pegasus_directory_search`
lists them with the codes of the Principals each is linked to and, given the
Case's Principal code, lists that Principal's linked contacts first, as the
Case page does (R1013, R1016). A Claim source contact given in the same call
as a newly chosen Claim source is that source's contact, and one given while
the Case has no Claim source is refused; the Case page ignores its contact
boxes in both situations, because a page save reposts the previous source's
contact (operator, 16 November 2026; [R1096](../rulings.md)). Naming the Repairer the Case is already linked
to keeps the link and its copy as they are, whether or not the record is
still active (R1022). Naming the Sign-off Engineer the Case already shows
records nothing, and the empty identifier removes the recorded pick so the
default applies again (R1019). `pegasus_case_get` returns the Case's Claim
source and Repairer, which the report sending rules read, and what still
holds the Case from being ready: its unmet requirements, the instruction
items still missing, a failed document storage and a missing original
report, as the Not ready queue and the Case's Next action name them (R929).
It also returns the Case's Audit workflow and, on a Garage authorisation &
invoice Audit, when the garage was marked authorised; `pegasus_case_action`
takes `authorise_garage`, with no reason, and `undo_garage_authorisation`,
with one, under the rules the Case page follows (operator, 17 November 2026;
[R1142](../rulings.md);
[FRD-13](frd-13-case-lifecycle-and-workflow.md#garage-authorisation)).
`pegasus_assessment_get` returns Case facts under `caseOwned`. The Case's
Received date, which is its instruction date, is read-only:
`caseOwned.receivedDate` and the Case summary's `receivedAtUtc` carry it, and
no tool accepts an instruction date. `pegasus_case_note_add` adds an
append-only note to the Case timeline, attributed to the Actor, as staff add
one; it changes no Case value and needs no version or lease.

**Case lifecycle.** <a id="case-lifecycle"></a> `pegasus_case_action` takes
one Actions-menu act through the same Core command as the staff Case page:
place on Hold, with an optional review date, and release it; return to
Review; assign the Engineer, or, with no `engineerId`, hand the Case to
Unassigned (#1213, operator, 15 October 2026); return to Engineer; close
with a named outcome; reopen to a named destination; archive; create the
linked replacement of a Case created in error; Create audit; request the
vehicle lookup; run the Experian vehicle history check
([ADR-0089](../adr/0089-experian-vehicle-history-check.md)); record a manual
chase. Each needs the reason staff give for
it, except where staff give none. Queries are `pegasus_case_query`: it logs a query by its channel,
marks one done, reopens one and assigns it (operator, 11 October 2026). A
query on a linked message is opened with `pegasus_mail_open_query` (R1042).
A tagged e-mail is a query too:
`pegasus_mail_correct_classification` to Query. Core alone decides which
act the Case's state allows
([FRD-13](frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries)).
A Case is never deleted.

**Reports.** <a id="reports"></a> `pegasus_report_list` returns a work's
report generations with their artifacts, filing state and SHA-256, the
recorded report approval, the linked report-Sent evidence and the retained
Sent evidence available to link. `pegasus_report_action` generates the report,
which goes on to make its separate fee note and then, for a Principal whose
setting asks for it, the ULA Invoice (operator, 17 November 2026;
[R1147](../rulings.md);
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#companion-documents-and-what-a-delivery-attaches)),
and the Repair Spec and images documents of a confirmed generation; records
report approval of one stored
artifact; and links or unlinks report-Sent evidence, each through the staff
command ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point)).
Report approval is by staff or the Automation actor (operator, 7 October
2026). A report that is not ready answers with its readiness reasons and
records nothing. An approval, archive or evidence link the Actor records is
attributed to it and reads back as the Automation actor. A generate_report
result gives the fee note's outcome as `feeNoteOutcome` and the ULA Invoice's
as `principalInvoiceOutcome`, each null when none was made; there is no act
that makes the ULA Invoice on its own.

**Document acts.** `pegasus_document_action` tags and untags an image, puts
it in the report or takes it out, marks the Audit's original report, changes
a document's type with a reason (`set_type`: any type but Original source;
R204; [FRD-05](frd-05-documents-extraction-and-custody.md#document-types)), removes
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

**Photo links.** <a id="photo-links"></a> The Actor works guided photo
capture links as a member of staff with `PerformCasework` does on the Photo
capture pages ([FRD-28](frd-28-guided-photo-capture.md#staff-pages)),
through the same Core commands and with no Case edit lease, as the pages
take none (R1039, R1040). `pegasus_photo_link_action` extends, cancels or
logs a call where the link's page offers it; a repeated operation key acts
once. What a link offers is one Core rule, which the page, its own handlers
and the tool all follow (operator, 16 November 2026;
[R1092](../rulings.md)); a call that repeats an operation key which already
recorded that act is answered as the first was, whatever the link now offers
(operator, 17 November 2026; [R1130](../rulings.md)). Sending a link by e-mail or text is `pegasus_photo_link_send`,
under `automation.send` ([Sending](#sending)). Standing and website links
are managed by Administrators only. With `Features:PhotoCapture` off every
photo link tool refuses.

**Sending.** <a id="sending"></a> The Actor sends as staff do, under
`automation.send` (operator, 7 October 2026;
[ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md)). EVA
submission is not part of it. Each send tool is the staff send it names,
through the same Core command, and takes what that staff composer takes, as
its parameters describe.

- `pegasus_report_send` is the Case page's **Send report**
  ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#companion-documents-and-what-a-delivery-attaches)),
  and on a Case that has its Audit `otherReport` adds the other work's
  current confirmed report to the same e-mail. Drawn from no form, it ticks
  no hold and overrides no Stop of the Principal's report sending rules
  ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-sending-rules)).
  It holds the Case's lease for its one send unless a lease token is
  presented. Its `attach` takes `FeeNote`, `PrincipalInvoice` (the ULA
  Invoice), `RepairSpecification` and `ImagePack`. Its `caseDocumentIds`
  names, by occurrence, the offered Case files to attach as well: the
  Case's confirmed External estimates and Repair invoices the Principal's
  rules offer; one that is not offered is refused as the send form refuses
  it (operator, 17 November 2026; [R1151](../rulings.md)). Its
  `caseDocumentNames` names, by occurrence, the Case's files the send
  attaches (the filed estimates the rules attach and the offered files),
  each keeping its own extension, as the send form's Rename does (operator,
  18 November 2026; [R1175](../rulings.md), [R1176](../rulings.md)). Its result is the send's
  state and `attachments`, the file names the e-mail carries, in order
  (R703); a caller reports what was sent
  from `attachments`, not from what it asked for (R704).
- `pegasus_mail_send` is the staff mail send with a `mode`: `chaser` is the
  Case's **Send chaser**, recorded as the Case's chaser; `reply`,
  `reply_all` and `forward` answer one retained message as its message page
  does; `triage_reply` is the Triage Case's reply to its origin e-mail, the
  chaser until the outcome is recorded and the outcome reply once Completed
  ([FRD-03](frd-03-triage.md)). `reply` and `reply_all` take
  `markQueryDone`, the composer's Mark query done tick, and `aiJobId`, the
  Draft ready Query response job the reply was composed on: a send the
  provider accepts completes it, as the staff composer's Send does
  ([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)),
  and a send that leaves it as it was returns the composer's line in
  `aiJobNotCompleted`. Every mode files the mail against a Case and checks
  the version the caller observed.
- `pegasus_photo_link_send` is **Send a photo link** (`send`) and **Resend**
  (`resend`), by `email` or `text`, through the same Core commands
  ([FRD-28](frd-28-guided-photo-capture.md#the-photo-link-e-mail)); its
  e-mail leaves from the Outgoing mailbox `fromMailbox` names, else the
  Case's default sender. A repeated operation key sends nothing more: a
  repeated e-mail answers `replayed`, a repeated text is refused.

For a report or mail send, `fromMailbox` defaults as the staff composer's
From does, and `deleteCaseCorrespondence`, the Delete case correspondence
tick, is taken where the composers offer it and refused anywhere else (R904,
R905); both are [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)'s
rules. A report or mail send is the one staff send operation of FRD-21: the
same approved mailbox, operation-key replay, Submitted-is-not-Sent rule and
Sent evidence. The operation, its history and the Case's Notes line record
the Automation Actor as the sender, never a member of staff. A photo link
e-mail is the same staff send operation, recorded as the Automation Actor's.
The Actor's current-sender check is its client registration's kill switch,
read at each step a staff send re-reads the staff account, so a send in
flight when the client is stopped fails as `staff_send_authorization_lost`.

**Intake submission.** Custody of a source submitted with
`pegasus_intake_submit` starts only with an authenticated accepted
submission. Each occurrence follows the ordinary source-occurrence,
idempotency, matching, classification and history rules
([FRD-02](frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity)).

**Background automation.** Queues and timers carry stable work identities;
Core owns transitions and idempotency
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
items, Image Intake records, uploads of several files, mail and the Work
Centre (operator, 7 October 2026;
[ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md); R1041).
Each calls the same Core command as the staff page, records the Actor's own
history and replays by its operation key.

- `pegasus_intake_get` reads one received item as the Create case page
  reviews it: version, decision, existing Case, classified case type, draft,
  missing identity-critical fields, allocation attempt, the inspection
  address state, suggestion and fingerprint, and who settled it, and the
  item's files, which a Triage reply attaches by asset id. It names the
  Principal the item has established (`establishedPrincipalCode`,
  [FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)). Where
  the item has established none, `senderDirectoryPrincipalCode` is the one
  active Principal Contact its sender belongs to by the Contacts directory:
  the Contact's e-mail on the sender's exact domain, or the sender's whole
  address on a shared mail domain. Two Principals on the domain, or a sender
  the directory also makes an intermediary for a different Principal, name
  none. It is a code to give `accept`, not a mail route
  ([R969](../rulings.md)). `websiteEnquiry` is true for an
  item the firm's website form sent (R972).
- `pegasus_intake_action` with `resolve_inspection_address` settles the
  inspection address as **Create case** does: it accepts the suggested
  address, corrects it, or, where nothing was suggested, supplies one
  ([FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address)).
  Accepting or correcting names the suggestion's fingerprint. The settlement
  records the Actor as its settler, by actor kind and identity, and the
  Case's address carries an Automation label, never a staff one.
- With `accept` it turns a received item into a Case as **Create case**
  does: the reviewed draft is recorded (named draft fields replace the
  item's), then the Case is allocated for the Principal and case type. With
  no `principalCode` the Principal is the one the item established. An
  unsettled inspection address does not refuse `accept`: the Case starts
  Not ready and its instruction names Inspection address as missing, as a
  Case made from **New case** with the address blank does; a Principal that
  inspects by images needs no settling ([R965](../rulings.md)). An Audit is
  accepted only for an item classified as one. With `allocate` it retries a
  failed allocation with a reason, naming the attempt, as the Intake log's
  **Retry allocation** does.
  With `open_triage` it opens the Triage Case from an item classified, or
  corrected to, Triage request, as **Open the Triage** on its message does
  ([FRD-03](frd-03-triage.md#triage); R223).
  With `register_images` it registers the item's images as an Image Intake
  record under the registration given, with a reason, as **Register images**
  on its message does, where the message offers it.
- `pegasus_case_create` creates a Case directly, as staff **New case** does
  ([FRD-02](frd-02-intake-and-source-identity.md#ways-intake-starts)): an
  Inspection, Audit or Inspection and Audit Case with its identity-critical
  facts, a Triage Case from its Principal and registration, or, with
  `privateCustomer` and no `principalCode`, a one-off Case for a private
  customer from the claimant's name and the registration, with an optional
  `claimantAddress`
  ([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity);
  R577); a one-off may be an Audit, `a.` and its registration (R600, R601).
  An Audit created this way starts with **Original report missing** until a
  filed document is recorded as the original report ([R595](../rulings.md)).
- `pegasus_unidentified_resolve` with `targetKind` Closed is **Close with
  reason**: it names no destination. `pegasus_unidentified_reopen` withdraws
  a resolution or closure with a reason. `pegasus_unidentified_get` returns
  the registration readings the record shows and whether it offers
  **Register images**; `pegasus_unidentified_action` takes the record's
  **Register images** (`register_images`; an upload group item registers its
  whole group as one record) and a reading's **Dismiss**
  (`dismiss_reading`), each with a reason. Dismiss is offered, by one Core
  rule the record, its own handler and the tool follow, only for a pending
  suggested reading on an open item of one file ([R1092](../rulings.md)):
  `pegasus_unidentified_get` says so on each reading (`canDismiss`), and
  `dismiss_reading` on any other pending reading is refused with
  "dismiss_reading is not offered on this reading; pegasus_unidentified_get
  lists the readings that offer it." (operator, 17 November 2026;
  [R1132](../rulings.md)). A reading already decided answers the operation
  key that decided it ([R1130](../rulings.md)). Unidentified reason codes on the
  wire are the Core list under `unidentified`.
- `pegasus_image_intake_list` lists the Image Intake records (staff label
  Vehicle images) awaiting instruction with no Case, as the Cases page does;
  `pegasus_image_intake_get` reads one as its page shows it, with the active
  Principals its Principal may be set to; `pegasus_image_intake_action`
  records, replaces or clears its known Principal (`set_principal`) or closes
  it with a reason (`close`), each holding the record's edit scope for its
  one command unless an Image Intake lease is presented
  ([FRD-14](frd-14-record-edit-leases.md#record-edit-scopes)).
- `pegasus_upload_group_get` reads a stored upload of several files as its
  page does: each file's status and outcome, whether its one decision may
  attach every file to one Case and the Cases they share, and whether it may
  be discarded. `pegasus_upload_group_action` takes that decision: `attach`
  every file to one Case, `leave` the matched Cases undecided, or `discard`
  the upload. A `discard` call is its own confirmation: the tool asks for no
  counterpart of the page's consequences tick (operator, 16 November 2026;
  [R1094](../rulings.md)). `leave` is offered, by one Core rule the upload
  pages, their handlers and the tool follow, only while the upload awaits
  its decision and every file is a possible match and image-only
  ([R1092](../rulings.md), [R1135](../rulings.md)): `pegasus_upload_group_get`
  says so (`canLeave`), and `leave` on any other upload is refused with
  "leave is not offered on this upload; pegasus_upload_group_get says whether
  it is." ([R1132](../rulings.md)); an upload already left answers as done
  ([R1130](../rulings.md)). Any member of staff who does casework may decide any upload, so
  the Automation Actor may too.
- The Automation Actor reads, reclassifies and links mail through the mail
  tools in scope `automation.mail` (operator ruling, 15 October 2026).
- `pegasus_mail_list` folder is `inbox`, `sent` or `dismissed`. Its first
  page also carries `awaitingDecisionCount`: the Inbox messages, not
  dismissed, whose classification the pipeline decided and that are
  Unclassified or have no Case, counted outside the list's own filters so
  that a list hiding mail shows (R735). Each row names who decided its
  current type in `classificationActor`.
- `pegasus_mail_correct_classification` takes `correspondence`, one of the
  four category codes, or `new-instruction:Inspection` (and Audit,
  InspectionAndAudit), with a reason, as the staff Type select does
  ([FRD-08](frd-08-email-mailbox-and-background-processing.md#mail-categories-and-correction)).
- `pegasus_mail_link_destinations` lists the Cases and Triage Cases the
  staff **Case / PO** picker offers for **Link to Case**, or for a linked
  message those **Change linked Case** offers, and `pegasus_mail_link_case`
  links the message (`link`), unlinks it (`unlink`) or changes its linked
  Case (`change`) through the staff commands; a change holds the new Case's
  lease before anything is unlinked
  ([FRD-20](frd-20-mailbox-workspace.md#classification-and-linking-actions)).
- `pegasus_mail_action` with `dismiss` or `restore` changes the message's
  Pegasus scope only
  ([FRD-20](frd-20-mailbox-workspace.md#dismiss)).
- `pegasus_mail_attachment_text` returns the text the intake reader already
  extracted from one attachment of a retained message (a PDF's text layer or
  a Word document's text), named by its position in `pegasus_mail_get`'s
  attachment list and cut at the caller's cap; it returns no bytes and
  writes nothing (R732; #1891). It reads that one attachment's row, not the
  whole message.
- `pegasus_mail_photo_registrations` returns the plate reads the Worker
  recorded for a retained message's photographs when it arrived, and the one
  registration they agree on at the accepted bar (`read`), none (`none`) or
  more than one (`disagree`); it reads no image and writes nothing.
- `pegasus_mail_mailbox_search` and `pegasus_old_system_folder_search` are
  the Actor's two read-only lookups outside Pegasus's retained records
  (R561): the first searches one or every approved mailbox in Outlook for a
  term and returns message headers only (subject, sender, received time,
  preview), marking nothing read and moving nothing; the second searches
  folder names below the old system's Box folder and returns names and
  identities only
  ([FRD-05](frd-05-documents-extraction-and-custody.md#custody-and-derived-reads)).
  The old-system folders are named by the firm's own Case reference, so a
  reference-shaped term keeps only the folders that name it as a whole token
  (R592), filtered before the hits are capped, so near-miss names cannot
  crowd out the folder itself. Each takes a term of 1 to 200 characters and
  at most 100 items, and reports a source that refused, throttled, failed or
  is not composed as `unavailable`, never as nothing found.
- `pegasus_work_centre_dismiss` dismisses a record from the Work Centre for
  everyone, as a row's **Dismiss** does. The record id is the Case, the
  Unidentified item, the AI job, or — for a System Action — the underlying
  receipt, image intake or query
  ([FRD-15](frd-15-work-centre-queues-and-search.md#work-centre),
  [ADR-0086](../adr/0086-work-centre-views-are-computed-on-read.md)).
  Its operation key is optional: Core's dismissal takes none, so a key only
  labels the call's history (R1042).

Which of these tools the classifier agent calls, and when, is FRD-08's
([Mail categories and correction](frd-08-email-mailbox-and-background-processing.md#mail-categories-and-correction)).

### AI job and estimate tools

The Actor's inventory includes the AI job ledger tools from
[ADR-0035](../adr/0035-ai-job-ledger.md). Job kinds and states are owned by
[FRD-27 AI Job List](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list).
Every tool calls the same Core command as the staff application, supplies the
resolved Automation identity and the connecting client's name, and needs an
operation key and expected version on every change.

| Tool | Action |
| --- | --- |
| `pegasus_ai_job_list` | List jobs by state and kind; a client sees every queued job and its own taken jobs |
| `pegasus_ai_job_create` | Create a job of any kind with the subject the staff action names: Estimate (a Case, optional direction and target), MarketResearch (a Case and guide month; a pending job for the Case is returned instead), QueryResponse (a retained Query e-mail linked to its Case), UnidentifiedResolution (an open U-reference) or UnidentifiedQueuePass (an instruction). Jobs are how external agents on automated runs hand work to each other (operator, 7 October 2026) |
| `pegasus_ai_job_transition` | One tool with an `action`: Take claims a queued job under a bounded lease held by the client's name (refused when the job is not queued or the kill switch is on); Progress renews the lease and records a short note (refused after cancellation, lease expiry or while the kill switch is on); Complete marks a non-MarketResearch job `Draft ready`, naming its result kind; Fail marks it `Failed` with a reason; Release returns a taken job to `Queued` before the lease ends. Those five act only on the client's own job. Cancel stops any queued, taken or `Draft ready` job with a reason; Confirm marks a `Draft ready` QueryResponse or UnidentifiedQueuePass `Completed`, as the Work Centre's **Complete job** does |
| `pegasus_ai_job_complete_market_research` | File one findings document, from `contentBase64` or a staged `uploadId`, and one AI market research card and mark the client's MarketResearch job `Completed`. It takes no Case edit lease and no Case version: a source card is not a Case field edit, and the Engineer who asked is usually still editing, so it never waits on or ends their session. The Case history records the attachment at the Case's current version. Refused for an archived or closed Case (operator, 28 September 2026) |

**Raw estimate import.** `pegasus_estimate_import` and **Import estimate** on
the Repair Spec section are two callers of one Core command,
`IImportRawEstimate`, run after custody retention; there are no separate
import paths. The tool names one retained PDF, XML or JSON file and returns
the estimate identity or the same structured refusal as the Case caller.
Both callers use the same parser types, the same fail-closed provider
detection, the same provider-plus-sequence naming, the same labour-rate card
and the same replay rule. The caller does not choose a trusted provider
route. Even a source-hash replay needs the current actor, version and lease
authority and the exact retained source tuple. An unsupported estimate
document is refused without OCR or partial rows. A staff Import on the Case
is the Current repair spec at once; the MCP import runs as the Automation
actor, so it stays a Draft until a staff member uses it.

**Estimate saves.** `pegasus_estimate_save` saves through the same Core
command as the Case's Repair Spec editor (operator, 7 October 2026). A new
estimate lands as an AI-draft `Draft`. An edit changes any live estimate in
place, the Current one included; editing the Current one makes a generated
report stale. Each kept line sends its `lineId` so it keeps its source
evidence. The header carries the labour rates, the repairer's VAT status,
the VAT categories, discounts and regional uplift; a header value the call
omits keeps its recorded value. An omitted repairer VAT status keeps the
recorded choice, or records Registered when none is recorded or the recorded
status is Unknown (operator, 22 October 2026). Registered charges VAT on
labour, parts, materials and specialist. The choices are Registered and
NotRegistered; Core refuses an explicit Unknown, as it does for every caller.
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
work, not the authority to save, so citing the Estimate job a save fulfils is
optional.

### Glass's sessions

**The Automation's own sessions (18 November 2026; R1177 to R1180).** The
Actor runs Glass's sessions of its own under `automation.glass`, through the
gateway staff sessions use
([FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-launch-and-return),
[ADR-0091](../adr/0091-pegasus-sends-the-glass-editor-its-own-requests.md)).
A session signs in with the Glass's login the deployment holds for the
Automation (R1177). It names no member of staff, and any Automation grant
acts in it, because a grant is renewed every fortnight and the session
outlives it. The Actor acts in a session a member of staff launched only
while they have turned help on for it ([help](#help-in-an-engineers-open-session)).
Each tool takes the Case and finds the Automation's session on it. The tool
descriptions and refusal sentences are R1178's. On a host that does not
compose Glass's, each tool is refused with "Glass's is not available."

- `pegasus_glass_session_act` `start` makes a new Glass's estimate for the
  Case's vehicle, as a first press of **Glass's** does, and opens the
  estimate in Glass's editor, which stays open between calls. It never
  reopens a spec already on the Case. While the Automation's own session on
  the Case still holds the login, `start` continues it instead. The same
  operation key names the same session.
- A Glass's login holds one live session. While the Automation's login is
  held on another Case, `start` is refused before Glass's is asked, with
  "The Glass's account is in use by a member of staff on Case {reference}."
  or "The Glass's account is in use by the Automation's session on Case
  {reference}; finish or close it first." It never ends a staff member's
  session.
- `finish` saves the estimate in Glass's and brings it back through the
  return staff use. The import runs as the Actor, so the estimate lands as a
  Draft repair spec that records its Glass's estimate; only staff put it in
  use. An estimate with no position is not saved: `finish` is refused with
  "The estimate has no positions; close the session instead." and the
  session stays open. When the Case moved on or a member of staff holds it,
  the estimate is held, as a staff return is, and a later `finish` lands it.
  The same finish key answers the session as it stands.
- `close` ends the session without saving, with a reason of 1 to 2,000
  characters. The call is the confirmation of external closure: nothing is
  sent to Glass's and no Case lease is taken. A `finish` or `close` with no
  Automation session holding the login on the Case is refused with "The
  Automation has no Glass's session open on this Case."
- `start` and `finish` take the Case version and hold the Case's edit lease
  for that one command, or use an `editLeaseToken`. `finish`, `close` and
  `pegasus_glass_position_act` take the session version, and a stale one is
  refused. The search and the position acts take no Case lease, so a member
  of staff editing the Case does not stop the Actor's work in Glass's.
- Each act that asks Glass's runs as background work keyed on the session,
  one at a time, and the call waits for it for up to 45 seconds. A session
  still working answers as it stands, with `working` set; the Actor reads it
  again with `pegasus_glass_session_get`.
- `pegasus_glass_session_get` answers the session (state, version, failure
  code, whether work is running, and the Draft spec a finish landed as),
  and whether the Glass's login is free or which Case holds it, by staff or
  by the Automation, naming no member of staff. A session left waiting on
  work that nothing runs is settled first, as the Glass's window's poll
  settles it.
- **19 November 2026; R1206.** With `read`, `pegasus_glass_session_get` asks
  Glass's once for what its editor shows a user, and nothing changes at
  Glass's. `positions` answers the positions the open estimate holds and
  Glass's running totals from the same page, each with Glass's own label
  (such as Parts, Paint material, Body, Paint, Total (£), VAT (20.00 %) and
  Amount due (£)); Glass's lists only the totals the estimate has.
  `position`, with a `positionId` from that list, answers the position's
  repair type, price, hours, side, part number and, when it has paint, its
  paint price and labour time. `calculation` answers the Summary of Glass's
  calculation: each trade's labour rate, labour time, labour costs and
  material costs, then Total Labour, Total Material, Repair costs excl. VAT,
  VAT and Repair costs incl. VAT. A figure Glass's does not give is absent,
  never zero. A read Glass's does not answer gives its `glass.*` code as
  `readCode` and nothing read, and leaves the session as it was. This
  replaces `readEstimate`.
- `pegasus_glass_catalogue_search` reads the open session's part list and
  special operations, and changes nothing. `pegasus_glass_position_act`
  `add` adds a position by a row the search returned, posted as Glass's
  dialog serves it with nothing typed, and only where the row is certain
  (18 November 2026; R1182, as R1168 and R1172 for a rebuild). `remove`
  removes a position by its id.
- **19 November 2026; R1206.** In place of R1182's set, `add` gives a
  part's dialog what a person gives it without typing, in both kinds of
  live session. Paint on its own and Part and Paint are sent: the button is
  pressed on the dialog of a part that can be painted, and the paint section
  is sent as Glass's serves it. `inputs.operation` chooses any other
  operation the dialog offers, by Glass's own code, with the price and
  hours its own script sets for it. No operation is refused for the
  return's sake: the return reads every repair kind Glass's exports (issue
  2155). An operation that reloads the dialog, Paintless dent repair (DD)
  and any the script's `SubClassMap` marks `WS`, is followed as the editor
  follows it: the form its ChangeRepart submits is posted to the address
  the script names, and the dialog Glass's answers is posted with the rest
  of the inputs; `preview` shows the reload as a question and does not
  follow it. An operation the script holds no figures for (the bonnet's
  NL) is sent with 0.00 in each box, as CheckSubmit writes them, once the
  question before no figures is answered. Both were judged offline on 19
  November 2026 on the bonnet (#745). Repair answers OK to the
  dialog's question before a part with no price and no hours (R1188); any
  other operation does so only with `inputs.confirmNoFigures`, and is
  refused without it. `inputs.button` presses Part, Paint or Part and Paint;
  a part that cannot be painted has no paint button and is refused with
  `glass.row.not_paintable`. `inputs.side` `both` ticks Both where the
  dialog offers it; the other side's own dialog, which the editor opens
  next, is then posted as served, and if it does not come as expected the
  first side stays added with `glass.row.other_side_not_added`.
  `inputs.optionalParts` names the optional parts to tick on the
  "Mandatory parts:" page, one box each as a person ticks them: by the id
  its listing carries on an earlier act, a code the page lists twice being
  two boxes (`1102450.ER.#2` is the second), or by its code, which ticks
  the first optional box of that code. The top-level `operation` keeps its meaning;
  `inputs.operation` and `inputs.button` are given with `part` only. Every
  input given is kept on the act, beside the figures Glass's served.
  The server's rebuild of an outside estimate is unchanged (R1168, R1169,
  R1172).
- **19 November 2026; R1206.** `inputs.hours`, `inputs.price` and
  `inputs.overlapHours` type the labour hours, the price and the overlap
  hours in the dialog of a special operation or of a part, whether it can
  be painted or not, one side at a time, in both kinds of live session. A value the Automation types is its own input, as a person's
  would be. Pegasus sends each with two decimal places after a full stop
  (`2.80`, `1234.00`), as a person types it; the dialog posts the text
  typed unchanged. A negative figure, one with more than two decimal
  places, hours or overlap hours above 1,000 and a price above 999999.99
  are refused before anything is asked of Glass's. Hours above Glass's own,
  where Glass's has hours of its own for the operation, need a reason: the
  Automation writes it in `inputs.reason`, 1 to 150 characters, and Glass's
  keeps it on the position. Pegasus never writes a reason. An add above
  Glass's hours with no reason is refused with
  `glass.build.reason_required` before anything is posted, and a reason
  given where the dialog asks for none with `glass.build.reason_not_asked`.
  Overlap hours above the hours sent are refused with
  `glass.build.overlap_above_hours`, because the return refuses a whole
  export with such a position. On a part that can be painted the reason
  rule runs for Part and for Part and Paint, as its CheckSubmit runs it.
  The act keeps every input beside the figures the
  dialog served and the figures sent, recorded just before the post.
- **19 November 2026; R1206.** The dialog's own fields are given as a
  person gives them, in both kinds of live session: `inputs.place` ticks the
  side boxes where the dialog lets its side be changed (left or right, and
  on a special row front or rear), and the dialog then reads its figures and
  part number for that side; `inputs.trade` chooses the labour type;
  `inputs.partNumber` and `inputs.labourDescription` are typed, the labour
  description as a person types it or picks it in Glass's picker
  (`freetxt.php`, which only writes the box); `inputs.description` types a
  special operation's own description, 3 to 250 characters as its dialog
  wants at least 3; `inputs.outsideWork` ticks outside work on a special
  operation's dialog, which then chooses ZK and greys the hours and overlap
  hours; `inputs.overlapFull` presses Full, which copies the hours into the
  overlap hours. Each was judged offline on 19 November 2026 on the walk's
  door glass (#643), door shell (#894), yard charge (#1109) and bulb row
  (#1257), seven posts identical to the dialog's own script's and pinned.
  No captured dialog offers a quantity box, so none is built.
- **19 November 2026; R1206.** `change` changes a position already on the
  estimate, any position of it as `remove` takes any (R1185), in both kinds
  of live session. It takes the position's id and `inputs`: the typed
  figures and reason, the paint choices, the OK to no figures, the button
  and the optional parts to tick. Its equipment answer, its operation and Both stay as the
  position has them. It reads the estimate first and opens the dialog the
  editor's own list opens for the position: `part.php` for a part with no
  paint or a special row, `comp.php` for a part with its paint. A paint
  position of its own (`lack.php`) is refused with `glass.build.not_built`,
  and a position the estimate does not hold with `glass.row.unknown`. The
  dialog must be the position's own. It is posted with the change, its
  "Mandatory parts:" page is confirmed as an add's is, the estimate is read
  back and must still hold the position, which keeps its id, and the
  position's own figures are read. An empty answer to that read leaves them
  unread. The act is `changed`, with the figures served and sent; a post
  whose answer is lost is `not_confirmed`, and the position's own read
  settles it. `preview` of a position takes the same inputs, reads the
  estimate to choose the dialog, and says what the change would send.
  `remove` opens the dialog the editor's own list opens for the position,
  `part.php`, `comp.php` or `lack.php`, before its Delete.
- **19 November 2026; R1206.** `inputs.paint` makes every paint choice a
  person makes in the dialog of a part that can be painted, where Paint or
  Part and Paint is pressed, or in a `change`: the paint area, the paint
  type (`coats`), the procedure, the method, the level (a plastic part's as
  well as a metal one's), Surcharge, Coloured clear, scratch-resistant
  clear, and the typed paint price, paint hours and paint reason. Pegasus
  makes them in that order, each with the dialog's own handler as its
  script runs it: the area rebuilds the paint types and levels, picks the
  area's default level and sets the price and hours; the paint type sets
  the paint state and its tick boxes; the procedure and the method each set
  the paint state the script picks for them, its price, hours and tick
  boxes. Glass's asks no reason for paint hours; the paint reason window
  (`reason.php?lreason=`) only writes the box, so the Automation's reason is
  sent as given, its line breaks as bars. Coloured clear is posted as the
  paint state sets it, greyed or not, as the dialog posts it. A value the
  dialog does not offer there, Surcharge or Coloured clear where the paint
  state fixes it, scratch-resistant clear where the area or paint type greys
  it, and a button the page hides are refused before anything is posted.
  Each was judged offline on 19 November 2026 on the bonnet (#745), the left
  front door shell (#894) and the front bumper (#552), the dialog's own
  script's post pinned. `preview` names the input of every paint box, gives
  each paint type and level the area offers with Glass's price and hours,
  and says when the figures are the position's own.
- **19 November 2026; R1206.** `preview` asks Glass's for the dialog an
  `add` with the same row and inputs would post, chosen as the add chooses
  it, or for the dialog of a position on the estimate by its id, and
  answers it as a form. It posts nothing, stores no equipment answer,
  records no act and takes no session version. It gives the dialog's kind,
  description, part number, part code and side; each operation it offers
  with the price, hours and Glass's own hours it serves, and whether
  Pegasus may choose it; each field a person sees with Glass's own label,
  its served value, its choices and whether Pegasus may change it; the
  paint section; the buttons; and, for the inputs given, what the add would
  send, each question on the way, and the code the add would be refused
  with. Each box the Automation may type in names the input that changes it, and
  the hours box gives Glass's own hours, above which a reason is needed.
  Glass's own labels and texts pass through unchanged. Which
  mandatory parts Glass's lists after a post cannot be known before it.
- **19 November 2026; R1208.** The Automation changes no setting of the
  estimate as a whole or of the Glass's account: not Preferences, not the
  paint settings, not the Overlap-time deductions. What it gives are the
  inputs of adding or changing one position.
- A part whose block in Glass's part list asks Glass's equipment question
  is added as Part with the Actor's answer (19 November 2026; R1206, in
  place of R1182's "parts whose row needs no equipment answer").
  `inputs.equipment` names the option codes it ticks, or none, and is the
  whole answer: an option the vehicle has that it does not name is
  unticked. The search gives each such part its question, built from the
  part list alone: Glass's own option names, which are ticked now, which are
  greyed, their radio groups, and Glass's own "Criteria affects other
  parts!". With `row` it answers that one row and every answer that leads to
  a part, with the row pair, part number, price and hours the part list
  gives; above 256 answers it gives the map's own part and hours entries
  instead.
- An add on such a part with no answer is refused with
  `glass.build.equipment` and the question. An answer that names a code
  that is no option (`glass.build.equipment_unknown`) or a greyed one
  (`glass.build.equipment_greyed`), names a code twice
  (`glass.build.equipment_repeated`) or two options of one group
  (`glass.build.equipment_same_group`), leads to no part
  (`glass.build.equipment_no_part`), or is given for a row that asks
  nothing (`glass.build.equipment_not_asked`) is refused with its code.
  Nothing is sent for any of them. Otherwise the answer is stored at
  Glass's as the editor stores it, just before the dialog is asked for, and
  Glass's keeps it for the vehicle. The dialog must serve the row pair the
  answer leads to and that row's part number before it is posted as
  served. The act keeps the answer, the row pair and the stored values it
  changed, and says when the answer was stored though the add was then
  refused. Repair and remove-and-refit on such a part stay refused, with
  `glass.build.equipment_part_only` (R1172), and the server's rebuild of an
  outside estimate answers no question (R1168, R1169, R1172).
- A part that asks no question but is shown or hidden on its drawing by
  equipment is added as any certain part when the vehicle's criteria show
  it, as Glass's editor shows it and opens its dialog with no question
  (19 November 2026; R1206). One the vehicle's criteria hide is refused
  with `glass.build.equipment_hidden`. For the Actor
  `glass.build.equipment` means only that the row asks Glass's equipment
  question and no answer was given, and an add or a preview always carries
  the question with it. Every other equipment refusal has a code of its
  own: `glass.build.equipment_wheel` for a wheel, which Glass's chooses in
  its own wheel selector, and `glass.build.equipment_uncertain` for a part
  whose rows depend on equipment Glass's does not ask about for it. Judged
  offline against the editor's own click on both captured part lists: of
  the heads that ask nothing and that the vehicle's equipment shows, 40 of
  61 on the SEAT's list and 46 of 72 on the BMW's give the editor's own
  row pair; the others are refused with a reason, and none gives another
  pair.
- A session the Automation owns has no lifetime, as any Glass's session.
  Staff see it on its Case, and any member of staff who can open the Case's
  assessment may close it with a reason (R1180;
  [FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-launch-and-return)).

#### Help in an Engineer's open session

**18 November 2026; R1170, R1182, R1184, R1185.** An Engineer may let the
Automation help in their own open Glass's session from the Case page
([FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-launch-and-return)).
No tool is added. `pegasus_glass_session_get`,
`pegasus_glass_catalogue_search` and `pegasus_glass_position_act` find, by
the Case, the Automation's own live session or an Engineer's `Active`
session with help on. With more than one, the call names one by
`sessionId`, which `pegasus_glass_session_get` lists; no member of staff is
named.

- In an Engineer's session the Actor reads the estimate as in its own
  session (R1206), searches its lists, previews a dialog and
  adds the same positions with the same inputs as in its own session,
  posted as Glass's dialog serves them with nothing typed (R1182; 19
  November 2026, R1206), adds a part whose equipment question it
  answers as in its own session, the answer stored for the vehicle and any
  option the vehicle has unticked if the answer leaves it out (19 November
  2026; R1206), and removes any position of the estimate
  (R1185): the Engineer asked for the help and watches the window. It never
  saves, exits or closes the session and never reads the Engineer's Glass's
  login; it works on the editor key the Pegasus helper handed over
  ([ADR-0091](../adr/0091-pegasus-sends-the-glass-editor-its-own-requests.md)).
  `pegasus_glass_session_act` refuses an Engineer's session.
- No Case lease is taken and the session's version does not move. One act
  runs at a time in the call, never as background work, and each is a
  history row on the session as the Automation's. The same operation key
  answers that row and sends nothing.
- An add or a removal reads the estimate first and answers what it holds
  after. An add's own positions carry the code its dialog served; the rest
  came from the Engineer's window meanwhile (`alsoNew`). The Engineer's
  window is then asked to show Glass's selected list again, and the answer
  says whether it has (`shownInWindow`).
- `pegasus_glass_session_get` answers whose session it is, whether help is
  on and whether the Engineer's window is connected.
- The Engineer's own Save & Exit brings the estimate back as any return does
  (R1184).
- Refusals, each before anything is asked of Glass's, carry their code:
  `glass.help.no_session` (with "The Automation has no Glass's session open
  on this Case."), `glass.help.off`, `glass.help.several`,
  `glass.help.not_active`, `glass.help.busy`, `glass.help.no_window` and
  `glass.help.not_automation_session`. Their sentences and the sentence each
  tool's description gains are in `AutomationToolText`.

### Tool inventory

The Actor's tools are served by one MCP server, `pegasus-automation`, at
`/mcp` (R547;
[ADR-0087](../adr/0087-one-automation-mcp-server-with-per-scope-consent.md)).
`tools/list` holds only the tools whose scope the token carries; the list is
disclosure, and each call is still checked against the token's scope. The
endpoint needs the shared audience and any one scope, and its
protected-resource metadata lists all eight scopes, so a connector asks for
them all and the Administrator ticks the ones its grant holds (R548). The
server's instructions restate this document's cross-tool rules and make
none.

**Scopes.** A token without `automation.jobs` cannot see the AI job ledger.
The estimate tools are under `automation.assessment` because they write
assessment values. The Glass's tools are under `automation.glass`
([Glass's sessions](#glasss-sessions)). Every scope has a consent
description on the Administrator consent page, which prints it without the
scope's identifier (operator, 15 and 19 October 2026); `automation.glass`'s
is R1179's.

The Actor's whole inventory, 87 tools, by scope. "One-command lease" means
the tool takes `expectedVersion` and `operationKey`, accepts an
`editLeaseToken` from `pegasus_edit_begin`, and holds the record's lease for
its one command when none is given.

| Scope | Tool | Action | Lease |
| --- | --- | --- | --- |
| `automation.cases` | `pegasus_case_search` | Search Cases by text, reference, registration, claimant, claim number, Principal, state; cursor page | none |
| `automation.cases` | `pegasus_case_get` | One Case with its Claim source and Repairer, what still holds it from being ready, and its paged documents (occurrence and version ids for download) and history | none |
| `automation.cases` | `pegasus_case_update_details` | Case-detail edit through the staff Case save; an omitted value is unchanged, an empty string clears | one-command lease |
| `automation.cases` | `pegasus_case_note_add` | Add an append-only note to the Case timeline | key only |
| `automation.cases` | `pegasus_case_action` | One Case lifecycle act with an `action`: hold, release_hold, return_to_review, assign_engineer, return_to_engineer, close, reopen, archive, create_linked_replacement, create_audit, vehicle_lookup, vehicle_history_check, manual_chase, authorise_garage, undo_garage_authorisation | one-command lease |
| `automation.cases` | `pegasus_case_query` | One query act with an `action`: open (logged by its channel), mark_done, reopen, assign. No edit lease, expected version or reason. Reopen is refused on a closed or archived Case | key only |
| `automation.cases` | `pegasus_directory_search` | The active directory Repairers or Claim sources a Case links to, each with its linked Principal codes; a named Principal's linked contacts first | none |
| `automation.cases` | `pegasus_vocabulary_get` | The vocabularies the write tools accept: assessment field paths, codes, labels and writability; estimate line types and evidence labels; VAT statuses and categories; valuation sources; image tags; enabled staff by username; Sign-off Engineers by printed name; the unroadworthy wordings bank | none |
| `automation.cases` | `pegasus_work_centre_dismiss` | Dismiss a Case, Unidentified item, AI job, or a System Action's underlying receipt, image intake or query from the Work Centre | optional key |
| `automation.cases` | `pegasus_photo_link_list`, `pegasus_photo_link_get` | Photo links as the Photo capture page lists them (Direct links by scope, Standing links' captures, or one Case's links) with status, reminder due and what is missing; one link as its page shows it, with its history and the acts its page offers | none |
| `automation.cases` | `pegasus_photo_link_action` | One photo link act that sends nothing, with an `action`: issue (a new link's address to copy), resend_copy, extend, cancel, log_call | key only |
| `automation.cases` / `automation.intake` | `pegasus_edit_begin`, `pegasus_edit_renew`, `pegasus_edit_end` | Hold a Case (`automation.cases`), a Triage Case or an Image Intake record (`automation.intake`) for multi-step work; `takeOver` asks a staff holder of a Case to hand over and waits; takes over a staff Triage or Image Intake scope | explicit lease |
| `automation.intake` | `pegasus_intake_queue_list` | List intake receipts by decision and allocation | none |
| `automation.intake` | `pegasus_intake_submit` | Submit one immutable source on the automation channel | none |
| `automation.intake` | `pegasus_intake_get` | One received item as the Create case page reviews it | none |
| `automation.intake` | `pegasus_intake_action` | `resolve_inspection_address` (accept, correct or supply it), `accept` a received item as a Case, `allocate` (retry) a failed allocation, `open_triage` on a Triage request, or `register_images` as an Image Intake record | item version and key |
| `automation.intake` | `pegasus_case_create` | Create a Case or Triage Case directly | key only |
| `automation.intake` | `pegasus_unidentified_list`, `pegasus_unidentified_get` | The open Unidentified queue; one item by U-reference with its sources, history and registration readings | none |
| `automation.intake` | `pegasus_unidentified_source_download` | A retained source as native content | none |
| `automation.intake` | `pegasus_unidentified_resolve` | Resolve an item to a destination, or Close with reason | version and key |
| `automation.intake` | `pegasus_unidentified_reopen` | Reopen a resolved or closed item with a reason | version and key |
| `automation.intake` | `pegasus_unidentified_action` | `register_images` as an Image Intake record, or `dismiss_reading` a registration reading, with a reason | key |
| `automation.intake` | `pegasus_image_intake_list`, `pegasus_image_intake_get` | The Image Intake records awaiting instruction with no Case; one record as its page shows it | none |
| `automation.intake` | `pegasus_image_intake_action` | `set_principal` (record, replace or clear the known Principal) or `close` with a reason | one-command lease |
| `automation.intake` | `pegasus_upload_group_get` | A stored upload of several files with its one decision's state and the Cases its files share | none |
| `automation.intake` | `pegasus_upload_group_action` | The upload's one decision: `attach` every file to one Case, `leave` the matched Cases undecided, or `discard` it | Case version, receipt versions and key |
| `automation.intake` | `pegasus_triage_list`, `pegasus_triage_get` | Triage records; one Triage with findings, evidence, candidates and history | none |
| `automation.intake` | `pegasus_triage_source_download` | The Triage's retained origin source as native content | none |
| `automation.intake` | `pegasus_triage_await_information`, `pegasus_triage_record_finding`, `pegasus_triage_response_evidence`, `pegasus_triage_complete`, `pegasus_triage_cancel`, `pegasus_triage_reopen` | The Triage contract above | one-command lease |
| `automation.intake` | `pegasus_triage_case_link` | Link or unlink the Triage and an instruction Case; both records' versions and leases | one-command lease on each record |
| `automation.intake` | `pegasus_triage_assign`, `pegasus_triage_note_add` | Assign or unassign the Triage; add a Triage note | one-command lease |
| `automation.documents` | `pegasus_document_add` | Retain one document in Case custody, Automation-sourced, from `contentBase64` or a staged `uploadId` | one-command lease |
| `automation.documents` | `pegasus_upload_begin`, `pegasus_upload_part` | Stage one document in parts for `pegasus_document_add` or `pegasus_ai_job_complete_market_research` | none |
| `automation.documents` | `pegasus_document_download`, `pegasus_document_download_batch` | One exact document version, or 1 to 25 versions sharing the inline budget, as native content | none |
| `automation.documents` | `pegasus_document_action` | One document act with an `action`: tag, untag, set_in_report, mark_original_report, set_type, remove, retry_custody; create_image_tag changes no Case and takes no version | one-command lease |
| `automation.documents` | `pegasus_report_list` | A work's report generations and artifacts, its report approval and its report-Sent evidence | none |
| `automation.documents` | `pegasus_report_action` | Generate the report, Repair Spec or images; record report approval; link or unlink report-Sent evidence | one-command lease |
| `automation.documents` | `pegasus_report_wording_get`, `pegasus_image_preparation_get` | A work's report wording blocks with their composed sentences; each image's report place, role, rotation, crop, page of its own and preparation version | none |
| `automation.documents` | `pegasus_report_wording_save`, `pegasus_image_prepare` | Change wording blocks through the Case save; crop, rotate, order and set a page of its own for images through the Case save | one-command lease |
| `automation.assessment` | `pegasus_assessment_get`, `pegasus_estimate_list`, `pegasus_estimate_get`, `pegasus_valuation_list` | The recorded assessment surface; a Case's estimate headers; one estimate in full; the valuation cards | none |
| `automation.assessment` | `pegasus_assessment_update`, `pegasus_valuation_save`, `pegasus_estimate_save`, `pegasus_estimate_import` | The assessment, valuation and estimate writes above | one-command lease |
| `automation.assessment` | `pegasus_estimate_act` | One Repair Spec act with an `action`: duplicate or discard (each with a reason; the estimate in use cannot be discarded, and a discard deletes the estimate, so the result carries none), scale to a percentage of the Engineer's Value, remove_scaling, or restore a snapshot | one-command lease |
| `automation.assessment` | `pegasus_valuation_get` | Get valuation for one guide source, recording its card as the Valuation section's Save does | one-command lease |
| `automation.mail` | `pegasus_mail_list`, `pegasus_mail_get` | The retained mail workspace; one message with classification and history | none |
| `automation.mail` | `pegasus_mail_correct_classification` | Correct a classification to a mail category or Unclassified through the staff command | version and key |
| `automation.mail` | `pegasus_mail_link_destinations` | The Cases and Triage Cases the message may be linked to, with their versions | none |
| `automation.mail` | `pegasus_mail_link_case` | Link the message to a Case or Triage Case, unlink it, or change its linked Case, through the staff commands | one-command lease on each record |
| `automation.mail` | `pegasus_mail_open_query` | Open a query on a linked message whose report is sent | key only |
| `automation.mail` | `pegasus_mail_action` | `dismiss` or `restore` in Pegasus | key |
| `automation.mail` | `pegasus_mail_mailbox_search` | Read-only: message headers (subject, sender, received time, preview) matching a term in one or every approved mailbox; a mailbox the tenant has not admitted reports unavailable (R561) | none |
| `automation.mail` | `pegasus_mail_attachment_text` | Read-only: the text the intake reader extracted from one attachment of a retained message, by the attachment's position, cut at the caller's cap (at most 20,000 characters); null when the reader produced none; never the bytes (#1891) | none |
| `automation.mail` | `pegasus_mail_photo_registrations` | Read-only: the plate reads recorded for a retained message's photographs and the registration they agree on at the accepted bar; no image (R732 to R734) | none |
| `automation.mail` | `pegasus_old_system_folder_search` | Read-only: folder names and identities matching a term below the old system's Box folder; a reference-shaped term matches a folder name only as a whole token (R592); unavailable until that folder is configured (R561) | none |
| `automation.jobs` | `pegasus_ai_job_list`, `pegasus_ai_job_create`, `pegasus_ai_job_transition`, `pegasus_ai_job_complete_market_research` | The AI job ledger above | job version and key |
| `automation.send` | `pegasus_report_send` | Send a generated report to the Principal, as **Send report** does, with `deleteCaseCorrespondence` | one-command lease |
| `automation.send` | `pegasus_photo_link_send` | Send a new link (`send`) or a live one again (`resend`) by `email`, the Photo link staff send, or by `text` | key only |
| `automation.send` | `pegasus_mail_send` | Send e-mail with a `mode`: `chaser`, `reply`, `reply_all` (either with `markQueryDone`, `aiJobId` and `deleteCaseCorrespondence`), `forward`, `triage_reply` (with `deleteCaseCorrespondence` on the outcome reply) | Case or Triage version and key |
| `automation.glass` | `pegasus_glass_session_get` | The Automation's own Glass's session on a Case, or an Engineer's with help on, whether the Glass's login is free or which Case holds it, and with `read` the open estimate's positions and Glass's running totals, one position's figures, or Glass's calculation (R1206; [Glass's sessions](#glasss-sessions)) | none |
| `automation.glass` | `pegasus_glass_session_act` | One act with an `action`: `start` (a new estimate, or the Automation's live session on the Case continued), `finish` (save in Glass's and land a Draft repair spec), `close` (end without saving, with a reason) | `start` and `finish`: one-command lease, and `finish` the session version; `close`: session version and key |
| `automation.glass` | `pegasus_glass_catalogue_search` | Read-only: the open session's part list and special operations, each row with the value `pegasus_glass_position_act` adds it by and, for a part whose row asks, its equipment question; with `row`, that row's every answer that leads to a part (R1206); the Automation's own session or an Engineer's with help on | none |
| `automation.glass` | `pegasus_glass_position_act` | `add` a position by a row the search returned, as Glass's dialog serves it with nothing typed, only where the row is certain (R1182) or a part's equipment question is answered with `inputs.equipment`, with the dialog's own choices in `inputs`: another operation, the paint buttons, Both and optional parts (R1206); `remove` one by its id (R1185); `preview` a dialog as a form, posting nothing (R1206); the Automation's own session or an Engineer's with help on, where no Case lease is taken | session version and key; `preview` key only |

## States and transitions

The Actor changes no state of its own. Each tool moves the record it acts on
through that record's owner: Unidentified and intake in FRD-02, Triage in
FRD-03, Cases in FRD-13, AI jobs in FRD-27.

## Edge cases and fail-closed behaviour

- A foreign or oversize cursor or document request gets a non-disclosing
  failure.
- An incomplete, mismatched, foreign or expired staged upload is refused by
  the call that uses it, and nothing is written.
- A stopped automation client is refused by the kill switch before any tool
  runs ([ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md)).
  Its refused call writes one security event on every address:
  `automation_client_disabled`, and on the token address
  `automation_token_rejected`, because Stop removes the client's token
  permissions and the request is refused before the client is read
  (operator, 16 and 17 November 2026; [R1097](../rulings.md),
  [R1133](../rulings.md)).
- A lifecycle, report or document act the Case's state does not allow is
  refused with Core's reason and writes nothing; an act missing an input it
  needs is refused before any lease is claimed.
- Recording report approval sends nothing and claims nothing was sent.
- A send is Submitted, not Sent, until its Sent item is observed; an Unknown
  send is never resent, its operation key is replayed. A send whose provider
  answer is ambiguous (a server error, `408`, `429` or a timeout) answers its
  Unknown operation as a replay does, not a failed call (operator, 17 November
  2026; [R1123](../rulings.md)).
- A `pegasus_report_send` whose `attachmentNames` names one document kind
  twice is refused.
- A photo link act the link's page does not offer (extend on a cancelled
  link, cancel on a dead one, log_call on a standing capture) is refused
  and changes nothing; a resend Core refuses, on a cancelled or expired
  link, is refused with Core's sentence. A `dismiss_reading` or a `leave`
  its page does not offer is refused in the same way.
- A reply or reply all takes no recipients of its own; a chaser or forward
  without a To recipient, or with an address that is not a plain e-mail
  address, is refused and nothing is sent.
- A send with no default sender, without `fromMailbox`, is refused; a
  `fromMailbox` that is not a send-ready Outgoing mailbox, or one on a
  `forward`, is refused rather than ignored (R655).
- Mail is filed against an instruction Case; a Triage Case is answered only
  with `triage_reply`, and a cancelled Triage takes no reply.
- A stale item version or suggestion fingerprint given to
  `pegasus_intake_action` is refused and writes nothing.
- Confirm is refused for a job that is not a `Draft ready` QueryResponse or
  UnidentifiedQueuePass; those others complete through their record's own
  act.
- A subject field a job kind does not take is refused, not ignored.
- A Glass's `start` while the Automation's login is held on another Case,
  by staff or by the Automation, is refused naming that Case before Glass's
  is asked, and ends nobody's session (18 November 2026; R1178). A `close`
  while the session's work runs is refused, as the staff Close is.
- In an Engineer's session, help off, more than one session open to the
  Actor, a session no longer `Active`, provider work or another act running,
  and no window connected are each refused with their `glass.help.*` code
  before anything is sent; a session found no longer `Active` forgets its
  editor key (18 November 2026; R1170).

## Acceptance evidence

Registration, a tool schema or an endpoint file proves nothing. Each tool
tranche is proven with a real caller: success, authorisation failure,
validation failure and the resulting history line. Integration tests cover
the automation ingress over real HTTP. Deployment and live acceptance are
separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MCP-01`–`MCP-05` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-03](frd-03-triage.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md),
  [FRD-28](frd-28-guided-photo-capture.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).
- Technical constraints:
  [ADR-0011](../adr/0011-restrict-mcp-to-automation-actor.md),
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md),
  [ADR-0035](../adr/0035-ai-job-ledger.md),
  [ADR-0059](../adr/0059-native-mcp-file-content-and-consolidated-tool-inventory.md),
  [ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md),
  [ADR-0080](../adr/superseded/0080-automation-mcp-one-server-per-scope.md)
  (superseded by ADR-0087),
  [ADR-0087](../adr/0087-one-automation-mcp-server-with-per-scope-consent.md),
  [ADR-0083](../adr/0083-stage-large-automation-documents-in-parts.md).
