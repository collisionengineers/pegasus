# FRD-19: Image-led intake and pairing

> Owner capabilities: INT-13, INT-27, INT-28, INT-32 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Vehicle images that arrive without an instruction, but with a readable
  registration, become an Image-initiated Case with an Image reference.
  It is searchable and waits for an instruction. It is never a formal
  Case/PO.
- A multi-image submission is judged as one group. The group, not any one
  image, gets one outcome: attached to a Case, registered as one
  Image-initiated Case, or one Unidentified item.
- When exactly one eligible Case carries the registration, the images
  attach automatically. Manual uploads are the exception: staff must
  confirm even a single match among the Cases that existed when the images
  registered. A Case created afterwards pairs automatically.
- An Image-initiated Case merges into one formal Case, or staff close it
  with a reason. Nothing is deleted or reused.
- It falls due for chasing after the same interval as a Not ready Case
  (1 to 365 whole days, default 7), but has no hold, no stop, and no
  chaser text.

## Purpose

This document says what happens to vehicle images that arrive before, or
without, an instruction. It serves the PRD outcomes for image-led work with
a provisional registration-based identity, automatic pairing with the
instruction when it arrives, and no invented Case identity.

## Behaviour

### Image-initiated Case projection

Image-only material with a usable registration creates a searchable
Image-initiated Case with an Image reference. It is not Unidentified just
because it has no instruction or accepted Principal. A usable registration
is either a staff-confirmed registration or an automatic read that meets the
accepted recognition bar; the bar is owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis).
Image material without a usable registration enters Unidentified with a
reason ([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference)).
Image-only material a member of staff has classified as images received
([FRD-08](frd-08-email-mailbox-and-background-processing.md#settled-mailbox-taxonomy-and-correction))
may be registered by staff from the message record or the Unidentified item
while no Case holds it; automation never widens to it.

An Image-initiated Case is a separate, image-first lifecycle projected over
the ImageIntake record. It never allocates a Principal, a Case/PO, or a
formal Case row. Its reference stays visible after it is merged into one
eligible instruction-initiated Case. Merge and staff closure are named,
reasoned history events: the formal Case's history shows the merged
reference, and the image record shows its formal Case target.

A usable registration settles into one of two outcomes the operator sees:

- It matches no existing Case. The Image-initiated Case is the visible,
  searchable, Awaiting instruction record until something changes it.
- It matches exactly one eligible Case at registration time. The reference
  is still allocated, but the automatic merge runs in the same pipeline
  pass, so the operator finds the images already attached to that Case as
  evidence and the Image reference kept as linked history rather than an
  open record.

Every association, reversal, or correction records the same attributable
relationship evidence without closing or creating a Case. The Case, its
Case/PO, the Image reference, the source relationships, and the chronology
all stay intact.

### Pairing and merge

An Image-initiated Case stays Awaiting instruction until it is associated
with a Case. Automatic association needs exactly one eligible, pre-report,
instructed Case, an unambiguous normalised registration match and no
contradictory identity evidence; a Case that has delivered its report is not
eligible for it. Otherwise an authorised staff member makes the decision.
Staff "Link to case" reaches every Case in any state and every Triage Case,
refusing only an archived Case (operator, 24 September 2026), and the merge
completes there.

Association keeps both permanent identities and both source histories. The
instructed Case/PO is the only formal Case identity; the Image reference is
linked history. On a unique match the Image-initiated Case becomes Merged
into Instruction-initiated Case. If no instruction ever arrives, staff may
record a permanent Staff-closed outcome with a reason. No identity, source
fact, or relationship event is reused, rewritten, or deleted.

The record's photographs become Case images when the merge's fold completes
(operator, 27 September 2026). The fold moves the record's folder into the
Case folder
([FRD-05](frd-05-documents-extraction-and-custody.md#staging-and-custody))
and files each photograph it moved as an image document of the Case. A
file the record was registered from that is not a photograph, such as the
PDF the photographs came in, is filed as a Case document beside them
(operator, 28 September 2026). Each takes the next Case document number. Each is in the report, as any new
image is. The crop, rotation and tags staff gave a photograph on the record
come with it, and one tagged Third party or Reflection arrives out of the
report. A Triage Case takes them the same way. The record keeps its
reference, its page and its photographs. The fold files them as the system
worker and holds no staff lease
([FRD-14](frd-14-record-edit-leases.md#record-edit-scopes)). It yields to a
member of staff editing the Case: it records nothing, leaves their lease,
and retries after they finish
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). A replayed fold
files nothing twice.

Filing them completes the Case's images. A Not ready Case with nothing else
missing then moves to Review
([FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)). A
Triage Case records no image completeness.

Pairing uses the Case's current accepted registration and Principal, not
its original instruction draft. A known Principal is a hard scope on the
candidates, as an established Principal is on the mail path
([FRD-09](frd-09-principal-and-intermediary-routes.md#accepted-principal-email-routes-and-automatic-association)):
only that Principal's Cases are candidates, and they are chosen before any
uniqueness count (operator, 28 September 2026). Two Principals' Cases with
the same registration are then no tie. An unknown Principal leaves every
eligible Case as a candidate. A registered image identity needs an exact
registration match among the candidates. The single-image
exact-match precedence also applies to one-member groups. Multi-member
groups keep the stricter complete-candidate uniqueness rule, counted among
the same candidates. Persisted
expected membership, not the presence of a group identifier, tells the two
apart. Registration itself has no known Principal, so it scopes nothing.

The record's page lists every eligible Case for the registration, filtered
by nothing, and names each one's Principal. Staff may link to any of them: a
staff decision is a reasoned override and stays authoritative. When
automation is withheld for a Principal or an ambiguity, the page says why:
the recorded Principal has no eligible Case with the registration, or more
than one Case could match. Manual-upload material waiting on staff over
Cases that existed at registration (below) has no such notice. The reason is
computed from the same policy, and nothing is stored. This is not a Triage,
Unidentified or conflict outcome; the record stays Awaiting
instruction ([FRD-22](frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association)).

Manual-upload image material needs explicit staff confirmation even for one
eligible match among the Cases that existed when it registered: those are the
destinations the upload offered
([FRD-18](frd-18-manual-upload.md#upload-confirmation-surface)). Its Image
reference may be registered automatically. A Case created after it registered
pairs with it under the automatic rules above, on the Case's acceptance, on
the reconciliation timer and on replay (operator, 28 September 2026). A Case
that existed and a later Case carrying the same registration are not a unique
match. Once staff have linked or unlinked any member of a manual group, the
group keeps their per-member decisions and gains no automatic link.

Both arrival orders, a registered-receipt replay, and an acceptance replay
resume the same pairing operation. The reconciliation timer retries the
oldest eligible Awaiting instruction records, including linked-but-unmerged
ones. Current non-matches, including a manual record whose only match existed
when it registered or whose staff decision is unfinished, neither consume
that bounded batch nor become permanently excluded. Failures stay visible and
do not stop unrelated pairings.

Every current image member must be associated before a group merges, and a
group merges once. Automatic writes recheck current identity and uniqueness
in their transaction. Merge rechecks every current association, an automatic
destination's eligibility, and the active Case edit lease
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). A deliberate
staff unlink or reassignment is never undone by recovery. A still-current
reasoned staff association keeps its authority, including an intentional
identity override, whoever retries.

A reasoned staff decision on the origin of a non-manual registered group
also authorises completion of its untouched image members. That completion
checks the current origin decision and its observed version, records the
originating staff identity, reason, and version with SystemWorker completion
attribution, and never overwrites or revives a sibling that has association
history. Final merge refuses a changed origin decision even when the target
Case is unchanged. Manual groups staff have started keep the confirmation's reviewed
per-member decisions, and reconciliation may finish their merge only after
every member is associated.

### Age and chase state

Each half of a pairing keeps its own chronology: the instruction side's
opened or received time, and the Image-initiated Case's own
`RegisteredAtUtc`. Both already show on their queue rows. No relative "age"
figure is computed or shown anywhere, so none is added for either half.

While an Image-initiated Case is Awaiting instruction, its chase-due state
is a derived read, not a stored schedule. It is due once `RegisteredAtUtc`
has stood for the configured chase interval, the same one global
whole-calendar-day value (1 to 365, default 7) that a Not ready Case's first
chase uses ([FRD-13](frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing)).
Before that it is not due. There is no held or stopped state and no chaser
draft for the image half; those exist only on a formal Case.

Pairing completion is visible in three places: the derived `Associated with
Case` label wherever the origin receipt's Case association is shown, the
merge event on the resulting Case's timeline the moment it happens, and the
Work Centre. There is no personal notification for pairing.

When early images are paired with a pre-report Case, the Work Centre shows
the Case as a **Vehicle images paired** Needs attention item, so staff see the
job is ready (operator decisions, 18 and 28 September 2026). It is due at the
pairing. It leaves when a member of staff next changes the Case; a note, a
preview, a download or system work does not count
([FRD-15](frd-15-work-centre-queues-and-search.md#work-centre)).

### Grouped image-intake routing

A retained vehicle image either shows a readable registration that matches
an existing eligible Case, in which case every image in its group attaches
to that Case as evidence, or it shows a readable registration that matches
no Case, in which case it starts an Image-initiated Case. A multi-file image
submission from manual upload or the eligible mailbox route below is one
evidence group, not independent images. A damage close-up with no visible
plate must not detach from the overview image submitted with it. The group,
never an individual image, is what reaches an association, a registration,
or an Unidentified outcome.

For manual upload, staff confirmation replaces automatic attachment:
matching supplies suggestions, usable image identity may register, and the
group waits for one explicit staff destination even when exactly one Case
matches. Only a Case created after the group registered pairs with it
automatically ([Pairing and merge](#pairing-and-merge)). The precedence below
applies to non-manual routes. Recognition,
complete membership, and fail-closed source-identity rules apply to both.

- **PDF and mailbox photograph entry.** Otherwise-unrouted standalone PDFs
  and mailbox receipts containing selected photographs use the same image
  lifecycle as direct image uploads. An established instruction, report,
  Case, or Triage route takes precedence. OCR and technical failures keep
  their own outcomes. The original PDF, the selected photographs, and for
  email the original message stay on their parent receipt with one
  destination; no child image receipts are made. Manual-upload confirmation
  still applies even when one Case matches. No readable registration, or
  conflicting ones, produces one Unidentified item holding the PDF and its
  photographs. Completed historical mail is not backfilled automatically.
- **Mail for an established Case.** A mailbox receipt already
  associated with one eligible pre-report Case files its original message,
  attached documents, and selected photographs on that Case, with or without
  photographs (operator, 23 September 2026); nothing it files is also kept in
  the holding folder. This route
  needs no further instruction, no readable registration, and no Image
  reference. The receipt keeps its classification and source identity.
  Linking alone does not prove the files reached Case custody. Promotion
  rechecks the current association, Case version, and edit authority,
  yields to a staff editor, and resumes through existing intake and custody
  work. Re-evaluating an existing association uses the same document
  operation identities.
- **Photograph selection.** One Core policy selects direct image evidence
  and embedded PDF photographs before separate asset retention. Inline and
  signature graphics are excluded. A full-page raster on a page with almost
  no text is read by its colour first
  ([ADR-0061](../adr/0061-ocr-every-scanned-document-page.md)): mostly
  paper-white is a scanned document page and produces no embedded image at
  all; anything else is a photograph that fills the page and is an ordinary
  embedded image. Photos laid out with margins or several to a page are
  selected as before. Embedded images need at least 40,000
  encoded bytes and, when dimensions are known, a longest-to-shortest side
  ratio below 3. Unknown dimensions keep the size-based selection. These are
  image heuristics, not a logo classifier. Repeated photograph content is
  shown and recognised once while provenance is kept. Excluded document art
  stays only inside its retained original.
- **Membership and completeness.** A group's member count is fixed at
  submission and never inferred from how many members happen to be stored.
  Routing runs only once every declared member is present and every present
  member's image evidence has a terminal recognition outcome. A suggestion,
  no readable result, an unavailable dependency, and a technical failure
  all count as terminal; an empty or still-processing result does not. A
  short or non-terminal group reaches no decision and is re-evaluated as
  members complete.
- **Non-image members are excluded.** A batch may mix vehicle images with
  other material from the same request, such as an instruction document.
  Only image-only members contribute to recognition and routing. A
  non-image member still counts toward the declared membership check, but
  it is never scanned for a registration and never blocks the image
  members' decision.
- **Distinct registration aggregation.** Only reads at or above the
  accepted automatic recognition bar count. The decision inspects the
  distinct set of accepted, normalised registrations across every selected
  image asset in every image member, never one preferred read per PDF or
  one member in isolation.
- **Associate-or-hand-off precedence, in this order:**
  1. Any image member's recognition ended in a technical failure or an
     unavailable dependency: the group fails closed to a named technical
     outcome. Nothing is associated or registered while any member's
     evidence is unreliable.
  2. Exactly one distinct accepted registration across the group, and
     exactly one eligible pre-report instructed Case carries it: every
     member associates to that Case as evidence, under the same unambiguous
     normalised-match rule (including its confirmed-registration completion
     for a one-character-missing read) that governs single images.
  3. Exactly one distinct accepted registration, but zero or more than one
     eligible Case carries it: the registration is usable but not uniquely
     matched. The group registers as **one** Image-initiated Case with
     exactly one Image reference for the whole group, never one per member.
     Every member's receipt and evidence records against that one
     registration, and none associates to any Case.
  4. Zero distinct accepted registrations, or more than one: no single
     usable identity exists. The whole group stays together as one
     `Unidentified` item. No registration-based reference is made for it and
     no member is split off. The routing outcome tag `conflicting_vrms` maps
     to the `ConflictingIdentification` reason.
- **Fail-closed is a group property.** Rows 3 and 4 are handled differently
  but both withhold association. A per-member candidate search run while
  registering that member must never resolve an ambiguity the group did not
  resolve. If the group's eligible-Case count for its one registration was
  zero or more than one, no member may associate, even where a member-level
  search could pick one by exact match. The group's decision is the sole
  authority.
- **Recognition is idempotent per image.** A group may be re-evaluated more
  than once (a sibling arriving, a replay). An image whose recognition
  outcome is already recorded is never re-scanned; the recorded outcome is
  reused, so each image is recognised once however many times its group is
  evaluated.

### Operator surfaces

**Awaiting instruction queue.** On Cases, Awaiting instruction lists the
Image-initiated Cases still waiting for an instruction, never formal Cases.
Rows show reference, registration,
image count, custody, received, source, and chase facts; `?tab=` selects
the queue. Not ready holds only formal instructed Cases. Selecting a row
shows a quick detail with the definition list, the open action, and **Add
to an existing case**. The operator types a Case reference and presses the
button once: that one press finds the Case and adds the image to it. There
is no second confirm step on this page. If no single Case matches, or the
add cannot be made, the page says so, keeps the typed reference and changes
nothing. Pressing the button again after it worked also says no Case
matched, because the image was already added; it adds nothing twice.
A submission of several images is handled on its own submission
page, where the review dialog applies
([FRD-18](frd-18-manual-upload.md)).

**Image record page.** The Image-initiated record shows its image gallery
with the preserved filenames and group evidence, its custody, and its
chronological merge and closure history. It shows **Open Triage** when a
Triage Case shares the same origin receipt. There is no "Open in Box"
action, because this UI exposes no Box destination for Image-initiated
material; that is a known limitation. Staff closure is a reasoned action.
Terminal records are read-only. There is no generic Close control. Editing
the record takes a record-scoped edit lease
([FRD-14](frd-14-record-edit-leases.md#record-edit-scopes)).

**Search.** Image-initiated Cases are searchable by their Image reference or
registration and use the named states Awaiting instruction, Merged into
Instruction-initiated Case, and Staff-closed.

**Crop and tag.** Images on an image record or an Unidentified item carry
the same stored crop, rotation, and tags as Case images, editable there with
the casework right and the image's own version. A Triage Case's page offers
no crop and no tag
([FRD-15](frd-15-work-centre-queues-and-search.md#the-triage-case-page)).
The viewer offers Crop (Apply, Clear, Cancel) and the Tag select; the tile
shows the cropped region with a Cropped badge and its tag chips; the viewer
draws the recorded region over the original. A pre-Case image tile is a
rendering of the image at tile size, whether or not a crop or rotation has
been recorded; the viewer and Open file still read the original. On the Case
Files page, an unmerged photo with a recorded crop or rotation still shows the
original. When the image becomes a Case document, the crop, rotation, and tags
travel with it.
That is when a Case is created from its message, when a record merges into a
Case, and when a linked message's photographs are filed on its Case.

**Screen words.** Screens say "Vehicle images" and "Image reference"; the
word "intake" appears only on the Administrator's Intake log tab
([CONTEXT](../../CONTEXT.md)).

## States and transitions

- An Image-initiated Case is Awaiting instruction, Merged into
  Instruction-initiated Case, or Staff-closed. Merge and closure are
  reasoned history events. Merged and Staff-closed records are read-only.
- A group is incomplete, awaiting recognition, or decided. A decided group
  is associated, registered, or Unidentified.
- Chase-due is derived from `RegisteredAtUtc` plus the configured interval
  while Awaiting instruction.

## Edge cases and fail-closed behaviour

- One member's recognition failed technically: the whole group fails
  closed to a technical outcome.
- One registration, two eligible Cases: registered as one Image-initiated
  Case, not attached.
- Two different valid registrations in one group: one Unidentified item,
  reason Conflicting identification.
- Manual upload with one matching Case that existed when it registered:
  waits for staff.
- Manual upload, then a Case created afterwards is its one match: pairs
  automatically.
- Manual upload with a Case that existed and a later Case both matching:
  waits for staff.
- A manual group staff have started: gains no automatic link.
- Merge meets a live Case edit lease: yields and retries.
- The fold meets a live Case edit lease: records nothing, leaves the lease,
  and retries after the editor finishes. Files it had already moved stay in
  the Case folder.
- A photograph the record's folder never held: not filed by the fold.
- The fold is replayed: each photograph is a Case image once.
- Origin decision changed since a member's completion was authorised: the
  final merge refuses.
- A sibling with association history is never overwritten or revived.

## Acceptance evidence

Acceptance proves, through the real Worker and Web callers: one reference
per group; the four precedence rows; the group-level fail-closed rule; the
manual-upload confirmation exception, and a manual upload pairing with a Case
created after it on acceptance and on the timer; both arrival orders and
replays resuming one pairing operation; the merge on the Case timeline; the
Vehicle images paired item appearing and leaving; merge and the fold each yielding to a live
lease; a merged record's photographs as Case images with their crop and
tags, on a Case and on a Triage Case; those photographs completing the
Case's images; the derived chase-due read; and the three named states in
search. Deployment and live
evidence are separate tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `INT-13`, `INT-27`, `INT-28`, `INT-32` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-03](frd-03-triage.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-18](frd-18-manual-upload.md).
- Technical constraints:
  [ADR-0029](../adr/0029-image-initiated-case-projection.md).
