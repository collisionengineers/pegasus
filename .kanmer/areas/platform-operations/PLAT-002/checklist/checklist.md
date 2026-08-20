# Checklist — PLAT-002

- [ ] Add metadata-free StaffPageModel with the sole nullable TryGetActor and public operation-key generator; add no authorization, receipt-token logic, or unrelated behaviour.
- [ ] Derive AdministrationPageModel and CaseMutationPageModel from StaffPageModel, remove their actor/operation-key copies/usings, and record the early green Release build.
- [ ] Migrate all 18 direct actor callers, preserving conditions, failure results, authorization, and local staff-Guid parsing; remove remaining operation-key copies while keeping Upload.ExternalReceiptToken local.
- [ ] Keep Uploads/Request AllowAnonymous on PageModel, reuse StaffPageModel.NewOperationKey statically, and add proportional ownership/anonymous/receipt-boundary architecture assertions.
- [ ] Refresh docs/current-architecture.md; run all four simplification lenses over the diff and immediate surroundings; apply findings and append dated dispositions to plan.md.
- [ ] After simplification, run the exact restore/build/architecture/focused-integration/inventory checks from plan.md and record results in the post-implementation report.

## Progress notes

Append implementation progress and command evidence here.
