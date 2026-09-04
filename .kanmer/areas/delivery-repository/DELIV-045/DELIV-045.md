---
id: DELIV-045
type: ticket
title: >-
  Refresh current-architecture for the Case Workspace v2 and open the dev→main
  release PR
status: preparing
area: delivery-repository
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-09-04T10:13:38.677Z'
labels:
  - docs
  - release
  - case-workspace-v2
groups:
  - EPIC-012
links:
  - DELIV-030
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-04T09:53:12.069Z'
updated: '2026-09-04T10:13:38.677Z'
---

## What

Replaces the archived [[DELIV-030]] (delivered by PLAT-067 for release 38) for [[EPIC-012]]'s final step. Refresh `docs/current-architecture.md` to the as-built v2 Case workspace: the single-scroll record and its eleven sections, the `/Cases/{id}/Section` fragment route, the retired `/Cases/{id}/Assessment` 301, the Engineer-notes, sign-off, storage-location, valuation guide-month, report-image-curation and vehicle-record stores, the MarketResearch AI job, the Awaiting instruction queue, and Operations without the service-health table; re-check the design README's source-and-runtime map. Then open — never merge — the release PR from `dev` to `main` listing every EPIC-012 ticket with its PR and merge SHA, the final verification SHA and proof pointer, and the two pre-merge conditions below.

`docs/operations.md` describes the deployed state and is refreshed in the deploy task, not here.

## Why

CLAUDE.md requires the as-built snapshot to match what ships. The release boundary is human (`MERGE AUTH GRANTED`), and `origin/main` currently carries two direct-push commits that `dev` lacks, so `Test-MainBranchHistory.ps1` would fail a push to `main` until an administrator reconciles them; the PR records both conditions instead of merging.

## Approach

- Edit the existing canonical files only; no new Markdown.
- Read each shipped ticket's Outcome and merge SHA from the board and verify every claim in the merged tree with grep before writing it.

## Verification

- [ ] `./scripts/Test-DocumentationLinks.ps1` passes and the documentation CI lane is green.
- [ ] The release PR is open against `main` with the ticket table and both conditions, and is not merged.

## Outcome
