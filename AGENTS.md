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

# Pegasus repository instructions

Pegasus is Collision Engineers' clean-room case-management and reporting
application. The managed block and Kanmer skills own the ticket lifecycle;
this guide supplies Pegasus-specific requirements. Use the
[documentation index](docs/index.md) for each question's owner and authority.

## Project principles

Unless the task states otherwise, treat Pegasus as under development.
Existing code, deployments, tests and history do not establish a stable
contract. Compatibility requires a named current consumer or persisted-data
dependency and a requirement to preserve it; do not infer it from age or
production-like infrastructure. Current task requirements and authoritative
specifications govern the intended state.

Prefer the simplest correct change within the brief. Update affected callers,
tests and documentation, remove superseded paths, and leave one coherent
implementation. Use normal schema/migration mechanisms without inventing
preservation machinery for disposable development data. Fallbacks must serve
a real supported condition or required recovery. Preserve required validation,
security, reliability and data integrity; stop when the requirements are met.

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

Core CI uses `-- xUnit.MaxParallelThreads=1` so concurrent test collections do
not compete against the intake parsers' short regex time budgets. Keep those
production budgets and every test assertion intact.
SQL shard jobs allow 30 minutes including their build, with the existing
test-concurrency cap and complete-shard coverage checks retained.

Reference-data generator checks run with
`python -m unittest discover -s scripts/reference_data/tests -p 'test_*.py'`.
Text snapshots marked `normalized-lf` hash and count normalized bytes; immutable
domain evidence keeps its exact raw bytes.

Release `PreProvision` validation requires the Box holding-folder identifier
and both Automation MCP certificate URI lists. It validates versioned HTTPS
Key Vault addresses before the read-only Worker smoke; see the release inputs
in `docs/runbook.md`. This check does not authorize provisioning.

After changing a routed Razor page, regenerate the Test UI snapshots with
`./scripts/Update-TestUiSnapshots.ps1`, then prove them with
`./scripts/Update-TestUiSnapshots.ps1 -Verify` (a fresh capture; add
`-SkipCapture` to reuse the last one) and `./scripts/Test-UiCatalogue.ps1`.
Use `-MaxParallelThreads 1` to serialize capture tests when host memory is
limited; the default zero preserves the existing lane concurrency.
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

- `src/Pegasus.Core` — the sole owner of business policy and ports. A second
  business implementation is a stop condition.
- `src/Pegasus.Infrastructure` — adapters; depends on Core only.
- `src/Pegasus.Web` / `src/Pegasus.Worker` — composition roots depending on
  Core and Infrastructure.
- `tests/` — Core, Architecture and Integration test projects.
- `docs/` — governing documents; `docs/current-architecture.md` records the
  as-built shape and `docs/operations.md` the deployed/runtime state.
- `.kanmer/` — the work queue, accessed through Kanmer tools.
- `workspaces/` — provenance for retired source imports. Accepted slices live
  in the application; the historical imports are not active build units.
  Never add an import to `Pegasus.slnx`, reference or dynamically load it from
  the application, or deploy it without a separately accepted integration
  contract and caller-backed proof. A workspace, skill, prompt or model never
  becomes an application policy owner.
- `corpus/` — local domain evidence, protected under Gotchas.
- `infra/` — deployment infrastructure.

A new top-level directory, project, store, runtime, migration stream or
deployment unit requires an accepted ADR proving the existing boundary cannot
carry it.

## Conventions

