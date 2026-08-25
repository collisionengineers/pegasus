# Checklist — PR-056

- [x] Remove the two completeness-waiver fields from Core configuration/update/default contracts and make existing Review/assignment policy always require instructions and images, preserving staff-review settings and CASE-013 automatic intake.
- [x] Remove the obsolete persistence fields and mappings; add the normal EF migration/snapshot change dropping their columns with no compatibility path.
- [x] Remove the obsolete administration controls/bindings and mechanically update all remaining configuration/request callers.
- [x] Add or update Core, lifecycle, persistence, migration and administration tests proving incomplete evidence never reaches `Review` in any remaining configuration.
- [x] Run the focused build/tests, full Core and Architecture suites, and the required SQL integration profile.
- [x] Perform the four-lens simplification pass, apply or explicitly disposition in-scope findings, and write the post-implementation report.

## Progress notes

Planning complete on 2026-08-25. No user-visible replacement copy, new readiness abstraction, Export validation, compatibility mechanism, or unresolved question is authorized.


2026-08-25: implementation and focused verification completed at c86b803c. Simplification lenses: reused existing lock and batch-read conventions; removed obsolete switches and catch/retry path; no new abstraction; no deferred code finding.

## Closeout — PR-056

- [x] PR merge verified: PR #539 merged at 2026-08-25T00:47:21Z
- [x] proof.md finalised with PR URL, merge date and immutable Release 28 evidence
- [x] Moved to final stage
- [x] Outcome recorded with release evidence and honest verification boundary
- [ ] Shared worktree removal — deliberately deferred to preserve the two pre-existing modified EVA reference samples
- [ ] Shared branch deletion — deliberately deferred with the shared worktree
- [ ] Fetch/prune — deliberately deferred; no shared Git state changed
- [ ] Ticket claim release — performed only after all Kanmer records are finalised

- [x] Ticket claim released after Kanmer proof, traceability, outcome and deployment records were finalised. Shared Git cleanup remains intentionally deferred to preserve the modified reference samples.
