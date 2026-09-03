---
id: AUTO-010
type: ticket
title: 'Administration: Automation & AI settings with job counts and kill switch'
status: backlog
area: automation-integrations
order: 20
assignee: ''
profile: feature
labels:
  - ui
  - wave-4
  - administration
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
archived: false
created: '2026-08-28T08:35:24.084Z'
updated: '2026-09-03T15:15:26.904Z'
---

## What

Wave 4 of [[EPIC-011]]. Second pass on `Pages/Administration/Automation/**` after [[AUTO-006]]: Automation panel (status, registered clients, active/failed job counts, Stop/Start automation with reason) and AI settings panel (proposal, timeout, enabled — backed by `ISendToAiControl` and `IAiChannelConnectorStore`).

## Owns

`src/Pegasus.Web/Pages/Administration/Automation/**`, tests.

## Blocked by

[[AUTO-006]], the AI job ledger ticket.
