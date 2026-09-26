# FRD-18: Manual upload

> Owner capabilities: INT-19, UI-08 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Staff upload files on `/Upload`.
- **Add evidence** on a Case page opens Upload for that Case. The
  destination is declared before the upload: nothing is matched or sorted,
  no Unidentified item is made, and the files go straight to the Case's
  Box folder. The operator returns to the Case's Files panel.
- Limits: 100 MiB per file, 20 files and 200 MiB per request.
- An upload is one submission with one decision. Each file still reports
  its own outcome.
- A manual upload never creates a Case or attaches to one by itself. Staff
  must confirm the destination, even when exactly one Case matches. The
  declared destination of Add evidence is that confirmation, made first.
- Confirming a destination files the material on the Case in the same
  request and resolves its Unidentified item.
- Cancel changes nothing. The material stays kept and honestly labelled.

## Purpose

This document says how material gets into Pegasus by hand, through the
staff Upload page, and how staff decide where it goes. It serves the PRD
outcomes for manual intake and no automatic Case creation from manual
material.

## Behaviour

### Staff upload page

The authenticated `/Upload` route exists only where durable production
intake and Case custody exist. A production-local-only store is not accepted
custody. Without durable custody the route is absent, not offered.

The page is one surface (v30 Upload E, 25 September 2026): the picker on the
left, with the drop target, **Choose files** and the three limits under it,
and beside it the selected files, one row each with a thumbnail for an
image, its name, size and kind, and a remove control. Files chosen or
dropped in several steps accumulate into one upload; **Clear** empties it.
Before posting, the selection is checked against the same limits the server
enforces: file count, per-file size, total size, an empty file and an
unsupported type are each named. **Upload N files** posts the whole
selection once; every row reads Uploading together, because one submission
stores the whole batch and no finer signal exists, and the page moves to the
review only when the response proves the files are stored. A failed post
keeps the selection and states the failure. No mechanics words ("receipt",
"submission group" or similar) appear on the Upload or review screens.

### Upload for a declared Case

**Add evidence** on a Case page or a Triage Case page opens `/Upload` for
that Case (operator, 26 September 2026). The member of staff has already
decided where the files go, so the destination is declared before the
upload rather than chosen after it:

- The picker shows the declared Case above the files (reference,
  registration · claimant, Principal, stage) and states that the files go
  straight to it. The same card states a destination wherever one is
  stated. No Case is offered to pick. Any Case or Triage Case in any state
  may be declared; an archived or unknown Case is refused on the page and
  the picker falls back to an ordinary upload.
- The declaration travels with every file of the submission and is read
  again at post. The same upload receipt presented for a different Case is
  refused, like different bytes under one identity.
- Processing reads the files and keeps their photographs exactly as for any
  other upload, but runs no principal, route, match, OCR or registration
  work: there is nothing left to identify. It records the association as
  that member of staff's decision, in their name, and files the source, its
  documents and its photographs on the Case, straight to the Case's Box
  folder and never through holding. No Unidentified item and no Vehicle
  images record is made for it. A file that could not be read keeps that
  outcome; its original still files.
- After posting, the operator returns to the Case's Files panel with a
  one-time notice, "N files received for {reference}. They appear under
  Files once processed." A replay of the same upload says "Already received
  for {reference}." The files are in Files within seconds; while the Case
  is being edited, filing waits for the editor and retries.
- Only when the declared Case is gone or archived by the time processing
  runs does the file stay unlinked: it is then held and becomes an
  Unidentified item, from which staff link it by hand.

The review surface still exists for a declared upload (from the Intake
log): it never offers a decision, and reports **Added to Case** once the
link is recorded.

### Upload limits

| Setting | Accepted source value |
| --- | --- |
| Aggregate file bytes | 209 715 200 (200 MiB) |
| Multipart request | 200 MiB plus 64 KiB fixed overhead |
| Per-file bytes | 104 857 600 (100 MiB) |
| File count | 20 |
| Content types | `application/pdf`, `image/jpeg`, `image/png`, `…wordprocessingml.document`, `application/msword`, `message/rfc822`, `application/vnd.ms-outlook`, `video/mp4`, `video/quicktime` |

