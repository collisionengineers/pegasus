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

Pegasus is Collision Engineers' case-management and reporting application.
Start with [the documentation index](docs/index.md) for the owner of the question
and [CONTEXT.md](CONTEXT.md) for reserved business terminology.

## Project principles

- Treat the project as unreleased development unless the task establishes a
  real released consumer or a required persistent-data contract. Replace obsolete
  behavior and update affected consumers; do not invent compatibility machinery.
- Implement the requested scope with the simplest correct design. Add complexity
  only for a current requirement. Reuse the existing owner rather than creating
  another policy, vocabulary or workflow implementation.
- Core owns business policy and ports. Infrastructure implements those ports;
  Web and Worker compose both. Imported source, skills and models own no policy.
- Resolve contradictory requirements against current operator instructions and
  the governing specification; source history alone is not authority.

## Verification

Select verification from the effects of the change. Prose-only edits require
relevant documentation checks and semantic review; do not run dotnet restore,
build or test solely because Markdown changed. Application code, dependencies,
build inputs and embedded executable assets require affected build/test evidence.
Run full solution checks when the affected scope, explicit acceptance criteria
or release procedure requires them. Reuse qualifying exact-head CI evidence and
coordinate heavy verification through the active Kanmer execution context.

Read [engineering verification policy](docs/engineering.md#verification-policy)
and the [verification procedure](docs/runbook.md).
For routed Razor changes, follow the existing Razor skills and their scoped
snapshot procedure. Report failed, omitted and inconclusive checks honestly.
Obsolete documentation-parser contracts do not justify retaining incorrect docs.

## Repository map

- `src/Pegasus.Core`: business policy and ports.
- `src/Pegasus.Infrastructure`: adapters and persistence.
- `src/Pegasus.Web`, `src/Pegasus.Worker`: application composition and callers.
- `tests/`: Core, architecture and integration evidence.
- `infra/`: deployment definitions; `scripts/`: existing operational tooling.
- `docs/current-architecture.md`: source structure; `docs/operations.md`: dated
  deployed observations. A release updates operations; architecture changes only
  when source structure changes.
- `.agents/skills/`: Pegasus procedures. Installed Kanmer owns generic workflow;
  do not recreate removed local Kanmer copies or fork plugin instructions here.

## Non-obvious constraints

- `Audit`, `Triage`, `Unidentified`, `Image Intake` and `Blocked intake` are
  distinct. Use the glossary and owning FRD; never call generic sorting Triage.
- Never delete a Case or reuse its reference. Wrong-Principal correction and
  reasoned reopening follow FRD-01.
- `corpus/` is local, ignored and immutable: never upload, commit, rename or
  modify it. Use supplied domain evidence; generated evaluations go in artifacts.
- Read-only cloud inventory is permitted. External writes require authorization
  covering the actual operation and targets; tool availability is not a grant.
- Local alpha work does not mutate Outlook or Box except an explicitly approved
  test mailbox or disposable Box subtree.
- Use PowerShell 7 on Windows or Linux, one platform per evidence run. Paths and
  commands are repository-relative. Follow the existing release skill for the
  authorized workstation and platform-matching artifacts.
- A closed feature or composition gate is not delivery. An inert UI preview may
  exist only where its accepted interface contract permits it.

## Documentation and work context

[The index](docs/index.md) owns documentation placement, formatting and routing.
Use task-relevant owners rather than reading every document or copying rules.
Kanmer owns stages, claims, gates, review, proof and current work allocation.
Active task grants and the host verifier belong to the owning execution context;
their expiry and exact targets are not permanent repository permissions.
