# Stream A review blockers on the B head (relayed 2026-09-06, after `ca6a97c72`)

Each is a B-owned fix; dispositions are recorded when the fixing commit lands.

| # | Blocker | B disposition |
| --- | --- | --- |
| 1 | `GetCase` materializes all documents, history and tasks before paging; a host needs a bounded header projection | Add `GetCaseHeaderQuery`/`CaseHeader` (summary, workflow, lease, counts) + `IGetCaseHeader` in `Cases/CaseQueries.cs`, `EfCaseQueryStore.GetHeaderAsync`; publish the signature for A's MCP host |
| 2 | Document cursor pages aggregate documents but map all occurrences/versions; estimate pages embed unbounded lines | Bound returned items: document page items carry the current occurrence + current version only (older versions via `IGetCaseDocumentMetadata`/detail reads); estimate page items are header projections without lines (lines through the existing estimate read); cap serialized item size |
| 3 | `ImportRawEstimate` passes expected length 0 for non-empty A04 content | Obtain the exact `ContentLength` from `IGetCaseDocumentMetadata` (G6) before `IReadLogicalDocumentVersion.OpenAsync` |
| 4 | Import replay by hash precedes authorization, current actor, lease and source checks | Reorder: `StaffAuthorization.Require`, actor-kind check, expected Case version + lease validation, source/route validation, then replay; replay is authorized for every caller, including the same subject with a different actor kind |
| 5 | Numeric undefined `RepairSpecificationSourceRoute` must reject at host and Core | Core: `Enum.IsDefined` guard in `ImportRawEstimate`/`EstimatePolicy`; host: the Web/MCP binder refuses undefined numerics (Web handler is B's wiring; MCP is A's — B publishes the Core rule) |
| 6 | Sent-poll fixture needs an approved stable mailbox and Sent-folder identities plus activation and a positive generation | Align the B-owned fixture(s) that seed `ApprovedMailbox`/`ApprovedSentPollState` (F/G added `AllowSentEvidence`, `MailboxGeneration`, `ScopeFingerprint`, `Generation`, `StartBoundaryUtc`) — locate in `CaseWorkflowPersistenceTests`/`CaseWorkspacePersistenceTests`/B05 tests and fix |
| 7 | `CaseWorkflowMigrationTests` must migrate to pre-Foundation `20260905010654_CaseSignOffEngineer` after the QDOS recovery seed, not the latest destructive Foundation; keep identity/ordinal/EVA assertions | Pin the target migration in the B-owned historical test; assertions retained |

A's latest source/UI reference for combined checks: `task/pegasus-v1-platform` at `a1b1a5ea2`. B does not edit A's canonical docs; the domain documentation edits are published on PR #672 for A08.
