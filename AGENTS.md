<!-- kanmer:instructions:start — managed by kanmer-setup; edits inside will be overwritten -->
# Kanmer operating instructions

This repo's work is tracked on a Kanmer board in `.kanmer/`. In a Git repo set up
through the GUI the board lives in its own worktree, `.worktrees/kanmer`, on the
board branch, and MCP is already rooted there — never create, switch or push that
branch yourself. Your own ticket worktree is a separate thing, recorded by
`take_ticket`.

The board branch convention is the repository variable `KANMER_BOARD_BRANCH`,
falling back to `kanmer-board` when it is unset. A branch rename is an
administrator handoff: retarget branch protection and required checks, update
the repository variable, and only then reconcile the board worktree and remove
old refs. Agents must not mutate protected refs, branch protection, or repository
variables; stop and report when the observed branch and configured convention
disagree.

- **Resolve the request before starting a workflow.** Explaining code, reviewing a reference the owner supplied, or producing an isolated artifact is direct work: no ticket, no branch, no worktree. Track work when it changes this repo's shipped behaviour or when the owner asks. Then pick the profile by consequence, not size — a two-line change to authorization, schema, release behaviour or irreversible data still owes its profile's evidence. Never bypass a gate through late-stage creation or an empty `custom` profile.
- Start every session with `get_status`, then `list_board` / `list_items` to find your ticket.
- **Which documents a ticket needs depends on its profile, not on a fixed pipeline.** Call `get_doc_gates <id>` before every move. Not `board.yml` — requirements are injected at resolve time, so its `profiles:` block is not the effective set.
- Stages: backlog → preparing → implementing → review → verifying → done. **A move crosses at most one gated boundary**, so walk the stages one at a time; a jump is refused even when every document exists.
- **Gates constrain `move_item` and nothing else** — creation in any stage is ungated, and `gh pr merge` is outside the engine, so an unmet gate never stops a merge.
- An unticked `- [ ]` in `open-questions/` blocks a move: tick it, or move it below the literal `## Parked (explicitly deferred)` with a reason.
- Read what the current step needs: the ticket body, `get_doc_gates`, the governing decision, the relevant plan/checklist section and the latest proof/review pointer. Documents are folders (`research/`, `plan/`, …) so a type can hold several files — pull older attempts only when a claim or a failure investigation needs them. If the ticket is in a group, read the group's `context.md` too: the constraint binding the batch is written once, there.
- Work each fresh ticket on its own branch and worktree: worktree `.worktrees/<id>`, branch `<id>-<slug>`; `take_ticket` records both and moves the stage. A resumed execution packet is available only in `implementing` and must validate/reuse the exact recorded branch and **worktree root** — never create a second worktree or take the ticket again. It must not name the board, shared source checkout, another active ticket's worktree, or any child of those; its checked-out branch and Git common directory must match the record and source repository. Pause by retaining that taken record; never release a paused ticket while its worktree/branch remains a resume target.
- Write pipeline documents with `set_ticket_doc`. Running notes go to `append_scratch` — scratch is the notepad and is never gated, and neither is anything under `reference/` or `assets/`.
- Proof is written on the configured integration branch after review and the merge, not before. Read it from `get_status` → `delivery.integrationBranch` (default `main`); never hardcode a branch name. Ordinary Done means integrated and accepted there. Deployment belongs to a release or an explicitly deployment-scoped ticket and is never a condition of ordinary Done.
- **One heavy verification owner per host.** Full rails, packaging and installer builds serialize behind the named verifier recorded in the repo's operating index. A second agent waits for that run — or reuses a matching completed CI result — instead of starting a competing whole-repository build. Lightweight file checks do not queue behind it.
- Archive, don't delete. Reference other items with [[ID]] wiki-links.
- Skills run in this order **when a tracked ticket walks the full pipeline**: kanmer-tickets → -research → -plan → -execute → -review → -verify → -closeout. Direct work runs none of them. How far a tracked ticket walks it depends on its profile, so ask `get_doc_gates` rather than assuming every step. Off to the side: -auto (drives that order over many tickets), -docs (governing docs), -groom (fix the board), -report (read-only), -setup (reconcile after a Kanmer update).
- Each skill ends by naming what comes next — read that line before improvising a hand-off.

