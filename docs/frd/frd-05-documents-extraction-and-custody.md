# FRD-05: Documents, extraction, and custody
> Owner capabilities: DOC · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Documents, extraction, and custody

### Supported source boundary

The intended intake boundary covers PDF, DOC, DOCX, EML, and MSG source material plus attached images and route metadata. Current support is proved only by the actual application caller and current architecture/evidence, not by an imported workspace or plan.

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

Image-initiated Case files stay under the same intake source-artifact
retention as every other received item; there is no separate VRM-keyed Box
custody root for an Image-initiated Case in this slice, and none is claimed as
delivered. Once an Image-initiated Case merges into a formal Case, its
preserved origin evidence becomes available for retention into that Case's
Box custody under the rules above; the Image-initiated lifecycle state and
merge/closure history remain in SQL regardless of custody.
