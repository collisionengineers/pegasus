# GitHub and CI traps

Each of these cost an earlier combination run time or correctness.

## CI coverage

- **A PR with conflicts runs no CI at all.** GitHub cannot build its merge ref
  (`refs/pull/N/merge`), so `pull_request` workflows never start and the PR shows
  no checks, which is not the same as passing. Check
  `gh pr view N --json mergeable,mergeStateStatus` (`CONFLICTING`/`DIRTY`), not
  only check status.
- **Individually green is not jointly green.** Each PR's run tested its own
  merge ref. In one case PR A removed an interface member that PR B (merged first)
  had started using. Both PRs were green, and the target stopped compiling once both
  landed. Only a run on the combined tip is evidence for the combination.
- **A green descendant covers its ancestor's code**, so a stacked parent does not
  need its own rerun when a child containing it is green
  (`git merge-base --is-ancestor parent child`). It does not prove that the parent
  merges cleanly on its own; `mergeable` answers that.
- **Some repos run no CI on pushes** to the integration or target branch, only on
  PRs. The combined PR's run is then the only union evidence. Do not plan on
  "CI will run after the merge".
- **Stack-aware CI** may defer heavy jobs on a PR in the middle of a stack. A
  deferred or skipped job is not evidence. Read which jobs actually ran.

## Watching checks

- `gh pr checks N --watch` waits until every check finishes. Report the **first**
  failure as soon as it appears instead: poll
  `gh pr checks N --json name,state,bucket` and look for `bucket == "fail"`.
- `gh pr checks` exits non-zero while checks are pending or failing, so
  `gh pr checks N || echo failed` throws away the output you needed. Capture
  stdout first, then read it.
- Do not assume `jq` exists (Git Bash on Windows often has none). Use `gh --jq`
  or Python.
- A job log is readable while the run is live:
  `gh api repos/{owner}/{repo}/actions/jobs/<job-id>/logs`.
- Arm any background poller only after running its command once by hand.
  A poller that dies on a missing tool looks the same as one that is still waiting.

## Stale merge ref

If `actions/checkout` hangs and dies after about 5 minutes on a PR, the merge ref
is probably stale. Rerunning the job reuses the same ref and hangs again. Close
and reopen the PR (`gh pr close N && gh pr reopen N`) to make GitHub rebuild the
ref, then let the new run start.

## Merging and PR state

- **Squash and rebase merges break inclusion.** They create new commits, so the
  constituent PR heads are not ancestors of the target. GitHub then leaves those PRs
  "Closed", not "Merged", and nobody can prove from history that their commits
  landed. Land the combined PR with a merge commit, or fast-forward the target to
  the tested tip.
- **Constituents flip to Merged on their own** when their head commit becomes
  reachable from their base branch. This only works for PRs whose base *is* the
  target. A PR based on another branch (a stack) stays open.
- **Retargeting a stacked PR after the fact can be refused** with "no new commits"
  because everything is already in the target. Close it with a comment:
  `gh pr close N --comment "Merged into <target> via #M (<merge sha>); head <sha> is an ancestor."`
- **Never close the originals before the combined PR lands.** They are the
  fallback if the combination is abandoned. Closed-unmerged PRs also read as
  rejected work.
- **Closing keywords** (`Closes #12`) only fire when the PR merges into the
  repository's **default branch**. For any other target, list the issues and close
  them after landing, with the evidence (Landing step 4).
- **`closingIssuesReferences` is populated by the keyword, on any base branch.**
  A PR body that merely mentions `#12` ("raised from #12", "see #12") links
  nothing, so the inventory shows "none linked" even though the PR fixes it.
  Grep such bodies for `#\d+` and ask the user which of those the PR resolves.
- **Branch protection / required reviews** on the target may block the combined
  PR even when every constituent was approved. Check
  `gh pr view M --json reviewDecision,mergeStateStatus` and tell the user what is
  missing.

## Local git

- `git branch X origin/Y` and `git worktree add -b X <path> origin/Y` set `X` to
  track `origin/Y`. A later bare `git push` or `git pull` then targets the wrong
  branch. Use `--no-track`, or run `git branch --unset-upstream` straight away.
- Pin the target and every head as SHAs. Branch names move while you work
  (another session pushes a fix), and a moved head means your audit tested
  something other than what you merged.
- Build in a dedicated worktree. The main checkout may hold another session's
  uncommitted edits.
- Write any conflict-resolution scripts to a file, not into heredocs with escape
  sequences (`\b` becomes a backspace byte). Afterwards inspect the bytes you changed.
