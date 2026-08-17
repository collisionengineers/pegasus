# Files — TICK-195

## Where the change lands

| Path | Why |
|---|---|
| `scripts/Test-MarkdownPlacement.ps1` (new) | Own the diff-aware policy check, explicit base/head input, actionable diagnostics, and non-zero failure for unauthorized new Markdown paths. A new script avoids collision with KANMER-002's claimed link-checker edit. |
| `.github/workflows/ci.yml` | Make both comparison revisions available and invoke placement validation from the documentation job that already runs on every change set. |
| A focused non-UI PowerShell test/fixture under the repository's established script-test convention, chosen during planning | Prove allowed, forbidden, rename/copy, multiple-error, and invalid-comparison behavior without involving application or browser tests. If no durable convention exists after KANMER-002 lands, keep deterministic fixture verification in the ticket's verification commands rather than inventing a new top-level test system. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `AGENTS.md` | Canonical repository rule for new Markdown placement and task ownership. Re-read after KANMER-002; do not edit its claimed governance changes in this ticket. |
| `docs/index.md` | Navigation/authority interpretation, including how canonical docs, design assets, references, and workspace-local docs are classified. Re-read after KANMER-002. |
| `scripts/Test-DocumentationLinks.ps1` | Existing link checker, exclusions, Windows path behavior, and a boundary not to duplicate or edit while KANMER-002 owns it. |
| `.github/workflows/ci.yml` | Always-running documentation lane, current shallow checkout, Windows runner, and the existing full-history diff precedent in the changes job. |
| `workspaces/AGENTS.md` and `workspaces/README.md` | Existing contract for independently buildable workspace documentation; use it to define any workspace exception rather than treating every arbitrary subtree as allowed. |
| `EPIC-001/context.md` | Prohibits edits to Web presentation, UI tests, `design/**`, and `.stitch/**`, and requires a fresh overlap check before implementation. |
| `KANMER-002` research/files documents | Establish the active claim and pending retirement of `docs/temp-plans/`, so the validator must target the landed policy rather than current text. |

## Ripple effects

- Pull requests adding or relocating Markdown will gain an always-running repository-policy gate with path-specific failures.
- The documentation job needs sufficient Git history, modestly increasing checkout transfer while avoiding application build lanes.
- KANMER-002 must land or publish its final placement decision first; TICK-200 should optimize CI only after this stable lane exists.
- Existing Markdown is grandfathered unless moved/copied into a new location; this avoids turning historical cleanup into unrelated blocking work.

## Out of scope

- `src/Pegasus.Web/**`, all UI-focused browser/snapshot tests, `design/**`, and `.stitch/**`.
- Editing `AGENTS.md`, `docs/index.md`, `docs/temp-plans/**`, or `scripts/Test-DocumentationLinks.ps1` while KANMER-002 owns those changes.
- Reorganizing existing Markdown, checking Markdown style/content, or repairing broken links.
- Azure, mailbox, Box, deployment, credential, or other external writes.
