---
name: investigate-issue
description: Thoroughly investigate one GitHub issue before any code is written and post the findings as one comment on the issue - current behaviour at the tip, the governing requirement, blast radius (owner, callers, data, tests), two or more approaches with footprint and risk, a recommendation, the decisions only the operator can make, and a test plan. Use it whenever the user asks to investigate, look into, scope, assess, vet, size or triage an issue, asks "what would it take" or "how risky is #N", pastes an issue number or URL and wants analysis, or before implementing an issue (resolve-issue runs it first). Not for writing the fix itself (that is resolve-issue), not for reviewing a PR, and not for issues on non-GitHub hosts.
---

# Investigate an issue

Produce the analysis someone would want before touching code: what is really
happening, who owns it, what a fix touches, which way to go, and how it will
be proved. The output is one comment on the issue, so the next person (or the
`resolve-issue` skill) starts from facts rather than from the issue text,
which is often older than the code.

Inputs: an issue number or URL; optionally the repo (`owner/name`) and the
integration branch. If the repo's CLAUDE.md/AGENTS.md names an integration
branch (for example `dev`), investigate against that, not the default branch.

## Authority

Invoking this skill authorises:
- read-only inventory of the repo, GitHub and any branch;
- `git fetch`, and a throwaway detached worktree to read the tip cleanly;
- posting **one** comment on the named issue (the write is implied by the
  invocation; do not ask again).

It does not authorise changing labels, assignees or the issue body, creating a
branch, editing repo files, or writing to any external system. Echo the issue
number and title before posting, because a comment on the wrong issue is the
one mistake this skill can make.

Repo instructions win over defaults here. Read CLAUDE.md/AGENTS.md first for
the docs index, glossary, integration branch, verification tooling and any
"check it traces to supplied evidence" rule.

## Tools

- `scripts/issue_context.py <N> [--base <branch>] [--repo owner/name]`: issue,
  comments, cross-referenced PRs/issues, and the commits on the base since
  the issue was opened that touched the paths, files and types it names.
  Exit 2 means stop and ask (closed issue, or an open PR already references it).
- `references/report-template.md`: the comment's shape. Read it before writing.

## Procedure

### 1. Start from the tip, not the checkout

```bash
git fetch origin
git status -sb          # behind/ahead tells you whether the local branch is stale
```

Explore `origin/<base>`. If the main checkout is dirty or behind, add a
detached worktree (`git worktree add --detach <tmp> origin/<base>`) and read
there; remove it at the end. An investigation done on a stale checkout
describes code that no longer exists, and its plan is wrong before it starts.

### 2. Gather what GitHub knows

```bash
python <skill>/scripts/issue_context.py <N> --base <base>
```

Read the whole output. The last section matters most: commits on the base
since the issue was written that touched the code it names. Each one may have
already fixed, moved or renamed what the issue describes. On exit 2, tell the
user what blocks (closed, or an open PR already covers it) and stop.

### 3. Find the owner and the requirement

Follow the repo's docs index to the requirement that governs the behaviour
(FRD, ADR, capability, glossary term). Quote the clause. Then check that it
traces to supplied evidence rather than to earlier generated prose; a rule
that does not trace is a candidate for deletion, and removing it usually
makes the fix smaller. "No requirement found" is a finding, not a gap to fill
with an invented one.

Find the single Core owner of the behaviour, then every consumer: Web pages,
Worker functions, MCP tools, tests, fixtures, migrations, configuration,
docs. Use `git grep -w` on the type and member names; the owner's callers are
the blast radius, and a caller nobody listed is the regression.

### 4. Trace the actual path

Read the code along the path the issue describes, at the tip. Record where
the issue's file:line claims hold and where they moved (name the commit or
PR). Write down verified facts with the command or file that proves them,
separately from assumptions. If a five-minute read-only check would settle an
assumption, run it instead of carrying it.

For a reported bug, say what the user would see at the tip and why. For a
task or decision issue, say what the current design already provides.

### 5. Lay out the approaches

At least two genuinely different shapes, each with:
- which owner changes and what stays;
- footprint: files and a rough line count;
- data: no migration, additive, or destructive (and what retained records do);
- UI: none, or a Web change that binds the repo's UI guardrails;
- risks and what it leaves unresolved.

Recommend one. The smallest change that fully resolves the issue and keeps
one policy owner wins unless the requirement says otherwise. Say what the
recommendation deliberately does not do.

### 6. Separate the operator's decisions

Anything the requirement does not settle and that changes what the user sees
or how the product behaves is the operator's call: copy, defaults, which
untraced rule survives. List these as numbered questions. Do not answer them
with a sensible default; a default here becomes an invented rule downstream.

### 7. Plan the proof

Name the existing test classes that own the code and the CI lane that runs
them. Describe the new test that proves the fix (what it asserts), and what
only a running app or the live estate can show. Do not run builds or tests
that CI runs; this is a reading task.

### 8. Post and report

Write the comment from `references/report-template.md`, echo
`#<N> <title>`, then:

```bash
gh issue comment <N> --body-file <file>
```

Report in chat: the comment URL, the verdict, the recommendation and the
operator decisions. Remove any throwaway worktree.

## Quality bar

- Verified beats plausible. Every claim about the code names the file read or
  the command run; the tip SHA is in the comment.
- Blast radius is complete, not representative. If you grepped, say what for.
- Approaches are different shapes, not one approach with two names.
- Decisions are questions, not defaults.
- Short sentences, few paths in prose. The operator reads this on GitHub.
