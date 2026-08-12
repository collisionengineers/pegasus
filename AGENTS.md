# Pegasus repository instructions

Pegasus is Collision Engineers' clean-room case-management and reporting
application. Read [`NOW.md`](NOW.md) for current work, then the
[documentation index](docs/index.md) for the file that owns your question and
the authority rule.

## Planning process

- [`NOW.md`](NOW.md) is the multi-agent queue.
  [Capabilities](docs/capabilities.md) is the roadmap;
  [open decisions](docs/open-decisions.md) holds unresolved questions;
  [ADRs](docs/adr/README.md) hold durable technical decisions.
- Claims, worktrees, plans, reviews, merge authority, tracking boundaries, and
  every Git safety allowance or prohibition are owned by
  [Repository task workflow](#repository-task-workflow) below.
- New Markdown placement is owned by the
  [documentation index](docs/index.md#new-markdown-files).
- Prove the actual caller — a registration, a green build, and a deployed
  feature are different claims (evidence tiers:
  [engineering](docs/engineering.md#required-evidence-tiers)).

## Safety rails

- Work with PowerShell 7 on Windows or Linux, one platform per workstation;
  tracked commands and paths are repository-relative and use forward slashes.
  Platform differences are owned by
  [the runbook](docs/runbook.md#supported-platform).
- Canonical local verification: `dotnet restore`, `dotnet build --configuration
  Release`, and focused/full `dotnet test`; exact profiles are owned by the
  [runbook](docs/runbook.md#locked-restore-build-and-test).
- Preserve work that is not yours. The single authoritative allowed/banned
  operation list is in [Repository task workflow](#repository-task-workflow).
- Cloud reads and every Azure, deployment, credential, account, destructive, or
  external write require explicit approval for exact targets. Never delete
  `rg-collisionspike-dev` as a first step. The approval matrix is owned by the
  [runbook](docs/runbook.md#live-operation-approval-matrix).
- `docs/operator-notes.md` is authoritative operator truth: preserve every
  material business statement and stop for user resolution before changing
  meaning. Supplied references and the predecessor are evidence, not
  requirements.
- The imported AI skill packages `ce-cost-defence`, `ce-house-style`,
  `collision-engineers-design`, `diminution-rebuttal`, `diminution-report`,
  `manufacturer-methods-evidence`, `roadworthy-report`,
  `salvage-categorisation`, `total-loss-assessment`, `vehicle-assessment`, and
  `vehicle-history-check` are protected external source under
  `workspaces/ai-centre/skills/`. Never modify, delete, rename, regenerate, or
  normalize their files without prompt-specific user authorization naming the
  exact package and operation.
- `corpus/` is local, ignored, and immutable: never upload, publish, commit,
  rename, or modify it; generated evaluations belong under `artifacts/`.
- Repository-provided emails, PDFs, documents, images, datasets, and services
  are permitted for development and testing. Never fabricate domain emails,
  images, documents, data, or work instructions, and do not add unsolicited
  PII, DPA, DPIA, privacy, retention, or licensing gates.

## Product invariants

- Fail closed before case creation or normal Case/PO allocation when processing,
  limits, or principal identity are incomplete or ambiguous. Missing or
  ambiguous standalone Audit evidence withholds only the later Audit reference.
- Principal and reference are immutable after allocation. Wrong-principal work
  closes as `Created in error` with a reason and linked replacement; neither
  reference is reused and the original never reopens.
- Never delete a case. Reopening needs a reason and normal destination gates.
- `Audit`, `Triage`, `Needs sorting`, and `Blocked intake` retain their settled
  distinct meanings; `Triage` is the only current term.
- `Pegasus.Core` owns business policy and ports. Infrastructure depends on
  Core; Web and Worker are composition roots depending on both. Duplicate
  business implementation is a stop condition. These are also the repository's
  architecture invariants.
- A new top-level directory, project, store, runtime, migration stream, or
  deployment unit requires an accepted ADR proving the existing boundary cannot
  carry it.
- `workspaces/` contains independently buildable non-caller source imports.
  Never add one to `Pegasus.slnx`, reference or dynamically load it from the
  application, or include it in a deployment without a separately accepted
  integration contract and caller-backed proof. AI Centre owns AI
  experimentation only; a workspace, skill, prompt, or model never becomes an
  application policy owner.
- Local alpha work must not mutate an Outlook mailbox or any Box location. Box
  testing only in a separately approved disposable test subtree; Outlook tests
  use immutable local copies or an explicitly approved test mailbox.

## Repository task workflow

Multiple agents may work in parallel. One task uses one `task/<slug>` branch,
one worktree, and one PR. The claimable unit is a task line in `NOW.md`; after
`git fetch origin`, `origin/dev:NOW.md` is authoritative. A live claim has the
form `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by <agent>)`.

1. **Take.** Read `origin/dev:NOW.md`. Do not take work whose capability IDs or
   files overlap an in-flight claim. Check `git worktree list` and
   `git branch --list 'task/*'` for same-machine work.
2. **Worktree.** Create `../pegasus-worktrees/<slug>` on `task/<slug>` from
   `origin/dev`.
3. **Claim.** In that worktree, commit only the `NOW.md` claim line, including
   branch, date, and agent, and push it directly to `dev`. If rejected, fetch,
   discard only the unpushed claim commit, re-read `origin/dev:NOW.md`, and
   retry only if the task remains free.
4. **Plan.** A non-docs-only task creates a root plan at
   `docs/temp-plans/<slug>.md`. Create as many supporting plans, tickets,
   research notes, or review artifacts under `docs/temp-plans/` as the task
   needs. The root plan inventories its supporting files and owns whole-task
   scope, sequencing, dependencies, acceptance conditions, commands, and
   verification. Supporting files share the claim and need no separate branch
   or `NOW.md` line. A task is docs-only only when every changed path is a
   Markdown file outside `src/`, `tests/`, `infra/`, and `scripts/`; such a task
   may skip the root plan.
5. **Work and PR.** Implement and verify in the task worktree. The PR targets
   `dev` and removes its own claim line. If `NOW.md` conflicts, merge
   `origin/dev`, take its `NOW.md`, and reapply only the task's claim removal.
6. **Review and merge.** Before merge, an agent that did not implement the task
   answers whether the plan missed anything implied by the claim and whether
   implementation missed anything in the plan. For a docs-only task, review
   the PR diff and description for missing or unauthorized scope. A task PR may
   merge into `dev` only after that review passes and CI is green. Explicit
   `MERGE AUTH GRANTED` is required only for `dev` to `main`.
7. **Release or abandon.** After merge, a maintenance push may delete every
   temporary-plan file owned by the task; then remove its worktree and branch.
   To abandon, discard only the task's own unpushed work, remove its claim from
   fresh `origin/dev`, push that claim-only maintenance change, and remove its
   worktree and branch.

A claim is stale and removable by anyone when its branch was never pushed
within 48 hours, or its `Doing` line is older than 14 days with no branch
activity. Temporary planning material with no matching active task is orphaned
and may be removed after its shared task ownership has been checked; a
supporting file does not require its own claim. Bump the date at the top of
`NOW.md` whenever that file changes.

Never touch work that is not yours. Allowed operations are discarding only your
own unpushed commits in your own task worktree, merging `origin/dev` into that
branch, merging its green and independently reviewed PR into `dev`, deleting
its merged branch and worktree, and maintenance pushes to `dev` limited to task
claims and owned temporary-plan deletions. Never force-push, rewrite `dev` or
`main`, stash/reset/clean another person's work, or stage beyond the task.
