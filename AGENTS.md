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