The local MCP convention is `KANMER_BOARD_BRANCH` in each project-scoped
provider registration or exported local runtime, falling back to the default
board branch when unset. GUI Connect writes the saved board-branch setting into local
registrations. Hosted Actions should mirror the same value in the repository
variable, but Actions variables are not inherited by local processes.
When a native runtime supervisor launches Kanmer through an operator-private
wrapper, that wrapper must export both `KANMER_PROVIDER_CWD` and
`KANMER_BOARD_BRANCH` before invoking the stable launcher.
The GUI's OpenAI tunnel controls manage the same long-lived native runtime
alias through `tunnel-client runtimes connect/status/stop/rm`. Application quit
does not stop that runtime; readiness requires structured non-stale status, and
local removal must confirm the alias is stopped before deleting its metadata.

## Agent conduct

**Scope**

1. **Scope is the brief.** “While I’m here” changes are follow-up tickets, not commits.
2. **Never absorb another ticket’s scope.** Link it and let it be worked on its own record.
3. **Release and remediation work ships no new features.**
4. **The ticket precedes the branch.** No board record, no PR.
5. **Stop at the stop condition.** Never merge your own PR or start the next ticket; report deviations instead of redesigning.

**Build**

6. **Greenfield has no legacy.** Unless the brief names users or data, add no fallback, compatibility, or deprecation path; delete what you replace.
7. **Reuse before build.** Name the helper, port, or route you extend; report a genuinely unfit one instead of silently building a parallel copy.
8. **One list per concept.** A second copy in another layer is duplication, even when it is “just strings”.
9. **Paths are relative.** Use repo-root-relative or injected configuration, never machine-specific paths.
10. **Dependencies are approvals.** Add no package unless the brief lists it.
11. **Concurrency results are never discarded.** Retry, defer, or surface them; a swallowed conflict is data loss.
12. **Errors surface.** No catch-all suppression or empty catch.
13. **No fabricated domain data.** Fixtures use the documented estate.

**Prove**

14. **Done means wired.** New code needs a named production caller; registered-but-unreachable or test-only code is not done.
15. **Runtime dependencies ship in the artifact.** Prove the deployed image carries every required browser, font, or package.
16. **A schema change and its permissions ride the same diff.** Include migration, grants, and bootstrap census together.
17. **Recorded commits must be reachable.** Ticket SHAs must exist on the merge target.
18. **Stubs are not done.** Do not present TODOs, placeholders, or mocks as implementation.
19. **Tests prove the claim.** Never weaken or delete an assertion to pass; a failing test stops and is reported.
20. **Verify with exit codes.** Run stated commands and record outputs; INCONCLUSIVE is not PASS, and a later pass does not erase a failure. Done requires PASS; an explicitly disposed terminal non-PASS stays Verifying, is archived, and is released.
21. **No speculative CI or tests.** Delete a gate that gates nothing.

**Conduct**

22. **Review findings get dispositions.** Fix, reject with reason, accept risk, defer to a ticket, or mark obsolete after change, naming the superseding commit; never silence them. Findings from one root cause are one class with one remedy, never one patch per example, and a convention change lands in the same PR as the work that needs it.
23. **Secrets never appear in code, tickets, or proofs.**
24. **A PR that changes commands or conventions updates AGENTS.md in the same PR.**
<!-- kanmer:instructions:end -->

## Kanmer conventions for this repository