The Provider API has its own separate limits: 30 MiB decoded envelope and
42 MiB encoded request
([FRD-09](frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary)).

`IntakeEnvelopeLimits` in Core is the single owner of the manual per-file,
file-count, and aggregate ceilings. Host and ingress limits may tighten them
and may never raise them.

MP4 and MOV uploads are kept immutable as video evidence. Their extension,
declared media type, and ISO base-media header must agree. They can be
previewed in the browser where it supports the encoding and always
downloaded. Pegasus never sends video to OCR or image cropping.

Deployment configuration and dated live evidence are owned by
[operations](../operations.md), not by this policy.

### Upload confirmation surface

A grouped upload shows **one submission decision**, whether the submission
was accepted or refused, with each file's processing and outcome beneath it.
The submission decision never hides a per-file outcome.

Once a manually uploaded file's processing resolves, the operator sees an
explicit destination decision, not a passive status label. Manual upload
keeps the source and its extraction but never associates it automatically,
never allocates a Case/PO, and never treats a unique match as consent.
Mailbox and Provider routes keep their own automatic policy.

The decision table, judged against the current retained material:

1. **A Case is already associated** (`CurrentCaseId` set). This reports a
   decision already made, including a destination declared before the
   upload. The operator sees the Case reference and the existing reversal
   path. No second association mechanism is offered. The material is on
   the Case: a staff link files the source, its documents and its selected
   photographs on the Case in the same request, and resolves the material's
   Unidentified item to it ([FRD-22](frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association)).
