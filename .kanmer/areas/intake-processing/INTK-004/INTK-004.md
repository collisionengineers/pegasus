---
id: INTK-004
type: ticket
title: >-
  Reconcile intake decision labels and the Operations case-link claim with the
  code
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
updated: '2026-08-17T12:08:10.314Z'
---

## What

Two pre-existing doc/code mismatches surfaced by the PR #387 review of [[SIMPLI-010]]:

- `docs/design/README.md` label table says `OcrRequired` → "Needs text extraction" and `TechnicalFailure` → "Failed", but `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs:364-366` renders "Document text required" / "Technical failure" (and `Mail/Message.cshtml.cs` has its own copy). One of them is wrong; the design README binds (see memory: design authority).
- `docs/current-architecture.md:237` says Operations, retained Mail, Upload, MCP and retry surfaces "join the current allocation state and actual Case link", but the received-intake Operations row hard-codes `CaseId: null` (`src/Pegasus.Infrastructure/Persistence/EfOperationsStore.cs:158`).

## Why

Design README is the UI authority; current-architecture is the as-built snapshot — both must match the code or be corrected. Also the decision→label mapping exists twice in Web (Details / Message); one table.

## Verification

- [ ] Labels rendered by `Intake/Details` and `Mail/Message` equal the design README table (one mapping, one place).
- [ ] `docs/current-architecture.md` describes what the Operations received-intake row actually joins.

## Outcome
