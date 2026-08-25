---
id: INTK-004
type: ticket
title: Unify intake decision codes and labels and project Operations case links
status: backlog
area: intake-processing
assignee: ''
profile: chore
labels: []
groups:
  - EPIC-002
links:
  - SIMPLI-010
archived: false
created: '2026-08-17T12:08:10.314Z'
updated: '2026-08-25T06:40:23.576Z'
---

## What

Own the single persisted intake decision-code and operator-label mapping, and project the accepted Case link into the Operations received-intake view.

## Why

Three related mismatches currently drift together:

- `EfOperationsStore.MapIntakeState`, `EfIntakeReceiptStore.ParseDecision`, and `IntakeMcpTools` enumerate overlapping decision-code vocabularies with different unknown-code behavior.
- Intake Details and Mail Message render separate label copies that disagree with the design authority.
- The current-architecture snapshot says Operations joins the actual Case link, while the received-intake row hard-codes no Case.

The copy audit in [[PLAT-015]] also found Mail Message exposing policy keys and versions, predicate codes, decision-version integers, and raw classification names. They belong to this same projection and label owner.

## Requirements

- Define one persisted intake decision-code vocabulary and one staff-facing label mapping; Operations, Intake, Mail, and MCP consume it.
- Decide and test the fail-closed behavior for an unknown persisted code.
- Include supported decisions such as Blocked intake and Image Intake exactly once.
- Resolve the Operations Case link through the accepted receipt link or association route rather than a hard-coded null.
- Keep policy keys, policy/version integers, predicate codes, and raw enum or kebab-case names out of ordinary Mail UI; retain them only where diagnostics require them.
- Refresh `docs/current-architecture.md` to match the resulting as-built projection.

## Verification

- [ ] Exactly one owner enumerates persisted decision codes and staff labels.
- [ ] Intake Details, Mail Message, Operations, and MCP agree for every supported code and fail closed consistently for an unknown code.
- [ ] Operations exposes the actual Case link for both direct links and accepted associations.
- [ ] Mail Message renders business labels rather than policy, predicate, version, or raw classification metadata.

## Outcome
