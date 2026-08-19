---
id: TICK-207
type: ticket
title: Define the missing Audit renderer template
status: verifying
area: documents-reports
order: 20
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T09:04:30.646Z'
  review: '2026-08-19T09:37:36.335Z'
  verifying: '2026-08-19T09:38:07.704Z'
taken_at: '2026-08-19T09:35:39.871Z'
branch: task/tick-207-audit-template-deferral
worktree: ../pegasus-worktrees/tick-207-audit-template-deferral
labels:
  - now
  - source-now
  - decision-required
groups:
  - EPIC-004
links:
  - TICK-098
  - SIMPLI-015
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
deployment: n/a
archived: false
created: '2026-08-12T15:08:05.409Z'
updated: '2026-08-19T09:38:07.704Z'
---

## What

Resolve the missing Audit renderer-template question without inventing unavailable evidence.

## Why

RPT-03 has an accepted data direction, but Pegasus has no supplied or approved representative Audit report/template from which to derive fixed wording, layout, fields, conditional rules, signatures, or visual acceptance.

## Approach

Audit rendering remains unavailable and fails closed until a concrete representative Audit artifact is supplied and explicitly approved. Assessment evidence in `reference/rendererref1/`, generic expert-report templates, and the imported renderer catalogue are not Audit authority.

## Verification

- [x] The deferral, fail-closed boundary, prohibited substitutes, future activation evidence, and owners are explicit.
- [x] Completion is recorded at the deferral/closed-boundary tier only; no Audit template, renderer action, or RPT-03 delivery is claimed.

## Notes

- Prohibited substitutes: no assessment clone, generic expert fallback, caller-authored blocks, placeholder, dormant descriptor, disabled feature, inferred legal wording, fabricated reference artifact, or guessed signature/fee treatment.
- [[TICK-205]] supplies the accepted dual immutable conservative/maximised data decision.
- [[TICK-098]] remains the RPT-03 capability owner and cannot claim Audit rendering until this evidence gate is satisfied.
- [[SIMPLI-014]] remains assessment/fee-note only and must expose no Audit family.
- When an actual representative artifact is supplied, create a new linked activation ticket to research it and obtain explicit approval of wording, layout, field rules, comparison labels, conditional behaviour, signatures, and representative minimal/long cases before modifying FRD-11, Core, or templates.

## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.

## Outcome

The missing Audit template is explicitly **deferred**, not guessed. No approved representative Audit artifact currently exists, so Audit rendering and template registration remain absent and unavailable. TICK-207 makes no repository, FRD, Core, Infrastructure, template, reference, artifact, deployment, Azure, Worker, or `main` change. The next product action is triggered only by receipt of a concrete representative Audit artifact; that artifact requires explicit approval through a new linked activation ticket before implementation.
