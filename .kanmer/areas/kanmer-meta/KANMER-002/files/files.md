# Files — KANMER-002

## Change surface

| Path | Change | Risk / mitigation |
|---|---|---|
| `design/**` → `docs/design/**` | Move the governed assets and design-system package; `docs/design.md` becomes `docs/design/README.md` | High path-churn risk; use `git mv`, exhaustive literal-path search, design-system build, renderer tests and documentation-link validation |
| `.design-sync/config.json`, `.design-sync/NOTES.md` | Retarget design-system and authority paths | External sync integration; validate JSON and build command |
| `.gitattributes`, `.gitignore` | Retarget moved design globs and generated-package ignores | Prevent line-ending/generated-output drift |
| `AGENTS.md`, `docs/index.md`, `scripts/Test-DocumentationLinks.ps1` | Retire `docs/temp-plans/` as a repository planning home and route task evidence to Kanmer | Process ownership remains in AGENTS.md; link validator must no longer exempt retired paths |
| `docs/temp-plans/**` | Delete after per-file Git/Kanmer coverage check and SIMPLI-015 preservation | Preserve durable decisions in board research; Git history remains historical record |
| `reference/workproviders-and-repairers/contacts/providers.xlsx` | Delete byte-identical unreferenced duplicate | Recheck hash and inbound refs immediately before deletion |
| Documentation, source comments, renderer project/Docker files | Retarget `docs/design.md` and `design/` paths | Exhaustive search plus focused builds/tests |
| Local ignored `artifacts/` exact obsolete subtrees | Remove only proven regenerated/completed planning output | Never touch active intake/evaluation/local-development/reference-data staging |

## Context files

| Path | Why read it |
|---|---|
| `AGENTS.md` | Owns repository process and restricts new Markdown placement |
| `docs/index.md` | Owns navigation and authority chain |
| `reference/README.md` | Establishes evidence retention and intentional reference/design duplication |
| `.design-sync/NOTES.md` | Records the current design-system build and sync contract |
| `workspaces/report-renderer/*.csproj`, `Dockerfile` | Consume governed design assets by path |
| KANMER-002 research and [[SIMPLI-015]] | Preserve plan dispositions and the renderer integration direction |

## Deliberately out of scope

- Changing product behaviour or business requirements.
- Retiring or rewriting `docs/operator-notes.md`.
- Deleting retained raw evidence from `reference/`.
- Deleting active or sensitive ignored artifacts.
- Implementing SIMPLI-015's renderer/extractor integration.
