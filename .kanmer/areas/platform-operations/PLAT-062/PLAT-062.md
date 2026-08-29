---
id: PLAT-062
type: ticket
title: >-
  Add administrator-configurable instruction/image completeness and
  chase-interval settings
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - backend
  - administration
  - workflow
  - follow-up
groups:
  - EPIC-011
links:
  - PLAT-025
archived: false
created: '2026-08-29T10:23:02.768Z'
updated: '2026-08-29T10:23:02.768Z'
---

## What

EPIC-011 `context.md` §1.12 names the Workflow configuration admin surface as:

> Instruction completeness (2 checkboxes), Review (2 checkboxes), Due work
> (chase interval); Save configuration.

PLAT-025 ported the page and could only wire the "Review (2 checkboxes)"
group — `RequireStaffInstructionReviewBeforeEngineerAssignment` and
`RequireStaffImageReviewBeforeEngineerAssignment` — because that is the only
part of the contract with real Core backing. The other two groups have none:

- **Instruction completeness (2 checkboxes).** There is no administrator-
  configurable completeness *policy* anywhere in `Pegasus.Core` — instruction
  and image completeness (`CaseReadinessEvidence.InstructionsComplete` /
  `ImagesComplete`) are per-case evidence flags, not a toggleable rule.
- **Due work (chase interval).** `CaseWorkScheduling.cs` fixes the chase
  interval as a constant; there is no admin-configurable global interval
  setting, port, or persisted column.

## Why

The redesign contract calls for both groups, but building them for real needs
a new Core port, a persistence change, and a migration — explicitly out of
scope for PLAT-025 (a wave-2 UI-port lane may not add a migration or a new
Core port; EPIC-011 migrations are serialized in wave 3). Per AGENTS.md rule
22 / EPIC-011 D19 this is deferred to its own ticket rather than built
inline or left silently unaddressed.

## Approach

- Needs an explicit operator decision on what the two completeness checkboxes
  and the chase-interval control should actually govern — the two-line
  contract sentence does not specify the rule, only the control shapes.
- Add the Core port/config surface once that decision exists, its persistence
  and migration, then extend `Pages/Administration/Configuration.*` (PLAT-025's
  file) to render the additional controls through it.

## Verification

- [ ] The approved backend design supports administrator-configurable
      instruction/image completeness and a chase interval.
- [ ] `Pages/Administration/Configuration.*` renders all three groups from the
      contract, each backed by a real Core setting.

## Outcome
