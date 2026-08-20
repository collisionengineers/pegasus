# Checklist — PLAT-002

- [ ] Add metadata-free StaffPageModel with the one nullable TryGetActor and one public N-format NewOperationKey implementation; add no authorization or unrelated behaviour.
- [ ] Derive AdministrationPageModel and CaseMutationPageModel from StaffPageModel, remove their copies/usings, and record a green Release build before direct-page migration.
- [ ] Migrate every direct actor-calling page in files.md to StaffPageModel, remove all remaining application key copies, and preserve local staff-Guid parsing and existing failure conditions.
- [ ] Keep Uploads/Request AllowAnonymous on PageModel, call StaffPageModel.NewOperationKey statically, and add the one-owner/anonymous-boundary architecture assertions.
- [ ] Refresh docs/current-architecture.md, run the exact restore/build/architecture/focused-integration/rg verification from plan.md, and record results for the post-implementation report.
- [ ] Run the required four-lens simplification pass, apply behaviour-preserving findings, and append a dated findings/dispositions section to plan.md before opening the PR.

## Progress notes

Append implementation progress and command evidence here.
