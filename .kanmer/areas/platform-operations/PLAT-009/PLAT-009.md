---
id: PLAT-009
type: ticket
title: >-
  Rebuild the Approved mailboxes layout: data table and edit panel, not a form
  in a table cell
status: backlog
area: platform-operations
assignee: ''
profile: fix
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
updated: '2026-08-19T22:58:23.870Z'
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

- [ ] The policy row renders at normal table height with its data left-aligned and readable.
- [ ] Editing still works end to end (scope toggle, state, reason, save) — covered by the existing Web tests.
- [ ] Browser + AccessibilityTests green.
- [ ] Visual check at 1920 and 1366.

## Outcome
