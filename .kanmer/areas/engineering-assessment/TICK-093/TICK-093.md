---
id: TICK-093
type: ticket
title: >-
  ENG-01 — One canonical repair specification with route provenance for Glass's,
  Audatex PDF, or an approved AI proposal
status: done
area: engineering-assessment
order: 230
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T09:05:03.087Z'
  review: '2026-08-19T11:05:25.746Z'
  implementing: '2026-08-19T11:17:31.308Z'
  verifying: '2026-08-19T12:16:19.642Z'
  done: '2026-08-20T01:29:43.129Z'
labels:
  - capability
  - ENG-01
  - later
groups:
  - EPIC-003
  - EPIC-004
links:
  - SIMPLI-014
  - TICK-205
  - TICK-098
  - PR-011
blocks:
  - TICK-096
  - TICK-097
  - TICK-098
  - TICK-081
  - TICK-092
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
commits:
  - 7c238f52
  - 28318bf4
  - b0596c9b
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/420'
deployment: production
archived: false
created: '2026-08-12T15:06:02.568Z'
updated: '2026-08-25T06:48:37.588Z'
---

## Outcome

Implemented one case-scoped canonical accepted repair specification. Each immutable version retains ordered estimate lines, source route/artifact/version/hash, accepted calculation inputs, Engineer acceptance, and correction lineage. Corrections create and accept a reasoned successor; accepted evidence is never overwritten. Exact-version and current-accepted queries support downstream snapshot binding.

Legacy estimate lines migrate to one explicit `LegacyUnresolved` draft without fabricated provenance or acceptance.

PR: https://github.com/collisionengineers/pegasus/pull/420

## Operator correction

[[TICK-205]] confirms that Audit does not require conservative/maximised specifications, a dual-specification aggregate, or uplift. [[PR-011]] is resolved: no Audit purpose/role vocabulary remains in Core, persistence, schema/migration, tests, or FRD-06.

## Boundaries

- [[TICK-092]] owns render-snapshot projection of the exact accepted version.
- Provider-specific extraction/mapping remains downstream.
- No report/template/FRD-11, provider integration, cloud, or `main` change is included.

## Verification

Corrected Release build passes with zero warnings/errors. Core Assessment is 45/45, focused SQL lifecycle/schema/legacy migration is 3/3, and architecture is 97/97. The proportional full run completed Core 640/640 and architecture 97/97 with no integration failures before its local ceiling; isolated PR CI is the authoritative completion gate.

## Governing document

`docs/frd/frd-06-vehicle-and-engineering-evidence.md`
