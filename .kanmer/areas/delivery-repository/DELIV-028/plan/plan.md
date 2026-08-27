# Plan — DELIV-028

1. Branch `task/docs-007-restore-design-readme` from `origin/main`
   (`b56fd024`, which equals `dev`).
2. `git checkout a4da02a5 -- docs/design/README.md`.
3. Drop the four references into deleted folders: the design-system preview
   logo row, the Claude Design bindings row, and the two comparison-raster
   link sentences. Drop the `docs/design/references/mockups/` row from
   `docs/current-architecture.md`.
4. `Test-DocumentationLinks.ps1`, `Test-MarkdownPlacement.ps1`, `git diff
   --check`; PR → dev; independent review; merge before #562 (which edits the
   same file) is remerged.

## Simplification pass

n/a — docs-only.
