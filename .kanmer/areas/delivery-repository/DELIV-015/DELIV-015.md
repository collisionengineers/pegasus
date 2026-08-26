---
id: DELIV-015
type: ticket
title: >-
  Release 16: merge all open PRs, deploy dev to production, verify every shipped
  ticket live, promote to main
status: done
area: delivery-repository
order: 940
assignee: claude-code
profile: chore
stageEntered:
  review: '2026-08-21T15:21:35.445Z'
  verifying: '2026-08-21T15:21:41.555Z'
  done: '2026-08-21T15:22:01.861Z'
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
deployment: production
archived: false
created: '2026-08-21T14:02:39.637Z'
updated: '2026-08-26T14:34:44.192Z'
---

# Why

Release 15 (deployed 6d04f89d, main=dev=f0b01f39) was followed by the operator's intake-regression remediation (PRs #493–#501) and the codex mail-workspace lanes (#490–#492), all merged to dev, plus five open PRs (#470, #473, #495, #496, #497) the operator decided to review, merge, and ship in this release. The operator directed a full deployment of everything on dev, live verification of every related ticket, and git hygiene back to three branches / two worktrees.

# Scope

- Phase 0: local dev synced/pushed; docs/principal-rules-and-mappings/ (QDOS rules doc) committed to dev.
- Review + merge the 5 open PRs serially on green CI.
- Lost-work audit of every merge since f0b01f39 (recorded in research).
- Build, validate, promote dev→main (two MERGE AUTH GRANTED gates), deploy (oras + azd provision + efbundle + worker config-zip), smoke.
- Refresh current-state docs; second promotion.
- Live-verify and close every related ticket; prune merged branches/worktrees.

# How to verify

Production smoke passes at the new SHA; migration head advances; grant readbacks match the censuses; the operator-reported regressions verify fixed live; board roster fully dispositioned; branches/worktrees reduced to the release-owned set.

# Outcome

**Release 16 shipped 2026-08-21.** Production serves `4111ad29` (digest sha256:3b891b45…, revision `--4111ad291779`, migration head `20260821100623`); main = dev = `adf0237e`. All five open PRs reviewed (one real defect fixed in review: #496's missing censuses; MAIL-004's visual gate performed and recorded) and merged; smoke passed; grants, automatic vehicle lookups (3 enqueued + 3 observed within one tick), mail workspace, CSP dialog fix, categories admin, assessment prefill, retained search with match locations, and the Deleted Items honest-unavailable state all verified live. 38 roster tickets closed to done; INTK-023, INTK-025, DOCS-006 remain at verifying awaiting the operator's post-wipe fresh test mail (extraction v4 / embedded-photo custody live-tier proof). Live-found defect filed: [[INTK-027]] (re-evaluation after staging cleanup). Git hygiene: 0 open PRs; all release-owned branches and worktrees pruned; other sessions' active lanes (task/plat-018, .worktrees/UIOPER-001) deliberately untouched. Operator actions remaining: post-wipe test mail, test-data wipe.
