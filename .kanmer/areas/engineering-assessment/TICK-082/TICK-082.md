---
id: TICK-082
type: ticket
title: >-
  EXT-09 — Versioned repair-estimate lines, source versions, global labour-rate
  cards, and per-version VAT
status: backlog
area: engineering-assessment
order: 1110
assignee: ''
profile: feature
labels:
  - capability
  - EXT-09
  - now
  - requires-live-approval
  - work-pack-activated
groups:
  - HZN-002
  - EPIC-009
links: []
blocks:
  - TICK-081
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-12T15:05:40.173Z'
updated: '2026-09-03T15:15:29.228Z'
---

## What

Deliver EXT-09 as decided in D17: versioned repair-estimate lines with immutable imported provider versions and printed totals; multiple global versioned labour-rate cards (id, name, non-paint hourly rate, enabled state, actor, timestamps) selected per new or amended estimate version; non-paint labour = normalized non-paint hours × the selected card rate; paint labour, paint materials, parts and other costs explicit; VAT % belongs to the estimate version and applies to the whole subtotal; betterment and guide codes are evidence only. No original-versus-assessed comparison and no savings feature.

## Why

Brought forward to Now on 2026-08-28 for EPIC-011; D17 confirmed binding on 2026-09-01 (EPIC-011 `context.md`, `decisions/2026-09-01-work-pack.md`). `docs/capabilities.md` EXT-09 and FRD-06/11/12 still describe comparison and savings; [[DELIV-040]] corrects FRD-04/06/11/12, `capabilities.md`, `open-decisions.md` and the design README first, so this ticket leaves Backlog after that merge.

## Approach

- Core: `Core/Assessment/Estimates.cs` (estimate versions, card selection, VAT on the whole subtotal — reuse [[ENG-026]]'s per-estimate VAT and [[ENG-028]]'s editor), a rate-card entity, store and one migration (`migration` lock, `Test-MigrationGrants.ps1`).
- Administration: a rate-card area per FRD-12 § Administration (the planner decides whether the optional NEW-RATE-CARD-ADMIN split from the pack ledger is warranted).
- Disabling a card blocks future selection without changing history; an Engineer successor version selects a card and can become the accepted/report version; imported provider versions and their printed totals are immutable.
- Blocks [[TICK-081]] (report activation).

## Verification

- [ ] Card create, rename, enable and disable are administrator-only, versioned and attributed; a disabled card is unselectable for new versions and history is unchanged.
- [ ] Non-paint labour and VAT derive deterministically from the selected card and the estimate-version VAT % (unit tests with literal comparisons).
- [ ] Imported provider versions and printed totals are immutable; a successor version selects a card.
- [ ] No comparison or savings figure is computed or rendered.
- [ ] Migration, grants and `Test-MigrationGrants.ps1` pass; Core and integration tests green.

## Notes

Source: `docs/capabilities.md` EXT-09; D17 in EPIC-011 `context.md`. Retitled 2026-09-01 (comparison and savings dropped); body rewritten 2026-09-02 to match.

## Outcome
