---
name: combine-prs
description: Combine several GitHub pull requests into one target branch through a single integration branch and combined PR, without overwriting, dropping or regressing any PR's work or the target's. Takes PR numbers plus a target branch. It pins head SHAs, simulates the merge train, merges each PR with --no-ff, resolves conflicts as a union of intents, audits every file and line for lost work, opens one combined PR and watches its CI. Use it whenever the user wants to merge, combine, batch, consolidate or "merge down" several PRs together. Typical phrasings are "merge PRs 12, 14 and 15 into dev", "merge-all", "combine these PRs", "build an integration branch", "land these together", a merge train, stacked PRs to fold into one branch, or "merge everything green into dev without losing work", even if the word "combine" never appears. Not for merging a single PR on its own, for promoting or releasing one long-lived branch to another, or for non-GitHub hosts.
---

# Combine PRs

Combine a set of GitHub PRs into one target branch through one integration
branch and one combined PR. The goal is three guarantees, and every step below
serves at least one:

1. **Nothing overwritten.** Each PR's change survives unless another PR in the
   set deliberately replaces it, and every replacement is written down.
2. **Nothing lost.** Every PR's commits are in the result, and so is all of the
   target's own work.
3. **No regression.** The combination is built and tested as a whole. Per-PR
   green runs do not cover the interaction between PRs.

Inputs: PR numbers, a target branch, and optionally an order, PRs to hold, and a
repo (`owner/name`). Ask for the target if it is missing; do not guess `main`.

## Authority

This repository's AGENTS.md governs every write; invoking this skill authorises none.

Invoking this skill authorises:
- read-only inventory;
- fetching objects;
- building the integration branch in its own worktree;
- pushing **that new branch**;
- opening **one** combined PR;
- committing fixes that only the combination needs on that branch;
- once the user has asked for landing and it has landed: closing the issues
  the landed constituent PRs link, each with a comment (Landing step 4).

It does **not** authorise any of these:
- merging the combined PR, or pushing to the target branch;
- force-pushing, rebasing or committing to a constituent PR's branch (ask first; see step 8);
- closing PRs, closing an issue before its PR has landed, or deleting branches;
- including a PR that the user, a label or the title says to hold.

Ask before each of those, and treat an approval as covering only the operation
it names.

Repo instructions override the defaults here. Read CLAUDE.md, AGENTS.md,
CONTRIBUTING and any PR template first. Look for branch naming, worktree
location, local test limits ("CI runs the tests, don't repeat them"), required
approval wording, and verification scripts. Use the repo's verification tooling
when it has some.

## Tools

Scripts live in this skill's `scripts/` folder. Run them from inside the
repository with Python 3. They use `git` (≥ 2.38) and an authenticated `gh`, and
they print markdown you can paste into the PR body.

- `inventory.py --target <branch> <PR…>`: pins head SHAs, flags blockers, reads review feedback, detects stacks and maps file overlap. Exit code 2 means there is something to ask about.
- `simulate_merge.py --target <sha> [--pairwise] N=<sha>…`: dry-runs the merge train in memory.
- `audit_combined.py --target <sha> --tip HEAD N=<sha>…`: the lost-work audit.

References, read when the step points to them:
- `references/conflict-patterns.md`: how to resolve each kind of conflict.
- `references/github-traps.md`: CI and GitHub behaviours that bit earlier runs.
- `references/pr-body-template.md`: the combined PR body.

## Procedure

### 1. Inventory and pin

```bash
git fetch <remote>
python <skill>/scripts/inventory.py --target <target> <PR numbers…>
```

The pinned SHAs it prints are the identity of the work from here on. Every later
command uses those SHAs, never branch names, because a branch can move under you
when someone pushes a fix. If a head moves, the audit would vouch for commits
you never merged.

Stop and ask the user when the inventory shows any of these:
- a PR that is closed, merged or a draft;
- a hold (label, title, or an earlier instruction in the conversation);
- a base that is neither the target nor another PR in the set;
- a fork head the user may not control;
- a head that moved during the inventory.

