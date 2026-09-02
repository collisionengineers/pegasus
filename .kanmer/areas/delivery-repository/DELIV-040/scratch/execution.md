## Deviation 2026-09-02 — shell guard rule 8 denies every Git mutation (implementer attempt 1)

Observed, verbatim, from both the Bash and the PowerShell tool (the denial line reads
`pegasus-guard: rule 8 - mutating g` + `it in the primary checkout: <command>`; the literal
text is not reproduced here because the guard also inspects the text of any command that
quotes it):

- attempted: `g` + `it -C C:/Users/PGUSER/Documents/github/pegasus-worktrees/deliv-040-governing-docs add -A`
- attempted: the same staging step from PowerShell after `Set-Location` into the worktree
- both denied with rule 8

Cause: `hooks/pegasus-guard.ps1` L122 computes
`$targetsRepo = ($cwd -eq $repo) -or ($lc -match ('-c\s+"?' + $repoPattern + '"?(\s|$)'))`.
A Claude Code subagent inherits the *session* working directory, which is the primary
checkout `C:\Users\PGUSER\documents\github\pegasus`, so `$cwd -eq $repo` is true for every
worker and rule 8 fires on every Git mutation regardless of the `-C` target. `cd` does not
persist between Bash tool calls (verified: a `cd` into the worktree in one call, `pwd`
reports the primary checkout in the next), so the working directory cannot be moved.

Effect: the commit, push and PR half of the DELIV-040 stop condition cannot be performed
from this worker. The documentation edits themselves are unaffected and are complete in the
worktree working tree, uncommitted.

The orchestration plan L256 states the intent as "mutating g" + "it in the primary checkout
never", so a worker committing inside its own recorded worktree is meant to be allowed; the
`$cwd -eq $repo` clause is the false positive. The hook is controller-owned infrastructure
(M5, M11, M14), so no repair was attempted from here. The minimal fix is to drop the
`($cwd -eq $repo)` disjunct when the command names another worktree with `-C`, or to give
the worker session a working directory inside its worktree.

## Resume point — implementer attempt 1, 2026-09-02

- Branch `task/deliv-040-governing-docs`; worktree
  `C:\Users\PGUSER\Documents\github\pegasus-worktrees\deliv-040-governing-docs`; HEAD
  `9b8f78a36151313bc6d48625edee7f13a2173127` (unchanged — nothing committed).
- Packet versions read: plan `ec9310a3d01c2fba`, checklist `d5a1696c6f9c0853` (now
  `890d7dd4aca84938`), files `3b6bdc624273cfc0`, open-questions `d7b1d9bac4fc6ff9` (now
  `20df46221c7e6870`).
- Working tree holds all sixteen Expected files modified, +326/-112, no added or deleted file.
  Steps 1–11 done; Step 12 done except its `capabilities.md` row and recount.
- Last command and result: staging the worktree was denied by shell guard rule 8 (see the
  deviation note above). Nothing after that step ran.
- To resume once the guard is fixed: commit in logical slices (one per decision group), push
  `task/deliv-040-governing-docs`, open the PR against `dev` titled
  "Record the 2026-09-01 operator interface decisions in the governing documents (DELIV-040)"
  with the footer `Kanmer: DELIV-040`, then `get_doc_gates` and move `implementing` → `review`.
  Step 12's capability row needs a free id first (`ACC-15` is next free; `ACC-10` is taken).
- The ticket stays taken. The uncommitted changes belong to this ticket; do not clean or recreate
  the worktree.

## PR_OPEN reached 2026-09-02

PR https://github.com/collisionengineers/pegasus/pull/643 (base dev, head 25c14574a9e34c77e977f8a8eb203c2fe85dc13e, footer Kanmer: DELIV-040). Gates read immediately before the move; ticket moved implementing to review. Branch task/deliv-040-governing-docs and its worktree stay recorded and taken for the reviewer.
