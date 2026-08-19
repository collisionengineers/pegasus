# Post-implementation report — INTK-007

## Summary

INTK-007 establishes the Core-owned Unidentified destination and makes it reachable: safely retained terminal intake outcomes receive an immutable U-reference, canonical reason, durable origin/history, atomic persistence, operator queue/detail/resolution surfaces, dashboard/navigation exposure, and MCP lookup/resolution. The implementation is deliberately compatible with the existing NeedsSorting storage code while current producers and the grouped INTK-005/006 hand-off converge on the new aggregate.

## Changes

| File/group | Change | Why |
|---|---|---|
| `docs/operator-notes.md`, PRD, FRD-01/02/03/06/08/09/10/12, capabilities, current architecture, runbook, design README and design-system examples | Added the confirmed Unidentified product vocabulary, U-reference/group rules, reason taxonomy, resolution/history boundary, and preserved Triage/Blocked/Audit/Image Intake distinctions. | Governing documents must own this semantic replacement before code. |
| `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs` | Added six reason codes, Open/Resolved states, Receipt/SubmissionGroup origin, canonical reference format/parser, commands, queries, validation, actor/version/replay contracts, and use cases. | One policy owner prevents persistence/UI/MCP drift and rejects U references as other identities. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Registers terminal unsupported/OCR/technical/unidentified outcomes after durable receipt persistence, skipping image-only material for Image Intake. | Ensures custody precedes U allocation and retryable/image-specialist paths are not stranded. |
| `src/Pegasus.Core/Operations/DashboardCounts.cs`, `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` | Added an open-Unidentified metric while retaining the legacy property for rolling compatibility. | Dashboard count comes from the durable Unidentified aggregate. |
| `src/Pegasus.Infrastructure/Persistence/UnidentifiedEntities.cs`, `PegasusDbContext.cs`, `EfUnidentifiedStore.cs`, DI | Added item/sequence/history schema, constraints/indexes, serializable allocation, origin/operation replay, expected-version resolution, list/detail/reference/history queries, and registrations. | Provides durable atomic identity and one transaction per mutation. |
| `Persistence/Migrations/20260819115323_UnidentifiedWork.*` and model snapshot | Added tables, sequence seed, deterministic legacy backfill and reason mapping, indexes and rollback. | Existing retained records receive durable Unidentified identity without MAX+1 or reference reuse. |
| `src/Pegasus.Web/Pages/Unidentified/*`, layout, dashboard, status chip, operator labels, intake/mail outcome labels | Added authenticated queue/detail/resolution journey, navigation, exact U search, status/reason labels, and dashboard link. | Operators can locate, understand, and resolve work without a Case placeholder. |
| `src/Pegasus.Web/Mcp/UnidentifiedMcpTools.cs` | Added list/get/resolve tools that delegate to the same Core contracts and require automation scope/version/operation key. | Automation sees the same canonical identity and mutation rules. |
| `tests/Pegasus.Core.Tests/Intake/UnidentifiedContractsTests.cs`, HealthEndpoint assertion | Added reference/validation/origin tests and reconciled the dashboard vocabulary assertion. | Proves canonical parser/authorization basics and current operator wording. |

## Governing docs

The linked operator authority, PRD, FRDs, capability inventory, architecture/runbook, and design README were explicitly updated with the confirmed requirement. No ADR was added: the aggregate stays within the existing Core/Infrastructure/Web/MCP boundary and reuses existing persistence conventions. Historical ADR text and legacy decision codes remain compatibility evidence, not current operator vocabulary.

## Risks / follow-ups

- INTK-005 group persistence is not present on this origin/main branch. The Core origin contract accepts a SubmissionGroup id, but the EF backfill currently handles legacy receipt origins; rebase/extend this branch after INTK-005 lands so one grouped submission receives one U item and all member files are projected.
- Retained-mail, Operations, Intake/Mail detail, and full search projections still contain legacy decision compatibility paths; the current dashboard and new Unidentified surfaces are wired, but the final stale-term audit and those projections need completion before claiming the broad replacement is fully shipped.
- Runtime-role grant verification and clean/upgrade migration integration tests remain for review/verification.
- The old `IntakeDecision.NeedsSorting` enum/code is deliberately retained for rolling compatibility and Image Intake/Triage producers; it must not be interpreted as current operator vocabulary.
- Full-suite execution reached the Core and Architecture suites and the corrected HealthEndpoint test; the long IntegrationTests process did not emit a final summary before the test host lingered, so verify on merged main.

## Verification hand-off

On merged `main`, run:

1. `dotnet restore`.
2. `dotnet build --configuration Release`.
3. `dotnet test Pegasus.slnx --configuration Release` and record Core, Integration, and Architecture totals.
4. Apply migrations to a clean SQL database and to a fixture containing legacy `needs_sorting`, `unsupported`, `ocr_required`, and `technical_failure` receipts; verify deterministic U references, one initial history row, unique origin/reference/operation constraints, and sequence continuation.
5. Exercise concurrent register/replay/resolve operations and stale-version conflict; verify no reference reuse after resolution.
6. Run the Unidentified browser journey: navigation → open queue → exact U search → detail/history → authorized resolution → resolved search/detail.
7. Exercise MCP list/get/resolve with valid and invalid actor, operation key, target and version; verify U references cannot be used as Case/Audit/Image Intake identifiers.
8. After INTK-005/006 are merged, upload a grouped vehicle submission with one unreadable sibling, no readable VRM, and conflicting VRMs; verify one grouped Unidentified item or the documented Image Intake/Case route.
