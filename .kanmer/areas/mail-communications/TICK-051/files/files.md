# Files — MAIL-09

## Change surface

| Path | Purpose / risk |
| --- | --- |
| `src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs` | Core-owned contract/policy; reuse existing vocabulary and avoid a second business implementation |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | Persistence or external adapter boundary; preserve mailbox scope, idempotency and durable history |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Real staff or Automation caller; thin orchestration only |
| `tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs` | Focused acceptance and regression evidence |
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

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/CaseMatching/CaseMatchContracts.cs` and a focused Core MAIL-09 use case in this module | Reuse the existing automatic-association request/store vocabulary while owning the distinct system-wide-VRM/mailbox-thread decision and abstention reasons once. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Existing system-worker post-processing caller; invoke the general MAIL-09 policy only when the receipt has no current Case and preserve the existing QDOS-direct decision path/replay behavior. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | Existing serializable idempotent association/history transaction; add only the stale-evidence revalidation needed by the accepted MAIL-09 rule. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` or one focused EF MAIL-09 query adapter reusing its projection | Read exact normalized VRM candidates across all Cases; do not use the provider-filtered `CaseMatchIndex` as system-wide authority. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Resolve exact mailbox/conversation candidates through current receipt associations and project active automatic/manual association back into list/detail. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register the one Core policy/use case and query port for Worker/Web compositions without a second policy owner. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | Display the resulting association and preserve Case/back navigation; do not add TICK-052's manual mutation flow here. |
| `tests/Pegasus.Core.Tests/Intake/CaseMatching/` | Prove unique system-wide VRM, exact mailbox-thread, agreeing evidence, and zero/multiple/stale/contradictory abstention. |
| `tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs` | Extend existing transaction/replay/reversal evidence for MAIL-09 stale revalidation and immutable history. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` and `MailWorkspaceWebTests.cs` | Prove thread candidate scoping and that active automatic/manual associations appear through the real retained-mail Web caller. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` and `docs/capabilities.md` | Canonicalize the accepted MAIL-09 rule and record only the evidence tier delivered. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs` | Existing QDOS/provider eliminator remains a separate accepted subset; its multi-key/provider-scoped semantics must not leak into general MAIL-09. |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosCaseMatchPolicy.cs` | Existing normalization grammar and external provider-claim semantics; it does not authorize inbound Case/PO matching. |
| `src/Pegasus.Infrastructure/Persistence/CaseMatchEntities.cs` | Current index contains only cases with a registered provider match policy and therefore cannot prove system-wide VRM uniqueness unchanged. |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | Canonical staff search registration normalization and exact Case result shape; share the convention without invoking staff authorization from Worker. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | Sole current-association precedence: manual-association history controls whether the active manual link or accepted Case link is current. |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Existing exact-message display contract consumed by Web and MCP; association remains a fact on its summary, not a separate UI policy. |
| `src/Pegasus.Web/Mcp/MailMcpTools.cs` | Downstream read caller of retained detail; must continue to report the same Core association. AUTO-003 owns future association tools. |
| `docs/operator-notes.md` and FRD-02/08 | Case/PO is internal/non-universal; one source has at most one current Case, reversal is permanent, and thread identity is evidence rather than identity. |
| EPIC-006 `context.md` | Worker/Web/Automation reuse one Core implementation; local-alpha work never mutates Outlook. |

## Ripple effects and exact overlaps

- **TICK-052 / MAIL-10:** exact overlap in `EfIntakeMutationStore.cs`, retained current-association projection, `Message.cshtml(.cs)`, `CaseMatchIntegrationTests.cs` and Web association tests. Required order: TICK-051 then TICK-052.
- **TICK-056 / UI-10:** overlaps the mail detail/read projection and `MailWorkspaceWebTests.cs`; UI-10 assembles the delivered behavior after MAIL-09/10 and must not recreate matching/link policy.
- **TICK-053 / MAIL-11:** overlaps `EfRetainedMailboxMessageStore.cs`, retained result shapes and mail Web tests. Do not run concurrently unless refreshed plans split those files explicitly.
- **TICK-050 / MAIL-08:** consumes Case-association state for suggested actions and overlaps retained Core/store/detail; run after MAIL-09.
- **AUTO-003:** downstream Automation caller only after the Core action lands; no MCP policy or direct EF/Graph call belongs in TICK-051.

## Out of scope

No inbound Case/PO matching, provider-wide precedence change, generic confidence score, new association/history table, duplicate VRM normalization, one-to-many association, attachment copy, Case/reference mutation, manual link/unlink/relink UI, MCP mutation tool, Graph/Outlook/Box write, historical replay, deployment or unspecified live production write.
