---
id: PLAT-009
type: ticket
title: >-
  Rebuild the Approved mailboxes layout: data table and edit panel, not a form
  in a table cell
status: review
area: platform-operations
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-19T23:02:07.263Z'
  review: '2026-08-19T23:45:18.497Z'
taken_at: '2026-08-19T23:02:40.159Z'
branch: task/plat-009-mailboxes-layout
worktree: ../pegasus-worktrees/plat-009-mailboxes-layout
labels:
  - ui
  - administration
  - design
links:
  - DELIV-012
refs:
  - docs/design/README.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-19T22:58:23.870Z'
updated: '2026-08-19T23:45:18.497Z'
---

## What

`/Administration/Mailboxes` renders its whole per-row **edit form inside the last column of the CURRENT POLICIES table**, so the one policy row stretches to the form's full height (~600px) and the row's data — address, route scope, state chip, polling, version — floats vertically centred in a void. Restructure: a compact data table (one row per mailbox, normal row height), with the update form in its own panel following the page's existing panel conventions (the "Add an approved address" panel below it is already the right shape).

## Why

Surfaced by [[DELIV-012]]'s release-12 production verification screenshot and reported by the operator. **Verified pre-existing, not a release-12 regression**: the page's markup has zero commits between `d8de29cb` (release 10) and `ed3be51c` (release 12), and release 12's CSS diff touches only the PLAT-006 shell/upload rules — identical DOM and CSS render identically. The defect predates the release; the screenshot was the first close look the page ever got.

## Constraints

- Form field names, handler (`OnPostUpdateAsync`), antiforgery, version/operation-key semantics must not change — layout only.
- `docs/design/README.md` binds: existing panel/table/notice conventions, no inline styles, state never by colour alone.
- The browser/accessibility suites cover administration routes; they must stay green.

## Verification

- [x] The policy row renders at normal table height with its data left-aligned and readable — confirmed via real screenshots at 1920 and 1366 (two-mailbox estate).
- [x] Editing still works end to end (scope toggle, state, reason, save) — covered by the existing Web tests (56/56 passing, including the immutability, missing-identity and duplicate-address paths).
- [x] Browser + AccessibilityTests green — 37/37, including axe scan of `/Administration/Mailboxes` with the new markup (0 violations, 1 h1, no inline styles).
- [x] Visual check at 1920 and 1366 — done via the Playwright test harness (BrowserTestSupport), see post-implementation-report.

## Outcome

Table restructured to a compact 5-column data table (Address/Route scope/State/Polling/Version); each mailbox's update form moved into its own `panel form-panel` below the table, `aria-labelledby` a heading naming its address. Also stripped UI narration duplicating `docs/runbook.md` and fixed the banned "intake" word in operator-facing copy (route-scope label centralized into `OperatorLabels.RouteScope`, following a simplification-pass finding). See plan.md for the full change log and simplification-pass disposition.
