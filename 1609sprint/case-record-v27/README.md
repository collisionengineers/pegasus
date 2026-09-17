# Case record v27 — sprint 1609 handover

Snapshot: 16 September 2026, `dev` at `45a011165`. Plan only — nothing here
is implemented, and nothing in the v27 round is approved. Sprint handover
files under `1609sprint/` sit outside the Markdown placement allow-list
(`scripts/Test-MarkdownPlacement.ps1`); the folder is committed directly on
`dev` by the operator, not through a PR.

## What this folder is

The v27 design round re-examined the operator's reference dashboard
(`design/planning-and-old-designs/v27_planning/pegasus_case_dashboard_2026-09-15.html`,
titled "v25 · 9 Sep 2026") against the live Case record, built a faithful
mockup of the live page, and layered every reference idea on it as a
reversible switch. This folder turns that round into sprint work: what each
feature is as product behaviour, how it integrates, what is unresolved, and
exactly which front-end and back-end pieces change.

Sources read for this folder, all on `dev`:

| Source | Path |
| --- | --- |
| Reference file, inventory, differences | `design/planning-and-old-designs/v27_planning/pegasus_case_dashboard_2026-09-15{.html,-features.md,-differences.md}` |
| Mockup, self-check, 94 shots | `…/v27_planning/current/pegasus_case_record_v27.html`, `v27-selfcheck.html`, `v27-shots/` |
| Notes (§ 11–15 carry the proposals and sign-off items), discussion log | `…/v27_planning/current/v27-notes.md`, `discussion-log.md` |
| Build inputs (markup, script, proposals, wording) | `…/v27_planning/current/v27-build/` |
| Page folders | `…/v27_planning/pages/cases/case-record/**` |
| Live owners | `src/Pegasus.Core/Assessment/*`, `src/Pegasus.Core/Reports/*`, `src/Pegasus.Infrastructure/Reports/AssessmentReportLayout.cs`, `src/Pegasus.Infrastructure/Persistence/AssessmentFieldWriter.cs`, `src/Pegasus.Web/Pages/Cases/**`, `src/Pegasus.Web/wwwroot/js/case-workspace.js` |
| Governing documents | FRD-01, FRD-06, FRD-11, FRD-12, ADR-0050, `docs/design/README.md`, `CONTEXT.md` |

## Files

| File | Contents |
| --- | --- |
| [INTEGRATION-PLAN.md](INTEGRATION-PLAN.md) | The full integration plan: the decision gate, eight work packages in dependency order, branch and PR shape, verification per package, documentation impact, release and close-out |
| [FEATURES.md](FEATURES.md) | The full feature description: every v27 feature as product behaviour, section by section, with its origin, the rule it touches and its sign-off letter; then the decision-first features and what is already live |
| [OPEN-ISSUES.md](OPEN-ISSUES.md) | Every open or unresolved item: the lettered sign-off list as raised, the new issues this re-examination found (J1–J24), the decision-first list, cross-sprint dependencies and defects noticed on the way |
| [BACKEND.md](BACKEND.md) | Exact back-end changes per feature: Core policy and ports, Infrastructure persistence and renderer, migrations, tests |
| [FRONTEND.md](FRONTEND.md) | Exact front-end changes per feature: Razor partials, page-model handlers, script, styles, labels, tests and the conformance evidence |

## Status

- Stage 1 (mockup) is complete on the mockup's own evidence: self-check
  `RESULT {"fail":[],"okCount":680}`, 94 shots, coverage audit 494/18
  accounted for. It is not application evidence.
- Stage 2 has not started. It starts only when the sign-off letters in
  OPEN-ISSUES § 1 are settled; the integration plan's gate 0 is that list.
- Nothing is committed from this session except by the operator.
