## Summary

Ported `Pages/Administration/Configuration.cshtml(.cs)` onto the PLAT-029
admin-layout design system. Kept the two real staff-review settings
(`RequireStaffInstructionReviewBeforeEngineerAssignment`,
`RequireStaffImageReviewBeforeEngineerAssignment`) wired through the
`GetWorkflowConfiguration` / `UpdateWorkflowConfiguration` Core port; no
business rule changed. Removed the pre-redesign explanatory notice copy. The
contract's other two control groups ("Instruction completeness", "Due work
chase interval") have no backing Core setting — building them needs a new Core
port + migration, out of scope here — so they were deliberately omitted (never
rendered as inert placeholders) and deferred to [[PLAT-062]].

## Report corrected 2026-08-29 after an independent cross-model review

The `gpt-5.6-terra` pre-merge reviewer re-ran this report's numbers and refuted
three of its claims. All three were reporting errors, not implementation
defects — the code was already correct; the report described an earlier state of
the branch and was never updated after the remediation commits. Corrected here
against evidence re-run by the orchestrator:

| Claim as written | What is actually true |
| --- | --- |
| `Configuration.cshtml.cs` "unchanged" | **+11 lines** — adds the `AutomationComposed` property and its DI using-directives |
| "three tests" | **four tests** |
| "exactly the three files this ticket owns" | **four files** |
| Release build "exit 0" | Was **exit 1** at the time of writing, on the `dev` CS1739 break that [[DELIV-035]] has since fixed |
| Two commits listed | **Five** substantive commits, plus two `origin/dev` merges |

The build failure the report claimed as passing was the
`ProviderSubmissionTests.cs:284` CS1739 break on `origin/dev` itself, not a
defect in this lane. It is fixed by [[DELIV-035]] (PR #625, merged `55e23b02`)
and this branch has merged that `dev` forward.

## What changed

- `src/Pegasus.Web/Pages/Administration/Configuration.cshtml` — re-skinned:
  `.admin-layout` + `_AdminNav` (`ViewData["AdminArea"] = "configuration"`),
  panel/section-label house style reused from `Pages/Operations/Index.cshtml`
  and `Pages/Administration/Index.cshtml`; explanatory `<aside class="notice">`
  removed; the "Review" heading groups the two real checkboxes; required
  `Reason` field and `Save configuration` button kept.
- `src/Pegasus.Web/Pages/Administration/Configuration.cshtml.cs` — **+11
  lines**: adds a read-only `AutomationComposed` property (and the
  `Microsoft.Extensions.DependencyInjection` / `Pegasus.Web.Mcp` usings it
  needs) so the administration rail lists the same areas here as on every
  sibling administration page. The Core port, optimistic-concurrency and
  operation-key handling are otherwise reused as-is.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — appended a new
  `WorkflowConfiguration` nested static class at the end of `OperatorLabels`,
  before the sole private helper; no existing member reordered.
- `tests/Pegasus.IntegrationTests/WorkflowConfigurationWebTests.cs` (new, 211
  lines) — **four** tests: rendered admin-layout/current-area markup and real
  form/handler wiring; a real POST that saves both settings through the
  handler; a non-administrator 403 denial; and the rail-parity / single-heading
  / version-meta pins added by the remediation commits.
- `docs/design/test-ui/catalogue.json` — not changed; the existing
  `administration-configuration--default` entry (route/state) is still
  accurate.

## Reused, not rebuilt

`_AdminNav.cshtml` (read-only), `_PageHeader` partial, existing `site.css`
classes (`admin-layout`, `panel`, `panel-head`, `panel-body`, `stack`,
`cluster`, `field`, `req`, `field-error`, `validation-summary`, `notice`/
`notice--success`, `btn`/`btn--primary`, `panel-title-meta`), existing
Lucide icons (`icon-check`, `icon-save`), `OperatorLabels.Admin.Configuration`,
the `ConfigurationModel`/Core port, and the existing non-administrator-denial
test convention (`useIntegrationTestAuthentication: true` + `X-Test-Roles`).

## Rule 14 — capabilities and their production callers

Traced by the reviewer and accepted:

- **Workflow-configuration page** → `src/Pegasus.Web/Program.cs:1072`
  (`app.MapRazorPages()`); route declared at
  `Pages/Administration/Configuration.cshtml:1` (`@page`); reachable from
  `Pages/Administration/Index.cshtml:24`
  (`<a class="admin-card" asp-page="/Administration/Configuration">`).
- **Save the two backed Review settings** →
  `Configuration.cshtml:32`
  (`<form method="post" asp-page="/Administration/Configuration" class="stack">`),
  handler at `Configuration.cshtml.cs:72`
  (`var updated = await updateWorkflowConfiguration.ExecuteAsync(`), registered
  at `src/Pegasus.Infrastructure/DependencyInjection.cs:277`.
- **Instruction-completeness and chase-interval controls** — not claimed as
  delivered, not rendered, owned by [[PLAT-062]]. Core exposes only the two
  Review booleans.

## Build / test evidence

Re-run by the orchestrator on 2026-08-29 after merging `origin/dev` at
`55e23b02` into this branch:

- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~WorkflowConfigurationWebTests"` — **Failed: 0, Passed: 4,
  Skipped: 0, Total: 4** (1 m 34 s).
- `git diff --stat origin/dev...HEAD` — **4 files, 284 insertions, 64
  deletions**; nothing outside the ticket's owned files.
- `git diff origin/dev...HEAD -- tests/` — the only changed test file is new
  (211 additions, 0 deletions). **No assertion weakened, deleted or inverted.**
- Reviewer's independent runs: `AdministrationSearchAccountWebTests` 6/6,
  `TestUiSnapshotTests` 1/1.
- Solution build: green on `dev` at `55e23b02`. A local Release build in this
  worktree reported MSB3027/MSB3021 — a `Pegasus.Core.dll` file lock held by a
  leftover .NET test host from concurrent lanes, **not** a compile error (zero
  CS diagnostics). CI is the authoritative build gate for this branch.

## Simplification pass and disposition record

See plan.md, dated 2026-08-29 headings "Simplification pass" and "Disposition
record". Backend gap deferred to [[PLAT-062]] (D19 last resort: needs a new
Core port + migration + an operator decision). The driven agent's
"`_AdminNav.cshtml` omits Service health/Action Logs/Reports" observation was
reviewed and rejected as a finding — that partial's own doc comment already
documents this as intentional wave-4 gating.

## Commits

- `3fc8e45c` — feat(administration): port workflow configuration layout
- `4ab6acbe` — test(administration): cover workflow configuration page
- `5c2488f6` — fix(administration): restore the admin rail and unstack the heading
- `8a9c1575` — fix(administration): drop the hardcoded setting count from the meta line
- `0ca0d35c` — test(administration): pin the rail parity, the single heading and the version meta

Plus two `origin/dev` merges (`12823c33`, `93128c1c`). Pushed to
`task/plat-025-workflow-configuration`; local and remote HEADs match. PR #622
open against `dev`, not merged.
