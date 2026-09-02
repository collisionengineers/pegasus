---
id: UIIMP-011
type: ticket
title: Retarget the two cases--* snapshot state constants at the ported Search markup
status: backlog
area: ui-improvement
assignee: ''
profile: fix
labels:
  - ui
  - tests
groups:
  - EPIC-011
links:
  - CASE-026
  - UIIMP-005
archived: true
created: '2026-08-29T08:07:00.986Z'
updated: '2026-09-02T12:50:31.732Z'
---

## What

`tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` matches two `/Search`
states against pre-port markup, so snapshot capture reports both as missing in
update and verify modes after [[CASE-026]]:

- line 28 `["cases--empty"] = new("No matching cases.")` — the ported page
  renders the single settled sentence `No cases match these filters.`
  (the stuttering `No matching cases. No cases match these filters.` shipped
  by [[PLAT-029]] `865b4c0c` is gone).
- line 29 `["cases--unavailable"] = new("<h2>Cases are unavailable</h2>")` —
  the ported failure notice is `notice notice--danger` with `<strong>`, not
  the pre-port `status-card` `<h2>`.

The two committed snapshot pages
(`docs/design/test-ui/pages/cases--empty.html`,
`cases--unavailable.html`) need the same regeneration pass.

## Why

Two one-line constants gate the snapshot tooling; until they match the ported
page the `/Search` states cannot be captured or verified.

## Outcome

Already delivered by [[UIIMP-005]] in commit
`fbb4c2cde0a00fe408e6e2ab9b20aecc5e162d69`, which is reachable from
`main`. The two state constants now match the Search page:

- `cases--empty` matches `No cases match these filters.`
- `cases--unavailable` matches
  `<strong>Cases are unavailable</strong>`

The regenerated `docs/design/test-ui/pages/cases--empty.html` and
`cases--unavailable.html` contain the same current markup. No separate branch,
worktree, or PR is required; this duplicate backlog record is archived with its
delivery evidence preserved.
