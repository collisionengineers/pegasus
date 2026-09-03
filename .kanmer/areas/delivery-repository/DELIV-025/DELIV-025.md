---
id: DELIV-025
type: ticket
title: Run each CI job only for relevant changed paths
status: backlog
area: delivery-repository
order: 220
assignee: ''
profile: fix
labels:
  - ci
  - github-actions
  - efficiency
links:
  - TICK-200
deployment: n/a
archived: false
created: '2026-08-26T17:34:29.481Z'
updated: '2026-09-03T15:15:27.348Z'
---

## What

Make the GitHub Actions workflow run each validation lane only when the changed paths can affect what that lane proves. Skill-only changes should receive focused skill and Markdown validation without LocalDB, migration, Azure-plan, reference-data, .NET, browser, or SQL work.

## Why

The current workflow correctly classifies skill-only changes as `Build = false` and `Infrastructure = false`, but four jobs still run unconditionally. Three are unrelated, while the relevant `quick_validate.py` skill check is absent. This wastes Actions time and makes CI evidence less meaningful. Related to the completed wall-clock work in [[TICK-200]].

## Approach

- Extend path classification to the currently unconditional lanes.
- Add focused validation for changed skills and retain Markdown link validation where applicable.
- Keep fail-closed full validation when path detection fails.

## Verification

- [ ] A skill-only fixture schedules only focused skill/Markdown checks.
- [ ] Documentation, scripts, reference data, infrastructure and application fixtures schedule their owning lanes.
- [ ] An unknown or failed change classification schedules all safety-critical lanes.

## Outcome
