# Temporary session plan: remove public upload links

> Temporary operator-requested task artifact. This session plan is the execution
> checklist; after every individual file change, reread this file before making
> the next file change. Keep it available through review and handoff.

## Goal and boundary

Remove the complete INT-31 public upload-link capability from this unreleased
application. No public-upload data exists to preserve. Do not add a replacement
route, redirect, compatibility path, disabled service, feature flag, data
conversion or migration baseline reset.

Preserve authenticated staff document upload/download/preview, ordinary intake
retention and custody reconciliation, provider/mailbox intake, ordinary due
chasers, and Operations external-work monitoring/retry/edit-lease behavior.
Remove only public-link-specific branches from shared owners; preserve surviving
authorization and integrity boundaries. Keep existing UI geometry.

## Worktree and change discipline

- Work only in `C:/Users/Alex/Documents/GitHub/pegasus-worktrees/remove-public-upload-links`
  on branch `task/remove-public-upload-links`.
- Base is committed `dev` at `81b87049e2575917b2342315aad29616b9a84857`.
- Leave the dirty primary checkout unchanged. Do not stash, transfer, overwrite,
  or commit its unrelated changes.
- Use PowerShell 7 on Windows for one verification run. Keep generated output in
  ignored artifact locations.
- Before every next file edit, reread this plan. Apply edits one file at a time;
  after each file edit, reread the plan and then choose the next file from it.
- Keep changes within this feature removal. Inspect status before commits and
  commit only the public-upload removal and this requested plan artifact.
- Keep the worktree after commit for review. Do not merge, push or remove it.
- If `dev` advances before delivery, merge it into this branch and rerun the
  checks affected by the merge.

## Ordered implementation

### 1. Extract ordinary intake retention

`EfDocumentRequestStore.cs` currently also contains
`EfPublicUploadRetentionStore`, which owns the `IIncomingArtifactRetentionStore`
implementation used by ordinary intake. Create
`EfIncomingArtifactRetentionStore` in the existing Infrastructure persistence
area and move only the intake behavior:

- Parse `intake:{receiptId:N}:{assetId:N}` operation keys and resolve the
  corresponding `IntakeAssetEntity`.
- Preserve the conditional one-winner custody claim, retry and uncertain-result
  reconciliation, state-transition validation, content identity and remote
  file/version values.
- Keep custody-state encoding/decoding required by ordinary intake; update
  `EfIntakeReceiptStore` to call the new surviving owner.
- Reject unsupported operation-key shapes explicitly; never query public-upload
  tables from this store.
- Preserve `IIncomingArtifactRetentionStore` and `RetainIncomingArtifact`.
- Register the new implementation at the existing document-surface boundary,
  update Worker composition assertions and relevant fixture registrations.

Then remove public-upload persistence behavior and delete
`EfDocumentRequestStore` and `UnavailableDocumentRequestStore` after confirming
no surviving consumer uses them.

### 2. Remove Core upload contracts and authority

- Delete `RequestUploadPolicy.cs` and its upload-link commands/results, limits,
  token/session/occurrence types and interfaces.
- Remove `ActorKind.RequestLink`, its actor factory/display/mapping, and
  `StaffAccessRight.SubmitRequestUpload`.
- Remove upload-only document-source values and Case request-upload summaries.
- Simplify `RetainIncomingArtifact` and `EfCaseArtifactCustody` authorization;
  remove token-link lookups and locking/transaction branches used only to
  coordinate link revocation. Preserve remaining actor permissions and custody
  integrity checks.
- Inspect persisted enums before deleting values. Preserve the stored identity
  of surviving members if their numeric or textual representation is persisted.

### 3. Remove upload references in chasers and Operations

- Remove `RequestLinkReference`, `RequestLinkPurpose`,
  `MissingMaterialRequestLinkPurpose`, active-link queries and the corresponding
  entity fields, constraints, foreign key and serialized payload data. Keep
  ordinary due-chaser scheduling, deduplication, dispatch and history.
- Remove upload-link projection/list/revoke behavior from Operations. Remove
  upload-only fields, states and kind discriminators when external work is the
  sole remaining projection. Keep retry, bounds, authorization and edit-lease
  recovery unchanged.
- Update all producers, consumers and mixed fixtures together.

### 4. Remove the Web feature

