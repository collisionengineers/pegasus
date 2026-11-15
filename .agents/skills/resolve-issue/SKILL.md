---
name: resolve-issue
description: "Take an issue number all the way to a finished merge request (pull request, PR, MR) - investigate first (via investigate-issue), create a task worktree and branch from the integration branch, implement the smallest complete fix with its tests and docs, push, open one MR whose body says \"Closes #N\", watch CI to green and fix the first failure, then remove the worktree and local branch. Use it whenever the user asks to resolve, fix, implement, do, work, pick up, close out or \"take to a PR\" or \"take to an MR\" an issue or #N, says \"work until it's done\", or points at ready-for-agent issues, even if they never say \"resolve\". Not for merging or combining PRs or MRs (combine-prs lands them), and not for analysis only (investigate-issue)."
---

# Resolve an issue

"Done" means: one MR open against the integration branch, body linking the
issue with `Closes #N`, the pipeline green on the MR's exact head, the task
worktree and local branch removed, and a report that says what was proved and
what was left out. Merging is a separate, audited step (`combine-prs`), and
the issue is closed when that lands.

Inputs: one issue number (or several the user says are one change); optionally
the project and the integration branch. Read the repo's CLAUDE.md/AGENTS.md first:
integration branch, branch and worktree naming, verification tooling, "do not
run locally what CI runs", UI guardrails, attribution lines. Those override
the defaults below.

## Authority

This repository's AGENTS.md governs every write; invoking this skill authorises none.

Invoking this skill authorises:
- everything `investigate-issue` authorises, including its issue comment;
- one new worktree and branch from the integration branch;
- commits on that branch, pushing **that branch**, opening **one** MR per issue;
- one comment on the issue saying which MR was opened;
- removing that worktree and local branch once CI is green.

It does not authorise: pushing to or merging into the integration branch,
closing the issue, force-pushing, touching other worktrees or branches,
non-additive migrations (the class in
[engineering](../../../docs/engineering.md#migrations)), or any write to a
cloud, mailbox, storage or vendor system. Ask for each of those; an approval
covers only the operation named.

## Tools

- `scripts/watch_checks.py <MR> [--once]`: polls the MR's pipeline and exits on
  the first failure with the job id, or when all pass. Read
  `references/ci-watch.md` when something fails or nothing starts.
- `scripts/cleanup_worktree.py --pr <MR> --worktree <path> [--dry-run]`:
  refuses unless the MR head equals the worktree HEAD and the tree is clean,
  then removes the worktree and local branch.
- `references/pr-body-template.md`: the MR body.
- The `investigate-issue` skill (`../investigate-issue/SKILL.md`): step 1.

## Procedure

### 1. Investigate before touching code

Look for an existing investigation comment on the issue. Reuse it only if the
tip SHA it records is still `origin/<base>` or nothing it names has changed
since (`git log <recorded-sha>..origin/<base> -- <paths it names>`).
Otherwise run `investigate-issue` now.

Read its recommendation and its **Decisions for the operator**. If any
decision is unanswered, stop and ask the user, listing the questions. Those
are product calls; a sensible default here becomes an invented rule the next
person treats as a requirement. If the investigation found the issue already
fixed at the tip, say so with the evidence and propose closing; do not close.

### 2. One worktree, one branch, no upstream

Slug from the issue title: kebab-case, ≤ 40 characters, no number. Use the
repo's convention when it has one; default:

```bash
git fetch origin
git worktree add --no-track -b task/<slug> ../<repo>-worktrees/<slug> origin/<base>
cd ../<repo>-worktrees/<slug>
git rev-parse --abbrev-ref --symbolic-full-name @{upstream} 2>/dev/null   # expect "no upstream"
```

`--no-track` matters: a branch created from `origin/<base>` otherwise tracks
it, and a later bare `git push` lands on the integration branch. Work only in
this worktree; the main checkout may hold another session's edits.

### 3. Implement the recommended approach

The smallest change that fully resolves the issue, including everything it
reaches: callers, tests, fixtures, migrations, configuration, and the docs
the repo's owner map requires when behaviour or structure changes (FRD, ADR,
architecture, operations). Keep one policy owner; do not add a second
implementation of a rule that already has one.

- Grep every caller of what you change before you change it. The blast
  radius table from the investigation is the checklist.
- New non-trivial logic gets a test in the owning test class.
- A Web change reads the repo's UI guardrail skill first; a bug fix is not a
  redesign.
- Do not run builds or tests that CI runs (repo policy; it costs minutes and
  proves nothing CI will not). `-WhatIf` on the repo's verification chooser is
  fine to see what CI will route. Cheap invariants the repo names (link
  checks, classifiers) are fine.
- Stop and ask if the change needs a destructive migration, a live external
  write, or grows past the issue's scope. Scope is the user's call.

### 4. Simplify, then commit

Run the repo's simplification lenses over `git diff origin/<base>` (reuse,
simplification, efficiency, altitude). Apply what holds, note the rest for
the MR body. Then commit with a conventional subject and a body that says
what and why, ending with the attribution line the environment specifies:

```bash
git add -A
git commit -F <msg-file>        # subject; blank; what/why; "Closes #N"; attribution
git push -u origin task/<slug>  # first push sets the upstream to the task branch only
```

### 5. Open the MR

```bash
glab mr create --source-branch task/<slug> --target-branch <base> --title "<subject>" --description "$(cat <body>)" --yes
glab issue note <N> -m "MR !<M> opened at <head sha>: <url>"
```

Body from `references/pr-body-template.md`. `Closes #N` on its own line is
what links the issue for `combine-prs` to close at landing. Honour the repo's
merge request template headings.

### 6. Watch CI, fix the first failure

```bash
glab mr view <M> -F json    # has_conflicts, detailed_merge_status, head_pipeline.status
python <skill>/scripts/watch_checks.py <M> --once      # see real output first
python <skill>/scripts/watch_checks.py <M>             # then in the background
```

On a failure: read the job log (`glab ci trace <job-id>`, or the repo's status script), decide whether it is this change's defect or a
pre-existing one on the base, fix on the branch, push, re-arm. Details in
`references/ci-watch.md`.

If a fix changes the approach materially, update the MR body; it is the
record.

### 7. Clean up and report

When every check is green on the current head:

```bash
cd <main checkout>
python <skill>/scripts/cleanup_worktree.py --pr <M> --worktree ../<repo>-worktrees/<slug>
```

It refuses if the MR head and worktree HEAD differ or the tree is dirty.
Ignored files under the worktree go with it; copy out anything worth keeping.

Report, so a reader who sees only this knows the state:
- MR URL and the head SHA CI passed on;
- the issue and the investigation comment;
- operator decisions applied, and anything left out and where it went;
- that landing is `combine-prs`'s step and the issue closes then.

## Several issues at once

Only when the user says they are one change: one branch, one MR, one `Closes
#N` line per issue. Otherwise run the procedure once per issue, sequentially,
each in its own worktree; parallel worktrees on one host contend for the same
build and database slots.

## What good looks like

- The MR body alone explains the change, its proof and its gaps.
- No local build or test run duplicated CI.
- No branch tracked the integration branch, nothing was pushed there.
- The first CI failure was reported the minute it appeared, not after the run.
- The worktree is gone and `git worktree list` is one entry shorter.
