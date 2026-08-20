# Files — TICK-051 / MAIL-09 post-merge slice

## Changed files

| Path | Change |
|---|---|
| `src/Pegasus.Core/Cases/CaseQueries.cs` | Extract the existing registration normalization as the shared Case convention; keep SearchCases validation unchanged. |
| `src/Pegasus.Core/Intake/CaseMatching/CaseMatchContracts.cs` | Add only the optional stale-evidence fingerprint to the existing automatic-association request. |
| `src/Pegasus.Core/Intake/CaseMatching/AutomaticMailCaseAssociation.cs` | Focused evidence contract/query port and Core evaluator for unique VRM/thread agreement/abstention. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Invoke the focused MAIL-09 use case after the existing provider match on live and replay paths, only while unassociated. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | Implement the evidence read and compare its fingerprint inside the existing serializable automatic-association transaction; preserve legacy provider hashes. |
| `src/Pegasus.Infrastructure/Persistence/CurrentIntakeAssociations.cs` | One focused query helper for current association precedence, shared by mail evidence and retained projection. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Project current automatic/manual association instead of accepted links alone. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register the one evidence port/use case; no new runtime unit. |
| Core/Integration tests | Prove policy abstention/agreement, stale recheck, idempotent append-only write, current projection and real queued caller. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md`, `docs/capabilities.md`, `docs/current-architecture.md` | Canonical accepted behavior and record local evidence only. |

## Context

- Existing `EvaluateIntakeCaseMatch`/QDOS policy remains provider-scoped and unchanged.
- Existing `EfIntakeMutationStore` remains the sole association/history writer.
- TICK-053/049/050 retained search, current folder and suggested Move shapes are preserved.
- TICK-052 owns staff search/link/unlink/relink controls after this shape lands.

## Out of scope

No Case/PO matching; no duplicate matcher, association/history table, normalized-registration column/index, migration, generic action or match framework, UI mutation, MCP tool, attachment copy, Graph/Outlook/Box/cloud/deployment/live production write.
