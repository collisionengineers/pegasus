# Files — complete-assessment report workflow

## Expected implementation surface after prerequisites merge

| Path/module | Expected responsibility | Risk / constraint |
| --- | --- | --- |
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` and later merged TICK-092 report contracts | Evolve the existing snapshot/use case boundary into a durable trigger/result workflow; consume, do not duplicate, the accepted snapshot/query | Highest overlap with TICK-092/TICK-094; names are not stable yet |
| `src/Pegasus.Core/Reports/**` (new focused files if warranted) | Report request/version/reference states, typed assessment+fee-note result identity, deterministic logical key, retry/failure policy, correction lineage | Keep generation distinct from approval/Sent; no generic job abstraction |
| `src/Pegasus.Core/Assessment/AssessmentOperations.cs` or the merged TICK-092 accepted-snapshot operation | Invoke/enqueue only after a committed complete accepted snapshot exists | Never render inside the assessment transaction; replay/concurrency races |
| `src/Pegasus.Core/Documents/DocumentContracts.cs` | Reuse immutable content addresses/hash verification; potentially name fee-note semantic role | Avoid forcing generated system work through staff edit leases |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | Relate report-version artifacts to later approval/Sent evidence if the merged ownership requires it | Current single approval/Sent fields can erase version association |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, report entities/configuration, migration and model snapshot | Atomic request/version/artifact/provenance state; unique logical idempotency key; claims/leases/attempts/failures; predecessor link | Migration and concurrency correctness; never overwrite prior versions |
| `src/Pegasus.Infrastructure/Persistence/**Report**Store.cs` | Consistent enqueue, claim, completion/reconciliation, retry and query projection | Must commit bytes/content custody and database result safely across failures |
| Existing `IDocumentContentStore` implementation | Store/open assessment and fee-note bytes with expected SHA-256 and length | Database/content-store partial failure and replay cleanup |
| `src/Pegasus.Infrastructure/DependencyInjection.cs`, `src/Pegasus.Web/Program.cs` | Compose the durable caller/processor in Web with the existing renderer | Web is the accepted runtime; no Worker/standalone/API/MCP renderer |
| `src/Pegasus.Web/Pages/Cases/Assessment/**` and/or existing Case documents/status partials | Show exact readiness/generation state, actionable failure/retry, version/artifact download | Must not imply approval, issue, sending, or receipt |
| `src/Pegasus.Web/Pages/Operations/**` / operations projection if reused | Surface stuck/failed generation work consistently | Avoid a second operational UI convention |
| Core, persistence, integration and real-Chromium tests | Prove complete-only trigger, deterministic replay, concurrency, recovery, two-artifact custody, corrections, and non-finality | Requires real merged dependency fixtures |

## Ripple effects

- TICK-096/TICK-097 consume the generated deterministic assessment surface; DOCS-001 currently blocks them.
- TICK-208 must bind preserved final Sent evidence to immutable report versions after corrections.
- TICK-100 addenda must append a distinct family/version without mutating assessment history.
- PLAT-007 deploys and proves the composed runtime only after local caller/persistence behavior exists.
- Report approval UI/store may need a version-specific foreign key rather than the current free-form artifact identity/hash only.
- Package locks/model snapshot and documentation/current architecture will change during implementation; no such edits belong in research.

## Context files an implementer must read

| Path / ticket | What it establishes |
| --- | --- |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Closed active family/outcomes, Core ownership, draft-vs-approval/Sent boundary, immutable correction requirements |
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` and ADR-0028 | Monolith boundary and Web execution location |
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | Merged renderer input/result/validation and current non-durable use case |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Existing single readiness/confirmation owner and current gaps |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs` | Serializable save/replay/version/history boundary |
| `src/Pegasus.Core/Documents/DocumentContracts.cs`, `EfDocumentCustodyStore.cs` | Immutable content/hash/custody mechanics |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs`, `EfCaseWorkflowStore.cs` | Approval/Sent finality, operation replay, Case concurrency |
| Durable intake/custody/lookup work contracts and EF stores | Existing pending/claim/lease/retry/terminal-failure conventions |
| [[TICK-093]], [[TICK-094]], [[TICK-092]] merged PIRs/contracts | Mandatory upstream accepted-source types and ownership |
| [[TICK-208]], [[TICK-100]], [[PLAT-007]] | Downstream correction-Sent, addendum, and deployment ownership |
| EPIC-004 `context.md` | Binding monolith, immutable identity/custody, and no-cloud-write constraints |

## Deliberately out of scope

- Implementing or planning before TICK-092 and its TICK-093/TICK-094 prerequisites merge.
- Azure deployment or any cloud write.
- Report approval, outward sending, receipt, invoicing, Audit, diminution, addenda, or Sent-evidence correction policy owned by downstream tickets.
- A standalone renderer host, endpoint, MCP tool, generic job framework, or second editable report-data record.
- Inventing Ed/Neil qualifications, unsupported salvage wording, reference formats, or report content absent from accepted authority.
