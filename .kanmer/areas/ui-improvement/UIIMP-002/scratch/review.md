# Independent review — PR #556 — 2026-08-26

Reviewer did not implement this ticket.

## Changes observed

- `docs/design/README.md`: adds the Test UI evidence boundary, naming convention, and separation from Live UI.
- `docs/design/test-ui/index.html`: adds the only route inventory, client-side catalogue navigation, 52 routed-source classifications, and links to 60 prototypes.
- `docs/design/test-ui/pages/*.html`: adds 60 offline static HTML files for the 39 routes classified as visual, reusing the tracked Web stylesheet and marks.
- `scripts/Test-UiCatalogue.ps1`: validates routed-source inventory coverage, classification vocabulary, unique source/state and prototype links, local file references, prototype orphans, and absence from project/release inputs.

## Comments and disposition

1. **Blocking — filed as [[PR-063]].** The implementation does not satisfy the plan's page-specific fidelity requirement. Multiple files named `--default` render only an exceptional or empty Razor branch and omit the route's defining normal interaction. Concrete checks:
   - `administration-principal-create--default.html` contains only the no-Work-Provider warning; the current `Create.cshtml` normal branch contains the Work Provider, principal code, inspection mode, and Create principal form.
   - `administration-roles--default.html` contains the no-accounts branch; the current `Roles/Index.cshtml` normal branch contains the account table, role choices, reason, and Save roles action.
   Route/source counting therefore proves catalogue coverage, but not an actual replica of each page/state.
   **Disposition:** filed as [[PR-063]], which blocks [[UIIMP-002]].

2. **Blocking — included in [[PR-063]].** The reported verification is not fully reproducible: `git diff --check origin/dev...HEAD` exits non-zero and reports trailing blank lines in the index and many prototype files, while the checklist/report/PR state that `git diff --check` passed.
   **Disposition:** filed in [[PR-063]] and requires corrected files plus corrected/rerun ticket evidence.

3. **Blocking — included in [[PR-063]].** The post-implementation report groups all 60 prototype changes under `pages/*.html` rather than accounting for which rendered Razor branch each file represents. Given the observed default/exception mismatch, the canonical inventory alone is not honest evidence that the plan's fidelity pass completed.
   **Disposition:** filed in [[PR-063]]; the report/checklist must be reconciled after the complete Razor-to-prototype pass.

4. **Non-blocking — won't-do-because.** CI skips unit, browser, infrastructure, and SQL jobs based on the change classifier. The applicable `changes`, `documentation`, `local-development-scripts`, and `reference-data` jobs are green. This is appropriate for the isolated documentation/design catalogue, but CI green does not cure the fidelity defects above.

## Required review questions

- **Did the plan omit anything implied by the ticket?** No material scope omission. The plan explicitly requires every visual route's current rendered shell and semantic page structure, applicable named states, real classes/assets, local browser/accessibility checks, and route/isolation validation. Its verification model could have named a per-state Razor-branch mapping more explicitly, but the existing text already requires the missing fidelity.
- **Did implementation omit anything in the plan?** Yes. At least the Create principal and Staff roles default prototypes omit their defining normal rendered branches; the same class of mismatch requires a complete audit across all 39 visual routes. The branch also fails the claimed diff check.
- **Was the simplification pass honest with dispositions?** No. The plan says independent lenses restored defining forms, tables, controls, actions, and state branches and found no remaining issue. Direct comparison with current Razor disproves that disposition for the examples above. The recorded simplification result is therefore not reliable enough to accept.

## Checks run

- Read the full ticket body and all pipeline documents, including open questions, research scratch, plan, checklist, and post-implementation report.
- Read the full PR metadata/diff inventory and the applicable `docs/design/README.md`, FRD-12, current Razor pages/PageModels, shared layout, and `site.css`.
- `./scripts/Test-UiCatalogue.ps1`: passed — 52 routed sources, 60 prototypes, 0 broken local references.
- `git diff --check origin/dev...HEAD`: failed with trailing-blank-line errors across the catalogue.
- GitHub checks: applicable jobs green; unrelated test lanes skipped by classifier.
- Worktree clean and at PR commit `63ce6901e9979cf5922be2ce4b361310230e62ef`.

## Verdict

**Needs changes.** Do not merge PR #556 or move UIIMP-002 to Verifying. [[PR-063]] blocks the ticket until the fidelity/evidence defects are corrected and the PR is re-reviewed.
