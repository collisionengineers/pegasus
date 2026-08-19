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

## Stale-term inventory — 2026-08-19 (takeover, claude-code)

`git grep -rn "Needs sorting\|NeedsSorting\|needs_sorting" -- src tests docs CLAUDE.md AGENTS.md`: 176 hits at the start of this session, 169 after the fixes below. Classified every hit into one of three buckets.

### Migrated (12 files touched this session)

- `docs/capabilities.md` — MAIL-02 row wording ("reasoned Other, Needs sorting" -> "reasoned Other, Unidentified").
- `docs/frd/frd-08-email-mailbox-and-background-processing.md` — the three mentions describing `MailOperationalDestinationPolicy`'s own abstention concept and output (the "operational abstention" sentence, the Ambiguous/Unclassified row's destination column, the "no automatic folder recommendation" sentence). Left the Triage-request row's "missing VRM remains Needs sorting under FRD-03" note alone — see Follow-up below.
- `AGENTS.md` / `CLAUDE.md` (symlink) — the product invariant now records Audit/Triage/Blocked intake keeping their meanings, with Unidentified superseding Needs sorting for that meaning.
- `docs/current-architecture.md` — the invariant's two mirrors (Architecture invariants section, and the "invariants remain in force" list), matching AGENTS.md. The other four mentions in this file (Image Intake/DOC-MSG routing, Audit-evidence ambiguity, case-match ambiguity, count/queue projections) are explicitly disclosed by the file's own opening "Unidentified intake boundary" section as historical compatibility descriptions — retained, not migrated.
- `docs/operator-notes.md` — the three literal statements the PR's new "Unidentified received material" section had left untouched are reworded (not deleted) to "Unidentified (formerly `Needs sorting`)", preserving their substance: the Triage stage-0 pre-VRM holding state, the malformed-forward-header outcome, and the 2026-08-04 interface-vocabulary rule.
- `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` — `MailOperationalDestination.NeedsSorting` renamed to `Unidentified` (item 6 hand-off; not reachable from any UI caller yet, see Follow-up).
- `tests/Pegasus.Core.Tests/Intake/Classification/MailOperationalDestinationPolicyTests.cs` — updated to match.
- `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs`, `QdosIntakeWebTests.cs` — three rendered-text assertions fixed (a real regression this branch's own commit introduced; see Risks below).
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`, `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs` — already migrated by the prior agent's commit (`NeedsSorting`/`MailRouteDisposition.NeedsSorting` -> operator label `"Unidentified"`); confirmed correct by the operator ruling, not reverted.
- `docs/design/README.md` — already documents the exact `NeedsSorting -> Unidentified` operator-label mapping as canonical (the "Core outcome to operator label" table); the other five mentions in this file are narrative examples using the internal code name alongside that same mapping, not stale.

### Deliberately retained (the remaining ~155 hits)

The persisted `IntakeDecision.NeedsSorting` / `MailRouteDisposition.NeedsSorting` enum members and their `needs_sorting` snake-case DB codes are explicitly out of scope: `operator-notes.md`'s own new section says "it does not rename or collapse Triage, Blocked intake, incomplete Audit evidence, or Image Intake" — the general Intake/Image-Intake decision pipeline these codes drive is that excluded, pre-existing area, distinct from the mail-classification/Unidentified-queue meaning this ticket replaces. Every UI surface that displays this persisted decision to an operator already shows the exact label `"Unidentified"` (confirmed above), so operator-facing vocabulary carries none of the stale wording; only the internal code name is retained, which is exactly what a persisted enum/DB value is for. This covers:

- Core: `IntakeContracts.cs`, `IntakeDecisionPolicy.cs`, `ProcessIntake.cs` (the decision-code usages, distinct from the reason-mapping this session fixed), `DurableIntake.cs`, `DirectProviders/Qdos/QdosMailRoutePolicy.cs`, `Operations/DashboardCounts.cs` (explicitly commented "NeedsSorting remains read-only compatibility during rollout"), `ImageIntake/ImageIntakeAutomation.cs`, `ImageIntake/ImageIntakeContracts.cs`.
- Infrastructure: `EfIntakeReceiptStore.cs`, `EfDashboardQueries.cs`, `EfImageIntakeStore.cs`, `EfRetainedMailboxMessageStore.cs`, `EfOperationsStore.cs`, and the `20260819115323_UnidentifiedWork.cs` migration's own `WHERE Decision IN ('needs_sorting', ...)` backfill predicate, which must name the legacy value it is migrating from.
- Web: `Mcp/IntakeMcpTools.cs` — the separate, pre-existing Intake-receipts MCP tool's `needs_sorting` filter value; changing that public API's filter vocabulary is a breaking change outside this ticket's diff, distinct from `UnidentifiedMcpTools.cs` (the new tool, already using `Unidentified`/`UnidentifiedState`).
- Tests: the bulk of the remaining hits (`Pegasus.IntegrationTests` and `Pegasus.Core.Tests`) assert `IntakeDecision.NeedsSorting` / `MailRouteDisposition.NeedsSorting` enum values or `"needs_sorting"` DB literals against the retained pipeline above — not stale, since the enum/code itself is retained.
- Docs describing the same retained pipeline: `docs/frd/frd-01-case-identity-and-lifecycle.md`, `frd-02-intake-and-source-identity.md`, `frd-03-triage.md`, `frd-09-provider-and-intermediary-routes.md`, `docs/adr/0006-provider-neutral-intake-with-contained-qdos-policy.md`, `docs/runbook.md`, and `docs/current-architecture.md`'s four historical-compatibility mentions (all pre-date this ticket and are outside operator-notes.md's confirmed replacement scope).

### Follow-up (not fixed this session — flagged, not silently dropped)

`docs/frd/frd-08-email-mailbox-and-background-processing.md`'s Triage-request row ("missing VRM remains Needs sorting under FRD-03") and `docs/frd/frd-03-triage.md`'s own wording for the same pre-Triage holding state still say "Needs sorting", while `docs/operator-notes.md` line ~42 (the same concept) now says "Unidentified (formerly `Needs sorting`)". Left both FRDs untouched to avoid editing FRD-03 out of this ticket's bounded diff, but this is a genuine, named cross-document inconsistency between the protected operator-notes.md and its two restating FRDs — a follow-on ticket should reconcile FRD-03/FRD-08's Triage-without-VRM wording with operator-notes.md.

## Real regression found and fixed

This PR's own commit (`abd8a923`) changed `Intake/Details.cshtml.cs`'s `DecisionLabel` from `"Needs sorting"` to `"Unidentified"` for `IntakeDecision.NeedsSorting`, matching `docs/design/README.md`'s canonical operator-label mapping, but left three pre-existing integration tests asserting the old literal rendered text. Confirmed by running `MultiFormatIntakeWebTests.DeferredLegacyContainersAreAcceptedIntoNeedsSortingWithoutReference` before the fix: both cases failed (the review page renders `"Unidentified"`, not `"Needs sorting"`). Fixed the three assertions in `MultiFormatIntakeWebTests.cs` and `QdosIntakeWebTests.cs`; re-ran and confirmed green (the two `QdosIntakeWebTests` cases are `[GenuineQdosCorpusFact]`-gated and skip locally without the corpus fixture, as expected).
