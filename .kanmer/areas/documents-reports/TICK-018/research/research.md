# TICK-018 (DOC-02) research — what exists now vs what DOC-02 still requires

Assessment 2026-08-20, after INTK-014 (PR #462, merged to dev) and against production release 13 diagnostics. Read-only; no implementation in this ticket yet.

## DOC-02 scope (capability row + FRD-05)

"Store source emails, instruction documents, images, correspondence, and reports in Box." Box is the accepted case-file custody system; blob is temporary hot staging only; a Box failure keeps the Case `Not ready` with staff-initiated retry; closed cases are read-only; the approved test subtree governs non-production Box writes.

## What exists and is live (verified)

- **Case custody roots + source email retention — LIVE in production.** Sole writer path: acceptance enqueues `create_case_custody` (`EfCaseAcceptanceStore`; replacement path in `EfLinkedCaseReplacementStore`); `EfQueuedCustodyProcessor` → `BoxCaseCustody` creates the bound case folder under approved root 405543781910 and retains the accepted source under `Evidence/Original instruction`, plus the Audit reference folder. Production evidence: both cases (QDOS26001/QDOS26002) carry confirmed `CustodyRootRemoteId`/`CustodySourceRemoteId`; both work items completed. Failure path matches FRD-05 (Not ready + `IRetryCaseCustody` staff retry; no automatic business retry for case custody).
  - The **source email** slice of DOC-02 is delivered for accepted cases: the retained source is the instruction email itself (attachments travel inside the .eml).
- **Managed case documents — Box-backed in production.** `AddProductionBoxCustody` composes `IDocumentContentStore` → `BoxDocumentContentStore` (versioned files under the case root's Evidence tree). Callers: staff document upload and MCP `pegasus_document_add` with semantic roles `OriginalSource/Instruction/Image/Correspondence/EngineerReport/AuditReport/Other`. So instruction documents, correspondence, and reports **can** reach Box today — as reasoned manual/MCP document additions.
- **Image-initiated Case custody — merged to dev by INTK-014 (deploy-stage verification pending).** Registration enqueues `create_image_case_custody` → Box folder named for the Image Intake Reference holding every registered group image; merge enqueues `merge_image_case_custody` → contents fold into the paired case's `Evidence/Images` and the emptied folder is removed; dependency failures re-arm with bounded backoff; custody state recorded on `ImageIntakes`. FRD-05's staging-and-custody section was updated in the same PR to state this behaviour. Not yet verified in production Box (INTK-014's remaining verification item).

## What DOC-02 still requires (gaps, honest)

1. **Correspondence (automatic).** Later inbound emails associated to a case (link/auto-association via `EfIntakeMutationStore`, mail classification workspace) are retained in SQL/blob (`IntakeAssets`, `RetainedMailboxMessages`) but **no custody work is enqueued on association** — only acceptance and replacement enqueue case custody. Outbound sent evidence (`SentEmailEvidence`, Sent-items polling) is likewise SQL-only. Nothing moves case correspondence into the Box case folder automatically.
2. **Reports (automatic).** A sent engineer/audit report reaches Box only if staff (or the AI connector) add it as a case document with the `EngineerReport`/`AuditReport` role; there is no automatic retention of the sent report artifact or its sent-evidence email into the case's Box folder.
3. **Instruction documents beyond the origin source.** Individual attachments of the accepted source are not exploded into Box as separate files (the .eml is the custody item). Whether DOC-02 requires per-attachment files or the source-of-truth .eml satisfies it is an FRD interpretation to settle in the plan — FRD-05's wording ("retains its source emails, instruction documents, images, correspondence, and reports there") reads as satisfied by the retained source for day-one, but the operator may expect visible per-file content.
4. **Production verification of the image-case slice** (INTK-014's deploy-stage checklist item) — after the next release: folder per new image-initiated case, fold on merge, verified in Box.

## Next (for the plan phase, not this ticket move)

- Decide the correspondence/report automatic-retention contract: most likely the same durable outbox convention (new work kinds keyed on the association/sent-evidence row), reusing `BoxCaseCustody`/`BoxDocumentContentStore`; failure behaviour must follow FRD-05 (explicit failure + staff retry for case-scoped custody).
- Any live Box verification uses only the approved disposable test subtree (`docs/operations.md#approved-box-integration-test-target`) — `requires-live-approval` label stands.
