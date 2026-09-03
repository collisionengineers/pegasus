---
kind: proof-record
merged_sha: "fbf8ee40983ee30030b296d9e61274b238c80b04"
environment: "Detached verification worktree C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-kanmer-010-fbf8ee40983ee30030b296d9e61274b238c80b04 at the exact PR #642 mergeCommit.oid, Windows 11 / PowerShell 7 + Git Bash, Kanmer 0.4.0 plugin bundle; Part 2 reachability run against the primary checkout C:\\Users\\PGUSER\\documents\\github\\pegasus"
verified_at: "2026-09-03T09:10:09Z"
result: PASS
attempts:
  - attempted_at: "2026-09-02T04:03:36Z"
    command: "gh pr view 642 --json state,mergeCommit,url"
    cwd: "C:\\Users\\PGUSER\\documents\\github\\pegasus"
    exit_code: 0
    result: PASS
    summary: "state MERGED; mergeCommit.oid fbf8ee40983ee30030b296d9e61274b238c80b04; url https://github.com/collisionengineers/pegasus/pull/642. Matches the reviewer's recorded merge_commit and the dispatched merge SHA."
  - attempted_at: "2026-09-02T04:03:40Z"
    command: "git rev-parse HEAD"
    cwd: "verify-kanmer-010-fbf8ee40983ee30030b296d9e61274b238c80b04 (detached worktree)"
    exit_code: 0
    result: PASS
    summary: "fbf8ee40983ee30030b296d9e61274b238c80b04 — equals the PR's full mergeCommit.oid."
  - attempted_at: "2026-09-02T04:03:40Z"
    command: "git symbolic-ref --short -q HEAD"
    cwd: "verify-kanmer-010-fbf8ee40983ee30030b296d9e61274b238c80b04 (detached worktree)"
    exit_code: 1
    result: PASS
    summary: "Empty output, non-zero exit — the expected signal of a detached HEAD (no symbolic ref to resolve)."
  - attempted_at: "2026-09-02T04:03:40Z"
    command: "git status --short --branch"
    cwd: "verify-kanmer-010-fbf8ee40983ee30030b296d9e61274b238c80b04 (detached worktree)"
    exit_code: 0
    result: PASS
    summary: "## HEAD (no branch); no changed/untracked files. Worktree clean at the merge SHA. Not .worktrees/kanmer and not the ticket's implementation worktree (kanmer-010-setup-drift)."
  - attempted_at: "2026-09-02T04:03:00Z"
    command: "pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1"
    cwd: "verify-kanmer-010-fbf8ee40983ee30030b296d9e61274b238c80b04 (detached worktree)"
    exit_code: 0
    result: PASS
    summary: "All relative Markdown links resolve (87 files checked). Runner artefacts: tests/v1-docs-links.exit (0), tests/v1-docs-links.log."
  - attempted_at: "2026-09-02T04:03:00Z"
    command: "pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base HEAD^1 -Head HEAD"
    cwd: "verify-kanmer-010-fbf8ee40983ee30030b296d9e61274b238c80b04 (detached worktree)"
    exit_code: 0
    result: PASS
    summary: "Markdown placement passed for HEAD^1..HEAD (the merge commit's first parent is the dev tip before the merge). Runner artefacts: tests/v2-markdown-placement.exit (0), tests/v2-markdown-placement.log."
  - attempted_at: "2026-09-02T04:03:00Z"
    command: "bash -c 'rc=0; for tree in .agents/skills .grok/skills; do for d in C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills/*/; do n=$(basename \"$d\"); [ -d \"$tree/$n\" ] || continue; diff -rq --strip-trailing-cr \"$tree/$n\" \"$d\" || rc=1; done; done; exit $rc'"
    cwd: "verify-kanmer-010-fbf8ee40983ee30030b296d9e61274b238c80b04 (detached worktree)"
    exit_code: 0
    result: PASS
    summary: "Silent (no diff lines) across all twelve kanmer-* skill folders in both .agents/skills and .grok/skills, byte-identical to the 0.4.0 bundle modulo line endings. Runner artefacts: tests/v3-skills-match-bundle.exit (0), tests/v3-skills-match-bundle.log (empty = no differences). Cross-checked against the reviewer's independent re-run at the pre-merge head (93ec918e), which reported the same silent result for all 24 tree comparisons."
  - attempted_at: "2026-09-02T03:03:36Z"
    command: "KANMER_ROOT=C:/Users/PGUSER/Documents/github/pegasus/.worktrees/kanmer KANMER_REPO_ROOT=C:/Users/PGUSER/Documents/github/pegasus-worktrees/verify-kanmer-010-fbf8ee40983ee30030b296d9e61274b238c80b04 bash tools/kanmer-call.sh get_status | node -e '... filters repo.stale to behind rows other than mcp-registration ...'"
    cwd: "C:\\Users\\PGUSER\\documents\\github\\pegasus"
    exit_code: 0
    result: PASS
    summary: "No behind row other than mcp-registration. repo.stale = [board-config: compensated (informational — profiles omit questions-resolved; core injects it at read time), mcp-registration: behind (opencode.json points at a different workstation's worktree path, unrelated to this verification; deferred per ticket body, fix is 'reconnect this project in the Kanmer app')]. This is the wired consumer for rule 14 (Kanmer server's staleness audit reading .agents/skills / .grok/skills and their stamps at the merge-SHA tree). AGENTS.md line endings were not rewritten in the verify worktree afterwards (no modified files reported), so no restore was required."
  - attempted_at: "2026-09-02T04:04:10Z"
    command: "gh pr checks 642"
    cwd: "C:\\Users\\PGUSER\\documents\\github\\pegasus"
    exit_code: 0
    result: PASS
    summary: "repository-check run 33581680729 on head 93ec918e: browser, infrastructure, sql-integration, sql-integration-coverage, test-ui, unit all 'skipping' (path-skip — no built code touched); changes, documentation, local-development-scripts, reference-data all pass. Consistent with a docs/skills-only PR."
  - attempted_at: "2026-09-02T04:05:00Z"
    command: "git merge-base --is-ancestor fbf8ee40983ee30030b296d9e61274b238c80b04 origin/dev"
    cwd: "C:\\Users\\PGUSER\\documents\\github\\pegasus"
    exit_code: 0
    result: PASS
    summary: "True — the merge SHA is an ancestor of origin/dev (9b8f78a36151313bc6d48625edee7f13a2173127 at run resume). Confirms the integration-branch landing this ticket's PR made."
  - attempted_at: "2026-09-02T04:05:05Z"
    command: "git merge-base --is-ancestor fbf8ee40983ee30030b296d9e61274b238c80b04 origin/main"
    cwd: "C:\\Users\\PGUSER\\documents\\github\\pegasus"
    exit_code: 1
    result: INCONCLUSIVE
    summary: "False — the merge SHA is not yet an ancestor of origin/main (fb3f07acc8cca8d9d8b57db8a431b607772436dc). Expected pre-promotion state, not a defect: dev has not yet been promoted to main since this PR merged. This is the open half of Part 2."
  - attempted_at: "2026-09-02T04:05:10Z"
    command: "git merge-base --is-ancestor fbf8ee40983ee30030b296d9e61274b238c80b04 0b3ec847aae42ee1c1bee4fb99459f9192534dca"
    cwd: "C:\\Users\\PGUSER\\documents\\github\\pegasus"
    exit_code: 1
    result: INCONCLUSIVE
    summary: "False — the merge SHA is not an ancestor of the release-37 source SHA (0b3ec847aae42ee1c1bee4fb99459f9192534dca, docs/operations.md release table, deployed 2026-08-30). Expected: release 37 predates this PR's merge. Confirms no release has shipped this ticket's content yet."
  - attempted_at: "2026-09-02T04:05:30Z"
    command: "manual review: scripts/Invoke-ProductionSmoke.ps1 reference and post-implementation-report canary check (KANMER-verify Part 2 requirement)"
    cwd: "C:\\Users\\PGUSER\\documents\\github\\pegasus"
    exit_code: null
    result: NOT_APPLICABLE
    summary: "scripts/Invoke-ProductionSmoke.ps1 exists in the repo but this ticket's deployment field is 'n/a' (a repository-tooling chore: AGENTS.md managed block, Kanmer skill-tree refresh, .kanmer-skills-version stamps — no product code, no deployed artefact, no UI surface). No production canary is named in the ticket's post-implementation-report, and none is owed: per the dispatch's explicit carve-out, a deployment: n/a chore's Part 2 release evidence is reachability from the promoted main only, which the two INCONCLUSIVE reachability attempts above already cover. No operator UI acceptance is owed either — this is not a UI ticket."
  - attempted_at: "2026-09-03T09:10:09Z"
    command: "gh pr view 642 --json state,mergedAt,mergeCommit,url,headRefName,baseRefName"
    cwd: "C:\\Users\\Alex\\Documents\\GitHub\\pegasus"
    exit_code: 0
    result: PASS
    summary: "PR #642 remains MERGED into dev; mergeCommit.oid is fbf8ee40983ee30030b296d9e61274b238c80b04; mergedAt 2026-09-02T02:56:50Z; URL https://github.com/collisionengineers/pegasus/pull/642."
  - attempted_at: "2026-09-03T09:10:09Z"
    command: "git fetch origin main dev --prune"
    cwd: "C:\\Users\\Alex\\Documents\\GitHub\\pegasus"
    exit_code: 0
    result: PASS
    summary: "Fetched current origin/main and origin/dev before the ancestry decision; origin/main resolved to 1b705bd01d88109b21affddd014fbaa06c82b1ce and origin/dev to 897db9530a45063e8f684f2800685afbfdced006."
  - attempted_at: "2026-09-03T09:10:09Z"
    command: "git merge-base --is-ancestor fbf8ee40983ee30030b296d9e61274b238c80b04 origin/dev"
    cwd: "C:\\Users\\Alex\\Documents\\GitHub\\pegasus"
    exit_code: 0
    result: PASS
    summary: "The exact PR merge SHA remains reachable from the integration branch."
  - attempted_at: "2026-09-03T09:10:09Z"
    command: "git merge-base --is-ancestor fbf8ee40983ee30030b296d9e61274b238c80b04 origin/main"
    cwd: "C:\\Users\\Alex\\Documents\\GitHub\\pegasus"
    exit_code: 0
    result: PASS
    summary: "The exact PR merge SHA is now reachable from origin/main at 1b705bd01d88109b21affddd014fbaa06c82b1ce. This resolves the earlier expected pre-promotion INCONCLUSIVE attempt without changing the verified merge artefact."

