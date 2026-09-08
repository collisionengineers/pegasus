# Checklist — DELIV-056 correction packet

The earlier D56 formal-document conversion remains recorded in scratch/notes.md. The completed items below cover the bounded ten-file correction and approved amendments; the retained verification history is in `scratch/verify.md@7fc07a84d76567c5`.

- [x] Step 1 — In CustodyOutboxIntegrationTests, make each of the two Audit custody fixtures retain the existing formal Audit notification plus a distinct honest original-report PDF with one unnegated assessment; preserve every existing custody/review assertion.
- [x] Step 2 — In CustodyOutboxIntegrationTests, use a dedicated actual pre-case QDOS source only for the two reevaluation methods, visibly identifying it as awaiting work-type classification; leave definitive CreateSource and every automatic acceptance caller unchanged.
- [x] Step 3 — In CountingCustody, delegate/count RetainAcceptedIntakeAttachmentAsync so CancellationSqlFaultAndLeaseLoss reaches its existing injected DbUpdateException and recovery assertions through the actual attachment path.
- [x] Step 4 — In InstructionDraftWebTests, replace the stale four-event total with explicit proof of two distinct receipts sharing one Case through UniqueMatch, one allocation-succeeded event, and two receipt-recorded events.
- [x] Step 5 — [pre-review] Run static diff/scope checks only; hand the exact focused methods to the designated host verifier. Preserve the original 13-class/269-case run as broad failure evidence, and report unchanged UploadConfirmation/browser as deferred baseline current-contract work.

## Approval boundary

- [x] Root read and approved each correction plan/checklist amendment before its corresponding source edit.

## SendToClaude display-correction addendum

- [x] In `SendToAiIntegrationTests.InaccessibleCaseCannotPostSendToClaude`, replaced the obsolete positive gated-control regex with the precise negative assertion that the interactive `data-dialog-open="send-to-claude-dialog"` launcher is absent; retained dialog absence and POST-404 assertions without fixture, helper, runtime or other-file changes.

## Audit acceptance-version correction addendum

- [x] Added only `expectedVersion: evidence.ReceiptVersion` to the existing `AcceptAsync` calls in the two Audit custody methods. The evidence query/type assertion and all acceptance, custody, review, identity and document assertions remain; no receipt refresh, evidence seed/upsert or production concurrency change occurred.

## Verification status

- `git diff --check` passed at final hash `bdbe80e82a6569a4a79ee57f44998f98f679eef2` (LF-to-CRLF advisories only).
- The designated host's incremental Release build passed. The exact two-Audit delta passed 2/2; the prior exact-seven run provides the five unchanged-source passes. Earlier broad/exact non-PASS attempts remain recorded and are not superseded.
- [[INTK-066]] owns the unresolved UploadConfirmation Attach/browser current-contract decision. It remains unmodified and is not a DELIV-056 pass claim.

---

## Closeout — DELIV-056

- [x] PR merge verified (`gh pr view --json state,mergedAt`)
- [x] proof.md finalised (PR URL + merge date appended)
- [x] Moved to final stage
- [x] Outcome recorded in ticket body (PR link, follow-ups)
- [x] cd out of worktree; `git worktree remove .worktrees/deliv-056`
- [x] `git branch -d DELIV-056-align-intake-regression-fixtures-with-definitive-instruction-evidence` (`-D` if squash/rebase-merged)
- [x] `git fetch --prune` + `git worktree prune`
- [ ] `take_ticket action: "release"`
