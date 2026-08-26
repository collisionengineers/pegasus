---
id: PLAT-003
type: ticket
title: Wire real outstanding counts into the operator rail
status: done
area: platform-operations
order: 1440
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-20T05:16:13.327Z'
  review: '2026-08-20T05:25:12.132Z'
  verifying: '2026-08-20T05:38:15.659Z'
  done: '2026-08-20T12:46:35.074Z'
labels:
  - ui
  - design
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
prs:
  - '458'
deployment: production
archived: false
created: '2026-08-18T09:39:12.271Z'
updated: '2026-08-26T14:34:45.194Z'
---

## What

Populate the `ViewData["RailCounts"]` mechanism in `_Layout.cshtml` with real per-route outstanding work figures, so an operator sees where the work is without opening anything.

## Why

The left rail (shipped in [[PLAT-001]]) supports a count badge per route, but no page supplies one — so no count renders. FRD-12 requires "clear counts that link to their exact filtered work and do not render stale zero placeholders" (line 13). Rendering nothing is correct until a real figure exists, but the figures should be wired. The Dashboard already has real Core count queries for `UI-02` and `UI-04` (deployed in release 6); the rail needs its own per-request query in the shell.

## Approach

- Decide the query: each route's count is the outstanding work for that route (Inbox = unread, Queues = not-ready + review + held, Cases = active, Operations = retryable, Upload = n/a or 0, Administration = n/a).
- Add a shell-level query (composition-gated) that supplies `ViewData["RailCounts"]` on each authenticated request.
- Respect the FRD-12 freshness rule: a count is a figure a page already queried, never one the shell invents; an absent count renders nothing.
- No stale zero placeholders.

## Verification

- [ ] Each rail route shows its real outstanding count when work exists.
- [ ] No count renders when the figure is unavailable (not a `0` placeholder).
- [ ] `dotnet build --configuration Release` and the focused test profile pass.

## Outcome
