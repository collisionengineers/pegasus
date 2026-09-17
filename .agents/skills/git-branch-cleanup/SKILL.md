---
name: git-branch-cleanup
description: Audit and safely clean GitHub repository branches and worktrees using Git and the gh CLI. Use when users want to identify merged, stale, rebased, squash-merged, or unintegrated local and remote branches, review worktrees, or delete approved cleanup candidates. Do not use for non-GitHub pull-request providers.
---

# Git Branch Cleanup

Inventory everything before recommending or deleting anything. Treat the user's cleanup request as permission to audit, not as approval of the eventual deletion list.

## Audit

1. Confirm the Git root, remotes, current branch, repository status, GitHub CLI authentication, and GitHub repository represented by each applicable remote. Stop before cleanup if repository ownership or authentication cannot be established reliably.
2. Resolve the default branch from GitHub, then its refreshed remote-tracking ref. Never assume it is named `main` or that `origin` is the authoritative remote.
3. Tell the user that remote-tracking refs will be refreshed, then run `git fetch --all --prune`. If it fails for any remote, report incomplete coverage and do not classify that remote's branches as safe to delete.
4. Collect the complete state before drawing conclusions:
   - Local branches and object IDs, upstreams, tracking state, and worktree paths with `git for-each-ref` or equivalent Git plumbing.
   - Actual remote heads from every remote with `git ls-remote --heads`; distinguish these from local `refs/remotes/*` and exclude symbolic `*/HEAD` refs.
   - Worktrees with `git worktree list --porcelain`, including locked and prunable records. Check each live worktree with `git -C <path> status --porcelain=v2 --untracked-files=all`.
   - PRs for every surviving branch name across the applicable GitHub repositories. Use `gh pr list --state all` with enough per-branch coverage and request at least `number,state,mergedAt,baseRefName,headRefName,headRefOid,headRepositoryOwner,mergeCommit,url`. Match owner, repository, branch, and head object ID; a branch-name match alone is not proof.
   - GitHub protection status for each proposed remote deletion. URL-encode branch names passed to `gh api`. Treat missing protection or ruleset visibility as uncertainty.

## Analyze Integration

Compare every local and remote head to the refreshed default-branch ref. Record behind and ahead counts from `git rev-list --left-right --count <base>...<branch>`; the left count is behind and the right count is ahead.

Use these signals in order:

1. **Ancestor-integrated:** `git merge-base --is-ancestor <branch> <base>` succeeds.
2. **Merged-PR-integrated:** the branch tip equals, or is an ancestor of, the verified `headRefOid` of a merged PR into the default branch. If the PR targeted another branch, first prove that target's relevant content reached the default branch; do not assume a chain of stacked branches is integrated. If the current tip descends from the PR head, analyze `headRefOid..<branch>` separately; post-merge commits are not covered by that PR. Use ancestry claims only when the PR-head object exists locally; otherwise treat a non-exact match as uncertain.
3. **Patch-equivalent:** use `git cherry <base> <branch>` to detect patches copied by rebase, cherry-pick, or squash workflows. Classify this way only when every unique non-merge commit is marked `-`, the comparison accounts for the full branch-only range, and no unique merge commit or unexplained empty commit is omitted. Record that ordinary `git branch -d` may still reject this branch.
4. **Needs review:** preserve the branch if any commit is marked `+`, a unique merge cannot be accounted for, PR identity is ambiguous, the patch series changed, or evidence is incomplete. Use `git range-diff`, logs, and diffs for human review where useful, but do not parse `range-diff` as stable machine output.

Git cannot prove that differently authored changes are semantically identical. Describe `+` commits as **unmatched work**, not definitively absent from the base. Absence of a PR or patch match is never evidence that deletion is safe.

## Propose

Show one compact table with branch/ref, local and remote locations, worktree state, PR state, behind/ahead, integration evidence, unmatched work, confidence, and recommendation. Group the exact proposed actions as:

- Delete safe local branches.
- Delete safe remote branches.
- Remove clean linked worktrees that block an approved branch deletion.
- Prune stale worktree metadata reported by `git worktree prune --dry-run --verbose`.
- Keep or review everything else, with the blocking evidence.

Never propose deleting the default branch, the current branch, a protected branch, a dirty or locked worktree, an open-PR branch, or a branch with unmatched or uncertain work. A clean worktree is not disposable by itself; remove it only when its branch is an approved cleanup candidate.

Present the numbered targets and exact commands, including the remote for every remote deletion. Ask the user to approve that list or a subset. Do not treat approval of this skill invocation as approval of those commands.

## Apply and Verify

Apply only the approved items, stopping on unexpected state changes:

1. Recheck branch object IDs, protection, and worktree cleanliness against the proposal. If anything changed, skip it and report the change.
2. Remove approved clean secondary worktrees without `--force`.
3. Delete approved local branches with `git branch -d -- <branch>`. If Git rejects a branch that is safe only by merged-PR or patch-equivalence evidence, show the evidence and exact `git branch -D -- <branch>` command, then obtain separate explicit approval before forcing it.
4. Delete approved remote branches with an exact `git push <remote> --delete <branch>` command. Never force a remote deletion, use a bulk deletion loop, or retry a protection failure by weakening safeguards.
5. Run only the approved worktree prune, then refresh remote-tracking refs again.
6. Repeat the inventory and report completed, rejected, skipped, and remaining review items. Never convert a partial failure into broader cleanup.
