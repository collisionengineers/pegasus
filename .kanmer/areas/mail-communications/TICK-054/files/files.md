# Files — MAIL-13

## Change surface

| Path | Purpose / risk |
| --- | --- |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Core-owned contract/policy; reuse existing vocabulary and avoid a second business implementation |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Persistence or external adapter boundary; preserve mailbox scope, idempotency and durable history |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Real staff or Automation caller; thin orchestration only |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Focused acceptance and regression evidence |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Governing behaviour; modify only after explicit answers where the behaviour is unresolved |
| `docs/capabilities.md` | Update evidence/status only after delivery |

## Context files

| Path | What it establishes |
| --- | --- |
| `docs/design/README.md` | Accessible interaction and confirmation conventions |
| `docs/open-decisions.md` | Inactive predicates, confidence/holdout and live activation boundaries |
| EPIC-006 `context.md` | One Core owner and no local-alpha mailbox mutation |
| `src/Pegasus.Web/Program.cs` | Existing composition and feature-gate conventions |

## Out of scope

No new taxonomy, speculative abstraction, bulk action, arbitrary client-supplied destination, real mailbox/cloud write, deployment claim or duplicated UI/MCP policy.

# File-map refresh — 2026-08-20

## Where the change lands after TICK-049

| Path | Why |
|---|---|
| TICK-049's landed focused Core exact-message action/operation files beside `src/Pegasus.Core/Intake/RetainedMail.cs` | Reuse actor, exact internal message id, expected state/version, reason, operation key, replay/conflict and provider-result conventions. Extend with one closed MAIL-13 action vocabulary; do not create a generic command bus. |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Expose immutable arrival read state separately from latest-known Outlook read/category/flag/location state and its version/freshness to the real Web/MCP callers. |
| TICK-049's landed operation entity/configuration/store beside `MailboxEntities.cs`, `MailboxModelConfiguration.cs`, and `PegasusDbContext.cs` | Reuse unique request fingerprint, pending/succeeded/failed/unknown outcome, actor/reason and ActionHistory. Reuse move/current-location records for Deleted Items and restore; add only the concrete state-action fields/records the landed shape cannot carry. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Overlay latest successful known state/location without mutating the write-once retained arrival row. |
| A single migration plus `Migrations/PegasusDbContextModelSnapshot.cs` | Add only schema needed beyond TICK-049, with constraints, delete restrictions and exact Web grants; preserve DELETE denial for retained evidence/history. |
| TICK-049's landed Graph adapter in `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Reuse token/host/immutable-id/error and move/probe mechanics. Add exact GET-state + PATCH for read/category/flag, reuse move for Deleted Items/restore, and add `permanentDelete` only if the authority conflict is resolved. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` and `src/Pegasus.Web/Program.cs` only as needed | Compose the Core/store/adapter in the existing host. Do not activate a production writer or broaden credentials as a code-side effect. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | One exact-message action surface with server-derived state, stale/replay/failure/unknown results, reasoned Confirm/Cancel and no external identity/arbitrary category/folder input. Permanent delete, if authorized, is a separately rendered fresh checkpoint. |
| `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml` | Reuse TICK-049's confirmation/focus pattern unchanged where it fits; do not add a second dialog convention. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` plus focused action tests | Prove authorization, closed action set, validation, category preservation, stale refusal, replay/conflict and permanent-delete checkpoint/unknown-result rules. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Prove immutable arrival evidence, operation uniqueness/concurrency, durable before/after/history, current-state overlay, failure/retry and unknown outcomes. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Fake-HTTP proof for exact paths/headers/bodies, GET state, PATCH property confinement, move/delete/restore, permanentDelete only if authorized, response mapping and outside-scope refusal. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Real authenticated detail caller, anti-forgery, exact-state controls, confirmation/focus, stale/replay/failure/unknown visibility and no row/preview/bulk actions. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` and `docs/capabilities.md` | Canonicalize only resolved behavior/evidence. Permanent deletion additionally needs protected operator-notes/FRD-04/design/ADR reconciliation before implementation can claim it. |

## Context files

| Path | What it establishes |
|---|---|
| TICK-049 research/files/open-questions | Planned first narrow Graph mutation, exact-message identity, immutable move, operation reservation/recovery, current-location projection and no present live-write authority. Refresh this map after its actual merge. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` | Current retained message is write-once arrival evidence; only observed-at-retention `IsRead` exists. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Current client is GET-only and already owns token, host confinement, immutable header and content-safe error behavior. |
| `src/Pegasus.Core/Identity/StaffAuthorization.cs` and FRD-04 | Existing ordinary casework authorization; every role currently prohibits permanent deletion. |
| `docs/operator-notes.md`, ADR-0004 and `docs/design/README.md` | Binding/protected and downstream “no permanent deletion through any surface” rule that conflicts with the ticket decision. |
| `docs/runbook.md#live-operation-approval-matrix`, `docs/operations.md`, `docs/current-architecture.md` | Production has read-only Graph evidence; permission change, RBAC scope/negative test and exact live action are separate approvals. |
| EPIC-006 `context.md` | One Core implementation and no local-alpha mailbox mutation. |

## Ripple effects and exact overlaps

- **TICK-049 / MAIL-07:** hard execution predecessor. Exact overlap in Core exact-message actions, Graph client/adapter, operation/history/current-location persistence, entities/configuration/migration/snapshot, DI, message detail, reason dialog, `ProductionGraphSourceTests`, `RetainedMailPersistenceTests`, `MailWorkspaceWebTests`, FRD-08 and capabilities. Land/rebase TICK-049 first.
- **TICK-053 / MAIL-11:** overlaps `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, folder/detail state, `Message.cshtml.cs`, persistence/Web tests and Deleted Items read scope. Stabilize read/search shapes before mutation.
- **TICK-056 / UI-10:** exact overlap in message-detail action presentation and `MailWorkspaceWebTests.cs`; UI-10 consumes MAIL-13's final Core result and must not duplicate authorization, category or deletion policy.
- **TICK-088 / MAIL-12:** overlaps Graph client, external-operation/history convention, DI, message detail and Graph/Web tests. Keep send contracts separate; sequence after MAIL-13/MAIL-07 shared seams stabilize.
- **TICK-064 / MAIL-23 and TICK-047 / MAIL-05:** upstream of TICK-049's approved folder/recommendation identities; restore/delete must reuse the resulting exact folder/current-location authority, not create another folder registry.
- **TICK-050 / MAIL-08:** consumes message state for suggestions and overlaps retained detail/Core tests; run after state results stabilize.
- **AUTO-003:** downstream thin Automation caller only; no direct Graph/EF implementation here. Current structured link is a backlink only; TICK-054 has no stored dependency edges.

## Out of scope

No free-form or generic category editor, mark-complete/due-date flag workflow, arbitrary folder move, MAIL-07 policy move duplication, compose/send, bulk/list/preview actions, retained-source/history deletion, generic mail-action framework, automatic retry, direct MCP Graph call, permission/RBAC/cloud/deployment write, live Outlook action without exact approval, or permanent-delete implementation before the authority conflict is resolved.
