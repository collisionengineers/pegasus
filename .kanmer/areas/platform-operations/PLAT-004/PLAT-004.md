---
id: PLAT-004
type: ticket
title: Place or retire the four unplaced commissioned marks
status: backlog
area: platform-operations
assignee: ''
profile: chore
labels:
  - ui
  - design
links: []
refs:
  - docs/design/README.md
archived: false
created: '2026-08-18T09:39:12.354Z'
updated: '2026-08-18T09:39:12.354Z'
---

## What

Decide whether to place the four supplied-but-unplaced commissioned marks (`activity.png`, `brand.png`, `calendar.png`, `casefolder.png`) on surfaces in the application, or formally retire them.

## Why

[[PLAT-001]] adopted 14 commissioned marks and placed 10 in the markup. Four (`activity`, `brand`, `calendar`, `casefolder`) are supplied but not referenced by any screen. The marks README and the design authority both list them as "supplied, not yet placed." A decision is needed: either find surfaces for them or remove them from the register so the asset list is honest.

## Approach

- Review the 14 source PNGs and the current screens for surfaces that would benefit from a mark.
- If surfaces exist: place the marks, update the design authority register, and add SHA-256 entries.
- If no surfaces exist: retire them — remove from the marks README's expected-files table and update the design authority to say only 10 were placed.

## Verification

- [ ] Each of the four marks is either placed in markup with a SHA-256 entry, or removed from the register with a reason.
- [ ] `docs/design/README.md` is consistent with the decision.

## Outcome
