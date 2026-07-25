# Git and GitHub handoff

Re-probe immediately before every authorized Git or GitHub action. These are templates, not standing permission.

When the approved plan justifies an isolated worktree and the user authorizes its creation, resolve an explicit path and branch first:

```powershell
$taskWorktreePath = [IO.Path]::GetFullPath('<absolute-worktree-path>')
git worktree list --porcelain
git worktree add -b <task-branch> $taskWorktreePath <base-ref>
git -C $taskWorktreePath status --short
```

Never derive the target from an unresolved environment variable or reuse an occupied path. Re-probe before any later authorized removal:

```powershell
git worktree list --porcelain
git worktree remove '<absolute-worktree-path>'
git worktree prune --dry-run
```

```powershell
git status --short
git branch --show-current
git diff --check
git diff -- <literal-approved-path>
git add -- <literal-approved-path-1> <literal-approved-path-2>
git diff --cached --check
git diff --cached --stat
git commit -m "<factual scoped message>"
```

Only when separately authorized:

```powershell
git push -u origin <branch>
gh pr create --base <base-branch> --head <branch> --title "<factual title>" --body-file <approved-body-file>
```

Inspect a candidate PR without posting:

```powershell
gh pr view <number> --json number,url,title,state,reviewDecision,statusCheckRollup
```

Do not create a branch or worktree, push, create a PR, reply to a comment, resolve a thread, remove a worktree, or assume the current branch is correct without the relevant authorization and fresh state evidence.
