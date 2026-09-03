---
id: DELIV-043
type: ticket
title: >-
  Cancel superseded repository-check runs and gate the heavy lanes behind the
  cheap invariant jobs
status: done
area: delivery-repository
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-09-03T19:24:35.091Z'
  review: '2026-09-03T19:30:31.553Z'
  verifying: '2026-09-03T23:51:19.996Z'
  done: '2026-09-03T23:52:31.641Z'
labels:
  - ci
links:
  - KANMER-011
  - UIIMP-013
commits:
  - 8cdbb3062913f8be335c46b72f75c07bee803090
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/653'
archived: false
created: '2026-09-03T19:23:48.496Z'
updated: '2026-09-03T23:52:39.271Z'
---

## What

Cancel superseded CI runs of the same pull request and make every heavy `.NET` lane wait for the three cheap invariant jobs (`documentation`, `local-development-scripts`, `reference-data`) so a broken link or catalogue no longer buys ~85 runner-minutes of tests whose verdict cannot change the outcome.

## Why

`repository-check` has no `concurrency` block: on `task/eng-028-multi-estimate-editor` two runs started 2m15s apart and both ran ~15 minutes; six runs accumulated on `task/auto-012-atomic-accept`. Run 33759702860 failed `documentation` after ~1.2 minutes and still spent 89 runner-minutes on unit, three SQL shards, browser and Test UI. The Kanmer test-churn audit of 2026-09-03 names both as the first Pegasus fixes; Kanmer's own generated workflow (planned) will replace this by hand-edit later, so the change is deliberately minimal: no job is renamed, merged or removed.

## Approach

- Workflow-level `concurrency: { group: <workflow>-<PR number or ref>, cancel-in-progress: <event is pull_request> }`; pushes to `main` are never cancelled.
- `infrastructure`, `unit`, `sql-integration`, `browser`, `test-ui`: `needs: [changes, documentation, local-development-scripts, reference-data]` (their `if` conditions unchanged).
- `sql-integration-coverage`: additionally skip when `sql-integration` was skipped, so a preflight failure does not produce a spurious artifact-download failure.
- No change to lane contents, path classification, timeouts or runners. `push: dev` CI and the duplicate Azure-plan invocation are recorded as follow-ups, not done here.

## Verification

- [ ] A PR whose `documentation` job fails starts zero heavy lanes (they show `skipped`).
- [ ] Two pushes to one PR branch within a minute leave one running `repository-check`; the older is `cancelled`.
- [ ] A green PR shows the same job set as before with every heavy lane `success`.

## Outcome

Shipped in PR #653, merged into dev at f479a948 on 2026-09-03. Proof PASS: heavy lanes started only after the three cheap jobs; all lanes green (test-ui needed one re-run after a 35-minute step-budget timeout unrelated to the diff). Follow-ups filed as one chore ticket (push: dev CI decision, duplicate Azure-plan invocation, coverage job !cancelled(), engineering.md wording).
