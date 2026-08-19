# Files — package lock disposition

| Path | Expected change | Risk |
| --- | --- | --- |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | Add minimal renderer engine dependencies | Dependency/native asset size |
| `src/Pegasus.Infrastructure/packages.lock.json` | Regenerate canonical locked graph | Transitive drift |
| Relevant test `*.csproj` and `packages.lock.json` | Add migrated tests/dependencies only | Duplicate Playwright versions |
| `workspaces/report-renderer/**/packages.lock.json` | No files added; workspace retires | None |
| `.github/actions/dotnet-build/action.yml` | Usually unchanged; already keys production lock files | Cache correctness |

## Context files

| Path | Why |
| --- | --- |
| `docs/runbook.md` | Locked restore/build authority |
| `.github/actions/dotnet-build/action.yml` | Lock-file cache inputs |
| `workspaces/report-renderer/docs/adr/0014-uplift-to-net10.md` | Historical six-lock deferral |
| `EPIC-004/context.md` | Integrated, not standalone direction |

## Out of scope

- Creating lock files for retired renderer hosts.
- Central package management without a second concrete need.
