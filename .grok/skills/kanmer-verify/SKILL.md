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
   retain the PR identity and packet commands. On any resumed or suspicious
   Review/Verifying ticket, call `reconcile_ticket id: <ID>` as a dry run first
   and, only when it returns a recommendation, apply that recommendation with
   `apply_reconciliation id: <ID>, expected_revision: <the recommendation's
   revision>` before re-reading
   anything by hand — the inspector never mutates, and its typed evidence names
   the unexplained state faster and more truthfully than a manual re-read.
2. Ask GitHub for `state`, `mergeCommit`, and `url`. If the PR is not `MERGED`
   or `mergeCommit` is null, stop immediately: this skill is running too early.
3. **Look up the bound receipt before any Git operation.** Read the project's
   verification contract from `get_status` — `delivery.integrationBranch` and
   `delivery.verification` — and query the run it names for this exact merge
   SHA before creating anything. On Kanmer's own board that contract is the
   default (`pr.yml`, job `verify`, event `push`); on another project it is
   whatever that board declares, and this skill never assumes a name.
4. **Classify each packet obligation** as `satisfied`, `missing`, or
   `rejected` against that receipt.
5. **Create the detached verification worktree only if step 4 left anything
   missing.** A fully satisfied receipt needs no new full run and no
   verification worktree at all. When the contract's workflow has **no** run at
   the exact merge SHA, every obligation is `missing`, the designated verifier
   runs them all here, and the proof records `receipts: []` with the reason —
   a complete proof, not a degraded one.
6. Run only the missing checks there. Record every command, cwd, exit code,
   observed result, and summary; preserve failures and inconclusive attempts.
7. Replace `proof/proof.md` as one version-aware proof record — the receipt
   that discharged an obligation goes in `receipts:`, and any check actually
   run goes in `attempts:`. Only a truthful top-level `PASS` may proceed to
   the Done gate. Give every non-PASS result a `failure_class` and route it by
   the table below: `transient` retries in Verifying, `inconclusive` waits in
   Verifying, `implementation` returns to Implementing, `plan` returns to
   Preparing. A failure that is irrecoverable or superseded may instead use
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

## Look up the bound receipt before any Git operation

The project's board decides which run counts. Read it first — never hardcode a
workflow or job name:

```sh
# get_status → delivery.integrationBranch, delivery.verification
# delivery.verification = { workflow, jobs: [...], event }
```

`delivery.verificationSource` says whether that contract came from `board.yml`
or is the shipped default (`pr.yml`, `["verify"]`, `push` — Kanmer's own
contract). Then ask GitHub for the run the contract names, at the exact merge
SHA:

```sh
gh run list --workflow <workflow> --event <event> --commit <MERGED_SHA> --limit 5 --json databaseId,headSha,event,status,conclusion,url,createdAt
gh run view <databaseId> --json jobs,conclusion,status,attempt,headSha,url
```

**Satisfied** only when ALL of the following hold: `headSha` string-equals
the full merge SHA; the run's `event` equals the contract's `event`; the
workflow is the contract's `workflow`; and **every** job named in the
contract's `jobs` has `status: completed` and `conclusion: success`. One green
job out of two required ones is not satisfied — the obligations the other job
runs were never run at all.

**Rejected** — not merely missing — for: `cancelled`, `skipped`,
`timed_out`, or `action_required`; a contract job absent from the run; or a
run for an event the contract does not name. A `pull_request` run is
acceptable *only* when the contract's `event` is `pull_request` **and** the
run's `head_sha` equals the merge SHA. For a squash merge it never does: the
PR head and the squash commit pushed to the integration branch are different
commits with different history, even when their diffs match. So a project
whose CI runs only on pull requests always takes the fallback below.

**No matching run at all** is the ordinary case for a project whose workflow
does not run on pushes to the integration branch. That is `missing`, not an
error, and it routes to the fallback — see "Exact detached worktree".

**In progress**: wait once with `gh run watch <databaseId> --exit-status`
rather than starting a competing local rail. Do not poll repeatedly and do
not fall back to a local run while the bound run is still queued or running.

## Classify packet obligations

