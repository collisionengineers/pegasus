## Summary

Ported `Pages/Administration/Configuration.cshtml(.cs)` onto the PLAT-029
admin-layout design system. Kept the two real staff-review settings
(`RequireStaffInstructionReviewBeforeEngineerAssignment`,
`RequireStaffImageReviewBeforeEngineerAssignment`) wired through the
unchanged `GetWorkflowConfiguration` / `UpdateWorkflowConfiguration` Core
port; no business rule changed. Removed the pre-redesign explanatory notice
copy. The contract's other two control groups ("Instruction completeness",
"Due work chase interval") have no backing Core setting — building them
needs a new Core port + migration, out of scope here — so they were
deliberately omitted (never rendered as inert placeholders) and deferred to
[[PLAT-062]].

## What changed

- `src/Pegasus.Web/Pages/Administration/Configuration.cshtml` — re-skinned:
  `.admin-layout` + `_AdminNav` (`ViewData["AdminArea"] = "configuration"`),
  panel/section-label house style reused from `Pages/Operations/Index.cshtml`
  and `Pages/Administration/Index.cshtml`; explanatory `<aside class="notice">`
  removed; the "Review" heading groups the two real checkboxes; required
  `Reason` field and `Save configuration` button kept.
- `src/Pegasus.Web/Pages/Administration/Configuration.cshtml.cs` — unchanged
  (the Core port, optimistic-concurrency and operation-key handling all
  reused as-is).
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — appended a new
  `WorkflowConfiguration` nested static class at the end of `OperatorLabels`,
  before the sole private helper; no existing member reordered.
- `tests/Pegasus.IntegrationTests/WorkflowConfigurationWebTests.cs` (new) —
  three tests: rendered admin-layout/current-area markup and real
  form/handler wiring, a real POST that saves both settings through the
  unchanged handler, and a non-administrator 403 denial.
- `docs/design/test-ui/catalogue.json` — not changed; the existing
  `administration-configuration--default` entry (route/state) is still
  accurate.

## Reused, not rebuilt

`_AdminNav.cshtml` (read-only), `_PageHeader` partial, existing `site.css`
classes (`admin-layout`, `panel`, `panel-head`, `panel-body`, `stack`,
`cluster`, `field`, `req`, `field-error`, `validation-summary`, `notice`/
`notice--success`, `btn`/`btn--primary`, `panel-title-meta`), existing
Lucide icons (`icon-check`, `icon-save`), `OperatorLabels.Admin.Configuration`,
the unchanged `ConfigurationModel`/Core port, and the existing
non-administrator-denial test convention
(`useIntegrationTestAuthentication: true` + `X-Test-Roles`).

## Build / test evidence (independently re-run by the orchestrator)

- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0, 0 warnings,
  0 errors.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~WorkflowConfigurationWebTests"` — exit 0, 3 passed, 0
  failed, 0 skipped.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~AdministrationSearchAccountWebTests"` (pre-existing
  regression check covering this route) — exit 0, 6 passed, 0 failed, 0
  skipped.
- `git diff origin/dev...HEAD -- tests/` reviewed: no assertion weakened,
  deleted, or inverted (the test file is entirely new).
- `git diff --stat origin/dev...HEAD`: exactly the three files this ticket
  owns; nothing outside scope.

## Simplification pass and disposition record

See plan.md, dated 2026-08-29 headings "Simplification pass" and
"Disposition record". Backend gap deferred to [[PLAT-062]] (D19 last resort:
needs a new Core port + migration + an operator decision). The driven
agent's "`_AdminNav.cshtml` omits Service health/Action Logs/Reports"
observation was reviewed and rejected as a finding — that partial's own doc
comment already documents this as intentional wave-4 gating.

## Commits

- `3fc8e45c3adf2cc0f7346680e45c8ea735af3de3` — feat(administration): port
  workflow configuration layout (PLAT-025)
- `4ab6acbe77beb8d039e5c84d9e70e916aaac56b3` — test(administration): cover
  workflow configuration page (PLAT-025)

Pushed to `task/plat-025-workflow-configuration`; local and remote HEADs
match. PR opened against `dev`, not merged.
