# FRD-05: Documents, extraction, and custody
> Owner capabilities: DOC · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Documents, extraction, and custody

### Supported source boundary

The intended intake boundary covers PDF, DOC, DOCX, EML, and MSG source material, attached images, and MP4/MOV video evidence plus route metadata. Current support is proved only by the actual application caller and current architecture/evidence, not by an imported workspace or plan. One engine owns each readable document format: PDF stays on the PdfPig path (ADR-0001/ADR-0003 — the only live PDF implementation), DOCX on OpenXml, EML on MimeKit, and DOC/MSG on the CollisionDocNet-derived compound-file readers integrated by ADR-0025 and scoped to those two formats. Video is retained and labelled for review, never treated as readable document text.

Pegasus must:

- preserve source bytes before deriving content;
- isolate parsing and enforce depth, count, size, decompression, relationship,
  and cancellation limits — source upload is bounded by the accepted 100 MiB
  per-file, 20-file and 200 MiB aggregate limits in
  [FRD-02](frd-02-intake-and-source-identity.md#source-upload-limits), while
  the Provider API envelope stays at 30 MB and is owned by
  [FRD-09](frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary);
- return structured text/images/provenance and explicit partial/unsupported/technical-failure outcomes;
- retain extraction engine/package/version and policy provenance;
- never execute macros, active content, external relationships, or embedded instructions;
- distinguish scan-like material from corrupt, blank, unsupported, or encrypted material.
- retain accepted MP4/MOV evidence without submitting it to document OCR or image cropping; offer safe preview only for a browser-supported encoding and retain download in every case.

### Qualified OCR

Eligible scan-like pages from incoming instructions use the approved Azure
Document Intelligence `prebuilt-layout` boundary
([ADR-0047](../adr/0047-scanned-instruction-ocr-only.md)). Corrupt,
encrypted and non-renderable inputs must never be submitted to OCR. Readable
embedded-text pages remain on the ordinary PDF path and are not submitted or
replaced. Estimate imports use their readable deterministic parsers only; a
scan-like, ambiguous, or otherwise unsupported estimate is refused without
an OCR fallback.

Each operation binds exactly one authorized incoming intake asset, its content
hash, length and selected page numbers. Retain the provider operation, pinned
API/model, response hash, page coordinates and confidence. Deterministic
instruction validation remains necessary; low confidence or missing structure
cannot silently produce accepted fields. Unknown submissions with no provider
identity remain visible and are not blindly repeated.

An instruction with scan-like pages from more than one retained source requires
staff review; Pegasus does not combine OCR outputs from separate sources.

The provider operation identity and page output are retained before
instruction analysis. Provider completion alone does not complete the durable
work: analysis failures or receipt-version conflicts retry against the retained
output, without resubmitting pages. Completion is acknowledged only after an
analyzed, no-profile, or ambiguous analysis outcome has been recorded. A bounded
exhausted retry remains visibly failed, with its original output retained.

### Staging and custody

Receipt/staging and accepted case custody are different states.

- Network, local, or Azure staging is temporary processing storage and is never accepted Case custody proof.
- Box is the required accepted case-file custody system for the day-one alpha. Every allocated Case/PO uses its immutable reference for its Box case folder, then retains its source emails, instruction documents, images, correspondence, and reports there.
- A Box failure after Case/PO allocation retains the Case as `Not ready` with explicit failure and staff-initiated retry/recovery evidence. It does not roll back, reuse, or reallocate the reference, and no background or automatic business retry is permitted.
- Staff may add manually received WhatsApp evidence with its source/channel provenance; this does not activate a WhatsApp integration.
- Case/file mutations use normal role, lease and version guards. Completed
  status does not permanently lock correspondence or files; query receipt or
  attachment follows FRD-01. Box offers no bypass of application authorization.
- Default local alpha work must not mutate any Outlook mailbox or Box location. The separately approved Box integration-test profile and explicitly approved non-production test deployments may create and update controlled non-corpus artifacts only in the approved disposable test subtree recorded in [operations](../operations.md#approved-box-integration-test-target); they must not delete, move, copy, or share Box content. Outlook tests use immutable local copies or an explicitly approved test mailbox and operation.
- A custody transition records source identity, content hash, target identity/version, actor/caller, time, and failure/retry state without deleting the source proof prematurely.

Incoming custody claims use the occurrence's operation identity to select its
own source record. Intake claims update the matching receipt/asset pair directly;
they do not probe public-upload records or require broader Worker permissions.

An Image-initiated Case also has its own Box folder from registration
(INTK-014): the folder is named for the permanent Image Intake Reference,
sits directly under the approved custody root, and retains every registered
image of the submission group in stored order. The storage is queued work
behind the registration — a Box failure never blocks or rolls back a
registration or a merge, the images remain authoritative in intake
source-artifact retention throughout, and the queued work re-arms itself
with bounded backoff for dependency failures before recording a terminal
failure honestly on the record. When the Image-initiated Case merges into a
formal Case, its folder's contents move into that Case's Box custody (the
case root's image evidence location) and the emptied folder is removed; the
removal is non-recursive, so unexpected content fails the fold closed
instead of being destroyed. The Image-initiated lifecycle state and
merge/closure history remain in SQL regardless of custody.

## Custody and staging distinctions

Box is durable file custody. Azure processing bytes and the 24-hour idle cache
are temporary; SQL retains arrival, idempotency and provenance identities.
Receipt, logical access and definitive association are separate claims. A
temporary file or cache hit does not establish an accepted Case association.

A secondary Audit folder nests under its original Inspection folder. It is
not a sibling of that Inspection folder. Image-origin references remain
distinct from formal Case/PO identity while their custody is resolved.


## Custody and derived reads

Box owns durable Case-document custody. Existing intake staging retains original
bytes until verified custody handoff; SQL retains identity, version and
provenance. A derived image/cache copy serves processing or presentation and
can be rebuilt from retained source evidence; it is not a second custody owner.
Read through the logical occurrence/version interface so current authorization
and exact version checks apply regardless of physical source. Do not erase
staging before verified handoff or invent a new store for this separation
(ADR-0045).

An inline image preview is served through this same cached content path,
never as an audited download: every read re-verifies the source content hash
and serves only confirmed custody. A cache entry is content-hash addressed,
so it is immutable and safely shared, and a smaller thumbnail variant is
served to gallery and tile surfaces while the full image serves the viewer
and crop. The instruction/receipt gallery does not omit an image whose
custody is still in flight (not yet confirmed); it renders as a placeholder
naming the file until custody resolves. The Case's own Images tab carries no
such placeholder: it is built from `CaseFiles.Live`, which requires Confirmed
custody, so an image still in flight is simply absent from it until custody
resolves.

## Image tags

Image tags are a Case-document classification, distinct from Box/SQL file
custody: a shared vocabulary (`Name`, `Colour`, `IsBuiltIn`) and a join
recording which tag sits on which image occurrence (`AppliedBy`,
`AppliedAtUtc`, its operation key). The seeded, non-deletable vocabulary is
Overview, Close-up, Third party and Reflection; an authorised staff member
may add a custom entry (up to 40 characters, case-insensitive unique against
every existing name) with one of six fixed design tints. An occurrence may
carry any number of tags.

Applying or removing a tag on a Case image carries the same guards as every
other Case mutation: the current Case edit lease, the expected Case version
and an operation key for replay, and it bumps the Case version — the tag is
on the record's timeline, not beside it. Adding to the shared vocabulary
takes no lease or Case version (it is not a Case fact); it requires the
casework right and an operation key, and a duplicate name (by the
case-insensitive key) is refused rather than creating a second entry.

Third party replaces the former one-way `ThirdPartyVehicleConfirmedAtUtc`
flag and keeps its EVA-exclusion behaviour
([FRD-07](frd-07-eva-and-external-engineering-handoff.md#eva-handoff-routes)).
The migration that introduced tags converted every recorded confirmation
into a Third party tag on the same occurrence, preserving its original
moment, actor and operation key, then dropped the three flag columns and
their index; there is no way back from a tag to the flag.
