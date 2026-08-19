# Files — accepted structured source record

| Path/module | Expected change | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Extend outcome/template-specific report readiness | Existing UI readiness behavior |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Add typed accepted report snapshot seam or projection data | Over-modeling/duplication |
| `src/Pegasus.Core/Cases/CaseDataContracts.cs` | Reuse accepted case facts/version | Cross-aggregate consistency |
| `src/Pegasus.Core/Documents/DocumentContracts.cs` | Reuse current/custodied photo/document identities | Stale evidence |
| `src/Pegasus.Core/Reports/**` | Derived immutable input snapshot and source-version/hash identity | New policy owner must stay in Core |
| `src/Pegasus.Infrastructure/Persistence/**` | Query/persist atomic snapshot/render request | Concurrency |
| `src/Pegasus.Web/Pages/Cases/Assessment/**` | Show exact blockers/status | Truthful UX |
| Assessment/case/report tests | Prove accepted-only, stale/conflict, outcome-specific readiness | Fixture breadth |

## Context files

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Existing readiness owner |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Existing accepted assessment model |
| `reference/rendererref1/report_data_schema.json` | Approved initial required payload |
| `docs/frd/frd-11-*.md` | Report behavior/finality |
| `TICK-093`, `TICK-094` | Repair specification and Engineer decisions |

## Out of scope

- A separately editable report data store.
- Unconfirmed automation values.
- Audit template/payload.
