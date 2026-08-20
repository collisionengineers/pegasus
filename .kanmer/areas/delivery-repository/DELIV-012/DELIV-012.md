---
id: DELIV-012
type: ticket
title: >-
  Release 12: quality-review every post-release-10 ticket, integrate all open
  PRs, restore git hygiene, deploy and verify production
status: done
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
  review: '2026-08-20T01:24:04.681Z'
  verifying: '2026-08-20T01:24:10.331Z'
  done: '2026-08-20T01:25:44.072Z'
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
commits:
  - ed3be51c95bc2a055606e5210131d37de9de2dd1
  - 2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5
prs:
  - '425'
  - '426'
  - '427'
  - '428'
  - '429'
  - '435'
  - '436'
deployment: production
archived: false
created: '2026-08-19T12:12:34.359Z'
updated: '2026-08-20T01:26:10.601Z'
---

## What

One self-contained delivery ticket that (1) establishes exactly what production serves today and what has landed since, (2) quality-reviews every ticket in Review / Verifying / Done since that deployment — PR comments, scope drift, dark or orphaned features, failing CI — and remediates verified findings through scoped subagents, (3) integrates every open PR into `dev` in a safe order, (4) restores git hygiene to three remote branches (`main`, `dev`, `kanmer-board`), three local branches and two worktrees (main checkout + `.worktrees/kanmer`), and (5) runs the full numbered release (12) to Azure, verifying UI changes in the browser and backend changes through the Azure CLI / endpoints.

## Why

Production still serves release 10 (`d8de29cb`). Release 11 ([[DELIV-011]], `feda958f`) was fully prepared but **held** by the operator on 2026-08-19 before the `main` push; since then `dev` has moved far past it and six task PRs are open in Review. The estate needs one careful, evidenced release that carries *everything* merged, nothing shipped dark, and leaves the repository clean.

## Verification

- [x] `gh pr list --state open` → empty.
- [x] `git branch -r` → `origin/main`, `origin/dev`, `origin/kanmer-board` only; local the same three; `git worktree list` → main checkout + `.worktrees/kanmer`.
- [x] `origin/main == origin/dev` at each promotion; `/diagnostics/version` reports the promoted SHA; smoke exit 0; migration head matches the manifest; worker package active.
- [x] Browser verification of every shipped UI change on production; docs refreshed and merged in the same work.
- [x] `proof/proof.md` = the successful, verified deployments (12 and 13).

## Outcome

**Two releases deployed and verified.** Release 12 (`ed3be51c`, 2026-08-19) carried 21 PR merges — the nine this ticket produced (PRs #416/417/422–428 + the six inherited merges), eight migrations, the Chromium-carrying Web image at 1.0 vCPU/2 GiB, and fixed a **live production defect** (`EvaHandoffDownloadOperations` had zero permission rows since 11 Aug). Release 13 (`2325ed4a`, 2026-08-20) carried the six operator-review remediations (PRs #430–#434 + docs), including the atomic image-group race fix whose reconciliation recovered the operator's stranded production upload as `U6`.

Quality review found and fixed before deployment: the release gate itself broken on `dev` (unaccounted grant-carrying migration — `-Mode Local` now runs in CI unconditionally); five migrations missing runtime grants (guarded by new `scripts/Test-MigrationGrants.ps1`); three dark surfaces given real callers (renderer entry point, `MailOperationalDestinationPolicy` on `/Inbox/{id}`, `IRepairSpecificationStore`); TICK-045 rebuilt from fabricated evidence to falsifiable tests; board contradictions corrected (TICK-011's unreachable commit citations, PLAT-001's deployment field). Sent-evidence polling approved and applied through the admin UI (Q4) — the once-a-minute exception stream ended.

Follow-ups filed: [[CASE-005]] (allocation deadlock, pre-existing), [[ENG-002]] (estimate import — operator truth recorded verbatim), [[PLAT-011]] (actor display names), [[INTK-012]] (ordinal-0 token ambiguity). Superseded: [[DELIV-011]].

Full evidence: `proof/` (both releases), `scratch/review` (nine independent PR reviews), `scratch/notes` (the execution log).
