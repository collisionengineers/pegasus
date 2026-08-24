# Checklist — ENG-016

## Git and scope

- [x] Preserve unrelated staged user changes outside every ENG-016 commit
- [x] Merge current `origin/dev` normally and audit the three-dot diff
- [x] Keep the ticket/PR branch free of unrelated stale-stack changes

## One Review readiness policy

- [x] Reuse existing Case completeness as the single readiness owner
- [x] Require complete instructions and images for every transition/return to Review
- [x] Prevent staff-review flags overriding incomplete evidence
- [x] Preserve existing behaviour that case-data edits demote to Not ready
- [x] Notify the operator that completeness must be confirmed again
- [x] Keep VAT optional
- [x] Keep Mileage optional and require Unit only with Mileage
- [x] Treat populated suggested values as usable
- [x] Default absent Inspection Date to the Export date

## Single Export

- [x] Collapse the duplicate mapping paths into one
- [x] Enforce Review server-side on the Export POST
- [x] Remove Case custody, Audit custody and accepted-only evidence as business gates
- [x] Retain technical package/image/replay/authorization checks
- [x] Preserve one POST surface, antiforgery and Content-Digest
- [x] Preserve deterministic thirteen-key JSON and eligible images
- [x] Record every export in ActionHistory and the once-per-Case first-sent proxy
- [x] Preserve exact replay and concurrent idempotency
- [x] Preserve deletion of duplicate hand-off routes, UI, MCP, ports and tables

## Documentation and migration

- [x] Update operator notes and FRD-07 to the one-Review/one-Export rule
- [x] Update capabilities/current architecture and remove stale custody/accepted-only claims
- [x] Keep the ADR-0030 direct pre-cutover migration with no rollback compatibility machinery
- [ ] Update the post-implementation report and PR description
- [x] Record the required simplification pass and dispositions

## Tests and proof

- [x] Core tests prove incomplete instructions/images cannot enter Review
- [x] Tests prove optional VAT/Mileage, conditional Mileage Unit and suggested values
- [x] Tests prove Inspection Date defaults on Export
- [x] Tests prove direct non-Review Export refusal
- [x] Tests prove package shape, history, proxy and replay semantics
- [x] Run locked restore and Release build
- [x] Run focused Core and Integration suites
- [ ] Complete the full non-corpus suite on the final build
- [x] Run `git diff --check` and scope audit
- [ ] Push normally and obtain green CI on the final head
- [ ] Hand off for independent Kanmer review
