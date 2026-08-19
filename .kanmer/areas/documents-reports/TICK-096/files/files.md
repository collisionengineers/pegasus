# Files — deterministic renderer foundation

| Path/module | Expected change | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Reports/**` | Port, request/result identities, versioned typed snapshot | Business/adapter boundary |
| `src/Pegasus.Infrastructure/**Reports**` | Migrate renderer engine/composer/Playwright adapter/resources | Native/process complexity |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` + lock | Packages/embedded assets | Size/advisories |
| `docs/design/assets/report-renderer/**`, brand assets | Canonical template/style sources | Logical resource names |
| `reference/rendererref1/**` | Immutable evidence/fixtures only | Must not become runtime input |
| Core/Infrastructure/integration/visual tests | Validation, calculations boundary, parity, hash/provenance | Browser variability |
| Workspace files | Retire after parity | Missed behavior |

## Context files

| Path | Why |
| --- | --- |
| `workspaces/report-renderer/src/CollisionRenderer.Core/**` | Reusable mechanics |
| `workspaces/report-renderer/docs/ARCHITECTURE.md` | Pipeline/runtime constraints |
| `reference/rendererref1/**` | Approved initial behavior |
| `SIMPLI-014 research` | Integration boundary |
| `TICK-092`–`TICK-094 research` | Accepted input/policy owners |

## Out of scope

- Standalone hosts and inactive templates.
- Report approval/sending.
- Audit until approved template.
