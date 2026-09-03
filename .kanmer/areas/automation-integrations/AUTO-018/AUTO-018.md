---
id: AUTO-018
type: ticket
title: >-
  AI market research job: external Cowork research completed through the
  Automation Actor into a valuation row and findings document
status: review
area: automation-integrations
assignee: wf-build/auto-018
profile: feature
stageEntered:
  preparing: '2026-09-02T22:06:47.198Z'
  review: '2026-09-03T20:09:56.795Z'
taken_at: '2026-09-03T13:49:10.474Z'
branch: task/auto-018-market-research-job
worktree: .worktrees/auto-018
claim_expires_at: '2026-09-03T14:19:10.474Z'
claim_controller: wf-build/auto-018
lease_id: 271d34a0-7f79-4a7b-ab98-abeb2a6ea4e4
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\auto-018'
lease_phase: implementing
lease_heartbeat_at: '2026-09-03T13:49:10.474Z'
labels:
  - ai
  - valuation
  - automation-actor
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links:
  - TICK-083
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
prs:
  - '654'
archived: false
created: '2026-09-02T20:31:38.846Z'
updated: '2026-09-03T20:09:56.795Z'
---

## What

An AI job kind MarketResearch created from the Valuation section. The operator's external Claude Cowork connector claims it through the Automation Actor connector tools, searches AutoTrader, and completes it with a findings document plus retail and trade figures. Pegasus retains the document as Case evidence and records a valuation row of source "AI market research"; proposal only.

## Why

D35; the operator asked for the button back. The research runs outside Pegasus, so no AutoTrader integration or scraping is built here. Mockup source: `Pegasus_UI_v2_src/src/22-case-engineer.js` (`market-research`).

## Approach

- Reuse the AUTO-011 job ledger and `automation.jobs` tools: add the kind, a claim/complete contract carrying the document and figures, and the Valuation-section caller (lands with CASE-029).
- Result handling reuses ENG-027's valuation record and the document custody path.
- Kill switch and Action Logs as for every AI job.

## Verification

- [ ] Job appears in Operations; an external connector can claim and complete it with a document and figures.
- [ ] Completion yields one retained document and one valuation row; nothing is accepted automatically.
- [ ] Stopping automation stops new claims.

## Outcome
