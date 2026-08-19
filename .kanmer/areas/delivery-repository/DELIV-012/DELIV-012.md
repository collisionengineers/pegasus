---
id: DELIV-012
type: ticket
title: >-
  Release 12: quality-review every post-release-10 ticket, integrate all open
  PRs, restore git hygiene, deploy and verify production
status: implementing
area: delivery-repository
assignee: claude-code
profile: custom
requires:
  leave-preparing:
    - research
    - plan
    - checklist
    - questions-resolved
  enter-review:
    - post-implementation-report
    - questions-resolved
  enter-done:
    - proof
    - questions-resolved
stageEntered:
  preparing: '2026-08-19T12:12:51.866Z'
taken_at: '2026-08-19T13:09:02.340Z'
branch: task/deliv-012-release-12
worktree: ../pegasus-worktrees/deliv-012-release-12
labels:
  - release
  - deployment
  - git-hygiene
  - quality-review
links:
  - DELIV-011
  - PLAT-006
refs:
  - docs/runbook.md
  - docs/engineering.md
  - docs/operations.md
archived: false
created: '2026-08-19T12:12:34.359Z'
updated: '2026-08-19T13:09:02.340Z'
---

## What

One self-contained delivery ticket that (1) establishes exactly what production serves today and what has landed since, (2) quality-reviews every ticket in Review / Verifying / Done since that deployment — PR comments, scope drift, dark or orphaned features, failing CI — and remediates verified findings through scoped subagents, (3) integrates every open PR into `dev` in a safe order, (4) restores git hygiene to three remote branches (`main`, `dev`, `kanmer-board`), three local branches and two worktrees (main checkout + `.worktrees/kanmer`), and (5) runs the full numbered release (12) to Azure, verifying UI changes in the browser and backend changes through the Azure CLI / endpoints.

## Why

Production still serves release 10 (`d8de29cb`). Release 11 ([[DELIV-011]], `feda958f`) was fully prepared but **held** by the operator on 2026-08-19 before the `main` push; since then `dev` has moved far past it (renderer integration, mailbox identity, classification policy, Upload redesign [[PLAT-006]], …) and six task PRs are open in Review. The estate needs one careful, evidenced release that carries *everything* merged, nothing shipped dark, and leaves the repository clean. This ticket supersedes [[DELIV-011]], whose local artefacts are stale.

## Approach

- **Research (three documents):** `research/current-estate.md` (Azure read-only diagnostics: last deployment, revision, image digest, worker package, DB migration head, settings census), `research/codebase-evidence.md` (dev-vs-main diff, merges since the last deploy, CI state, feature entry-point audit, migrations/infra pending), `research/recent-tickets.md` (every ticket updated since the last deploy in Review/Verifying/Done, open PRs and their comments, verified findings).
- **Plan:** ordered merge/rebase sequence for the open PRs with conflict analysis; remediation tasks (each a scoped Sonnet subagent brief); git-hygiene steps remote then local; the full runbook release route with stop conditions; verification (Chrome for UI, `az`/endpoints for backend); docs refresh.
- **Execute:** remediations → PR integration → hygiene → release 12 → verification → docs refresh PR → proof.
- Every question that only the operator can answer stops the lane and is asked with the question tool.
- Never touches `.worktrees/kanmer` / `kanmer-board` except through the Kanmer tools for ticket lifecycle.

## Verification

- [ ] `gh pr list --state open` → empty.
- [ ] `git branch -r` → `origin/main`, `origin/dev`, `origin/kanmer-board` only; `git branch` → `main`, `dev`, `kanmer-board`; `git worktree list` → main checkout + `.worktrees/kanmer`.
- [ ] `origin/main == origin/dev == <release SHA>`; `/diagnostics/version` on production reports that SHA; `Invoke-ProductionSmoke.ps1` exit 0; migration head matches the release manifest; worker package deployed.
- [ ] Browser check of the shipped UI changes on production; `docs/operations.md` + `docs/current-architecture.md` refreshed and merged.
- [ ] `proof.md` = the successful, verified deployment.

## Outcome

(filled at closeout)
