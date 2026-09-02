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

## Remediation round 1 — 2026-09-02

Reviewer verdict needs-changes on PR #643 at 25c14574 (attestation scratch/review v b9bcd6ad94d49f55).
Findings F-001 and F-002 (major) and F-003, F-004, F-005 (minor) are all fixed in one commit,
357f143b5c8c00cd5ec8981944b8684c4606b659, on the same branch and the same PR. F-006 to F-009 keep
the reviewer dispositions; F-008 is escalated to the controller for D18 ticket allocation and the
absence is now stated in RPT-02 rather than implied. Each id and its disposition is recorded in the
post-implementation-report under ## Remediation round 1.

Both majors were confirmed before being fixed, not taken on trust: FRD-11 L70-71 and L311 did
contradict the new D18 paragraph, and the accepted engineer-signature check does still ship
(AssessmentPolicy.cs:255, AssessmentReportProjection.cs:117-129, read at this head).

New head 357f143b5c8c00cd5ec8981944b8684c4606b659, 13 commits, 16 files, +345/-120, no file added
or deleted, no heading renamed, links resolve, capabilities arithmetic still 234/234 with
143+27+35+29=234. Working tree clean. Not sent to the remote: the correction dispatch said to stop
at READY_FOR_TESTS, so PR #643 still shows 25c14574 and needs one push before a fresh reviewer can
see this work. No second PR will be opened; the ticket stays in review with no backward move.

Owed to the test-runner role at the new head: the four docs lanes. The implementer ran none.

## Refresh onto the moved dev — 2026-09-02

origin/dev advanced 9b8f78a3 to 2a48be04 (KANMER-010 skill trees plus two mail PRs), which is why
the docs lanes at 357f143b misattributed dev-side changes to this branch. Refreshed with the merge
rule, never a rebase: conflict-free, merge commit 8e8dd8b25567d26a45d29ab7dcc5c19b9848971b.

dev had also edited docs/design/README.md and docs/frd/frd-08-email-mailbox-and-background-
processing.md, so the auto-merge was checked both ways: dev new mail-preview wording is present in
both merged files and this ticket D22 and D18 edits are still present.

Scope re-verified against the new origin/dev: exactly the sixteen Expected docs files, added and
deleted filters both empty, 16 files changed 345 insertions 120 deletions, capabilities arithmetic
still 234/234 with 143+27+35+29=234, tree clean.

Sent to the remote 25c14574..8e8dd8b2. PR #643 headRefOid is now
8e8dd8b25567d26a45d29ab7dcc5c19b9848971b, state OPEN, base dev, mergeStateStatus CLEAN. No second
PR, no stage move. Head for the lane rerun and the re-review: 8e8dd8b2.

## Transitions

- 2026-09-02T11:09:07.213Z stage review → implementing by codex-mcp-client; reason: needs-changes on 8e8dd8b25567d26a45d29ab7dcc5c19b9848971b: F-001 through F-013; review_round 1

- 2026-09-02T11:09:23.244Z claim-transfer claude-code/20260901T215000Z-claude-controller/implementer-a1 → codex/deliv-040-operator-remediation (expired; lease fe0dd564-63e7-456c-8ec1-1e3c86ff095d → 3d7aca2e-eb9a-43c7-81b5-9bad193fac91 rev 2; branch task/deliv-040-governing-docs; worktree ../pegasus-worktrees/deliv-040-governing-docs; expires 2026-09-02T11:39:23.239Z; evidence: workspace clean (matches-claim), pr open, commits 12, proof absent)
