# FRD-05: Documents, extraction, and custody
> Owner capabilities: DOC · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Documents, extraction, and custody

### Supported source boundary

The intended intake boundary covers PDF, DOC, DOCX, EML, and MSG source material plus attached images and route metadata. Current support is proved only by the actual application caller and current architecture/evidence, not by an imported workspace or plan. One engine owns each format: PDF stays on the PdfPig path (ADR-0001/ADR-0003 — the only live PDF implementation), DOCX on OpenXml, EML on MimeKit, and DOC/MSG on the CollisionDocNet-derived compound-file readers integrated by ADR-0025 and scoped to those two formats.

Pegasus must:

- preserve source bytes before deriving content;
- isolate parsing and enforce depth, count, size, decompression, relationship, and cancellation limits;
- return structured text/images/provenance and explicit partial/unsupported/technical-failure outcomes;
- retain extraction engine/package/version and policy provenance;
- never execute macros, active content, external relationships, or embedded instructions;
- distinguish scan-like material from corrupt, blank, unsupported, or encrypted material.

Alpha does not include dormant OCR. Scan-like OCR is a deferred capability and requires a separately accepted slice, provider, failure/recovery contract, caller proof, and evaluation.

### Staging and custody

Receipt/staging and accepted case custody are different states.

- Network, local, or Azure staging is temporary processing storage and is never accepted Case custody proof.
- Box is the required accepted case-file custody system for the day-one alpha. Every allocated Case/PO uses its immutable reference for its Box case folder, then retains its source emails, instruction documents, images, correspondence, and reports there.
- A Box failure after Case/PO allocation retains the Case as `Not ready` with explicit failure and staff-initiated retry/recovery evidence. It does not roll back, reuse, or reallocate the reference, and no background or automatic business retry is permitted.
- Staff may add manually received WhatsApp evidence with its source/channel provenance; this does not activate a WhatsApp integration.
- A closed case and its files are application-level read-only. A new version, revision, logical removal, move, copy, share, or other mutation requires a reasoned reopen first; no Box operation bypasses that gate, and the alpha infers no general move/copy/share/delete authority.
- Default local alpha work must not mutate any Outlook mailbox or Box location. The separately approved Box integration-test profile and explicitly approved non-production test deployments may create and update controlled non-corpus artifacts only in the approved disposable test subtree recorded in [operations](../operations.md#approved-box-integration-test-target); they must not delete, move, copy, or share Box content. Outlook tests use immutable local copies or an explicitly approved test mailbox and operation.
- A custody transition records source identity, content hash, target identity/version, actor/caller, time, and failure/retry state without deleting the source proof prematurely.

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
