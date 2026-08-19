# Research — INTK-005: multi-file staff Upload

## Question

How can the authenticated staff Upload accept a batch of files while preserving the existing durable receipt, replay, validation, and per-file outcome behaviour?

## Findings

- `src/Pegasus.Web/Pages/Upload.cshtml` renders one `<input type="file">` bound to `Upload`; it does not carry the HTML `multiple` attribute.
- `src/Pegasus.Web/Pages/Upload.cshtml.cs` binds one `IFormFile?`, validates one file against `IntakeEnvelopeLimits.MaximumContentLength`, creates one external receipt token and operation key, calls the existing `IIntakeSubmission` port once, then redirects to one `/Upload/Status/{id}` page.
- The durable intake boundary is already the correct per-file business owner. `IIntakeSubmission` stages source bytes and one pending work item; Worker owns later processing. Multi-file submission should reuse that port once per file, not introduce a second intake implementation.
- Replay identity is currently form-scoped because `ExternalReceiptToken` is singular. A batch needs a deterministic distinct token/operation key for every selected file so a retry cannot duplicate completed files or conflict because different bytes reuse one identity.
- `UploadStatus.cshtml(.cs)` represents one staged receipt. A batch therefore needs either a batch result surface composed from existing receipt statuses or an immediate per-file result list with links; redirecting to only the last receipt would silently hide the others.
- `PLAT-006` changes presentation and drag/drop only. Its implementation deliberately keeps a single file and leaves `Upload.cshtml.cs` untouched, so this ticket is not a duplicate and must integrate with that merged UI rather than overwrite it.
- Existing accepted formats and the per-file maximum remain authoritative. “As many as we want” removes the current one-file interaction constraint; it does not remove transport, memory, or platform request-size safety.
- Governing behaviour comes from FRD-02 (durable, idempotent intake outcomes) and FRD-12/design conventions (honest operator feedback). No new runtime, store, or Core abstraction is justified.

## Implications

Implement the smallest batch composition at the Web boundary: bind a collection, retain per-file validation, derive stable unique receipt identities, invoke the existing submission port per file, and render every result. Partial success must remain visible and retryable without rolling back or duplicating files already durably accepted.

## Verified premises

- Source inspection of the current `Upload` and `UploadStatus` page models and markup.
- Linked PLAT-006 ticket documents and its stated untouched page-model boundary.
- Production data was not needed to establish the one-file limitation.

## Open questions

None. The requested outcome establishes multiple selection; existing per-file and platform safety limits remain in force.
