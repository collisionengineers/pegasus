---
id: DELIV-044
type: ticket
title: >-
  Decide push:dev CI, drop the duplicate Azure-plan invocation, and tighten the
  coverage job to !cancelled()
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels:
  - ci
links:
  - DELIV-043
archived: false
created: '2026-09-03T23:52:42.122Z'
updated: '2026-09-03T23:52:42.122Z'
---

## What

Three small `repository-check` follow-ups left open by DELIV-043 (PR #653) and its review, each needing an operator decision or a one-line edit:

1. **`push: dev` CI.** `ci.yml` runs on pull requests and pushes to `main` only, so the integrated state of `dev` is never verified after a merge (DELIV-035 found a broken `dev` this way). Adding `push: branches: [dev]` gives every merge a bound run at the exact integrated SHA (what Kanmer's planned verification receipts consume) at the cost of one full run (~27 minutes wall, ~90 runner-minutes) per merge. Decide: add it, or wait for Kanmer's generated workflow.
2. **Duplicate Azure deployment-plan invocation.** `Test-AzureDeploymentPlan.ps1 -Mode Local` runs unconditionally in `changes` and again in `infrastructure`; drop the second (or the first, keeping the classifier gate).
3. **Coverage job under cancellation.** `sql-integration-coverage` uses `always()`, so when a superseded PR run is cancelled (new since DELIV-043) the shards report `cancelled` and the coverage job runs and fails against missing artifacts; `!cancelled()` is the tighter form. Also optional: a sentence in `docs/engineering.md` "Branches and delivery" saying a lane skipped because an upstream job failed is not green.

## Why

Recorded by the DELIV-043 review (findings F1, F3, F7) so they exist on the board rather than only in ticket prose. None blocks anything; items 2 and 3 are one-line edits that can ride the next `ci.yml` change; item 1 is a cost decision for the operator.

## Verification

- [ ] Operator decision on item 1 recorded here.
- [ ] Items 2 and 3 applied in one `ci.yml` PR (or explicitly superseded by Kanmer's generated workflow).

## Outcome
