# Checklist — PR-056

- [ ] Remove the two completeness-waiver fields from Core configuration/update/default contracts and make existing Review/assignment policy always require instructions and images, preserving staff-review settings and CASE-013 automatic intake.
- [ ] Remove the obsolete persistence fields and mappings; add the normal EF migration/snapshot change dropping their columns with no compatibility path.
- [ ] Remove the obsolete administration controls/bindings and mechanically update all remaining configuration/request callers.
- [ ] Add or update Core, lifecycle, persistence, migration and administration tests proving incomplete evidence never reaches `Review` in any remaining configuration.
- [ ] Run the focused build/tests, full Core and Architecture suites, and the required SQL integration profile.
- [ ] Perform the four-lens simplification pass, apply or explicitly disposition in-scope findings, and write the post-implementation report.

## Progress notes

Planning complete on 2026-08-25. No user-visible replacement copy, new readiness abstraction, Export validation, compatibility mechanism, or unresolved question is authorized.


2026-08-25: implementation and focused verification completed at c86b803c. Simplification lenses: reused existing lock and batch-read conventions; removed obsolete switches and catch/retry path; no new abstraction; no deferred code finding.
