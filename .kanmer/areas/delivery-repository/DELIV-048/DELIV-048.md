---
id: DELIV-048
type: ticket
title: Reassess CI and test lanes after WSL convergence
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels:
  - ci
  - tests
  - follow-up
groups:
  - EPIC-013
links: []
archived: false
created: '2026-09-04T11:58:34.805Z'
updated: '2026-09-04T11:58:34.805Z'
---

## What

Audit and revamp CI only after the WSL, database, accessibility and release contracts are settled.

## Why

Changing CI now would encode unresolved platform assumptions and duplicate troubleshooting.

## Verification

- [ ] Every retained CI gate proves a named behavior against the final Linux toolchain and speculative gates are removed.

## Outcome
