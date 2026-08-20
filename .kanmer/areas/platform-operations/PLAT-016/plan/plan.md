# Plan — PLAT-016

Docs-only, two files, one PR (branch task/design-no-explanations from origin/dev a1775841).

1. `docs/design/README.md`: add a "## No explanatory copy" section near the existing copy rules (reuses the banned-terms section's imperative style): field = label only (visual required marker, no "Required."/"Optional."/hint sentences); no how-it-works, worked-example, or self-describing prose anywhere; one consequence sentence max, only on destructive actions. Add "pages render only populated, relevant sections" (empty-state/edit-only panels absent read-only; long pages are defects) and "filters are dropdowns, not pill tabs; tables newest-first with column-header sort links" to the existing layout/navigation rules.
2. `AGENTS.md` Simplicity rails: one bullet binding UI changes to the design README's no-explanatory-copy rule; mirror into `CLAUDE.md` (same block, files differ — check diff first and keep them consistent).

Verify: Test-DocumentationLinks.ps1; documentation CI lane only.

Simplification pass: n/a — docs-only.
