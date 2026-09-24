# FRD-05: Documents, extraction, and custody

> Owner capabilities: AI-04, DOC-01 to DOC-05, DOC-07, DOC-08, EXT-14, INT-10 to INT-12, INT-14 to INT-16 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Pegasus reads PDF, DOC, DOCX, EML and MSG files, keeps attached images,
  and keeps MP4 and MOV video without reading it.
- Original bytes are saved before anything is extracted from them. Macros and
  active content are never run.
- Scanned pages go to Azure Document Intelligence OCR. Corrupt, encrypted or
  readable pages never do.
- Box is where a Case's files live for good. Staging areas and caches are
  temporary and never prove custody.
- A gallery never hides an image whose custody is still in progress; it shows
  a placeholder with the custody state.

## Purpose

This document says which files Pegasus accepts, how it extracts text and
images from them, where the files are kept, and how images are read back and
tagged. Upload limits are in
[FRD-18](frd-18-manual-upload.md#upload-limits).
The Provider API envelope is owned by
[FRD-09](frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary).

## Behaviour

### Documents, extraction, and custody

#### Supported source boundary

The intake boundary covers PDF, DOC, DOCX, EML and MSG source files, attached
images, MP4 and MOV video evidence, and route metadata. Support is proved
only by the actual application caller and current evidence, never by an
imported workspace or plan.

One engine owns each readable format. PDF uses PdfPig, the only live PDF
implementation (ADR-0001, ADR-0003). DOCX uses OpenXml. EML uses MimeKit.
DOC and MSG use the CollisionDocNet-derived compound-file readers brought in
by ADR-0025, and those readers handle only those two formats. Video is kept
and labelled for review; it is never treated as readable text.

Pegasus must:

- keep the source bytes before deriving anything from them;
- parse in isolation and enforce limits on depth, count, size,
  decompression, relationships and cancellation;
- return structured text, images and provenance, with explicit partial,
  unsupported or technical-failure outcomes;
- keep the extraction engine, package, version and policy provenance;
- never run macros, active content, external relationships or embedded
  instructions;
- tell scan-like material apart from corrupt, blank, unsupported or encrypted
  material;
- keep accepted MP4 and MOV evidence without sending it to OCR or image
  cropping, offer a safe preview only for a browser-supported encoding, and
  always offer download.

#### Qualified OCR

Scan-like pages from incoming instructions go to the approved Azure Document
Intelligence `prebuilt-layout` boundary
([ADR-0047](../adr/0047-scanned-instruction-ocr-only.md)). Corrupt, encrypted
and non-renderable inputs are never sent to OCR. Pages with readable embedded
text stay on the ordinary PDF path and are neither sent nor replaced.
Estimate imports use their deterministic parsers only. A scan-like,
ambiguous or unsupported estimate is refused; there is no OCR fallback.

Each OCR operation is tied to exactly one authorised intake asset, its
content hash, its length and the selected page numbers. Pegasus keeps the
provider operation, the pinned API and model, the response hash, page
coordinates and confidence. Instruction validation still runs afterwards.
Low confidence or missing structure never silently becomes an accepted
field. A submission with no provider identity stays visible and is not
blindly repeated.

An instruction with scan-like pages from more than one retained source needs
staff review. Pegasus does not combine OCR output from separate sources.

The provider operation identity and page output are kept before instruction
analysis runs. The provider finishing is not the same as the work finishing.
If analysis fails or the receipt version conflicts, Pegasus retries against
the kept output without resubmitting pages. Completion is acknowledged only
after an analysed, no-profile or ambiguous outcome has been recorded. When
bounded retries run out, the failure stays visible and the original output
stays kept.

#### Staging and custody

Receipt and staging are one state. Accepted Case custody is another.

A follow-up message that was matched to a Case automatically, with or
without photographs, has a separate Case-filing step (operator, 23 September
2026). Its original message, attached documents and selected photographs
become Case document occurrences with intake provenance and the right
source, correspondence or image role. Case promotion
has its own stable Case, receipt and asset operation identities. A partial
write keeps its pending document identities and resumes through the normal
custody reconciliation. A confirmed replay neither duplicates files nor
repeats the readiness transition. The current association, Case eligibility
and edit authority are checked again when delayed custody completes. Failures
stay visible for normal recovery.

- Network, local or Azure staging is temporary processing storage. It never
  proves Case custody.
- Box is the required custody system for accepted Case files. Every Case uses
  its permanent reference for its Box folder, and keeps its source emails,
  instruction documents, images, correspondence and reports there.
- If Box fails after the reference is allocated, the Case stays `Not ready`
  with the failure shown and staff-started retry or recovery recorded. The
  reference is not rolled back, reused or reallocated. No background or
  automatic business retry is allowed.
- Staff may add manually received WhatsApp evidence with its source and
  channel provenance. That does not switch on a WhatsApp integration.
- Case and file changes use the normal role, lease and version guards.
  Completed does not lock correspondence or files; a query on a Completed
  Case follows
  [FRD-13](frd-13-case-lifecycle-and-workflow.md#completed-and-query). Box
  offers no way around application authorisation.
- Default local development must not change any Outlook mailbox or Box
  location. The separately approved Box integration-test profile, and
  explicitly approved non-production test deployments, may create and update
  controlled non-corpus files only in the approved disposable test subtree
  recorded in
  [operations](../operations.md#approved-box-integration-test-target). They
  must not delete, move, copy or share Box content. Outlook tests use
  immutable local copies or an explicitly approved test mailbox and
  operation.
- A custody transition records the source identity, content hash, target
  identity and version, actor or caller, time, and failure or retry state.
  It never deletes the source proof early.

An incoming custody claim uses the occurrence's operation identity to find
its own source record. Intake claims update the matching receipt and asset
pair directly; they do not probe unrelated source records or need wider
Worker permissions. Receipt and asset GUIDs are typed identities, never matched by
filename, source label or formatted string. The designated Box holding
folder is only for intake whose destination is not settled automatically
(operator, 23 September 2026): Unidentified and refused material, Triage,
a manual upload, a failed or suppressed allocation, and material a member of
staff links later. Intake that automation files to a new Case, to a matched
Case or to a Vehicle images record goes straight to that destination's Box
folder and never through holding. The holding decision is taken after
destination automation; a held source, its documents and its selected
photographs each carry verified content and confirmed file and version IDs.
Each intake asset records the Box folder its confirmed copy is in (holding,
the Case root or the Vehicle images folder), and reads expect exactly that
folder. Unknown or pending holding custody is unfinished work, not success.
The existing bounded retries reuse the same asset and operation identities.
Exhausted failures stay visible for staff recovery. Re-evaluation reads the
retained source bytes after checking their hash and length. Missing or
corrupt bytes fail closed.

A Vehicle images record has its own Box folder from registration. The folder
is named for the record's permanent Image reference, sits directly under the
approved custody root, and keeps every registered photograph and its source
PDF in stored order. Each file is identified by its asset, so several
photographs from one PDF cannot collide. The storage is queued work behind
the registration. A Box failure never blocks or rolls back a registration or
a merge. The images stay authoritative in intake source retention
throughout, and the queued work re-arms itself with bounded backoff on
dependency failures before it records a terminal failure honestly on the
record. When the record merges into a formal Case
([FRD-19](frd-19-image-led-intake-and-pairing.md#pairing-and-merge)), the
folder's contents move into that Case's Box custody, at the Case root's
image evidence location, and the emptied folder is removed. The removal is
non-recursive, so unexpected content makes the fold fail closed instead of
being destroyed. The record's lifecycle state and merge or closure history
stay in SQL whatever happens to custody.

### Custody and staging distinctions

Box is durable file custody. Azure processing bytes and the 24-hour idle
cache are temporary. SQL keeps the arrival, idempotency and provenance
identities. Receipt, logical access and definitive association are three
separate claims. A temporary file or a cache hit never establishes an
accepted Case association.

A secondary Audit folder nests under its original Inspection folder. It is
never a sibling of that folder. Image-origin references stay distinct from
formal Case identity while their custody is resolved.

### Custody and derived reads

Box owns durable Case-document custody. Intake staging keeps the original
bytes until the handover to custody is verified. SQL keeps identity, version
and provenance. A derived image or cache copy serves processing or
presentation and can be rebuilt from the kept source; it is never a second
custody owner. Reads go through the logical occurrence and version interface,
so the current authorisation and exact version checks apply whatever the
physical source. Staging is not erased before verified handover, and no new
store is invented for this separation (ADR-0045).

A linked Audit Case created from an Inspection + Audit Case shares the
original's documents by reference to the same stored bytes; nothing is
copied. Its custody root is the `a.` subfolder Pegasus creates under the
original Case's Box folder when the Audit Case is created. Afterwards it is
found through the stored relationship, never from the reference prefix
([ADR-0051](../adr/0051-linked-audit-case-identity-and-custody.md),
[FRD-01](frd-01-case-identity-and-lifecycle.md)).

An inline image preview is served through this same cached content path,
never as an audited download. Every read re-verifies the source content hash
and serves only confirmed custody. A cache entry is addressed by content
hash, so it is immutable and safe to share. A smaller thumbnail serves
galleries and tiles; the full image serves the viewer and crop. No gallery
omits an image whose custody is still in flight or has failed. It shows a
placeholder naming the file and stating its custody state until custody
resolves. The Case's own Images tab draws the same placeholder. It is built
from `CaseFiles.Current`, every current image occurrence that has not been
logically removed, whatever its custody state, and offers the thumbnail,
viewer link, tags and Crop only on Confirmed ones. The Documents tab lists
the same set, each row stating its custody, and offers Preview and Save as
only where the bytes are held.

Unidentified and Vehicle images records likewise show selected photographs
whatever the source file's media type: the photo count, thumbnails or
custody placeholders, the VRM outcome, and the original PDF as a separate
file. A known file whose custody is unconfirmed returns an explanatory
availability response on direct access, not a generic not-found page. No
download bypasses confirmed custody by serving staging bytes. An unknown
identity still returns not-found.

### Image tags

Image tags classify Case images. They are separate from Box and SQL file
custody. The shared vocabulary has `Name`, `Colour` and `IsBuiltIn`. A join
records which tag sits on which image occurrence, with `AppliedBy`,
`AppliedAtUtc` and the operation key. The seeded, non-deletable tags are
Overview, Close-up, Third party and Reflection. An authorised staff member
may add a custom tag of up to 40 characters, unique against every existing
name ignoring case, with one of six fixed design tints. An occurrence may
carry any number of tags.

Applying or removing a tag on a Case image has the same guards as any other
Case change: the current Case edit lease, the expected Case version and an
operation key for replay. It bumps the Case version, so the tag is on the
record's timeline. Adding to the shared vocabulary takes no lease and no Case
version, because it is not a Case fact. It needs the casework right and an
operation key. A duplicate name, compared ignoring case, is refused rather
than creating a second entry.

Third party replaces the former one-way `ThirdPartyVehicleConfirmedAtUtc`
flag and keeps its EVA-exclusion behaviour
([FRD-07](frd-07-eva-and-external-engineering-handoff.md#eva-handoff-routes)).
The migration that introduced tags turned every recorded confirmation into a
Third party tag on the same occurrence, keeping its original moment, actor
and operation key, then dropped the three flag columns and their index.
There is no way back from a tag to the flag.

## States and transitions

| Thing | States | Notes |
| --- | --- | --- |
| Source file | Received and staged → destination custody (Case, Vehicle images folder, or holding while no destination is settled) | Staging is never proof of custody |
| OCR operation | Submitted → output kept → analysed, no-profile or ambiguous; or visibly failed | Retries reuse the kept output |
| Custody of one file | Pending → Confirmed; or Failed | Galleries show the state until Confirmed |
| Vehicle images folder | Queued → written; folded into a Case on merge | A Box failure never blocks the record |

## Edge cases and fail-closed behaviour

- Corrupt, encrypted or non-renderable input is never sent to OCR.
- A scan-like or ambiguous estimate is refused with no OCR fallback.
- A Box failure after allocation leaves the Case Not ready; staff retry it.
- Missing or corrupt retained bytes fail closed during re-evaluation.
- Unexpected content in a Vehicle images folder makes the fold fail closed.
- Unconfirmed custody returns an availability response, never staging bytes.
- A duplicate tag name is refused.

## Acceptance evidence

Core tests cover the format boundary, limits, OCR binding and tag rules.
Integration tests cover custody claims, holding only for unsettled intake,
direct filing to the Case and Vehicle images folders, thumbnails
and placeholders, and the Box integration-test profile against the approved
test subtree. Deployment and live acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `AI-04`, `DOC-01`–`DOC-05`, `DOC-07`, `DOC-08`, `EXT-14`,
  `INT-10`–`INT-12`, `INT-14`–`INT-16` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md),
  [FRD-09](frd-09-provider-and-intermediary-routes.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-18](frd-18-manual-upload.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md).
- Technical constraints: [ADR-0001](../adr/0001-hybrid-pdf-extraction.md),
  [ADR-0003](../adr/0003-pdfpig-for-first-qdos-slice.md),
  [ADR-0025](../adr/0025-integrate-renderer-and-extractor-into-the-application.md),
  [ADR-0045](../adr/0045-document-custody-and-derived-caches.md),
  [ADR-0047](../adr/0047-scanned-instruction-ocr-only.md),
  [ADR-0051](../adr/0051-linked-audit-case-identity-and-custody.md).
