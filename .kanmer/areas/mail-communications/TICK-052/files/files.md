# Files — MAIL-10

## Where the change lands

| Path | Why |
|---|---|
| TICK-051's merged association contracts in `src/Pegasus.Core/Intake/IntakeContracts.cs` and `DurableIntake.cs` | Reuse the final `ILinkIntake` / `IReverseIntakeLink` commands and validation. No mail-specific Core mutation or direct-swap abstraction should be added unless TICK-051 changes the proven seam. |
| `src/Pegasus.Web/Presentation/UploadCaseDecision.cs` | Existing shared Case search + exact-reference fallback + leased `ILinkIntake` orchestration. Minimally generalize/reuse it for retained mail and add reasoned reverse orchestration so Message does not become a second business flow. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Add authenticated exact-message search/link/unlink/relink handlers. Reload the retained message and derive `IntakeReceiptId`/current association server-side before calling the shared flow; preserve mailbox/folder/page context and fail closed on stale/mismatched state. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Render current association first, then the established Case search, target summary, reason and explicit confirmation. Offer unlink only for the exact current Case; replacement link only after unlink. Keep actions off rows/quick preview and retain the same-tab Case/back journey. |
| `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml` and `src/Pegasus.Web/wwwroot/js/site.js` | Reuse as-is if their current accessible/no-script Case-search convention satisfies the message form. Change only if a shared target-summary/confirmation refinement is required; do not create a second mail-only combobox implementation. |
| `src/Pegasus.Web/Program.cs` | Update registration only if the existing upload-named presenter is minimally generalized; register one shared Web orchestrator, not parallel upload/mail services. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Prove the actual authenticated `/Inbox/{id}` caller: search/summary/confirmation, exact message→receipt binding, unresolved-classification link, unauthorized/stale/wrong-target refusal, link→unlink→replacement link, notices and preserved return context. |
| `tests/Pegasus.IntegrationTests/CaseAcceptanceReplayTests.cs` and `CaseMatchIntegrationTests.cs` | Extend only where TICK-051's final transaction lacks proof. Reuse the existing accepted-lineage, replay, one-row and immutable-history evidence; do not duplicate it in a new mail persistence test class. |
| `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs` and `Browser/UploadCaseSearchBrowserTests.cs` | Regression coverage if the shared Case-search/association presenter or DOM convention is generalized. |
| `docs/capabilities.md` | After delivery only, record the actual caller/evidence tier and keep production acceptance separate from local proof. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | One serializable, replay-protected association/history transaction. Link refuses an active row; unlink deactivates it; later link reuses it. TICK-051 may alter the atomic revalidation seam, so consume its merged version. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Message→receipt projection and current Case display. TICK-051 owns correcting active manual/automatic association precedence; MAIL-10 should not reopen this file unless the merged projection lacks facts required by the real caller. |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Exact retained-message/detail contract already carries `IntakeReceiptId`, Case and list context facts. Avoid adding a second association state/result taxonomy. |
| `src/Pegasus.Core/Cases/CaseQueries.cs` and `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | Canonical authorized Case search and business-readable target shape; search by reference, registration, claimant and related current facts. |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml(.cs)` | Existing raw manual link/unlink caller proves command semantics but exposes GUID/version entry; mail must reuse the safer Case-search convention rather than copy that raw UI. |
| `src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs` | Expected Case version, exact actor lease, nonterminal/nonarchived checks, version increment and lease clearing for every association transition. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260729199000_RuntimeRoleReconciliation.cs` and `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Web already has the required receipt/Case/association/history DML and DELETE denials; no new migration/grant is expected. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Exact-message action, deliberate search, target summary, reason/confirmation, unresolved-classification allowance, one-to-one boundary and permanent before/after correction history. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Immutable source occurrence/origin and reversible versioned source-to-Case relationship. |
| `docs/design/README.md` | Accessible Case search/confirmation, reason-dialog, same-tab Case navigation and exact return-context behavior. |
| `docs/runbook.md#live-operation-approval-matrix` and EPIC-006 `context.md` | Local tests only; production database writes require exact-target approval, and local-alpha work never mutates Outlook. |

## Ripple effects and exact overlaps

- **TICK-051 / MAIL-09 — hard predecessor and exact serialization:** `IntakeContracts.cs`, `DurableIntake.cs`, `EfIntakeMutationStore.cs`, `EfRetainedMailboxMessageStore.cs`, `Message.cshtml(.cs)`, `CaseMatchIntegrationTests.cs` and `MailWorkspaceWebTests.cs`. Required order: merge TICK-051, then refresh and execute TICK-052.
- **TICK-053 / MAIL-11:** `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, `Message.cshtml(.cs)` return-context behavior and `MailWorkspaceWebTests.cs`. Do not run concurrently unless final plans explicitly split those files.
- **TICK-056 / UI-10:** downstream assembly overlaps `RetainedMail.cs`, the retained EF store, both Mail pages and `MailWorkspaceWebTests.cs`. UI-10 consumes MAIL-10; it must not recreate search/link policy.
- **TICK-050 / MAIL-08 and TICK-057 / UI-14:** exact message-detail markup/model and Web tests. Serialize their detail changes after the action contracts land.
- **TICK-047 / MAIL-05, TICK-049 / MAIL-07, TICK-054 / MAIL-13 and TICK-088 / MAIL-12:** all change `Mail/Message.cshtml(.cs)` and adjacent exact-message action UI; serialize or rebase these lanes even though their Core/Graph actions are independent.
- **AUTO-003:** downstream Automation caller of the landed Core actions. It should touch `MailMcpTools.cs` and Automation tests, not duplicate this Web flow or write EF/Graph directly.
- **Archived TICK-136:** its “Describe manually associated Inbox correspondence accurately” concern is absorbed by MAIL-10's real caller/projection acceptance; no separate implementation scope or dependency remains.

## Out of scope

No new association/history table, schema or runtime grant; no direct active-to-active swap command; no generic mail-action framework; no duplicate Case search/authorization/lease policy; no one-to-many association, message/attachment copy, Case/reference mutation or accepted-origin rewrite; no association controls on rows/preview; no bulk action; no MCP tool (AUTO-003); no Graph/Outlook/Box read or write; no deployment claim or unspecified production database write.
