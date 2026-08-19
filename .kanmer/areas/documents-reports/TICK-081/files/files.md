# Files — EXT-08 activation

| Path/module | Expected change | Risk |
| --- | --- | --- |
| `docs/capabilities.md` | Move EXT-08 and activated dependencies from Later to current approved schedule/status | Roadmap authority |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Specify readiness, generation, identity, failure, correction, acceptance | Behavioral completeness |
| `src/Pegasus.Core/Reports/**` | Port/use case/readiness/result identity | Policy duplication |
| `src/Pegasus.Infrastructure/**` | Renderer adapter/persistence/custody | Chromium and transaction failures |
| `src/Pegasus.Web/**` | Real accepted-assessment caller/status | Request interruption |
| `tests/**` | Unit/integration/architecture/visual/container evidence | Test cost |
| `docs/current-architecture.md`, `docs/operations.md` | Refresh after as-built/deployment | Evidence tier |

## Context files

| Path | Why |
| --- | --- |
| `SIMPLI-014 research` | Engine integration |
| `DOCS-001` | Real caller and report reference |
| `PLAT-007` | Azure integration/proof |
| `reference/rendererref1/**` | Approved assessment evidence |
| `docs/adr/0025-*.md` | Integration decision |
| `EPIC-004/context.md` | Binding direction |

## Out of scope

- Unsupported renderer catalogue families.
- Audit until template approval.
- Report sending/receipt.
