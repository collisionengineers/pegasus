---
id: INTK-019
type: ticket
title: Replace Triage “Assign to me” with Engineer selection
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - triage
  - assignment
  - ui
  - operator-reported
links:
  - AUTO-004
  - AUTO-005
refs:
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-20T10:30:55.359Z'
updated: '2026-08-20T10:31:58.511Z'
---

## What

Replace the Triage detail page’s actor-relative “Assign to me” and “Reassign to me” controls with one explicit `Assign to:` Engineer selector (uses a dropdown box). Retire the “Assign to me” function and wording.

## Why

Triage assignment is administrative allocation of work to a named Engineer, not an action whose target is implicitly the current user. An Engineer may assign Triage to themselves, but only by selecting their own name from the same Engineer list—for example, John selects `Assign to: John`. There must be no separate “Assign to me” shortcut or operation.

This distinction also keeps staff and Automation callers aligned around one explicit assignee contract: the assignee is selected data, while the acting principal remains separately attributed in permanent history. It avoids encoding a Web-only actor-relative command that cannot be represented consistently for the Automation Actor work in [[AUTO-004]] and [[AUTO-005]].

## Approach

- Present an eligible-Engineer selector labelled `Assign to:` on Triage detail.
- Permit any authorised staff member, including an Engineer, to select any eligible Engineer; selecting oneself uses the same control and command as selecting someone else.
- Preserve the acting principal separately from the selected assignee in authorization, operation replay, and permanent history.
- Keep explicit unassignment/reassignment behavior reasoned and version-guarded, but remove every “Assign to me”/“Reassign to me” UI and caller assumption.

## Verification

- [ ] The Triage page contains no “Assign to me” or “Reassign to me” control or operation.
- [ ] `Assign to:` lists only eligible Engineers and submits the selected Engineer’s identity.
- [ ] An Engineer can select their own name through the same selector used for every other Engineer.
- [ ] Administrator/User/Engineer authorization follows the accepted staff-role matrix, and assignment history distinguishes acting principal from selected assignee.
- [ ] Stale version, invalid/ineligible Engineer, missing reason, and replay/conflicting operation cases fail closed.
- [ ] AUTO-004/AUTO-005 use the same explicit-assignee Core contract rather than inventing an Automation-specific assignment rule.

## Outcome
