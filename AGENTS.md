## Codex reusable subagents

Project-local roles are defined in [`.codex/agents`](.codex/agents):
`pegasus-scout`, `pegasus-investigator`, `pegasus-implementer`,
`pegasus-reviewer`, and `pegasus-verifier`. Their configuration follows the
[OpenAI subagent configuration documentation](https://learn.chatgpt.com/docs/agent-configuration/subagents)
and [configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference),
read 2026-09-08.

- Delegate only independent work that is useful to the active task, normally to
  2–4 children and never beyond the configured eight-child ceiling. Every
  assignment names either its ticket and worktree, or an explicit direct-work
  designation and source root, plus input revision, allowed files, expected
  output, and stop condition. The primary owns assignment, file-overlap
  resolution, approvals, integration, and release operations.
- Children do not recursively delegate, change unrelated files, or autonomously
  start tests, builds, verification scripts, or packaging.
  Scout, investigator, and reviewer never perform external writes; other children
  need an explicit bounded workflow that authorizes the action and target. Role
  profiles do not grant permissions or override parent runtime policy.
- All host test/build work, including focused commands, verification scripts,
  and packaging, is serialized behind one explicit current
  host-slot owner. Each such assignment names the canonical
  `<ticket>/scratch/execution.md` host-slot record. Before running, the owner
  reads it and refuses a missing, stale, ambiguous, wrong-host, wrong-input, or
  non-owner slot; it also checks other active execution contexts and host
  processes. All host sessions share that record, with no second active record;
  process absence or a profile never grants a slot. The current owner records
  explicit idle before transfer, then the primary rereads and records the next
  owner. Static reads and Git diff inspection may overlap.
- The verifier runs commands sequentially against frozen inputs, retains failed
  results, and makes no product fixes. The primary may take the slot for release
  packaging only after an explicit idle handoff. Other host sessions coordinate
  and never kill foreign processes.

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
assign one owner for heavy verification on the host.

Read [engineering verification policy](docs/engineering.md#verification-policy)
and the [verification procedure](docs/runbook.md).
For routed Razor changes, follow the existing Razor skills. Report failed,
omitted and inconclusive checks honestly.
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
- `.agents/skills/`: Pegasus operational and UI procedures.

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
  commands are repository-relative. Follow the existing [release skill](.agents/skills/pegasus-release/SKILL.md)
  and [migration recipe](.agents/skills/pegasus-release/references/database-migration.md)
  for the authorized workstation and platform-matching artifacts.
- Plan destructive migrations explicitly: identify the affected capability and
  forward-only recovery boundary, then require approved old-Worker `Stopped` and
  exact old-Web inactive/zero-replica read-back before SQL. After actual release,
  approve a concrete window outside typical usage. The release is incomplete
  until approved new Web/Worker bytes are explicitly active, healthy and smoked;
  never revive old bytes after destructive SQL begins.
- A closed feature or composition gate is not delivery. An inert UI preview may
  exist only where its accepted interface contract permits it.

## Documentation and work context

[The index](docs/index.md) owns documentation placement, formatting and routing.
Use task-relevant owners rather than reading every document or copying rules.
Current Markdown additions and renames use the index-routed canonical location
and must pass the base..head `scripts/Test-MarkdownPlacement.ps1` placement gate.
The current operator task owns scope and authorization. Use PR review and CI
for delivery evidence, and name one verifier for heavy checks on this host.
Kanmer is disabled; do not require its board, claims or gates.