- Delete `src/Pegasus.Web/Pages/Uploads`.
- Remove upload page conventions, rate-limit policy, request limiter, transport
  filter, limits/composition gate and upload-specific middleware/response logic.
- Remove `PublicUploadTelemetryInitializer`, its DI registration and
  upload-only telemetry redaction.
- Remove the upload-only external layout once no consumer remains.
- Remove public-upload recognition, messages and layout selection from status
  pages. Preserve general errors, sign-in throttling and other ingress policies.
- Remove Case and Operations create/revoke controls, lists, dialogs, handlers,
  labels, upload-only styles/scripts/assets after checking remaining consumers.
- Former `/Uploads/{token}` requests use ordinary unmatched-route behavior.
  Add no special retirement endpoint or redirect.
- Apply Web guardrails: remove feature-owned controls/empty wrappers and retain
  surrounding layout, information hierarchy and shared design conventions.

### 5. Remove database/configuration support

- Remove mappings and entity types for `RequestUploadLinks`,
  `RequestUploadReceipts`, `PublicUploadSessions` and
  `PublicUploadOccurrences`.
- Remove chaser link FK, constraint, index and link reference/purpose columns.
- Generate one EF migration named `RemovePublicUploadLinks`; inspect migration
  SQL and snapshot. Drop constraints/indexes and dependent tables in valid order.
- Keep unrelated Case, document, intake, history and external-work structures.
  Do not rewrite historical migrations or baseline the database anew.
- Remove active `DocumentRequests` settings from Web/Infrastructure/Worker
  composition, deployment definitions, examples and config docs.
- Update bootstrap permission/table inventories and migration permission tests.

### 6. Update requirements and documentation

- Remove public upload links from supported intake channels and current
  functional requirements; remove the PRD boundary exception.
- Mark INT-31 retired without reusing its identifier. Correct DOC-06's
  retirement note so it does not name a nonexistent active successor.
- Update affected chaser, Operations, authorization, architecture and
  configuration documentation; repair inbound links and remove upload-specific
  open decisions.
- Preserve issued ADR history and dated deployment observations. Do not claim
  deployed state changed because source changed.

## Verification and acceptance

Read the applicable Web guardrail, Razor implementation/review instructions,
test-writing instructions and .NET test-running procedure before the related
work. Use existing test projects and fixture conventions.

Focused evidence must show:

- Former upload GET, multipart POST and finalize requests cannot resolve an
  upload handler; Cases and Operations show no link controls or lists.
- Ordinary intake claims once under concurrency; retries, custody uncertainty,
  result recording and content identity still reconcile correctly.
- Staff document custody and authorization, ordinary chasers, and external-work
  monitoring/retry/edit-lease recovery still work.
- Web and Worker composition resolve the new ordinary intake retention owner.

Using disposable local databases, test both full schema creation and upgrade
from the immediately preceding schema. Confirm all four public-upload tables and
chaser link columns are absent, representative unrelated Case/document/intake/
chaser records remain valid, and runtime grants match the final schema.

After focused checks, run locked restore and Release build; Core and architecture
tests; non-Corpus integration tests; relevant existing CI-routed config/infra
checks; and Markdown placement validation against this task's base and final
head. Serialize heavy host work. Inspect affected Case/Operations states at
1580x1000 and a smaller desktop width; include 760px if shared shell behavior
changes. Record any visual/browser verification not available.

Finish with a tracked-file reference sweep for upload routes, contracts, actor
names, settings and operator-facing terms. Remaining hits may only be historical
migrations, explicit retirement/history records and tests asserting absence.
Do not remove ordinary staff-upload behavior merely because it uses the word
upload. Acceptance requires no reachable public-link behavior, dormant
registration, final-schema upload table, active requirement or orphaned feature
asset.

## Handoff and release boundary

Report task branch/worktree, base and final SHA, concise removal summary,
migration description, checks/results, outstanding verification and any primary
checkout overlap. Commit only this removal and the requested plan artifact;
leave the task worktree available for review.

This task prepares source and migration only. A later authorized release must
follow the destructive-migration procedure: identify exact targets, stop the
old Worker, verify the old Web is inactive before SQL, then activate and smoke
new Web/Worker artifacts. After destructive SQL starts, recover forward; never
restart old bytes against the removed schema.
