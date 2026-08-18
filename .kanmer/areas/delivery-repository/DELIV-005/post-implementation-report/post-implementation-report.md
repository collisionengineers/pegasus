# Post-implementation report — DELIV-005

## Summary

Removes the CI step that fails a pull request merely because a new Markdown
file is outside a fixed folder allow-list. Documentation placement is no longer
a release gate; the documentation job retains its independent regression and
link-integrity checks.

## Changes

| File | Change | Why |
|---|---|---|
| `.github/workflows/ci.yml` | Removed the `Markdown placement` step and its PR/push SHA configuration. | Implements the operator’s direction to remove the non-correctness CI roadblock while preserving the rest of the documentation lane. |

## Governing docs

No PRD, FRD, or ADR applies. This is a focused repository workflow-maintenance
change governed by `AGENTS.md`; it edits the existing CI convention in place.

## Risks / follow-ups

The repository no longer enforces a placement allow-list for newly added
Markdown at pull-request time by design. The underlying script and regression
tests remain available, but no workflow invokes the gate.

## Verification hand-off

On merged `main`, run:

```powershell
./scripts/Test-TestMarkdownPlacement.ps1
./scripts/Test-DocumentationLinks.ps1
rg -n -C 2 "Markdown placement|Documentation links" .github/workflows/ci.yml
git diff --check HEAD^ HEAD
```

Expected results: both retained scripts pass; the workflow lists
`Markdown placement regression tests` and `Documentation links`, but no
`Markdown placement` gate; and the change is whitespace-clean.
