---
id: TICK-223
type: ticket
title: >-
  Dialog triggers must keep a static link target (script-off EVA Send
  unreachable from Case page)
status: backlog
area: ui-improvement
order: 1260
assignee: ''
profile: fix
labels:
  - ui
  - site-js
  - accessibility
  - wave-5
groups:
  - EPIC-011
links: []
refs:
  - docs/design/README.md
archived: false
created: '2026-08-28T15:36:16.887Z'
updated: '2026-09-03T15:15:29.535Z'
---

## What

The shell dialog system (PLAT-029's `site.js`) binds dialog triggers as
non-anchor elements: the binding never `preventDefault`s, so an anchor
trigger would both navigate and open the dialog. Record actions therefore
cannot be anchors, and pages whose only route to a sub-page was the dialog
trigger lose their static link.

Observed on CASE-012 (#599): with script off, nothing on the Case page links
to `/Cases/{id}/Eva/Send` — the EVA export page is unreachable from the Case
workspace without script; the retargeted `OperatorJourneyTests` export
journey navigates by URL as a recorded workaround.

## Approach

- Give the dialog trigger binding an anchor-intercept path (`click` +
  `keydown` Enter/Space `preventDefault` when the trigger is an anchor with
  `href`), so record actions can be anchors with real hrefs; or
- convert the shell dialog to the native `<dialog>` element and re-derive
  the focus/inert management from it.

Whichever is chosen, restore a static link target for every record action
that currently has none, starting with the Case page's EVA Send route.

## Verification

- [ ] With script disabled, `/Cases/{id}/Eva/Send` is reachable by keyboard
  from the Case workspace.
- [ ] `OperatorJourneyTests` export journey clicks the real link instead of
  navigating by URL.

## Notes

- Source: CASE-012 review round 1 (explicitly reported by the implementer).
- Site.js is PLAT-029's owned file; that ticket is in verifying, so this is
  the follow-up owner.
