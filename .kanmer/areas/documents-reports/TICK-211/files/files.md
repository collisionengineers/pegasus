# Files — analyzer strictness

## Change files

| Path | Expected change | Risk |
| --- | --- | --- |
| `workspaces/report-renderer/Directory.Build.props` | Retired with workspace | Hidden warning debt appears |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` and migrated source | Inherit root policy; add dependencies/resources only | Compilation/analyzer fixes |
| `Directory.Build.props` | No renderer-specific relaxation | Repository-wide blast radius if changed |
| Migrated `*.cs` | Fix warnings surfaced by latest-recommended + warnings-as-errors | Behavior-preserving cleanup |
| `.github/workflows/ci.yml` | Existing Release build proves policy | Build time |

## Context files

| Path | Why read it |
| --- | --- |
| `Directory.Build.props` | Canonical production build policy |
| `workspaces/report-renderer/Directory.Build.props` | Imported exception to eliminate |
| `docs/engineering.md` | Existing convention and simplicity rules |
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` | Integration, not a standalone product |

## Out of scope

- Weakening root analyzers.
- Preserving standalone package/product metadata.
- Speculative suppressions.
