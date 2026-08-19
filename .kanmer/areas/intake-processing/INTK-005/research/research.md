# Research — INTK-005: durable grouped multi-file staff Upload

## Question

What exact change lets authenticated staff select an arbitrary practical number of files in one Upload submission, preserves every file's custody and replay identity, and supplies the durable group identity required by [[INTK-006]]?

## Findings

### Current request and UI shape

- `src/Pegasus.Web/Pages/Upload.cshtml` binds one file control to `Upload`; the input has no `multiple` attribute.
- `src/Pegasus.Web/Pages/Upload.cshtml.cs` binds `IFormFile? Upload`, accepts one `ExternalReceiptToken`, validates one file, copies that file into one `MemoryStream`, calls `IIntakeSubmission.ExecuteAsync` once, and redirects to one staged-receipt status route.
- `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` is intentionally a single-receipt view. A batch surface must compose receipt statuses; it must not replace the existing receipt query or invent a second processing-state vocabulary.
- PLAT-006 owns the dropzone/presentation enhancement and intentionally retained a single-file page model. Implementation must begin from its merged markup and data-attribute conventions.

### Current durable boundary

- `IntakeStagedReceipt` in `src/Pegasus.Core/Intake/DurableIntake.cs` has an immutable receipt id, original safe filename, media type, length, hash, source identity, received time, actor, storage key, and staged time. It has no submission/group id.
- `IIntakeSubmission` and `ReceiveIntake` accept exactly one `IntakeSource`. The source identity uniqueness key is `(SourceChannel, ExternalReceiptToken)`; replay with the same token and same bytes returns the existing staged receipt, while different bytes under the same identity throws `IntakeSourceIdentityConflictException`.
- `IIntakeWorkStore.ReceiveAsync` creates one work item per staged receipt. Worker claim, retry, completion, and poison handling are also receipt-scoped.
- `EfIntakeWorkStore` and `PegasusDbContext` persist staged receipts and work items independently. There is no current aggregate that can answer “which receipts were selected together?”
- Original filenames are already retained as `IntakeStagedReceipt.SourceFileName` and later receipt source filenames. The group feature must preserve this field unchanged per member.

### Required group identity

- A group is a durable relationship among independently retained receipts. It must not replace receipt ids, source identities, source hashes, work items, or operation keys.
- One-file submissions are one-member groups so downstream code has one rule, not a single-file exception.
- The smallest viable shape is one durable submission-group row plus ordered member rows (or an equivalent normalized relation) created at receipt acceptance. The group needs a stable id and created/actor/channel metadata; each member needs the staged receipt id and stable ordinal. A JSON list on a receipt is unsuitable because membership spans rows, complicates uniqueness, and cannot be atomically queried.
- The group id is an internal correlation key, not a Case/PO, Audit, Image Intake, or U-reference.
- Per-file replay tokens must be deterministic children of one form-level submission token plus the stable member ordinal. Do not derive identity from filename because duplicate filenames are valid and filenames can change between retry attempts.
- Whole-form replay must return the same group and existing member receipts. A retry must not create a second group or duplicate already accepted members.

### Validation, boundedness, and failure semantics

- `IntakeEnvelopeLimits.MaximumContentLength` remains the per-file limit. “As many as we want” removes the one-file UI restriction; it does not authorize unbounded request bodies, unbounded memory, or removal of platform limits.
- Validate the collection before staging any bytes for deterministic form errors: at least one file, every filename/media type present, no empty member, each member within the existing limit, and aggregate request within the configured ASP.NET multipart boundary.
- Do not buffer the entire batch simultaneously. Process members sequentially and dispose each stream before the next.
- Once a member is durably accepted, later-member failure cannot be represented as an atomic rollback because artifact retention and work enqueue are deliberately durable. The response must list success/failure for every member and allow an idempotent retry with the same submission token.
- Group creation/member attachment must be idempotent and concurrency-safe. A unique group token and unique `(GroupId, Ordinal)` plus unique `StagedReceiptId` relation prevent replay duplication.

### Governing requirements and dependency

- FRD-02 owns receipt/source identity, durable custody, replay, and fail-closed association. FRD-12 and `docs/design/README.md` own honest, accessible operator feedback.
- [[INTK-006]] is a real second consumer of the group relation: it must evaluate all vehicle images selected together and apply one association-or-Image-Only outcome.
- No new runtime, deployment unit, top-level directory, or second intake implementation is required. Core owns the group contract; Infrastructure persists it; Web composes the existing per-file submission port.

## Verified premises

- Inspected the current Upload page/model, queued status model, `ReceiveIntake`, intake contracts, EF mappings, and focused upload/intake tests.
- Inspected EPIC-007 context and [[INTK-006]] requirements.
- Confirmed there is no existing submission/group identifier in staged-receipt or work-item contracts.

## Implications

Implement a normalized, durable submission-group relation and a Core batch orchestration use case that calls the existing single-source intake owner sequentially. The Web page binds a file collection and renders a group result containing every member. INTK-006 then queries that same group rather than attempting to infer siblings by time, filename, actor, or content.

## Open questions

None. The user established group semantics, one-member groups, preservation of original filenames, and the downstream vehicle routing rule. Existing safety limits remain binding.


## Parallel execution note — 2026-08-19

[[INTK-006]] may consume this branch's group contract before PR merge. Its worktree is intentionally based on `intk-005-grouped-upload`; review changes will be reconciled by a later rebase.
