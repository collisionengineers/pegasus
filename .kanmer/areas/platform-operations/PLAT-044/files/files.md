# Files — PLAT-044

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Assessment/*`, `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` | Define the narrow Assessment workspace query contract; remove Review-stage prerequisites from readiness and remove the GET-time report projection path. |
| `src/Pegasus.Core/Documents/DocumentContracts.cs` | Carry the persisted case-root remote id in the existing managed-content address. |
| `src/Pegasus.Infrastructure/Persistence/*Assessment*`, related existing persistence mappers | Load the Assessment workspace in at most six relational commands and batch report-photo reads without duplicating mapping rules. |
| `src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs` | Use the durable root id directly and retain existing ancestry/integrity checks. |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` and its DI registration | Replace the serial broad GET composition with the workspace query and pure readiness evaluation. |
| `docs/operator-notes.md`, `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Record the operator decision and remove the now-superseded report-readiness behaviour. |
| Existing Core/Web/Box integration tests | Prove readiness scope, zero content reads, bounded SQL commands, batch image retrieval, and durable-root fail-closed behaviour. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-01-case-identity-and-lifecycle.md` | Review/Engineer eligibility is gated by instruction and image completeness; downstream code must not create a second interpretation. |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Box custody is fail-closed and the immutable Case/PO folder remains the accepted storage boundary. |
| `PLAT-041` ticket documents | The batch content-read contract, exact Box call arithmetic, memory boundary, and intentionally deferred report loop. |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs` | Single-document and size-bounded ZIP callers must retain streaming reads rather than materialising a batch. |
| `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` | All explicit transitions/reopens into Review validate readiness evidence at the lifecycle boundary. |

## Ripple effects

- Constructor/DI changes affect Assessment Web tests and report-draft fakes.
- Adding a required address member affects managed-content callers and Box/local adapter tests.
- FRD-11's readiness paragraph changes meaning under direct operator authorization.
- Deployment is separate; current-state documents change only after the production revision is actually deployed.

## Out of scope

- Token refresh (PLAT-039), Box 429 retry policy, custody-binding verification, ZIP memory shape, report-cost formula EXT-09, renderer output, or live Azure/Box writes.
- General optimisation of the broad Case details screen.
- Compatibility constructors or name-based Box fallbacks.
