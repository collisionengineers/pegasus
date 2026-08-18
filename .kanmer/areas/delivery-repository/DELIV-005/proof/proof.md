# Proof — DELIV-005 (verified on merged `main` `f1e116c6`, 2026-08-18)

- `pwsh ./scripts/Test-TestMarkdownPlacement.ps1` → `Markdown placement regression tests passed.`
- `pwsh ./scripts/Test-DocumentationLinks.ps1` → `All relative Markdown links resolve (222 files checked).`
- `grep -n "Markdown placement|Documentation links" .github/workflows/ci.yml` → line 70 `Markdown placement regression tests`, line 73 `Documentation links`; no `Markdown placement` gate step.
- `git diff --check 015f2e21^ 015f2e21` → clean.
- Effect observed: PR #400's `documentation` job passed on `f1e116c6` (it had failed on `dev` before this change solely on `src/Pegasus.Web/wwwroot/images/marks/README.md`); main-push run 32133221206 `documentation` success.

PR #401 merged 2026-08-18T09:41:45Z; promoted with release 9.
