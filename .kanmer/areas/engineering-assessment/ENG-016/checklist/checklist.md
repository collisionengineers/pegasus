# Checklist — ENG-016

## Git and scope

- [ ] Preserve unrelated staged user changes outside every ENG-016 commit
- [ ] Merge current `origin/dev` normally and audit the three-dot diff
- [ ] Keep the ticket/PR branch free of unrelated stale-stack changes

## One Review readiness policy

- [ ] Add the concrete required-detail and eligible-image policy to Core
- [ ] Apply it to initial entry and every transition/return to Review
- [ ] Apply it after edits while a Case is in Review
- [ ] Save invalidating edits and atomically move the Case to Not ready
- [ ] Return missing requirement labels and show the existing Case status notification
- [ ] Keep VAT optional
- [ ] Keep Mileage optional and require Unit only with Mileage
- [ ] Treat populated suggested values as usable
- [ ] Preserve the specified Instruction Date default
- [ ] Default absent Inspection Date to the Export date

## Single Export

- [ ] Collapse the duplicate mapping paths into one
- [ ] Enforce Review server-side on the Export POST
- [ ] Remove Case custody, Audit custody and accepted-only evidence as Export gates
- [ ] Retain only technical package/image/replay/authorization checks
- [ ] Preserve one POST surface, antiforgery and Content-Digest
- [ ] Preserve deterministic thirteen-key JSON and eligible images
- [ ] Preserve per-export ActionHistory and once-per-Case first-sent proxy
- [ ] Preserve exact replay and concurrent idempotency
- [ ] Preserve deletion of duplicate hand-off routes, UI, MCP, ports and tables

## Documentation and migration

- [ ] Update operator notes and FRD-07 to the one-Review/one-Export rule
- [ ] Update capabilities/current architecture and remove stale custody/accepted-only claims
- [ ] Keep the ADR-0030 direct pre-cutover migration and roll-forward wording
- [ ] Update the post-implementation report and PR description
- [ ] Record the required simplification pass and dispositions

## Tests and proof

- [ ] Core tests cover every required detail and eligible-image Review gate
- [ ] Tests prove optional VAT/Mileage, conditional Mileage Unit and suggested values
- [ ] Tests prove Inspection Date defaults on Export
- [ ] Tests prove invalidating edits demote to Not ready and notify
- [ ] Tests prove direct non-Review Export refusal
- [ ] Tests prove Review Export ignores custody/status duplication
- [ ] Tests prove package shape, digest, history, proxy and replay semantics
- [ ] Run locked restore and Release build
- [ ] Run focused Core/Architecture/Web/Integration suites
- [ ] Run full required test chunks and repository scripts
- [ ] Run `git diff --check` and final scope audit
- [ ] Push normally and obtain green CI on the final head
- [ ] Hand off for independent Kanmer review