Classify each of the packet's named obligations as `satisfied`, `missing`, or
`rejected` against the receipt looked up above:

- An obligation the contract's jobs actually run is `satisfied` by a
  `satisfied` receipt. Do not re-run it locally. *In this repository* that
  mapping is `pr.yml`'s `verify` job running exactly the packet's
  `VERIFY_STEPS`; that is the worked example, not the rule. For another
  project, read what its declared jobs run and draw the line there.
- Manual GUI checks, installed-host checks, Windows-lock checks, and any
  provider/deployment check stay `missing` regardless of the receipt: the
  hosted rail cannot observe them, so they are always run in the worktree as
  today.
- A `rejected` receipt (see above) leaves every obligation it would have
  covered `missing` — reject it explicitly in the proof rather than silently
  falling back.

## Exact detached worktree

**Create this worktree only if the classification above left something
missing.** A fully `satisfied` receipt needs no new full run and no
verification worktree at all — skip straight to writing the proof with the
receipt in `receipts:` and no new `attempts:`.

**The fallback, stated explicitly.** When the contract's workflow has no run at
the exact merge SHA — because the project's CI runs on pull requests only, on a
different branch, or under a workflow that has not been declared yet — every
obligation is `missing`. There is nothing to reject and nothing to wait for:
the designated verifier runs every obligation here, in the detached worktree,
and the proof records `receipts: []` plus one body sentence naming why there
was no receipt (for example: "`ci.yml` does not run on pushes to `dev`, so
there is no post-integration run at this merge SHA; every obligation was run
locally"). That proof is complete and authorises Done exactly like a
receipt-bearing one. Never invent a receipt for a run that does not exist, and
never treat the absence of one as a failure of the change.

When something is missing, from a normal repository checkout (never
`.worktrees/kanmer`):

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
then run only the checks classification left `missing`, in the detached
worktree. Give this
verification its own log paths, named from the ticket and merged SHA: two
verifiers running at once and sharing one log file destroy each other's
evidence. Do not
invent a green result for a manual GUI, hosted GitHub, provider, deployment,
or Windows-lock check that is unavailable. Record that attempt as
`INCONCLUSIVE` with exit code `null` when no process ran. A failed command is
`FAIL` with its exact non-zero exit; if a later retry passes, retain both
attempts in chronological order.

Every rerun that can change the verdict is its own typed attempt. Never record
a rerun as prose appended below the frontmatter: prose is not read by anything,
and a proof whose body contradicted its own verdict is the defect this record
exists to make impossible.

For each attempt record, one of exactly two shapes. A command that ran:

```yaml
- attempted_at: "<ISO-8601 timestamp>"
  command: "<exact command>"
  cwd: "<repo-relative or injected detached path>"
  exit_code: 0 # integer; 0 for PASS, non-zero for FAIL
  result: PASS # PASS | FAIL | INCONCLUSIVE
  authority: authoritative # authoritative | supporting
  summary: "<observed output/result synopsis>"
```

A manual or unavailable check, where no process ran — omit `command` and `cwd`
entirely and describe the procedure in `summary`:

```yaml
- attempted_at: "<ISO-8601 timestamp>"
  exit_code: null
  result: INCONCLUSIVE
  authority: authoritative
  failure_class: inconclusive
  summary: "<what could not be run, and what would make it conclusive>"
```

`authority` is the field that makes the ledger mean something. **The final
entry must be `authoritative`**, and everything before it may be `supporting`.
So a rerun that fails after an earlier pass cannot be filed as a supporting
note: it becomes the final authoritative entry, and the top-level `result`
follows it. Attempt timestamps strictly increase; two attempts may not share
one. A `FAIL` attempt carries `failure_class: implementation | plan |
transient`; an `INCONCLUSIVE` attempt carries `failure_class: inconclusive`; a
`PASS` attempt carries none.

## Whole-file proof record and Done gate

Read `get_ticket_doc(id: <ID>, doc: "proof")` first. Replace it with
`set_ticket_doc` and pass the returned version as `expected_version`; do not
append a proof frontmatter record. The frontmatter is exactly:

```yaml
kind: proof-record
schema: 2
merged_sha: "<full merge commit SHA>"
environment: "<detached verification worktree and runtime>"
verified_at: "<ISO-8601 timestamp, equal to the final attempt's attempted_at>"
result: PASS
attempts:
  - # at least one; see the attempt shapes above
```

`schema: 2` is what declares the validated record. Write it on every proof you
write. Omit it and the record is reported `legacy` — never rewritten, never
reinterpreted, and no authority for Done on a board with strict proof
validation.

`merged_sha` is a full 40-hex object id; `environment` and `verified_at` are
non-empty, and `verified_at` equals the final authoritative attempt's
`attempted_at`. The top-level result is exactly
`PASS | FAIL | INCONCLUSIVE | WAIVED_BY_OPERATOR`, and — apart from a waiver —
it must equal the final authoritative attempt's result. A non-PASS record also
carries a top-level `failure_class` equal to that attempt's.

`WAIVED_BY_OPERATOR` is a human disposition only. It requires `waived_by` and
`waiver_reason` in the frontmatter naming the operator and the reason; it is
not an attempt result, the verifier never writes it on its own authority, and
reconciliation never recommends a move from one. Keep every failed or
inconclusive attempt when a later run passes.

When step 3 found a satisfied receipt, add it beside `attempts:` as one
`receipts` entry per obligation it discharged. This is additive: a proof with
no `receipts` key behaves exactly as it did before this list existed, and
existing proofs are never rewritten to add one.

```yaml
receipts:
  - kind: github-actions-run
    provider: github
    repo: collisionengineers/kanmer
    workflow: pr.yml
    event: push
    run_id: 1234567890
    attempt: 1
    head_sha: "<full merge SHA — must equal merged_sha>"
    job: verify
    conclusion: success
    url: "https://github.com/collisionengineers/kanmer/actions/runs/1234567890"
    covers: ["npm run verify"]
    observed_by: "<actor>"
```

The `workflow`, `job` and `event` values above are the *contract's*, read from
`get_status.delivery.verification` — the block shown is Kanmer's own default.
Write one receipt per contract job the run discharged; a proof that covers only
some of the required jobs is rejected as incomplete.

A receipt whose `head_sha` disagrees with `merged_sha` is rejected by
`assessReceipt` and, at runtime, by the reconciliation classifier's own
`PROOF_RECEIPT_SHA_MISMATCH` finding. Every other reason — a `job`, `workflow`
or `event` that is not the contract's, a non-`success` `conclusion`, an
unrecognised `kind`, a `run_id` that is not a positive integer, a missing
`url`, or a receipt set that leaves a required job uncovered — is likewise
checked at runtime through the classifier's `PROOF_RECEIPT_REJECTED` finding
(`packages/core/src/reconciliation.ts`'s `receiptAssessmentRejections`, which
calls `assessReceiptSet` on the proof's receipts with the board's contract). A
rejected receipt never authorises Done, and never authorises a backward
`implementation`/`plan` route on a FAIL proof either, whichever way it would
otherwise route the ticket. A proof with **no** receipts is not rejected: that
is the fallback, and it is judged on its attempts alone.

When the result is `FAIL` or `INCONCLUSIVE`, add one more key:

```yaml
failure_class: implementation # implementation | plan | transient | inconclusive
```

- `implementation` — the shipped code or artefact is wrong against the plan
  and governing docs (a real failing assertion, a missing production caller,
  a broken artefact).
- `plan` — the code does what the plan said and the plan is what is wrong
  (an acceptance check that cannot be true, a governing-doc conflict, an
  unmet requirement the plan never covered).
- `transient` — the environment, not the change: flake, timeout under load,
  a hosted service unavailable, a known host quirk already recorded on the
  board. **`transient` is a conclusion you earn, never one you assert.** A red
  run — local or hosted — is discharged only with all three of: a re-run of the
  same job at the same SHA with no code change, a confirmation that the failing
  test or file is untouched by this diff, and a mechanism argument for why the
  change cannot reach it. Retain every attempt, red and green, in the proof.
  Judging by a hosted rail is necessary and not sufficient: a single red hosted
  run is no more proof of a regression than a single green local run is proof of
  correctness.
- `inconclusive` — no process ran or the evidence cannot distinguish the
  three above; say what would make it conclusive.

The class routes the ticket. The verifier writes the proof; the move itself
is the controller's or operator's, made with `move_item` and a `reason` that
quotes the proof (every backward move is audited under `## Transitions`):

| `failure_class` | Next stage | How |
|---|---|---|
| `transient` | stays in Verifying | rerun the failed check; retain both attempts. Never the default: a proof that names no class is treated as `inconclusive`, not as retryable. |
| `inconclusive` | stays in Verifying | report the unavailable check and what would make it conclusive; hosted rails may be authoritative. Default for any non-PASS proof that names no class. |
| `implementation` | `verifying` → `implementing` | `move_item` with `reason: "proof FAIL implementation: <summary>"`; the fix reuses the same ticket, branch and worktree, but the reviewed PR is already merged, so the fix necessarily opens a new PR against the integration target and the next review binds to that new PR. |
| `plan` | `verifying` → `preparing` | `move_item` with `reason: "proof FAIL plan: <summary>"`; the plan is revised through `kanmer-plan` before any new implementation. |

## What is validated by code and what is human judgement in this release

Code validates: receipt shape (`kind` is `github-actions-run`; `workflow`,
`job` and `event` equal to the project's declared contract; `run_id` a positive
integer; `url` present; `attempt`, `provider` and `repo` well-formed when
present), `head_sha` exactly matching the PR's merge SHA, and `conclusion ==
"success"`. It also validates the receipts *as a set*: a proof whose accepted
receipts do not cover every job the contract requires is rejected as
incomplete, naming the missing jobs, so one green job of two can never stand in
for both. `assessReceipt`/`assessReceiptSet` are the functions that check every
one of these, and they run at verification time through
`reconcileEvidence`/`reconcile_ticket`: a receipt whose `head_sha` disagrees
with the merge SHA produces `PROOF_RECEIPT_SHA_MISMATCH`, and a receipt
rejected for any other reason (wrong job, wrong workflow, wrong event,
wrong conclusion, unknown kind, missing field) produces
`PROOF_RECEIPT_REJECTED` with the exact reasons — either finding blocks
`MOVE_TO_DONE` and the backward `ROUTE_VERIFICATION_FAILURE` routes alike.
A receipt that fails any of them is `rejected`, not merely unused.

The human recording the proof is responsible for two judgements this release
does not automate:

- **Provider provenance** — that the receipt genuinely names the GitHub Actions
  run it claims to (not a forged or hand-edited value), and that
  `observed_by` names the actual actor who looked it up.
- **Coverage** — that every obligation marked `satisfied` by the receipt is
  actually something the contract's jobs run. Kanmer's own mapping is "packet
  ⊆ `npm run verify`", because its `verify` job runs exactly `VERIFY_STEPS`;
  another project's mapping is whatever its declared jobs run. Code checks that
  the *jobs* are present and green; nothing checks that a given obligation is
  inside them. An obligation the contract's jobs do not run stays `missing` and
  is run in the worktree regardless of the receipt, and it is the verifier's
  judgement, not a mechanical check, that draws that line correctly for a given
  ticket's packet.

Read a proof record **in full** before acting on it, your own or an earlier
one — the frontmatter carries the only machine-readable verdict, and prose
appended below it can contradict `result:`. A frontmatter-only read is how a
failed attempt gets reported as a pass.

Only `PASS`, or an operator's `WAIVED_BY_OPERATOR`, permits the final move.
Call `get_doc_gates` immediately before `move_item`; move one boundary only,
`verifying` → `done`. If any required check failed or is unavailable, write
the truthful record and remain in Verifying until it is routed. Do not turn
the structural existence gate into a claim that the shipped result passed.

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

**Hand off to `kanmer-closeout`** after either the exact merged-SHA PASS (or
operator waiver) and Verifying → Done move, or an operator-disposed non-PASS
retirement that remains Verifying and is archived; an `implementation` or
`plan` failure hands off to `kanmer-execute` or `kanmer-plan` instead through
the routed backward move. Closeout owns final traceability, release, and
cleanup; this skill never self-reviews, merges, or mutates the board worktree.
