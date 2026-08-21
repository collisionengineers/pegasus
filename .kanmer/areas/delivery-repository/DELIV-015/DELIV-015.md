---
id: DELIV-015
type: ticket
title: >-
  Release 16: merge all open PRs, deploy dev to production, verify every shipped
  ticket live, promote to main
status: implementing
area: delivery-repository
assignee: claude-code
profile: chore
taken_at: '2026-08-21T14:03:41.580Z'
branch: task/deliv-015-release-16
worktree: ../pegasus-worktrees/deliv-015
labels:
  - release
  - deployment
  - requires-live-approval
  - git-hygiene
links: []
refs:
  - docs/runbook.md
  - docs/engineering.md
  - docs/operations.md
archived: false
created: '2026-08-21T14:02:39.637Z'
updated: '2026-08-21T14:03:41.580Z'
---

# Why

Release 15 (deployed 6d04f89d, main=dev=f0b01f39) was followed by the operator's intake-regression remediation (PRs #493–#501) and the codex mail-workspace lanes (#490–#492), all merged to dev, plus five open PRs (#470, #473, #495, #496, #497) the operator decided to review, merge, and ship in this release. The operator directed a full deployment of everything on dev, live verification of every related ticket, and git hygiene back to three branches / two worktrees.

# Scope

- Phase 0 (done): local dev synced/pushed; docs/principal-rules-and-mappings/ (QDOS rules doc) committed to dev.
- Review + merge the 5 open PRs serially on green CI.
- Lost-work audit of every merge since f0b01f39 (recorded in research).
- Build release artifacts at the pinned dev SHA; validate; promote dev→main (exact-SHA fast-forward, MERGE AUTH GRANTED required); deploy web image via oras+azd provision, migrations via efbundle, worker via config-zip; smoke.
- Refresh docs/current-architecture.md and docs/operations.md; second promotion.
- Live-verify and close every related ticket; prune merged branches/worktrees.

# How to verify

Production smoke passes at the new SHA; migration head advances to the release head; both runtime-role grant readbacks match the censuses; the six operator-reported regressions verify fixed live; board roster fully dispositioned; `git branch -a` = main/dev/kanmer-board, worktrees = pegasus + kanmer.

# Outcome

(closeout)
