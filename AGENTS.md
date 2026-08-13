<!-- kanmer:instructions:start — managed by kanmer-setup; edits inside will be overwritten -->
# Kanmer operating instructions

This repo's work is tracked on a Kanmer board in `.kanmer/`.

- Start every session with `get_status`, then `list_board` / `list_items` to find your ticket.
- Take a ticket before working: `take_ticket` records the time, branch and worktree, and moves the stage.
- Follow the doc pipeline in the ticket's folder: research.md + impact.md → plan.md → checklist.md → proof.md.
- proof.md is required before a ticket can reach the final stage.
- Add progress notes with `set_ticket_doc` (append: true) — don't rewrite whole documents to add a line.
- Archive, don't delete. Reference other items with [[ID]] wiki-links.
<!-- kanmer:instructions:end -->

# Pegasus repository instructions

Pegasus is Collision Engineers' clean-room case-management and reporting
application. Read the Kanmer board (`.kanmer/`, via the `kanmer` tools) for current work, then the
[documentation index](docs/index.md) for the file that owns your question and
the authority rule.

## Documentation model — PRD, FRD, ADR

This repository separates three questions and gives each a home. **Governance —
this model, the routing rules below, ADR conventions, and where new Markdown
goes — lives in this file, never in an ADR.** [`docs/index.md`](docs/index.md)
is the navigation index and owns the authority chain.

- **`operator-notes.md`** — the binding business truth (what Collision Engineers
  actually said). Protected: stop for user resolution before changing its
  meaning. It is the seed for every PRD and FRD; they restate and structure it,
  never overrule it.
- **PRD — `docs/prd/`** — *what the product must do and why*: business need,
  users, outcomes, scope, permanent boundaries, quality/capacity targets, and
  the acceptance model. A PRD states no mechanics.
- **FRD — `docs/frd/`** — *how a capability must behave*: inputs/outputs,
  states, rules, edge cases, fail-closed behaviour, and acceptance evidence. An
  FRD implements a PRD outcome and cites `docs/design.md` for UI behaviour. It
  never invents product scope or records a technical decision.
- **ADR — `docs/adr/`** — a durable *technical/architectural* product decision
  only. Not documentation rules, not process, not feature behaviour. If a
  decision has behavioural consequences, the behaviour is written in the FRD and
  the ADR links to it.
- **`docs/capabilities.md`** — the schedule and capability-ID registry. Its
  *Canonical owner* column is the join key from each capability ID to its PRD,
  FRD, or ADR. It never holds normative behaviour.
- **`docs/architecture.md` / `docs/operations.md`** — current implemented shape
  and current live state. **`docs/runbook.md` / `docs/engineering.md` /
  `docs/design.md`** — working rules within their scopes. These are downstream
  of PRD/FRD/ADR and never override them.

Routing — where to write, and where to send an agent:

| The change is about… | Write it in |
| --- | --- |
| Product intent, scope, an outcome, a boundary, success criteria | a **PRD** |
| Required behaviour of a capability — I/O, states, rules, edge cases, acceptance | an **FRD** |
| A chosen technical mechanism or architectural boundary | a **thin ADR** + the behaviour in the FRD |
| Schedule, allocation, a capability ID | **`docs/capabilities.md`** |
| A current-state fact (deployed, live, monitored) | **`docs/operations.md`** / **`docs/architecture.md`** |
| A business statement from the operator | **`docs/operator-notes.md`** (protected) |
| A repository rule, convention, or process | **this file** |

### ADR conventions

ADRs are an append-only decision log of durable technical/architectural choices.

- **Stable IDs.** Never renumber, reuse, or delete an ADR. Supersede a decision
  by writing a **new** ADR (the next free number) and setting the old one's
  `status: superseded`. The number is a permanent citation key used across code,
  tests, and tracked plans.
- **One decision per ADR** — a durable technical/architectural choice, not a
  bundle of them.
- **YAML frontmatter** on every ADR, so currency and relationships are
  machine-readable:

  ```yaml
  ---
  id: ADR-0002
  status: accepted        # proposed | accepted | superseded | deprecated
  date: 2026-07-23
  supersedes: []
  superseded_by: []
  related_capabilities: []
  related_frd: []
  tags: []
  ---
  ```

- **Template:** `Status · Context · Decision · Consequences · Options considered
  (optional) · Links`. Status is stated first so a body-only read is never
  mistaken for current when it is superseded.
