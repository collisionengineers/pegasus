# Combined MR body

Title: `Combine MRs !A, !B and !C into <target>`. Add a short theme if one
exists, for example `Combine MRs !1016, !1018–!1020 into dev: Inbox composer, Triage page, Work Centre`.

Fill every section. Write "None" where a section is empty rather than deleting it.
A reader should be able to tell from this body alone what landed, what was
changed to make it land, and what was not proved. Constituent MRs rarely get
comments, so this body is the record.

```markdown
## Summary

Combines N MRs into `<target>` as one tip, so CI runs once against all of them
together. Each MR's pinned head is merged with `--no-ff`, so every commit is
kept and each MR shows as merged when this lands.
<Authority line if the repo or user requires one, e.g. "Combined as asked by @user on 2026-10-05.">

| Order | MR | Head | Change |
|---|---|---|---|
| 1 | !A | `abc123def456` | one line on what the MR does |
| 2 | !B | `…` | … |

Built on `<target>` @ `<target sha>`.

## Review findings on the MRs

Per MR: each unresolved review thread or review body. For each, say whether it was
fixed (with the commit), refuted (with the reason), or left open for the user.
Write "No review feedback" where none existed.

## Merge resolution

Per conflicted file: what each side did and what was kept. Name any renumbering:
paste the `| MR | Was | Now | Files | Lines |` table `renumber_register.py` printed,
one row per moved id, and the fix commit that carries it.
Name any change one MR made that another deliberately replaced. Write "Everything
merged automatically" if there were no conflicts, and say whether the MRs shared
any files.

## Fixes added here

Commits on this branch that belong to no MR: the SHA, what it fixes, and which MRs'
interaction caused it. Also list fixes pushed to a constituent MR's own branch and
merged in again, with their SHAs.

## Lost-work audit

<paste the output of audit_combined.py: verdict, table, integration-only commits>

Each FAIL and REVIEW line above maps to an entry in "Merge resolution" or "Fixes
added here".

## Verification

- [x] Simulation: <clean | conflicts in …, resolved above>
- [x] Build of the combined tip: <command> → <result>
- [x] <any other local check the repo requires>
- [ ] CI on this MR (union evidence)

Per-MR CI was green at the pinned heads: <run links or "see each MR">. Checks
not run locally are left to CI on purpose.

## Held / not included

MRs the user asked to hold or that were excluded, and why. Write "None" if there are none.

## Linked issues

| Issue | From MR | Closes at landing |
|---|---|---|
| #12 | !A | yes (`Closes #12` in the MR body) |
| #15 | !B | ask: number in body, no closing keyword |

Closing keywords do not fire on `<target>` (not the default branch); the
Landing step closes each "yes" row with a comment citing the MR, this MR and
the merge SHA. Write "None" if no MR links an issue. The combined body keeps every member's `Closes #N` line: the release's version proposal reads them (R687), and a range closing only `type:bug` issues proposes a PATCH.

## Landing

Merge with a merge commit (or fast-forward `<target>` to the tested tip). Do not
squash or rebase, or the constituent MRs will not show as merged. Do not close the
constituent MRs before this lands.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

Use the attribution line your environment specifies. The one above is the
default for Claude Code.
