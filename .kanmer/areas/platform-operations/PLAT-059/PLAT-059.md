---
id: PLAT-059
type: ticket
title: Settle the "Create Case" entry point so one label has one destination
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - ui
  - shell
  - case-creation
  - work-pack
  - wave-B
groups:
  - EPIC-011
links:
  - CASE-026
  - PLAT-029
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-29T08:06:53.293Z'
updated: '2026-09-02T01:10:21.029Z'
---

## What

Give "Create Case" one destination at every call site — the Add dialog (`Pages/Shared/_ShellDialogs.cshtml`), the Ctrl N shortcut (`wwwroot/js/site.js`), the Search header (`Pages/Search/Index.cshtml`) and the Work Centre header (EPIC-011 context §1.2) — and make that destination the D26 flow: staff enter the required identity and attach or record the instruction; Pegasus persists an attributable intake receipt (actor, time, material), then reuses the normal principal and Case/PO allocation policy. `Pages/Cases/Create.cshtml(.cs)` stays receipt-bound; the new entry produces the receipt it needs.

## Why

Operator decision D26 (binding 2026-09-01, EPIC-011 `context.md`; `decisions/2026-09-01-work-pack.md` § Direct Case creation). Two shipped controls resolve to a route that does not exist today, and one label with two destinations breaks one-list-per-concept. The Add dialog keeps Upload files and Create Case as separate entries (context §1.1); direct creation is not a second allocation implementation (rule 7).

## Approach

- Reuse the intake-receipt producer the working call sites already use (`Presentation/UploadOutcome.cs`, `Pages/Intake/Details.cshtml`) and the existing allocation use case; no parallel allocation.
- Files: the shell files owned by the `global_shell` lock (`Pages/Shared/_ShellDialogs.cshtml`, `wwwroot/js/site.js`), `Pages/Cases/Create.*`, `Pages/Search/Index.cshtml`, `Pages/Index.cshtml` header, tests; labels in `Presentation/OperatorLabels.cs`.
- Governing documents: FRD-02 § Ways intake starts / INT-26 and FRD-12 § Add dialog carry D26 once [[DELIV-040]] merges; this ticket leaves Backlog after that.
- Blocked by [[DELIV-040]]; related [[CASE-026]], [[PLAT-029]].

## Verification

- [ ] Every Create Case call site resolves to one route; no 404.
- [ ] A Case created directly has an intake receipt (actor, time, material) recorded before allocation, and allocation reuses the existing use case (the test names it).
- [ ] The Add dialog keeps Upload files and Create Case as separate entries.
- [ ] Browser lane green; Test UI snapshots regenerated for the changed routed pages.

## Outcome
