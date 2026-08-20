# Checklist — PLAT-002

- [x] Add metadata-free StaffPageModel with the sole nullable TryGetActor and public operation-key generator; add no authorization, receipt-token logic, or unrelated behaviour.
- [x] Derive AdministrationPageModel, CaseMutationPageModel, and the merged UploadConfirmationPageModel from StaffPageModel, remove their copies/usings, and record the early green Release build.
- [x] Migrate all 17 reconciled direct actor callers, preserving conditions, failure results, authorization, and local staff-Guid parsing; remove remaining operation-key copies while keeping Upload.ExternalReceiptToken local.
- [x] Keep Uploads/Request AllowAnonymous on PageModel, reuse StaffPageModel.NewOperationKey statically, and add proportional ownership/anonymous/receipt-boundary architecture assertions.
- [x] Refresh docs/current-architecture.md; run all four simplification lenses over the diff and immediate surroundings; apply findings and append dated dispositions to plan.md.
- [x] After simplification; run the exact restore/build/architecture/focused-integration/inventory checks from plan.md and record results in the post-implementation report.

## Progress notes

- 2026-08-20: Final evidence: locked restore succeeded; Release build succeeded with 0 warnings/0 errors; 98/98 architecture tests passed; focused integration batches covered the exact planned 15 classes with 114 passed, 6 skipped, 0 failed; inventories found one actor factory site, one operation-key method, one separate receipt-token site, and the anonymous RequestModel boundary intact. The original combined integration wrapper outlived its timed-out parent and was stopped; clean captured batches then passed.

- 2026-08-20: Four-lens simplification pass complete; all findings applied, none skipped or deferred. Reused UploadConfirmationPageModel, removed obsolete helpers/usings, strengthened the GUID-N guard to count occurrences, and retained receipt-token ownership at intake altitude.

- 2026-08-20: Fresh `origin/dev` (`bc0646a6`) retained 20 actor files/26 calls but introduced UploadConfirmationPageModel and Intake/Image. Reconciled to three bases plus 17 direct callers; early and post-migration Release builds were green, and all 98 architecture tests passed.

Append implementation progress and command evidence here.
