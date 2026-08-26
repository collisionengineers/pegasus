---
name: kanmer-verify
description: Verify a reviewed and merged Kanmer ticket at the exact GitHub merge SHA in a disposable detached worktree, write the versioned proof record with every attempt, and move Verifying→Done only for a truthful PASS. Use after kanmer-review has merged a PR and moved the ticket to Verifying. DO NOT USE for pre-merge review, implementation, or git cleanup. Never update the mutable main checkout as a side effect.
---

# Kanmer verify

Verification is evidence of what shipped, not evidence of whatever `main` has
become since review. The source is the PR's exact GitHub `mergeCommit` SHA.
Verification happens in a disposable detached worktree named from that SHA;
the mutable `main` checkout, the board worktree, and the implementation
worktree remain untouched.

## Workflow

1. Read `get_item` and `get_doc_gates`; confirm the ticket is Verifying and
   retain the PR identity and packet commands.
2. Ask GitHub for `state`, `mergeCommit`, and `url`. If the PR is not `MERGED`
   or `mergeCommit` is null, stop immediately: this skill is running too early.
3. Fetch the exact commit and create a detached verification worktree named
   `.worktrees/verify-<id-lowercase>-<full-merged-sha>`.
4. Assert the worktree is detached, clean, at the exact full SHA, and is not
   `.worktrees/kanmer` or the ticket's implementation worktree.
5. Run the packet's named checks there. Record every command, cwd, exit code,
   observed result, and summary; preserve failures and inconclusive attempts.
6. Replace `proof/proof.md` as one version-aware proof record. Only a truthful
   top-level `PASS` may proceed to the Done gate.
7. Classify a non-PASS result as retryable by default and leave it in
   Verifying. A failure that is irrecoverable or superseded may instead use
   the explicit terminal-retirement path below, but only with the operator's
   disposition. PASS moves only `verifying` → `done`. Both terminal paths hand
   off to closeout.

## Confirm the merge before touching Git

The PR is the authority for the merge SHA:

```sh
gh pr view <pr> --json state,mergeCommit,url
```

Require `state: "MERGED"` and a non-null, full `mergeCommit.oid`. A source
branch SHA, a ticket `commits[]` entry, or the current `origin/main` is not a
substitute. Preserve the exact unmerged response in verify scratch and stop;
do not create a proof record, move the ticket, fetch a guessed ref, or update
main.

## Exact detached worktree

From a normal repository checkout (never `.worktrees/kanmer`):

```sh
git fetch origin
git worktree add --detach .worktrees/verify-<id-lowercase>-<full-merged-sha> <full-merged-sha>
```

Use the full SHA in both the directory name and the worktree argument. Confirm:

```sh
git -C .worktrees/verify-<id-lowercase>-<full-merged-sha> rev-parse HEAD
git -C .worktrees/verify-<id-lowercase>-<full-merged-sha> symbolic-ref --short -q HEAD
git -C .worktrees/verify-<id-lowercase>-<full-merged-sha> status --short --branch
```

`rev-parse HEAD` must equal the PR's full `mergeCommit.oid`, symbolic-ref must
be empty (detached), and status must be clean. If any assertion fails, record
the exact command and exit, stop, and do not repair by checking out or pulling
`main`. Never switch branches, reset, or update a mutable checkout as part of
verification. The detached worktree is disposable; do not
remove the ticket's implementation worktree or its branch.

If the deterministic verification path already exists, refuse to overwrite
it unless it is a clean detached worktree at this same full merge SHA. An
existing path owned by another ticket or pointing at another SHA is a stop and
report condition; do not reuse it by force or choose an unrecorded alternate.

## Run and record the evidence

Read the plan/checklist and packet command hint already bound to the ticket,
then run the named deterministic checks in the detached worktree. Do not
invent a green result for a manual GUI, hosted GitHub, provider, deployment,
or Windows-lock check that is unavailable. Record that attempt as
`INCONCLUSIVE` with exit code `null` when no process ran. A failed command is
`FAIL` with its exact non-zero exit; if a later retry passes, retain both
attempts in chronological order.

For each attempt record:

```yaml
- attempted_at: "<ISO-8601 timestamp>"
  command: "<exact command or manual check>"
  cwd: "<repo-relative or injected detached path>"
  exit_code: 0 # integer, or null for manual/inconclusive
  result: PASS # PASS | FAIL | INCONCLUSIVE | NOT_APPLICABLE
  summary: "<observed output/result synopsis>"
```

## Whole-file proof record and Done gate

Read `get_ticket_doc(id: <ID>, doc: "proof")` first. Replace it with
`set_ticket_doc` and pass the returned version as `expected_version`; do not
append a proof frontmatter record. The frontmatter is exactly:

```yaml
kind: proof-record
merged_sha: "<full merge commit SHA>"
environment: "<detached verification worktree and runtime>"
verified_at: "<ISO-8601 timestamp>"
result: PASS
attempts: []
```

`merged_sha`, environment, and timestamp are non-empty. The top-level result
is exactly `PASS | FAIL | INCONCLUSIVE | NOT_APPLICABLE | WAIVED_BY_OPERATOR`.
`WAIVED_BY_OPERATOR` is a human disposition only and requires the operator
identity and reason in the body; it is not a normal attempt result. Keep every
failed or inconclusive attempt when a later run passes.

Only `PASS` permits the final move. Call `get_doc_gates` immediately before
`move_item`; move one boundary only, `verifying` → `done`. If any required
check failed or is unavailable, write the truthful record and remain in
Verifying. Do not turn the structural existence gate into a claim that the
shipped result passed.

## Terminal retirement after failed verification

A non-PASS result is retryable by default. Do not infer terminal failure from
age, a second ticket, a failed command, or an agent's preference. Leave the
ticket active in Verifying while a rerun or remediation can still make its own
acceptance criteria true.

When the result cannot be repaired in place — for example an immutable release
attempt — the operator may explicitly declare it irrecoverable or superseded.
That disposition must name a reason and either a successor ticket or the
operator's explicit no-successor decision. Then, in this order:

1. preserve the final non-PASS `proof/proof.md` and read it back;
2. link the successor when one exists;
3. add an `## Outcome` note that names the operator, reason, proof result and
   successor/no-successor disposition;
4. set `archived: true` without changing the ticket's Verifying status; and
5. hand off to `kanmer-closeout` for traceability, Git cleanup and release.

Retirement is a terminal **non-success** outcome. Never move a non-PASS ticket
to Done, delete it, erase failed attempts, or archive it automatically. Archive
keeps the evidence recoverable while removing work that has an explicit
terminal disposition from the active board.

After the proof has been read back and either the Done move or explicit archive
succeeds, remove only the disposable detached verification worktree with the
exact recorded path. Keep the implementation worktree and branch for closeout,
and report cleanup and any failure to the next skill. No verification step
merges, rewrites, or pulls main.

---

**Hand off to `kanmer-closeout`** after either the exact merged-SHA PASS and
Verifying → Done move, or an operator-disposed non-PASS retirement that remains
Verifying and is archived. Closeout owns final traceability, release, and
cleanup; this skill never self-reviews, merges, or mutates the board worktree.
