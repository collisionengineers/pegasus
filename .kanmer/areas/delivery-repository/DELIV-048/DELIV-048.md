---
id: DELIV-048
type: ticket
title: Restore Windows and Linux release workstation support
status: preparing
area: delivery-repository
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-09-07T20:56:46.944Z'
labels:
  - ci
  - tests
  - follow-up
groups:
  - EPIC-013
  - EPIC-014
links: []
docs_todo: true
archived: false
created: '2026-09-04T11:58:34.805Z'
updated: '2026-09-07T20:56:46.944Z'
---

## What

Audit and revamp CI only after the WSL, database, accessibility and release contracts are settled.

## Why

Changing CI now would encode unresolved platform assumptions and duplicate troubleshooting.

## Verification

- [ ] Every retained CI gate proves a named behavior against the final Linux toolchain and speculative gates are removed.

## Outcome

## Current scope — 7 September 2026

The operator explicitly requires Windows and Linux development and deployment.
This supersedes the prior Linux-only CI-cleanup premise in EPIC-013; that
original description is retained above as history, not the current outcome.
Under [[EPIC-014]], correct the existing release scripts to emit a migration
bundle matching the x64 release workstation (Windows or Linux), preserve
Linux Web/Worker/OCI deployment, and align current guidance and ADR authority.
No CI redesign, extra test lanes, packages, cloud writes or deployment here.

## Current acceptance

- [ ] Both supported x64 workstations use the same direct release route.
- [ ] Manifest validation rejects wrong bundle/runtime pairs and host mismatch;
  Linux owner-execute checks never run on Windows.
- [ ] Linux Web/Worker packages, OCI linux/amd64 and existing approval gates stay.
- [ ] ADR-0039 supersedes ADR-0037; current instructions agree.
- [ ] Focused script checks and root-owned release validation are recorded.