The board branch convention is the repository variable `KANMER_BOARD_BRANCH`,
falling back to `kanmer-board` when it is unset. A branch rename is an
administrator handoff: retarget branch protection and required checks, update
the repository variable, and only then reconcile the board worktree and remove
old refs. Agents must not mutate protected refs, branch protection, or repository
variables; stop and report when the observed branch and configured convention
disagree.

A resumed execution packet is available only in `implementing` and must validate/reuse the exact recorded branch and **worktree root** — never create a second worktree or take the ticket again. It must not name the board, shared source checkout, another active ticket's worktree, or any child of those; its checked-out branch and Git common directory must match the record and source repository. Pause by retaining that taken record; never release a paused ticket while its worktree/branch remains a resume target.

The local MCP convention is `KANMER_BOARD_BRANCH` in each project-scoped
provider registration or exported local runtime, falling back to the default
board branch when unset. GUI Connect writes the saved board-branch setting into local
registrations. Hosted Actions should mirror the same value in the repository
variable, but Actions variables are not inherited by local processes.
When a native runtime supervisor launches Kanmer through an operator-private
wrapper, that wrapper must export both `KANMER_PROVIDER_CWD` and
`KANMER_BOARD_BRANCH` before invoking the stable launcher. Native
The GUI's OpenAI tunnel controls manage the same long-lived native runtime
alias through `tunnel-client runtimes connect/status/stop/rm`. Application quit
does not stop that runtime; readiness requires structured non-stale status, and
local removal must confirm the alias is stopped before deleting its metadata.

## Agent conduct

**Scope**

1. **Scope is the brief.** “While I’m here” changes are follow-up tickets, not commits.
2. **Never absorb another ticket’s scope.** Link it and let it be worked on its own record.
3. **Release and remediation work ships no new features.**
4. **The ticket precedes the branch.** No board record, no PR.
5. **Stop at the stop condition.** Never merge your own PR or start the next ticket; report deviations instead of redesigning.

**Build**

6. **Greenfield has no legacy.** Unless the brief names users or data, add no fallback, compatibility, or deprecation path; delete what you replace.
7. **Reuse before build.** Name the helper, port, or route you extend; report a genuinely unfit one instead of silently building a parallel copy.
8. **One list per concept.** A second copy in another layer is duplication, even when it is “just strings”.
9. **Paths are relative.** Use repo-root-relative or injected configuration, never machine-specific paths.
10. **Dependencies are approvals.** Add no package unless the brief lists it.
11. **Concurrency results are never discarded.** Retry, defer, or surface them; a swallowed conflict is data loss.
12. **Errors surface.** No catch-all suppression or empty catch.
13. **No fabricated domain data.** Fixtures use the documented estate.

**Prove**

14. **Done means wired.** New code needs a named production caller; registered-but-unreachable or test-only code is not done.
15. **Runtime dependencies ship in the artifact.** Prove the deployed image carries every required browser, font, or package.
16. **A schema change and its permissions ride the same diff.** Include migration, grants, and bootstrap census together.
17. **Recorded commits must be reachable.** Ticket SHAs must exist on the merge target.
18. **Stubs are not done.** Do not present TODOs, placeholders, or mocks as implementation.
19. **Tests prove the claim.** Never weaken or delete an assertion to pass; a failing test stops and is reported.
20. **Verify with exit codes.** Run stated commands and record outputs; INCONCLUSIVE is not PASS, and a later pass does not erase a failure. Done requires PASS; an explicitly disposed terminal non-PASS stays Verifying, is archived, and is released.
21. **No speculative CI or tests.** Delete a gate that gates nothing.

**Conduct**

22. **Review findings get dispositions.** Fix, reject with reason, accept risk, or defer to a ticket; never silence them.
23. **Secrets never appear in code, tickets, or proofs.**
24. **A PR that changes commands or conventions updates AGENTS.md in the same PR.**

# Pegasus repository instructions