---

# Proof — KANMER-010

Verification evidence for PR #642, merge commit `fbf8ee40983ee30030b296d9e61274b238c80b04`,
gathered in the disposable detached worktree
`C:\Users\PGUSER\Documents\github\pegasus-worktrees\verify-kanmer-010-fbf8ee40983ee30030b296d9e61274b238c80b04`
at that exact SHA (attempt 1, run `20260901T215000Z-claude-controller`).

## Part 1 — code evidence at the merge SHA

`gh pr view 642` confirms `state: MERGED`, `mergeCommit.oid` equal to the
recorded merge SHA. The verification worktree is detached (`symbolic-ref`
empty/exit 1), clean (`status --short --branch` shows no changes), and at
that exact commit (`rev-parse HEAD` matches) — not `.worktrees/kanmer`, not
the ticket's implementation worktree.

All four named lanes ran green in that worktree:

1. `Test-DocumentationLinks.ps1` — PASS (87 files, all relative links resolve).
2. `Test-MarkdownPlacement.ps1 -Base HEAD^1 -Head HEAD` — PASS.
3. The skill-tree byte-identity diff (`.agents/skills` and `.grok/skills`
   against the Kanmer 0.4.0 bundle, twelve `kanmer-*` skills, two trees) —
   PASS, silent on all 24 comparisons. This matches the reviewer's own
   independent re-run at the pre-merge head.
