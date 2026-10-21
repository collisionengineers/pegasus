# Combined PR body

Title: `Combine PRs #A, #B and #C into <target>`. Add a short theme if one
exists, for example `Combine PRs #1016, #1018–#1020 into dev: Inbox composer, Triage page, Work Centre`.

Fill every section. Write "None" where a section is empty rather than deleting it.
A reader should be able to tell from this body alone what landed, what was
changed to make it land, and what was not proved. Constituent PRs rarely get
comments, so this body is the record.

```markdown
## Summary

Combines N PRs into `<target>` as one tip, so CI runs once against all of them
together. Each PR's pinned head is merged with `--no-ff`, so every commit is
kept and each PR shows as merged when this lands.
<Authority line if the repo or user requires one, e.g. "Combined as asked by @user on 2026-10-05.">

| Order | PR | Head | Change |
|---|---|---|---|
| 1 | #A | `abc123def456` | one line on what the PR does |
| 2 | #B | `…` | … |

Built on `<target>` @ `<target sha>`.

## Review findings on the PRs

Per PR: each unresolved review thread or review body. For each, say whether it was
fixed (with the commit), refuted (with the reason), or left open for the user.
Write "No review feedback" where none existed.

## Merge resolution

Per conflicted file: what each side did and what was kept. Name any renumbering.
Name any change one PR made that another deliberately replaced. Write "Everything
merged automatically" if there were no conflicts, and say whether the PRs shared
any files.

## Fixes added here

Commits on this branch that belong to no PR: the SHA, what it fixes, and which PRs'
interaction caused it. Also list fixes pushed to a constituent PR's own branch and
merged in again, with their SHAs.

## Lost-work audit

<paste the output of audit_combined.py: verdict, table, integration-only commits>

Each FAIL and REVIEW line above maps to an entry in "Merge resolution" or "Fixes
added here".

## Verification

- [x] Simulation: <clean | conflicts in …, resolved above>
- [x] Build of the combined tip: <command> → <result>
- [x] <any other local check the repo requires>
- [ ] CI on this PR (union evidence)

Per-PR CI was green at the pinned heads: <run links or "see each PR">. Checks
not run locally are left to CI on purpose.

## Held / not included

PRs the user asked to hold or that were excluded, and why. Write "None" if there are none.

## Linked issues

| Issue | From PR | Closes at landing |
|---|---|---|
| #12 | #A | yes (`Closes #12` in the PR body) |
| #15 | #B | ask: number in body, no closing keyword |

Closing keywords do not fire on `<target>` (not the default branch); the
Landing step closes each "yes" row with a comment citing the PR, this PR and
the merge SHA. Write "None" if no PR links an issue.

## Landing

Merge with a merge commit (or fast-forward `<target>` to the tested tip). Do not
squash or rebase, or the constituent PRs will not show as merged. Do not close the
constituent PRs before this lands.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

Use the attribution line your environment specifies. The one above is the
default for Claude Code.
