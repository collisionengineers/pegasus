# Files — MAIL-10

## Delivery files

| Path | Change |
|---|---|
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Add the thin authenticated search, target-review, link and unlink caller. Resolve message→receipt/current Case afresh on the server and reuse `ISearchCases`, `IGetCase`, `IGetIntake`, `IAcquireCaseEditLease`, `ILinkIntake`, and `IReverseIntakeLink`. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Show the current association, side-effect-free Case search, canonical target summary, and separate reasoned confirmation forms. Offer replacement search only after unlink. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Prove the exact authenticated caller, server-side message/receipt binding, unresolved-classification allowance, canonical target summary, link/unlink/replacement journey, stale/mismatch refusal, authorization, and return context. |
| `docs/capabilities.md` | Record only the local caller/evidence actually delivered, if its current MAIL-10 row is stale. |

## Reused unchanged owners

- `src/Pegasus.Core/Intake/IntakeContracts.cs`, `DurableIntake.cs`: existing link/reverse contracts and validation.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs`: existing serializable, replay-protected association/history transaction.
- `src/Pegasus.Core/Cases/CaseQueries.cs`: canonical Case search and detail summaries.
- `src/Pegasus.Core/Intake/RetainedMail.cs` and `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`: landed MAIL-09 exact receipt/current-association projection.
- `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs` and `Pages/Shared/_ReasonDialog.cshtml`: existing lease/link/reverse and reason-confirmation conventions.

## Explicitly unchanged

No Core/EF schema, migration, permission, Graph/Box adapter, generic command framework, active-to-active swap, MCP surface, or live-production integration.

## PR-048..050 correction files — 2026-08-20

| Path | Focused correction |
|---|---|
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Add explicit prepare-link/prepare-unlink lease handlers; accept the prepared lease token in final handlers; let Core see exact replay/conflict before Web state checks; compensate definitive failures through existing `IReleaseCaseEditLease` with non-request cancellation. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Render final reason dialogs only when exact prepared lease authority is present; otherwise offer the explicit prepare step. Put every visible suggestion fact inside its one anchor. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Add exact replay/conflict, failure-release/retry, success-consumes-lease, and accessible-name evidence. |
| ticket research/plan/checklist/PIR/scratch | Reconcile blocker reasoning, commands, four lenses, file inventory, and PR traceability. |

Unchanged: Core link/reverse contracts, EF association/history transaction and fingerprint, Case lease store/schema, retained-mail projection, shared search helper, shared reason dialog, FRD/product policy, Graph/Box/cloud/deployment.

## PR-051/052 correction files — 2026-08-20

- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`: exact message/receipt/action payload binding, success replay retention, release-failure retention/outcome.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml`: action-specific confirmation rendering.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`: cross-message/cross-action and fail-once release retry proof.
- Ticket documents only otherwise; no Core/EF/schema/runtime/external file.