Then read the **review feedback** and linked issues it lists. Each PR was
written to address something. A combination that lands a PR with an
unaddressed bug report, or short of its issue's scope, is a regression in
disguise. For each unresolved thread or review, decide: already fixed, refuted
(with the reason), or open. Report open ones to the user. Do not silently fix
someone's PR inside the combination. Failing checks on a PR's own head are that
PR's defects; note them now so they are not blamed on the combination later.

### 2. Choose the order

Use stacked parents before children (the inventory's order does this), then the
user's order, then PR number. The file-overlap section shows which pairs will
interact. Merging the PR that restructures a shared file before the PRs that
make small edits to it usually gives smaller, clearer conflicts.

### 3. Simulate

```bash
python <skill>/scripts/simulate_merge.py --target <target-sha> --pairwise <N=sha …in order>
```

This merges in memory only, touching no worktree, index or ref. It shows the
step and the files where the train conflicts. The pairwise section tells an
order problem from a real collision. If a different order removes a conflict
without hiding a real one, use it. Tell the user what conflicts to expect before
you start.

### 4. Build the integration branch

Create a fresh worktree from the pinned target SHA. Name the branch after the
repo's convention, or default to `merge-<first>-<last>`. Do not let it track
the target:

```bash
git worktree add --no-track -b <branch> <worktree-path> <target-sha>
cd <worktree-path>
git merge --no-ff -m "Merge PR #<N> (<head-ref>) into <branch>" <head-sha>   # one PR at a time, in order
```

Merge every PR's pinned head **unchanged**, each with its own `--no-ff` merge commit.

Never squash, rebase or cherry-pick the constituents' commits. Keeping each
head commit as an ancestor of the tip is what lets the audit prove inclusion,
and what makes GitHub mark each PR "Merged" when the combination lands.
Rewritten commits lose both.

Also never resolve with `-X ours/theirs` or a whole-file `checkout --ours/--theirs`.
Each of those deletes one PR's work in that file, with no record.

### 5. Resolve conflicts as a union of intents

When a merge stops, read `references/conflict-patterns.md` and follow it. In short:

- Read what each side did and why: the diff from the merge base, the commit messages, and the PR body.
- Keep both changes.
- Where one side rewrote a file and the other edited it, re-apply the edits onto the rewrite.
- Keep ordered lists ordered, and renumber colliding sequence numbers.
- Regenerate lockfiles and generated files rather than hand-merging them.
- Preserve line endings.

If two PRs genuinely want opposite things (one removes what the other relies on,
and neither PR's purpose settles it), stop and ask the user. That is a product
decision, not a merge mechanic.

Record every resolution in the merge commit message: the file, what each side
did, and what you kept. You will reuse these notes in the PR body.

### 6. Check that the union holds together

A merge with no textual conflicts can still be broken. In one earlier case two
PRs were each green, but once both landed the target did not compile, because
one PR deleted an interface member the other had started using.

- For each PR that deletes or renames a symbol, file, route, option or setting, grep the tip for remaining uses, especially uses added by the other PRs.
- Build, compile, typecheck or lint the combined tip **once**. If several agents are involved, one build at the tip replaces their per-branch builds. Pegasus: CI is the build; no local test that CI runs.
- Leave the test suites to CI when the target's CI runs on PRs, unless the repo says otherwise or the user asks. Running locally what CI is about to run wastes the user's time.

### 7. Audit for lost work

```bash
python <skill>/scripts/audit_combined.py --target <target-sha> --tip HEAD <N=sha …>
```

It checks:
- every PR head and the target are ancestors of the tip;
- files only one PR touched are byte-identical to that PR's head;
- in shared files, every line each PR added is still there, and nothing a PR removed has come back;
- deleted files stay deleted;
- no target content vanished that no PR removed.

It ignores whitespace and a trailing `,`/`;`, so appending to a list is not
read as a loss.

Every **FAIL** must be fixed or match a resolution you recorded (for example,
"#1018's Reconcile form dropped because #1016 removed Reconcile"). Every
**REVIEW** line, such as text written only in a merge resolution, must match a
recorded resolution too. An unexplained item is work you lost by accident.
Do not push until the audit is clean or fully explained.

### 8. Fix what breaks, in the right place

- **The defect already exists at one PR's own head** (its CI fails there too, or a review found it): it belongs to that PR. Fixing it on the PR's branch keeps the PR correct on its own. Ask the user first, then push a normal (non-force) commit to that branch and merge the updated head into the integration branch with `--no-ff`. If the user would rather not touch the PR branch, commit the fix on the integration branch and say which PR it belongs to.
- **The defect exists only in the combination** (an interaction): commit it on the integration branch as its own commit, naming the PRs involved (`fix: build the union of #12 and #15 …`).

If a constituent head moves for any reason, merge the new head in rather than
rebuilding, and update the pinned SHA. After every fix, re-run the audit with
the current pins.

### 9. Push and open the combined PR

```bash
git push <remote> <branch>          # new branch only; never the target
gh pr create --base <target> --head <branch> --title "…" --body-file <file>
```

Write the body from `references/pr-body-template.md`. It includes the order
table with pinned SHAs, review findings, merge resolutions, fixes, the audit
output, verification, held PRs and linked issues. The constituent PRs rarely
get comments of their own, so this body is the durable record of what was
combined and how.

### 10. Watch CI and report the first failure

Poll the combined PR's checks and tell the user about the **first** failing
check as soon as it appears, with the job and the failing test or error. Do not
wait for the whole run. The poller is `resolve-issue`'s
`scripts/watch_checks.py` (same skills folder): run it once with `--once` to
see real output, then in the background; it exits on the first failure with
the job id, and exits 3 on a conflicting PR. Before trusting "no checks yet",
confirm the PR is mergeable: a conflicting PR runs no CI. A checkout that hangs
about 5 minutes in is a stale merge ref, so close and reopen the PR. See
`references/github-traps.md`.

Fix failures by the rule in step 8, push, re-run the audit, and update the PR
body. A flaky failure that also occurs on the target or on a constituent's own
head is not caused by the combination. Say so, with the evidence, rather than
"fixing" it.

### 11. Stop and report

Stop once the combined PR is open, either with CI green or with the first
failure reported. Report:
- the PR URL and the tip SHA;
- the audit verdict, with the resolutions behind any FAIL or REVIEW item;
- CI state;
- open review findings;
- anything held or excluded;
- what landing will need.

Do not merge without an explicit instruction.

## Landing (only when the user explicitly asks)

1. Fetch and confirm that the combined PR's head SHA is the one CI tested and the
   audit covered. If anything was pushed since, re-run the audit and wait for CI.
2. Land with a merge commit (`gh pr merge M --merge`), or fast-forward the target
   to the tested tip if the repo does that. A fast-forward must be a lease-guarded,
   non-forced push of exactly that SHA. Never squash or rebase, because the
   constituents would stay "Closed", not "Merged", and their commits would no longer
   be provably included.
3. Fetch, then confirm:
   - each constituent PR reads `MERGED` (`gh pr view N --json state`);
   - each head is an ancestor of the target.

   A stacked PR whose base was not the target stays open. Close it with a
   comment naming the combined PR, the merge SHA and the ancestry proof.
   Record the merge SHA; step 4's comments cite it.
4. Close the issues the landed PRs link. Closing keywords only fire on the
   default branch, so on any other target this step is the only thing that
   closes them. For each constituent PR, re-read its links at landing time:

   ```bash
   gh pr view N --json closingIssuesReferences --jq '.closingIssuesReferences[].number'
   ```

   For each issue: confirm the PR reads MERGED and its head is an ancestor of
   the target (step 3), confirm the issue is still open, and check no other
   still-open PR also links it (`gh pr list --state open --search "<issue> in:body"`).
   Then close it with the evidence:

   ```bash
   gh issue close <I> --comment "Fixed by #N, landed in <target> via #M (merge <sha>)."
   ```

   Skip, and report, an issue that is already closed or that another open PR
   still claims; it is not this landing's to settle. A PR whose body has an
   issue number but no closing keyword links nothing: list those from the
   body (`#\d+`) and ask the user before closing them. Leave branch and
   worktree cleanup for a separate, approved pass.

Never close a constituent PR before the combined PR has landed. Until then it
is the fallback.
