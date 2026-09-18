# FRD-18: Manual upload and upload links

> Owner capabilities: INT-19, INT-31, UI-08 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Staff upload files on `/Upload`. Staff can also create a public upload
  link for one request; the link shows a form and nothing else.
- Limits: 100 MiB per file, 20 files and 200 MiB per request, links last
  7 days, and a link's upload session is a fixed 15 minutes.
- An upload is one submission with one decision. Each file still reports
  its own outcome.
- A manual upload never creates a Case or attaches to one by itself. Staff
  must confirm the destination, even when exactly one Case matches.
- Cancel changes nothing. The material stays kept and honestly labelled.

## Purpose

This document says how material gets into Pegasus by hand, through the
staff Upload page or a public upload link, and how staff decide where it
goes. It serves the PRD outcomes for manual intake, isolated public upload,
and no automatic Case creation from manual material.

## Behaviour

### Staff upload page

The authenticated `/Upload` route exists only where durable production
intake and Case custody exist. A production-local-only store is not accepted
custody. Without durable custody the route is absent, not offered.

Selected files render one row each: name, size, and a per-file state. The
state is a spinner while the submission is in flight and a tick once the
response confirms the file is durably stored. A failed file states its
failure on its row. Every row enters the in-flight state together, because
one submission stores the whole batch and no finer signal exists. No row is
ticked before the response proves it. No mechanics words ("receipt",
"submission group" or similar) appear on the Upload or status screens.

### Request-scoped upload links

Only authenticated staff may create a link. Each link has a stable identity
and is bound to exactly one upload request, one allowed operation, and a
server-enforced expiry. The token is a cryptographically generated 256-bit
value kept only as a hash. It is security-sensitive: it is never written to
business history, diagnostic logs, or content-bearing telemetry. Staff may
send the link to its intended recipient in a chaser; that is not permission
to expose it anywhere else. Revoking a link refuses every later request. An
unauthenticated caller cannot extend expiry.

The public page shows only the request's upload fields and an immediate
structured success or failure. It shows no Case or reference identity, no
request or history state, no other document, no token management, no
external account, and no cross-request lookup. An accepted upload means only
that the request-local custody boundary succeeded. It is not Case creation,
Box custody, EVA handoff, report generation, or external delivery.

A link starts a fixed, non-sliding 15-minute submission session when it
first accepts a file. Failed attempts before that first success do not start
the session. During the session the requester may add or replace files.
Finalising, or the session expiring, closes the link; later bytes are
refused without disclosing the Case or earlier submissions. Idempotent
retries return the same result.

Public POST admission checks the route-bound token before reading the form.
An unavailable link refuses the body without buffering it. File and
total-body transport bounds apply before antiforgery and multipart model
binding, with a finite allowance for normal multipart overhead. Enforcement
counts actual bytes, including requests with a missing or understated
Content-Length, and a refused oversized body never reaches custody. Form or
query values cannot substitute a different token for the one in the route.

Every attempt returns the same bounded result classes and never reveals
whether another request, Case, reference, or file exists. This in-house
route replaces Box File Request.

Acceptance gates for links: file type, count, and size limits; staff
authentication for creation; expiry and revocation; idempotent retry; abuse
handling; durable custody; cross-request isolation; and non-disclosing
errors, all proved through the real caller.

### Source upload limits

| Setting | Accepted source value |
| --- | --- |
| Aggregate file bytes | 209 715 200 (200 MiB) |
| Multipart request | 200 MiB plus 64 KiB fixed overhead |
| Per-file bytes | 104 857 600 (100 MiB) |
| File count | 20 |
| Link lifetime | 168 h (7 days) |
| Submission session | Fixed, non-sliding 15 minutes |
| Rate, per token | 20 per 10 minutes |
| Rate, per address | 30 per minute |
| Content types | `application/pdf`, `image/jpeg`, `image/png`, `…wordprocessingml.document`, `application/msword`, `message/rfc822`, `application/vnd.ms-outlook`, `video/mp4`, `video/quicktime` |

The Provider API has its own separate limits: 30 MiB decoded envelope and
42 MiB encoded request
([FRD-09](frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary)).

`IntakeEnvelopeLimits` in Core is the single owner of the manual and public
per-file, file-count, and aggregate ceilings. `RequestUploadLimits` may
tighten them for a configured estate and may never raise them. A public
upload session reserves capacity before custody, admits at most one current
successor for a replacement occurrence, and expires after its fixed
15-minute window. Expiry, revocation, limit-version mismatch, and capacity
refusal are typed outcomes; none allows a fresh upload.

The per-address limiter is still needed because an unknown token is refused
before the token-partitioned limiter runs, while Razor's antiforgery
handling may otherwise buffer the multipart request first. It runs after
routing and before endpoint execution.

MP4 and MOV uploads are kept immutable as video evidence. Their extension,
declared media type, and ISO base-media header must agree. They can be
previewed in the browser where it supports the encoding and always
downloaded. Pegasus never sends video to OCR or image cropping.

Deployment configuration and dated live evidence are owned by
[operations](../operations.md), not by this policy. An existing link is
bound to the limits version and lifetime recorded when it was issued. A
mismatch fails closed; staff then issue a new link through the Case action.

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
   decision already made. The operator sees the Case reference and the
   existing reversal path. No second association mechanism is offered.
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
   confirm one viable existing Case or open the extracted new-Case proposal.
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

- **Add to an existing case.** The retained match candidates are shown
  first, including a sole candidate. Typed, receipt-scoped search adds only
  current viable Cases, showing reference, registration, claimant, and
  stage, never an internal identifier. A unique candidate is never
  auto-selected. Selecting a Case and confirming takes that Case's edit
  lease and links through the existing staff link path, which also runs the
  Image-initiated merge where one is registered. The route and group
  membership are loaded server-side; a posted receipt id is not authority.
  The page operation id, the reviewed receipt version, and the reviewed
  target Case version bind the decision. A typed reference first renders
  its exact target for confirmation before any write. A replay succeeds
  only for the identical committed decision (actor, target, reviewed
  input), not merely the same target. A stale version, a changed decision,
  a competing lease, an unavailable destination, or an incomplete group
  reports an honest conflict and changes nothing.
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

- A link is active, finalised, expired, or revoked. Only active links
  accept bytes. A session starts on the first accepted file and lasts 15
  minutes.
- A file row is in flight, stored, or failed.
- A submission is accepted or refused. Each member then reaches one of the
  four decision rows above.
- Rows 2 and 3 wait for a staff decision; rows 1 and 4 are reports.

## Edge cases and fail-closed behaviour

- Unknown, expired, or revoked token: refused before the body is read,
  with a result that discloses nothing.
- Content-Length missing or understated: actual bytes are counted and an
  oversized body never reaches custody.
- Limits version changed since a link was issued: the link fails closed and
  staff issue a new one.
- Exactly one Case matches a manual upload: still a suggestion; staff must
  confirm.
- Group with some unreadable members: readable members get their decision;
  unreadable ones are reported beside their names.
- Stale version, competing lease, or incomplete group on confirm: a
  conflict is shown and nothing changes.

## Acceptance evidence

Acceptance proves, through the real caller: every limit in the table;
token expiry, revocation, and cross-request isolation; the fixed 15-minute
session; idempotent retry; the four decision rows; that confirmation takes
the Case edit lease and binds to reviewed versions; and that Cancel and
reject change nothing. Deployment and live evidence are separate tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `INT-19`, `INT-31`, `UI-08` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-09](frd-09-provider-and-intermediary-routes.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md).
- Operations: [operations](../operations.md) for deployed limit
  configuration.
