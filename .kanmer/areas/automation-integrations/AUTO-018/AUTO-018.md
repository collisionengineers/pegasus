---
id: AUTO-018
type: ticket
title: >-
  AI market research job: external Cowork research completed through the
  Automation Actor into a valuation row and findings document
status: done
area: automation-integrations
assignee: wf-build/auto-018
profile: feature
stageEntered:
  preparing: '2026-09-02T22:06:47.198Z'
  review: '2026-09-03T20:09:56.795Z'
  verifying: '2026-09-04T06:14:28.102Z'
  done: '2026-09-04T07:13:28.634Z'
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
delivery_state: integrated
delivery_branch: dev
delivery_sha: 80f0ca262b0fe2ca354a5dfb18933dc3f105b917
delivery_recorded_at: '2026-09-04T06:14:29.639Z'
archived: false
created: '2026-09-02T20:31:38.846Z'
updated: '2026-09-04T07:14:25.926Z'
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
