# Pegasus repository instructions

Pegasus is Collision Engineers' case-management and reporting application.
Start with [the documentation index](docs/index.md) for the owner of the question
and [CONTEXT.md](CONTEXT.md) for reserved business terminology.

## Project principles

- Treat the project as unreleased development. Replace obsolete behaviour and
  update affected consumers; do not implement compatibility for features being
  removed, or legacy fallbacks.
- Core owns business policy and ports. Infrastructure implements those ports;
  Web and Worker compose both. Imported source, skills and models own no
  policy.
- Subtract before you add. Implement the requested scope with the simplest
  correct design and reuse the existing owner. A new FRD, glossary term,
  feature flag, skill, cache, edit-authority concept or plan names the
  existing one it replaces, or shows that none exists.
- Release as often as verifying a change needs, and keep each release cheap:
  the record in `docs/operations.md` is at most 15 lines, and anything still
  owed after a release becomes a GitHub issue.
- Decide once, in the repository. The operator's rulings are the decision
  record and live in [docs/rulings.md](docs/rulings.md); a behaviour rule goes
  into the FRD clause it changes, dated and citing its ruling. An ADR is
  written only for a choice that is expensive to reverse. Nothing cites a file
  the repository does not hold.
- Efficiency needs a number. Every page has a budget; a change is built only
  when it brings a page under budget or saves at least 100 ms at p95, shown
  before and after in the PR. Query-count tests assert a ceiling or a
  no-growth pair, never an exact total.
  [Engineering](docs/engineering.md#simplicity) holds the budgets.
- A Core use case is a class. It gets an interface only when a test fakes it
  or a second implementation exists; store and adapter ports keep theirs.
- A plan is one page, written against the head it is built on, when it is
  built. A plan older than a week is re-derived, not re-verified.

## Verification

If you are running subagents, do not have all the subagents running builds and tests simultaneously. Have them pass back to the lead, combine the work back together if needed, and run ONE single build process. We don't want to run 4 builds for 4 worktrees when we could just run one. Subagents run on Haiku, Sonnet or Opus, never on Fable (operator, 15 and 28 October 2026; R495).

If GitHub CI already runs a test, DO NOT RUN THE TEST LOCALLY unless SPECIFICALLY requested to do so by the user. DO NOT waste time running resource intensive local tests that could have been done on GitHub CIs. The same applies to a compile-check build before a push: CI builds the head.

CI on the exact head is the evidence: `.github/workflows/ci.yml` decides which
lanes a change set needs from the paths it touches, and nothing CI runs is
repeated here. `scripts/Get-CiStatus.ps1` prints a failing run's test names from
the job log.

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
- `.agents/skills/`: the Pegasus procedures (`pegasus-release`,
  `pegasus-wipe-intake-data`, `pegasus-corpus-holding`,
  `pegasus-ui-guardrails`, `razor-pages-ui-design`,
  `razor-pages-ui-implementation`, `razor-pages-ui-review`,
  `razor-html-mockup-creation`, `razor-html-mockup-conversion`) and the
  issue workflow skills (`combine-prs`, `investigate-issue`, `record-issue`,
  `resolve-issue`). `.claude/skills` is a symlink to this folder, so every
  skill is exposed to Claude Code; skills that are not about Pegasus live at
  user level, not here.
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
  `Blocked intake` is no longer a term. Only images become Unidentified, and
  refused image material is a closed Unidentified item; an e-mail with no
  Case is No Case Found. Use the glossary and owning FRD; never call generic
  sorting Triage.
- Never delete a Case or reuse its reference. Wrong-Principal correction and
  reasoned reopening follow FRD-01.
- `corpus/` is local, ignored and immutable: never upload, commit, rename or
  modify it. Use supplied domain evidence; generated evaluations go in artifacts.
  The one exception is `corpus/holding/`, which the
  [`pegasus-corpus-holding`](.agents/skills/pegasus-corpus-holding/SKILL.md)
  skill appends mailbox copies to; promoting a file out of holding is a manual
  operator step.
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
- The release containment sequence explicitly stops and verifies
  `pegasus-prod-classifier-252ow37gij` alongside Web and Worker before SQL
  migration begins, and restarts it after Web is healthy.
- The first App Service destructive cutover was Release 50
  ([record](docs/operations.md#release-50--14-september-2026-historical-app-service-cutover)):
  the old Container App source and active revisions were approval-bound and
  it was inactive, ingress-disabled and unserved before SQL; the new App
  Service was provisioned only after migration.
- Deploying a ZIP to a deliberately stopped Web App uses `--restart false`
  and `--track-status false`; activation is a separate release step.
- A closed feature or composition gate is not delivery. An inert UI preview may
  exist only where its accepted interface contract permits it.

## Working with the operator

- Never invent a business rule, a default or operator-facing copy. A sentence
  the application will show, or a rule the code will enforce, that no FRD,
  ruling or operator message states is a question, not a draft to build on.
- Every contradiction between documents, code, tests or rulings is a question
  to the operator, never a choice the agent makes.
- Questions go to the operator in rounds of at most four, each with a plain
  paragraph of explanation and the recommended option first. The answer is
  recorded in the operator's words as a row in [docs/rulings.md](docs/rulings.md).
- Agents do not merge pull requests, close issues, push to `dev` or `main`,
  force-push or rebase unless the operator says so for that case.
- The operator's decision of 28 October 2026 placing these rules here: R495.

## Documentation and work context

[The index](docs/index.md) owns documentation placement, formatting and routing.
Use task-relevant owners rather than reading every document or copying rules.
Current Markdown additions and renames use the index-routed canonical location;
CI checks that relative links resolve. The current operator task owns scope and authorization. Use PR review and CI
for delivery evidence, and name one verifier for heavy checks on this host.
Historical tickets and retired plans are provenance only.

Task work uses a worktree at `../pegasus-worktrees/<slug>` on branch
`task/<slug>`, created from `origin/dev` with `--no-track`. A release build
uses a detached worktree at `../pegasus-worktrees/release-<sha8>`.
`.agents/skills/resolve-issue/scripts/cleanup_worktree.py` removes a task
worktree once the PR head equals the worktree HEAD and the tree is clean.
