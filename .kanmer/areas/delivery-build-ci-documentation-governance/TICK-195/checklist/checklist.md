# Checklist — TICK-195

- [x] Add the explicit-range Markdown placement validator with canonical and registered-workspace allow-lists, A/C/R destination handling, aggregate diagnostics, and fail-closed comparison validation.
- [x] Add disposable-Git regression coverage for allowed and forbidden destinations, grandfathered changes, rename/copy handling, multiple violations, and invalid/all-zero revisions.
- [x] Wire full-history checkout, event-specific base/head selection, regression coverage, and placement validation into the existing Windows documentation job without altering TICK-200 lanes.
- [x] Run focused and real-repository verification, confirm the excluded UI/design/task-plan paths are untouched, commit, push, open the dev-targeted PR, and record traceability and the implementation report.

## Progress notes

- Implemented the validator and focused disposable-Git regression suite. The suite passes locally and covers canonical/workspace additions, forbidden destinations, grandfathered modifications/deletions, rename/copy destinations, aggregate errors, and invalid comparisons.
- Updated only the documentation job in CI; the TICK-200 change classifier, infrastructure lane, test shards, and build lanes are unchanged.
- `origin/dev` advanced during verification. Merged current `origin/dev` into the ticket branch, then confirmed the ticket diff against that base contains only the workflow and two validator scripts.
- Verification passed: focused regression suite; real `origin/dev..HEAD` placement check; 219-file documentation link check; invalid-revision fail-closed smoke test; and `git diff --check`.
- Opened PR #384 targeting `dev`.
