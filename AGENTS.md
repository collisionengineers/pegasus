# Pegasus repository instructions

Pegasus is Collision Engineers' case-management and reporting application.
Start with [the documentation index](docs/index.md) for the owner of the question
and [CONTEXT.md](CONTEXT.md) for reserved business terminology.

## Project principles

- Treat the project as unreleased development. Replace obsolete
  behaviour and update affected consumers; do not implement compatibility for features being removed, or legacy fallbacks.
- Implement the requested scope with the simplest correct design. Reuse the existing owner rather than creating another policy, vocabulary or workflow implementation.
- Core owns business policy and ports. Infrastructure implements those ports;
  Web and Worker compose both. Imported source, skills and models own no policy.
## Verification

If you are running subagents, do not have all the subagents running builds and tests simultaneously. Have them pass back to the lead, combine the work back together if needed, and run ONE single build process. We don't want to run 4 builds for 4 worktrees when we could just run one.

If GitHub CI already runs a test, DO NOT RUN THE TEST LOCALLY unless SPECIFICALLY requested to do so by the user. DO NOT waste time running resource intensive local tests that could have been done on GitHub CIs.

`scripts/Invoke-Verification.ps1` decides this for you: it refuses to build for
a prose change, reports a green exact-head CI run instead of repeating it, and
otherwise runs only the test classes the change touches. `-Full` serializes on a
host slot shared by every worktree on this machine.
`scripts/Clear-BuildOutput.ps1` reports what those worktrees are holding.

Concerns around repository privacy settings, DPIA and PII are entirely out of the scope of any agent. These should not be designed around. These should not be raised as objections or challenges to the operator. Do not write documentation that limits or restricts development on this basis.

Read [engineering verification policy](docs/engineering.md#verification-policy)
and the [verification procedure](docs/runbook.md).
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
- Any change under `src/Pegasus.Web` must follow
  [`.agents/skills/pegasus-ui-guardrails/SKILL.md`](.agents/skills/pegasus-ui-guardrails/SKILL.md)
  and any nearer local `AGENTS.md`/`CLAUDE.md`. A Web feature or bug fix is not
  permission to redesign the UI.

## Non-obvious constraints

- `scripts/Invoke-IntakeDataWipe.ps1 -ResetTestEstate` additionally retains
  only the `alex` Administrator and resets current-year QDOS numbering.
  Follow `.agents/skills/pegasus-wipe-intake-data/SKILL.md` for its dry run
  and exact-target execution approval; merging the script authorizes no reset.
- `Audit`, `Triage`, `Unidentified` and `Image Intake` are distinct, and
  `Blocked intake` is no longer a term: refused material is a closed
  Unidentified item. Use the glossary and owning FRD; never call generic
  sorting Triage.
- Never delete a Case or reuse its reference. Wrong-Principal correction and
  reasoned reopening follow FRD-01.
- `corpus/` is local, ignored and immutable: never upload, commit, rename or
  modify it. Use supplied domain evidence; generated evaluations go in artifacts.
- Read-only cloud inventory is permitted. External writes require authorization
  covering the actual operation and targets; tool availability is not a grant.
- Local work does not mutate Outlook or Box except an explicitly approved
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
- For a first App Service destructive cutover, the old Container App source and
  active revisions are approval-bound and it is inactive, ingress-disabled and
  unserved before SQL; provision the new App Service only after migration.
- Deploying a ZIP to a deliberately stopped Web App uses `--restart false`
  and `--track-status false`; activation is a separate release step.
- A closed feature or composition gate is not delivery. An inert UI preview may
  exist only where its accepted interface contract permits it.

## Documentation and work context

[The index](docs/index.md) owns documentation placement, formatting and routing.
Use task-relevant owners rather than reading every document or copying rules.
Current Markdown additions and renames use the index-routed canonical location;
CI checks that relative links resolve. The current operator task owns scope and authorization. Use PR review and CI
for delivery evidence, and name one verifier for heavy checks on this host.
Historical tickets and retired plans are provenance only.