4. `get_status` against the merge-SHA tree — PASS: `repo.stale` names only
   `board-config` (compensated/informational) and the deferred
   `mcp-registration` (a different workstation's `opencode.json`, unrelated
   to this repository state). This is the wired consumer for rule 14's
   "caller": the Kanmer server's own staleness audit of `.agents/skills` /
   `.grok/skills` and their `.kanmer-skills-version` stamps.

CI: `gh pr checks 642` on run `33581680729` shows the six build/test lanes
(`browser`, `infrastructure`, `sql-integration`, `sql-integration-coverage`,
`test-ui`, `unit`) path-skipped, and `changes`, `documentation`,
`local-development-scripts`, `reference-data` all green — consistent with a
docs/skills-only change.

Part 1 holds.

## Part 2 — release evidence

`git merge-base --is-ancestor <merged_sha> origin/dev` is true: the merge
landed on the integration branch. `git merge-base --is-ancestor <merged_sha>
origin/main` and `... 0b3ec847aae42ee1c1bee4fb99459f9192534dca` (release 37's
source SHA, from the `docs/operations.md` release table, deployed
2026-08-30) are both **false** today: `dev` has not yet been promoted to
`main` since this PR merged, and no release has shipped this content.

This ticket's `deployment` field is `n/a` — a repository-tooling chore with
no product code, no deployed artefact, and no UI surface. Per the dispatch's
carve-out, its Part 2 evidence is reachability from the promoted `main`
only; there is no `Invoke-ProductionSmoke.ps1` canary or operator UI
acceptance to record for this ticket (`scripts/Invoke-ProductionSmoke.ps1`
exists in the repo but is not applicable here, and none is named in the
ticket's post-implementation report).

**Part 2: pending release.**

## Not covered

- Promotion of `origin/dev` to `origin/main` and any subsequent production
  release/smoke evidence — outside this run, tracked as the open half of
  Part 2. A future verification attempt should re-run both `is-ancestor`
  checks once `dev` is promoted.
- `dotnet build`/test lanes — genuinely not applicable: this PR touches no
  `src/` code (CI's own path-skip on the six build/test lanes corroborates
  this).


## Part 2 resolution — 2026-09-03

PR #642 remains merged at exact commit
`fbf8ee40983ee30030b296d9e61274b238c80b04` on 2026-09-02T02:56:50Z
(https://github.com/collisionengineers/pegasus/pull/642). After a fresh fetch,
that commit is an ancestor of both `origin/dev` and `origin/main`; the latter
resolved to `1b705bd01d88109b21affddd014fbaa06c82b1ce`. This supplies the only
evidence unavailable in the original attempt. The earlier exact-SHA checks
remain recorded above and unchanged; no product deployment or canary is owed
for this `deployment: n/a` repository-tooling chore.
