# FRD-05: Documents, extraction, and custody
> Owner capabilities: DOC · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Documents, extraction, and custody

### Supported source boundary

The intended intake boundary covers PDF, DOC, DOCX, EML, and MSG source material plus attached images and route metadata. Current support is proved only by the actual application caller and current architecture/evidence, not by an imported workspace or plan. One engine owns each format: PDF stays on the PdfPig path (ADR-0001/ADR-0003 — the only live PDF implementation), DOCX on OpenXml, EML on MimeKit, and DOC/MSG on the CollisionDocNet-derived compound-file readers integrated by ADR-0025 and scoped to those two formats.

Pegasus must:

- preserve source bytes before deriving content;
- isolate parsing and enforce depth, count, size, decompression, relationship,
  and cancellation limits — manual upload currently remains bounded at 100 MiB
  per file; future intake bounds require the research and operator decision in
  `INTK-052`, while the Provider API envelope stays at 30 MB and is owned by
  [FRD-09](frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary);
- return structured text/images/provenance and explicit partial/unsupported/technical-failure outcomes;
- retain extraction engine/package/version and policy provenance;
- never execute macros, active content, external relationships, or embedded instructions;
- distinguish scan-like material from corrupt, blank, unsupported, or encrypted material.

### Qualified OCR

Scan-like pages use the approved Azure Document Intelligence `prebuilt-layout`
boundary ([ADR-0040](../adr/0040-qualified-document-intelligence-ocr.md)). A
retained estimate PDF may also qualify when its embedded text-map failure is
positively established and its pages are structurally readable. A failed or
ambiguous provider parser alone is insufficient. Corrupt, encrypted and
non-renderable inputs must never be submitted to OCR. Readable embedded-text
pages remain on the ordinary PDF path and are not submitted or replaced.

Each operation binds exactly one authorized intake asset or Case document
version, its content hash, length and selected page numbers. Retain the
provider operation, pinned API/model, response hash, page coordinates and
confidence. Deterministic instruction/estimate validation remains necessary;
low confidence, missing structure or inconsistent totals cannot silently
produce accepted fields or partial estimate lines. Unknown submissions with
no provider identity remain visible and are not blindly repeated.

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