2. **Registered as a new Image-initiated Case** (`ImageIntakeRegistered`).
   Registration is automatic for usable image identity kept pending a staff
   decision, including when a manual upload has one existing-Case match. It
   is reported with a link to its own searchable record and never re-offered
   as a manual creation; an Image-initiated Case's reference is keyed to the
   registration and cannot be hand-made without one. While the registration
   is still Awaiting instruction, the surface also offers **Add to an
   existing case** (below). That decision links the registration's origin
   receipt, which carries the Image-initiated Case through its normal merge
   ([FRD-19](frd-19-image-led-intake-and-pairing.md#pairing-and-merge)).
   Once merged, the surface reports the destination Case instead.
3. **A manual non-image file that could become a Case.** Staff must either
   confirm one existing Case or open the extracted new-Case proposal.
   A unique match is one suggestion, not a selection. The proposal is
   editable. Reject or cancel changes nothing and leaves the source
   unallocated. Accepting runs the existing allocation path and may allocate
   the Case/PO.
4. **Cannot become a Case** (could not be read, unsupported, or a technical
   failure) or **the file failed to process.** Reported plainly with its
   Unidentified item where one exists, and Open file. No offer is made,
   because none is genuine.

One upload is one submission group with one decision. Each member shows its
own read status. A member that could not be read says so beside its file
name and never vetoes the decision for the members that could be read. When
no file could be read, the upload becomes one Unidentified item with the
reason Could not be read.

Where the decision is genuinely open (rows 2 and 3) the surface carries it:

- **Add to an existing case.** The retained match candidates are shown first,
  including a sole candidate. Typed, receipt-scoped search adds every Case and
  every Triage Case, in any state, showing reference, registration, claimant,
  and stage, never an internal identifier. Staff linking is not limited by the
  Case's state; automatic association keeps its own rules
  ([FRD-22](frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association)).
  A unique candidate is never auto-selected. Selecting a Case and confirming
  takes that Case's edit lease, or a Triage Case's edit scope, and links
  through the existing staff link path, which also runs the Image-initiated
  merge where one is registered. The route and group membership are loaded
  server-side; a posted receipt id is not authority. The page operation id,
  the reviewed receipt version, and the reviewed target Case version (a Triage
  Case's Triage version) bind the decision. A typed reference first renders
  its exact target for confirmation before any write. A replay succeeds only
  for the identical committed decision (actor, target, reviewed input), not
  merely the same target. A stale version, a changed decision, a competing
  lease, an unavailable destination, or an incomplete group reports an honest
  conflict and changes nothing.
- **Cancel.** Returns to Upload and changes nothing. The material stays
  kept and its state stays honestly reported.

For a group the operator makes one submission-level choice. Completed
members must prove the same decision. Missing, elsewhere-associated, or
failed members are reported, not counted as success. Every other action on
the surface goes to a screen that already does it: the Case record, the
case-creation screen, the Image-initiated Case and Unidentified records, and
Open file.

Add to an existing case is keyboard-operable, and the active suggestion is
marked by more than colour. A search failure looks different from no
matches. A response for an earlier query cannot replace newer input.

## States and transitions

- A file row is in flight, stored, or failed.
- A submission is accepted or refused. Each member then reaches one of the
  four decision rows above.
- Rows 2 and 3 wait for a staff decision; rows 1 and 4 are reports.

## Edge cases and fail-closed behaviour

- Content-Length missing or understated: actual bytes are counted and an
  oversized body never reaches custody.
- Exactly one Case matches a manual upload: still a suggestion; staff must
  confirm.
- Group with some unreadable members: readable members get their decision;
  unreadable ones are reported beside their names.
- Stale version, competing lease, or incomplete group on confirm: a
  conflict is shown and nothing changes.

## Acceptance evidence

Acceptance proves, through the real caller: every limit in the table;
idempotent retry; the four decision rows; that confirmation takes the Case
edit lease and binds to reviewed versions; that confirmation files the
material on the Case and resolves its Unidentified item in the same
request; that a declared upload ends with the PDF and its photographs on
the Case, no Unidentified item and the operator on the Case's Files panel;
and that Cancel and reject change nothing. Deployment and live evidence
are separate tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `INT-19`, `UI-08` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-09](frd-09-provider-and-intermediary-routes.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md).
- Operations: [operations](../operations.md) for deployed limit
  configuration.

### The review's shape

A stored upload, of one file or several, is reviewed on one surface (v30
Upload E "Inspection studio", 25 September 2026). Its head reads "Upload",
when the upload was received and **New upload**. The left, wider part
inspects the files: the chosen file large (an image itself, a glyph for a
document, and beneath a document the photographs Pegasus pulled out of it,
each opening full size, with "N photographs found in this file"), its name
and **Open**, a filmstrip of every file to choose from,
and a folded list of **File names and outcomes** with each file's size,
kind and state (Received, Processing, Ready, Could not be read, Added to
Case, Discarded), opened by default when a file could not be read. The
right part carries the upload's one decision and nothing else:

- **Processing** shows a progress line, the count of stored files and
  Refresh; the page refreshes itself while any file is moving.
- **Choose a destination** lists the possible Cases as cards (reference,
  stage, registration, claimant, Principal), none selected; choosing one
  shows **Review and add to Case**, which repeats the exact target
  (reference, registration, claimant, Principal, stage) and the file count
  in a dialog before **Confirm and add to <reference>** writes anything.
  **Find another Case** (or **Find a Case** when nothing was suggested)
  searches every Case and Triage Case the upload may join, in any state;
  results are further cards, and a failed search says so. Instruction
  material also offers **Review new Case proposal**. An automatic Image
  intake registration is a subordinate record link (reference and state)
  under the decision, never a second decision; there is no manual
  registration or reason form. **Leave undecided** returns to Upload and
  changes nothing; **Discard upload** opens its own confirmation with the
  acknowledgement that the source and processing record are retained, and
  is offered only once every file has completed.
- A file that could not be read is marked on its row and in the filmstrip
  and disclosed beside the decision; its original still travels with the
  upload. When no file could be read, the decision reports it and opens the
  Unidentified item.
- **Added to Case** names the confirmed destination (reference,
  registration, claimant, Principal, stage) with **Open <reference>**;
  **Upload discarded** states that the material is retained.
- A conflict or refused confirmation is reported above the decision and
  changes nothing; the same page operation and reviewed versions are kept
  for the corrected decision.

Below 900 px the decision precedes the inspector. The handlers, the
operation id, the reviewed versions and the decisions themselves are
unchanged from the confirmation contract above.