- **Keep ADRs durable.** No dated cost tables, retail prices, or historical
  runbooks in an ADR — those belong in `docs/operations.md`/`docs/runbook.md`;
  git history keeps the record. Feature behaviour belongs in an FRD.
- **The index** (`docs/adr/README.md`) is a thin table derived from frontmatter:
  `ID | Title | Status | Superseded-by | Owner capability`. The set of current
  architecture decisions is that index filtered to `status: accepted` — a view,
  not a renumbering.

### New Markdown placement

A new Markdown file is one of: a **PRD** under `docs/prd/`, an **FRD** under
`docs/frd/`, a **technical ADR** under `docs/adr/`, or a **transient task plan**
under `docs/temp-plans/`. Everything else edits an existing canonical file. No
ADR is required to authorise a PRD or FRD; a new PRD or FRD records its canonical
owner in `docs/capabilities.md` and is linked from `docs/index.md`.
Workspace-local documentation stays governed by its accepted integration
contract and existing workspace tree.

## Planning process

- The Kanmer board (`.kanmer/`) is the multi-agent work queue.
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
- **Read-only Azure/cloud checks are fully permitted** with no per-target
  approval. Every Azure, deployment, credential, account, destructive, or
  external **write**, and any operation that changes cloud state, requires
  explicit approval for exact targets. Never delete `rg-collisionspike-dev` as a
  first step. The approval matrix is owned by the
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
one worktree, and one PR. **The claimable unit is a Kanmer ticket** on the board
in `.kanmer/`. Taking a ticket with `take_ticket` records the branch, worktree,
date, and agent and moves it to the working stage — that record *is* the claim.

1. **Take.** Orient with `get_status` and `list_items`, then `take_ticket` your
   ticket with the real branch and worktree. Do not take work whose capability
   IDs or files overlap an already-taken ticket. Check `git worktree list` and
   `git branch --list 'task/*'` for same-machine work; if a ticket is already
   taken, coordinate rather than passing `force`.
2. **Worktree.** Create `../pegasus-worktrees/<slug>` on `task/<slug>` from
   `origin/dev`.
3. **Plan.** A non-docs-only task creates a root plan at
   `docs/temp-plans/<slug>.md`. Create as many supporting plans, research notes,
   or review artifacts under `docs/temp-plans/` as the task needs. The root plan
   inventories its supporting files and owns whole-task scope, sequencing,
   dependencies, acceptance conditions, commands, and verification. Supporting
   files share the ticket and need no separate branch or ticket. A task is
   docs-only only when every changed path is a Markdown file outside `src/`,
   `tests/`, `infra/`, and `scripts/`; such a task may skip the root plan. Work
   the ticket's document pipeline (research + impact → plan → checklist →
   proof); `proof.md` is required before the ticket reaches the final stage.
4. **Work and PR.** Implement and verify in the task worktree. The PR targets
   `dev`. Keep the ticket's stage and checklist current as you go.
5. **Review and merge.** Before merge, an agent that did not implement the task
   answers whether the plan missed anything implied by the ticket and whether
   implementation missed anything in the plan. For a docs-only task, review the
   PR diff and description for missing or unauthorized scope. A task PR may merge
   into `dev` only after that review passes and CI is green. Explicit
   `MERGE AUTH GRANTED` is required only for `dev` to `main`.
   Committing is not gated: commit to your own task branch freely and often, in
   small logical slices, without operator authority. Only the `dev` → `main`
   merge requires `MERGE AUTH GRANTED`.
6. **Release or abandon.** After merge, a maintenance push may delete every
   temporary-plan file owned by the task; then remove its worktree and branch and
   move the ticket to the final stage. To abandon, discard only the task's own
   unpushed work, release the ticket (`take_ticket action: "release"`), and
   remove its worktree and branch.

A claim is stale and removable by anyone when its branch was never pushed within
48 hours, or its taken ticket is older than 14 days with no branch activity.
Temporary planning material with no matching active ticket is orphaned and may be
removed after its shared ownership has been checked; a supporting file does not
require its own ticket.

Never touch work that is not yours. Allowed operations are discarding only your
own unpushed commits in your own task worktree, merging `origin/dev` into that
branch, merging its green and independently reviewed PR into `dev`, deleting
its merged branch and worktree, and maintenance pushes to `dev` limited to task
claims and owned temporary-plan deletions. Never force-push, rewrite `dev` or
`main`, stash/reset/clean another person's work, or stage beyond the task.
