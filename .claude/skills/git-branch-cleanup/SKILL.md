---
name: git-branch-cleanup
description: Audit and safely clean GitHub repository branches and worktrees using Git and the gh CLI. Use when users want to identify merged, stale, rebased, squash-merged, or unintegrated local and remote branches, review worktrees, or delete approved cleanup candidates. Do not use for non-GitHub pull-request providers.
---

# Git Branch Cleanup

Read and follow the repository's canonical skill,
[.agents/skills/git-branch-cleanup/SKILL.md](../../../.agents/skills/git-branch-cleanup/SKILL.md),
completely before acting.

A pointer rather than a symlink: this entry used to be a symlink to an
absolute path under another machine's user profile, so the skill resolved
on exactly one checkout and silently did not exist on any other.