Pegasus is Collision Engineers' clean-room case-management and reporting
application. Read the Kanmer board (`.kanmer/`, via the `kanmer` tools) for current work, then the
[documentation index](docs/index.md) for the file that owns your question and
the authority rule.

## Commands

Canonical solution commands (`--locked-mode` enforces the committed package
locks); run these before delivery:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

Identical on Windows and Linux (`pwsh` either way). Focused per-project forms
and the two complementary integration-test filters are in
[the runbook](docs/runbook.md#locked-restore-build-and-test).

After changing a routed Razor page, regenerate the Test UI snapshots with
`./scripts/Update-TestUiSnapshots.ps1`, then prove them with
`./scripts/Update-TestUiSnapshots.ps1 -Verify` (a fresh capture; add
`-SkipCapture` to reuse the last one) and `./scripts/Test-UiCatalogue.ps1`.
For a focused refresh, pair the page prefix with the test cohort that captures
it:

```powershell
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 `
  -Scope case-details `
  -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests"
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 `
  -Verify -SkipCapture -Scope case-details
```

Commit `docs/design/test-ui/` with the page change: CI runs the same verify
in the build lane and the catalogue check on every change set.

## Architecture map

- `src/Pegasus.Core` — business policy and ports; the one owner of business
  rules.
- `src/Pegasus.Infrastructure` — adapters implementing Core's ports; depends
  on Core only.
- `src/Pegasus.Web` / `src/Pegasus.Worker` — composition roots; depend on both
  Core and Infrastructure.
- `tests/` — `Pegasus.Core.Tests`, `Pegasus.ArchitectureTests`,
  `Pegasus.IntegrationTests`.
- `docs/` — the PRD/FRD/ADR governance tree (below);
  [`docs/current-architecture.md`](docs/current-architecture.md) is the
  as-built system shape and dependency direction, and
  [`docs/operations.md`](docs/operations.md) is deployed/runtime state.
- `.kanmer/` — the Kanmer board (see Kanmer operating instructions above).
- `workspaces/` — independently buildable, non-caller source imports; never
  referenced by the application without a separately accepted integration
  contract.
- `corpus/` — local, ignored, immutable domain evidence; never committed,
  renamed, or modified.
- `infra/` — deployment infrastructure.

## Conventions

- Markdown: the H1 is line 1, a blank line precedes every heading, tables use
  the compact `| --- |` delimiter, and prose is hard-wrapped near 78 columns
  ([engineering](docs/engineering.md#markdown-convention)).
- A new repository Markdown file is a PRD, FRD, or technical ADR only;
  everything else edits an existing canonical file
  ([New Markdown placement](#new-markdown-placement)).
- `Pegasus.Core` owns business policy; a second implementation of a business
  rule anywhere else is a stop condition
  ([Product invariants](#product-invariants),
  [engineering](docs/engineering.md#one-core-owner)).
- The [Simplicity rails](#simplicity-rails) below (search-before-build, one
  list per concept, no speculative abstraction) bind every change.

## Gotchas

- One platform per workstation — Windows+PowerShell 7 or Linux+PowerShell 7 —
  never mixed in a single run or evidence record
  ([runbook](docs/runbook.md#supported-platform)). Release operations use the
  authorised Linux x64 terminal (ADR-0037).
- `corpus/` is local, ignored, and immutable — never upload, publish, commit,
  rename, or modify it.
- A closed composition or feature gate is a disabled flag, not a partially
  shipped feature — never claim or document it as delivered before it has a
  real caller and activation evidence.
- That rule applies to implemented backend behaviour shipped dark. A named,
  ticketed frontend preview may appear disabled and inert before its backend
  exists; it has no production handler and makes no delivery claim.
- Never delete a case; wrong-principal work closes as `Created in error` with
  a linked replacement instead, and neither reference is reused.
- `docs/operator-notes.md` is protected — stop for user resolution before
  changing its meaning.

## Verification

- The canonical solution commands above are the delivery gate.
- Prove the actual caller, not just a green build — a registration, a green
  build, and a deployed feature are different evidence tiers
  ([engineering](docs/engineering.md#required-evidence-tiers)).
- After any deployment, refresh
  [`docs/current-architecture.md`](docs/current-architecture.md) and
  [`docs/operations.md`](docs/operations.md) in the same task; a deploy that
  leaves either stale is unfinished.
- A ticket's `proof/proof.md` is required before it reaches Done, and is
  written on merged `main` after review and merge, not before.

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
  FRD implements a PRD outcome and cites `docs/design/README.md` for UI behaviour. It
  never invents product scope or records a technical decision.
- **ADR — `docs/adr/`** — a durable *technical/architectural* product decision
  only. Not documentation rules, not process, not feature behaviour. If a
  decision has behavioural consequences, the behaviour is written in the FRD and
  the ADR links to it.
- **`docs/capabilities.md`** — the schedule and capability-ID registry. Its
  *Canonical owner* column is the join key from each capability ID to its PRD,
  FRD, or ADR. It never holds normative behaviour.
- **`docs/boundaries.md`** — what is deliberately **deferred or excluded**, and
  the seams preserved to add it later. Boundary rules, not scheduling data.
- **`docs/current-architecture.md` / `docs/operations.md`** — the as-built
  snapshot (what exists and how it is wired now) and the deployed/runtime state.
  Both are living snapshots and must be refreshed after every deploy (see
  Safety rails). **`docs/runbook.md` / `docs/engineering.md` / `docs/design/README.md`**
  — working rules within their scopes. These are downstream of PRD/FRD/ADR and
  never override them.

Routing — where to write, and where to send an agent:

| The change is about… | Write it in |
| --- | --- |
| Product intent, scope, an outcome, a boundary, success criteria | a **PRD** |
| Required behaviour of a capability — I/O, states, rules, edge cases, acceptance | an **FRD** |
| A chosen technical mechanism or architectural boundary | a **thin ADR** + the behaviour in the FRD |
| Schedule, allocation, a capability ID | **`docs/capabilities.md`** |
| A current-state fact (deployed, live, monitored) | **`docs/operations.md`** / **`docs/current-architecture.md`** |
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

A new repository Markdown file is one of: a **PRD** under `docs/prd/`, an
**FRD** under `docs/frd/`, or a **technical ADR** under `docs/adr/`. Transient
task research, plans, checklists, reviews, and proof live in the owning Kanmer
ticket documents, not in the repository tree. Everything else edits an existing canonical file. No
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

## Simplicity rails

Over-engineering is a defect, not a style. The mechanics — the four review
lenses, skip rules, fault-handling and test-support shapes, plan sizing — are
owned by [engineering](docs/engineering.md#simplicity); these are the rules
every task carries:

- **Search before you build.** Name the existing port, helper, convention, or
  test fake you reuse, or say in the plan why none fits. A second business
  implementation, or a third copy of anything else, is a stop condition
  ([one Core owner](docs/engineering.md#one-core-owner)).
- **One list per concept.** An exception taxonomy, a state vocabulary, a label
  table, a precedence order lives in exactly one place. A second copy in
  another layer is duplication even when it is "just strings".
- **No abstraction without a second concrete caller, an external boundary, or
  an accepted ADR** ([abstractions and deferred capabilities](docs/engineering.md#abstractions-and-deferred-capabilities)).
  A wrapper, result record, flag, or optional parameter added so one call site
  can carry something past a design constraint is a smell: fix the constraint
  or use the host's own mechanism.
- **The existing convention wins.** A new way to do something the codebase
  already does (a notice, a header, a refresh, a fake) needs a reason recorded
  in the ticket plan, not a preference.
- **Facts are checked, not argued.** When a plan's premise is a fact about the
  world — production data, a caller's existence, a deployed shape — run the
  read-only check (permitted without approval) and record it, instead of
  reasoning it away in a research document.
- **Plans are proportional to their diff** — a plan longer than the change it
  describes, or carrying ritual steps, is itself over-engineered
  ([plan sizing](docs/engineering.md#plan-sizing)).
- **Operator-facing explanation is a defect.** Labels, values, and at most
  one consequence sentence on a destructive action; no field hints, no
  how-it-works copy, no empty-state panels in read-only view. The design
  authority's [No explanatory copy and page economy](docs/design/README.md#no-explanatory-copy-and-page-economy)
  rules bind every UI change.
- **Operator-facing explanation is a defect.** Labels, values, and at most
  one consequence sentence on a destructive action; no field hints, no
  how-it-works copy, no empty-state panels in read-only view. The design
  authority's [No explanatory copy and page economy](docs/design/README.md#no-explanatory-copy-and-page-economy)
  rules bind every UI change.
- **Simplify without over-correcting** — clarity beats brevity; a helpful
  abstraction stays ([balance](docs/engineering.md#balance)).
- **The simplification pass is quality, not correctness** — findings are
  behaviour-preserving; bugs go to review, scope to a ticket
  ([skip rules](docs/engineering.md#skip-rules)).

## Safety rails

- Work with PowerShell 7 on Windows or Linux, one platform per workstation;
  tracked commands and paths are repository-relative and use forward slashes.
  Platform differences are owned by
  [the runbook](docs/runbook.md#supported-platform).
- Canonical local verification: `dotnet restore`, `dotnet build --configuration
  Release`, and focused/full `dotnet test`; exact profiles are owned by the
  [runbook](docs/runbook.md#locked-restore-build-and-test).
- A closed composition or feature gate is a disabled flag, not a partially
  shipped feature. Do not ship, release, merge as delivered, claim, or document
  a feature behind one as delivered; defer it through the documented
  backlog/decision process until it has its real caller and activation evidence.
- Preserve work that is not yours. The single authoritative allowed/banned
  operation list is in [Repository task workflow](#repository-task-workflow).
- **Read-only Azure/cloud checks are fully permitted** with no per-target
  approval. Every Azure, deployment, credential, account, destructive, or
  external **write**, and any operation that changes cloud state, requires
  explicit approval for exact targets. Never delete `rg-collisionspike-dev` as a
  first step. The approval matrix is owned by the
  [runbook](docs/runbook.md#live-operation-approval-matrix).
- After any deployment or release, refresh the current-state docs in the same
  task, before it merges: [`docs/current-architecture.md`](docs/current-architecture.md)
  (the as-built shape) and [`docs/operations.md`](docs/operations.md) (deployed
  and runtime state) must match the reality just shipped. A deploy that leaves
  either stale is unfinished.
- `docs/operator-notes.md` is authoritative operator truth: preserve every
  material business statement and stop for user resolution before changing
  meaning. Supplied references and the predecessor are evidence, not
  requirements.
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
- `Audit`, `Triage`, and `Blocked intake` retain their settled distinct
  meanings; `Triage` is the only current term. `Needs sorting` is superseded
  by `Unidentified` for that meaning (INTK-007) — see
  [`docs/operator-notes.md`](docs/operator-notes.md#unidentified-received-material);
  it does not rename or collapse Triage, Blocked intake, incomplete Audit
  evidence, or Image Intake.
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
  integration contract and caller-backed proof. A workspace, skill, prompt, or
  model never becomes an application policy owner.
- Local alpha work must not mutate an Outlook mailbox or any Box location. Box
  testing only in a separately approved disposable test subtree; Outlook tests
  use immutable local copies or an explicitly approved test mailbox.

## Repository task workflow

### Approved v1 three-stream exception

The 6 September 2026 implementation uses three owner tickets: PLAT-075
(A/platform and Foundation), CASE-047 (B/casework), and INTK-060 (C/intake).
Their branches are `task/pegasus-v1-platform`, `task/pegasus-v1-casework`, and
`task/pegasus-v1-intake`, based on common dev commit
`3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`. Each machine records its actual
worktree on its own owner ticket. The approved sibling worktree convention is
`../pegasus-worktrees/v1-<stream>`.

A alone authors the common Foundation contracts, schema, migration, grants and
composition. B/C fast-forward to the exact reviewed Foundation commit before
domain work; later shared corrections use the same common commit identity.
The owner-ticket plans carry exact file ownership and contract exceptions.
Existing claims, branches, commits and dirty files remain preserved. One
verification process per host owns builds and full tests; PLAT-075 records the
Codex host's verifier. Other agents perform disjoint work without competing
builds.

The stop is three reviewed PRs to dev, open and unmerged. Local combined
verification creates no fourth PR or published integration branch. No merge,
deployment, reset, real email, Outlook mutation, Box write or live Glass's/EVA
operation is authorized by this implementation. This exception governs the
three owners only; ordinary task rules below continue to apply elsewhere.

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
3. **Plan.** Work the owning Kanmer ticket's document pipeline: research and
   file mapping, impact where needed, then plan and checklist before
   implementation. The ticket plan owns whole-task scope, sequencing,
   dependencies, acceptance conditions, commands, and verification; supporting
   research belongs in named documents inside that ticket. A plan states, per
   step, what existing code it reuses; research states which of its premises
   were verified by a read-only check and which are assumed. `proof.md` is
   required before the ticket reaches the final stage. Do not create transient
   repository task-plan files.
4. **Work and PR.** Implement and verify in the task worktree. For a task
   that changes code, run the simplification pass over the branch's own diff
   before opening the PR — reuse, simplification, efficiency, altitude
   (`/simplify` plus the `code-simplifier` agent, or equivalent independent
   lenses) — apply the behaviour-preserving fixes, and record findings and
   dispositions in the ticket's plan under a dated "Simplification pass"
   heading; a docs-only task records "n/a — docs-only". It is part of the
   work, not a review stage. The PR targets `dev`. Keep the ticket's stage
   and checklist current as you go.
5. **Review and merge.** Before merge, an agent that did not implement the task
   answers whether the plan missed anything implied by the ticket, whether
   implementation missed anything in the plan, and whether the simplification
   pass ran with honest dispositions (unapplied findings named, with a reason
   or a ticket). For a docs-only task, review the PR diff and description for
   missing or unauthorized scope. A task PR may merge
   into `dev` only after that review passes and CI is green. A `dev` to `main`
   release is an exact-SHA, non-force promotion governed by
   [engineering](docs/engineering.md#branches-and-delivery), and needs
   explicit `MERGE AUTH GRANTED` immediately before the `main` update.
   Committing is not gated: commit to your own task branch freely and often, in
   small logical slices, without operator authority. Only the `dev` → `main`
   merge requires `MERGE AUTH GRANTED`.
   DELIV-046 is the sole exception allowed to merge the authorised
   `origin/main` head `32f8679d3695e0dcab8f310a1c20f8b129d20190` into its
   task branch and deliver it through a merge-commit PR to `dev`; the exception
   expires when that PR merges and grants no direct shared-ref update or
   history rewrite.
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
its merged branch and worktree, maintenance pushes to `dev` limited to task
claims and owned temporary-plan deletions, and the authorised exact-SHA,
non-force `dev` to `main` promotion specified in
[engineering](docs/engineering.md#branches-and-delivery). The sole migration
exception is DELIV-003: after DELIV-002 has merged to `dev` with green CI, its
own `origin/dev`-based task branch may merge `origin/main` and deliver that
merge through its reviewed PR to `dev`; it never permits a direct `dev` update
and expires as that PR merges. Never force-push, rewrite `dev` or `main`,
stash/reset/clean another person's work, or stage beyond the task.