- Markdown: H1 on line 1, blank lines before headings, compact `| --- |`
  delimiters and prose wrapped near 78 columns. The generated Kanmer preamble
  in this file is exempt; normal formatting applies to the human-owned guide.
  See [engineering](docs/engineering.md#markdown-convention).
- Tracked commands and paths are repository-relative, with forward slashes.
- New Markdown follows [New Markdown placement](#new-markdown-placement).
  Engineering decisions follow [Simplicity rails](#simplicity-rails).

## Gotchas

- Use PowerShell 7 on Windows or Linux, one platform per workstation and
  evidence record. Releases support the authorised Windows x64 or Linux x64
  terminal (ADR-0039); migration bundles match the workstation, while deployed
  hosts remain Linux. See [supported platform](docs/runbook.md#supported-platform).
- `corpus/` is local, ignored and immutable: never upload, publish, commit,
  rename or modify it. Generated evaluations belong under `artifacts/`.
- `docs/operator-notes.md` is protected business truth. Preserve every material
  statement and stop for user resolution before changing meaning. Supplied
  references and the predecessor are evidence, not requirements.
- Read-only Azure/cloud checks need no per-target approval. Cloud-state,
  deployment, credential, account, destructive and external writes require
  explicit authorization for exact targets; use the
  [approval matrix](docs/runbook.md#live-operation-approval-matrix). Never
  delete `rg-collisionspike-dev` as a first step.
- Repository-provided emails, PDFs, documents, images, datasets and services
  are permitted for development/testing. Use the documented estate for
  fixtures; add no unsolicited PII, DPA, DPIA, privacy, retention or licensing
  gates.
- Local alpha work must not mutate an Outlook mailbox or any Box location.
  Box testing requires a separately approved disposable subtree; Outlook tests
  use immutable local copies or an explicitly approved test mailbox.

## Verification

- Run the canonical Commands above before delivery. Apply the
  [evidence tiers](docs/engineering.md#required-evidence-tiers) to the actual
  caller; Kanmer owns proof records, stage movement and closeout.
- After deployment or release, refresh `docs/current-architecture.md` and
  `docs/operations.md` in the same task, before it merges. Both must match the
  reality shipped; stale current-state documentation leaves the task unfinished.
- Git regression fixtures must remove inherited `GIT_*` variables from child
  environments, prove their resolved temporary repository root before writing,
  and check absolute cleanup targets stay inside their own fixture. Tests
  must not mutate the invoking checkout's HEAD, index or configuration.
- CI change routing has one conditional infrastructure-plan owner; an empty
  diff is a supported no-change result. Coordinate whole-solution verification
  with the named host verifier; do not start a competing run.

## Documentation model — PRD, FRD, ADR

This file owns documentation governance, routing, ADR conventions and file
placement. The [documentation index](docs/index.md) owns the authority chain
and navigation. Generic authoring procedures belong to `kanmer-docs`.

| Subject | Canonical owner and constraint |
| --- | --- |
| Operator business statements | `docs/operator-notes.md`; PRDs and FRDs restate it, never overrule it. |
| Product need, users, outcomes, scope, permanent boundaries, quality/capacity and acceptance model | PRD under `docs/prd/`; no mechanics. |
| Capability inputs/outputs, states, rules, edge cases, fail-closed behavior and acceptance evidence | FRD under `docs/frd/`; implements a PRD outcome, cites `docs/design/README.md` for UI behavior, invents no scope or technical decision. |
| Durable technical/architectural product decision | ADR under `docs/adr/`; one decision, with behavioral consequences written in and linked to the FRD. No governance or process rules. |
| Schedule and capability-ID registry | `docs/capabilities.md`; Canonical owner joins each ID to its PRD/FRD/ADR, not normative behavior. |
| Deferred/excluded work and preserved seams | `docs/boundaries.md`; boundary rules, not scheduling data. |
| As-built and deployed/runtime facts | `docs/current-architecture.md` and `docs/operations.md`; living snapshots. |
| Engineering, operating and UI mechanics | `docs/engineering.md`, `docs/runbook.md`, `docs/design/README.md`; downstream of PRD/FRD/ADR. |
| Unresolved decisions | `docs/open-decisions.md`. |
| Repository rules and workflow supplements | This file; ticket-specific material stays on Kanmer. |

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

## Simplicity rails

The managed conduct rules apply throughout. Pegasus adds these constraints;
[engineering](docs/engineering.md#simplicity) owns the four lenses, skip rules,
fault handling, test-support shapes and plan-sizing mechanics.

- Name the existing port, helper, convention or fake reused in the plan, or
  explain why none fits. A third copy of anything is a stop condition;
  Core's exclusive business-policy ownership applies before that threshold.
- Add no abstraction without a second concrete caller, an external boundary
  or an accepted ADR. Fix a design constraint rather than wrapping one call
  site to carry something around it. No horizontal Common/Helpers/Utilities
  packages or version-suffixed replacement components.
- Existing conventions win. Record a concrete reason before introducing a
  different notice, header, refresh or fake convention.
- Check factual premises with read-only evidence, record the result, and
  distinguish assumptions. Keep plans proportional to the diff.
- Classifier/extraction precedence is explicit and covered by contradiction
  tests. Preserve terminal, transient and unknown provider outcomes; metrics
  count successful effects separately from attempts.
- Operator-facing explanation is a defect: labels, values and at most one
  consequence sentence on a destructive action; no field hints, how-it-works
  copy or empty-state panels in read-only views. Follow the design authority's
  [No explanatory copy and page economy](docs/design/README.md#no-explanatory-copy-and-page-economy).
- Clarity beats brevity. The simplification pass preserves behavior; suspected
  bugs go to review and additional scope to a ticket. Apply engineering's
  [balance](docs/engineering.md#balance) and [skip rules](docs/engineering.md#skip-rules).

## Product invariants

- Fail closed before case creation or normal Case/PO allocation when
  processing, limits or principal identity are incomplete or ambiguous.
  Missing or ambiguous standalone Audit evidence withholds only the later
  Audit reference.
- Principal and reference are immutable after allocation. Wrong-principal
  work closes as `Created in error`, with a reason and linked replacement;
  neither reference is reused and the original never reopens.
- Never delete a case. Reopening needs a reason and normal destination gates.
- `Audit`, `Triage` and `Blocked intake` retain distinct meanings. `Triage`
  is the only current term. `Unidentified` supersedes `Needs sorting` for
  that meaning (INTK-007); it does not rename or collapse Triage, Blocked
  intake, incomplete Audit evidence or Image Intake. See
  [operator notes](docs/operator-notes.md#unidentified-received-material).
- A closed composition or feature gate is disabled, not partially delivered.
  Do not ship, release, merge as delivered, claim or document dark backend
  behavior as delivered before its real caller and activation evidence exist;
  defer it through the backlog/decision process. A named, ticketed frontend
  preview may be disabled and inert before its backend exists, with no
  production handler or delivery claim.

## Repository task workflow

Kanmer's managed block and phase skills own ticket stages, leases, gates,
worktree naming, execution packets, proof and cleanup. This section adds
Pegasus requirements; it does not introduce a second pipeline. Reconcile
repository-local Kanmer skill mirrors from the installed distribution through
`kanmer-setup`; do not maintain project-specific forks of those skills.

### Ordinary task rules

- Resolve branches from the board's delivery configuration (currently `dev`
  integration and `main` release). Reuse recorded workspaces on resume; do not
  overlap another taken ticket's files or capability IDs. Coordinate ownership
  rather than forcing a claim.
- Before PR handoff, apply the [simplification pass](docs/engineering.md#simplicity)
  to the branch's own diff using the four independent lenses. Record findings
  and dispositions under a dated Simplification pass in the ticket plan;
  docs-only work records `n/a — docs-only`.
- Commits on the owned branch need no operator grant. The implementer stops
  at PR review handoff. An authorized independent reviewer checks ticket/plan
  coverage, implementation, tests and simplification dispositions (including
  missing or unauthorized scope for docs-only work) and may merge only with
  green CI. The author never merges its own PR.
- Release promotion follows the unchanged exact-SHA procedure in
  [engineering](docs/engineering.md#branches-and-delivery) and the release
  skill. When the current task has not already granted release authority,
  obtain `MERGE AUTH GRANTED` for the candidate immediately before the update.
- Preserve other owners' branches, commits, worktrees and dirty files. Never
  rewrite shared history, force-push, stage beyond the task, manually push the
  board branch, or stash/reset/clean another owner's work. The release route's
  explicit lease is an expected-value assertion, not permission to rewrite.
- Age alone never authorizes discarding work or releasing a resume target.
  Use Kanmer's ownership-aware reconciliation and closeout. Archive evidence;
  no direct maintenance pushes for claims or temporary plans. Cleanup follows
  scoped authorization and the skill's Git-cleanup-before-release order.

### V1 remediation authority

The three-stream implementation was integrated through PR 674 on 7 September
2026. PLAT-075, CASE-047 and INTK-060 retain its source and review evidence;
their former branch-sharing exception has ended. Do not replay that workflow
or infer unfinished product work solely from an old ticket's stage.

The operator's subsequent remediation request authorizes implementation,
independent review, merge and deployment to complete v1. EPIC-014 records the
scope, remaining work, credentials exclusions and named host verifier. That
authorization supersedes the former open-unmerged release stop. Record and
verify each exact candidate and its deployment targets before acting. Test
email may be sent only to `digital@collisionengineers.co.uk`, using the
requested `pegasustest` customer. It does not authorize an intake-data wipe.

The intake-data wipe script requires a separately approved maintenance window
with the Worker stopped. It advances the persisted receive-time cutoff as
part of SQL deletion; it never replays mail from original onboarding merely
because occurrence rows were cleared. See the runbook's explicit wipe procedure.

One verification owner per host coordinates focused checks and reuses matching
CI evidence; no competing whole-repository builds or capacity/soak runs are
part of this remediation. Product behavior and runtime state remain owned by
the documents routed from [the documentation index](docs/index.md).
