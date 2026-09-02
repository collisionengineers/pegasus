---
id: AUTO-018
type: ticket
title: >-
  AI market research job: external Cowork research completed through the
  Automation Actor into a valuation row and findings document
status: backlog
area: automation-integrations
assignee: ''
profile: feature
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
docs_todo: true
archived: false
created: '2026-09-02T20:31:38.846Z'
updated: '2026-09-02T20:34:04.283Z'
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
