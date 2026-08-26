---
id: TICK-206
type: ticket
title: Map renderer templates to capabilities and decide proposed retirements
status: done
area: documents-reports
order: 2090
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T09:02:12.676Z'
  implementing: '2026-08-25T06:49:10.268Z'
  review: '2026-08-25T06:49:10.606Z'
  verifying: '2026-08-25T06:49:28.833Z'
  done: '2026-08-25T06:49:29.132Z'
labels:
  - now
  - source-now
groups:
  - EPIC-004
links:
  - SIMPLI-015
  - SIMPLI-014
blocks:
  - TICK-081
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
deployment: n/a
archived: false
created: '2026-08-12T15:08:05.359Z'
updated: '2026-08-26T14:34:46.702Z'
---

## What

Map the imported renderer catalogue to the approved Pegasus capabilities and decide the unsupported entries' disposition.

## Decision

The Pegasus application exposes one typed rendererref1 assessment operation covering Total loss, Repairable, Cash in lieu, and Contract repair, plus the accepted fee-note artifact. The mapping belongs to RPT-01/RPT-02, with EXT-08/CASE-31/ENG-02 as the caller and source-data joins. Callers never select or discover a workspace template identifier.

Every other imported catalogue entry is unavailable and non-discoverable. Similar names do not activate Audit, diminution, addendum, valuation, evidence-pack, letter, Part 35, or generic-report behaviour.

## Outcome

Closed as a no-code acceptance slice on 2026-08-25. [[SIMPLI-014]] implemented and proved the closed Core contract, four outcomes, fee note, retired workspace/catalogue, fail-closed unsupported paths, and single Infrastructure adapter in PR #415 at `b548b674e31d05de6f43eeb285a25dedd7d2a768`. TICK-206 created no separate diff, PR, deployment, or cloud action.
