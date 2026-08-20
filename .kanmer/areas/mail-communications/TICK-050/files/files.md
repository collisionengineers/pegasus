# Files — TICK-050 / MAIL-08 advisory next action

*Surveyed on current `origin/dev` (`b36c6666`) and `origin/main` (`2325ed4a`). Implementation waits for [[TICK-047]] and [[TICK-049]], then refreshes to their actual merged symbols.*

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Add the concrete advisory result/policy to the existing exact-message read path. For the confirmed slice it derives zero or one Move suggestion solely from current MAIL-05 recommendation and MAIL-07 eligibility; no persistence or execution logic. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Expose the Core-derived advice on the already authorized message-detail caller. Do not recompute eligibility, accept a destination, or invoke Graph. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Render the advisory explanation and, only when eligible, a Move control targeting MAIL-07's separate confirmation workflow. Show an honest absence rather than disabled speculative actions. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` | Prove no/one suggestion, deterministic order, current-state re-derivation, fail-closed stale/unavailable inputs and no mutation. Reuse the MAIL-05/07 fakes or value types that land; do not build a generic action test framework. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Prove the real authenticated detail caller renders the eligible Move advice, delegates to MAIL-07 confirmation, hides it when ineligible, and performs no write merely by viewing. |

## Context files

| Path | What it tells the implementer |
|---|---|
| MAIL-05's landed recommendation result in `src/Pegasus.Core/Intake/RetainedMail.cs` | Current exact folder, classification/binding provenance and unavailable state. Consume it; do not reproduce MAIL-23/MAIL-05 mapping. |
| MAIL-07's landed Core command/eligibility contract in the retained-mail action boundary | The sole owner of confirmation eligibility, exact destination/version checks, authorization, history, failure and retry. MAIL-08 only links to it. |
| `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` | MAIL-02's application destination, already available on current detail. It may explain advice but is not action eligibility. |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | MAIL-22's canonical category and Other vocabulary; no second taxonomy or action mapping. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Existing source of processing, allocation and Case-association facts. Read to understand the projection; do not modify it for derived advice. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Existing state/action presentation conventions; keep Core action kind and Web label ownership separate. |
| `src/Pegasus.Web/Mcp/MailMcpTools.cs` | Existing second retained-mail reader. [[AUTO-003]] owns later Automation exposure; do not extend tools here. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Governs advisory-only Move, separate confirmation, exact-message actions and no arbitrary destination. It contains no accepted broader suggestion matrix. |
| `docs/capabilities.md` | MAIL-08 is Next/0.3.0 while MAIL-12 is Later/0.5.0; do not create an unsupported send-action dependency. |
| EPIC-006 `context.md` | One Core implementation and no unapproved local-alpha mailbox mutation. |

## Ripple effects and exact overlaps

- Structured hard dependencies: [[TICK-047]] and [[TICK-049]] block this ticket. MAIL-23 is transitive through MAIL-05.
- `RetainedMail.cs` overlaps [[TICK-047]], [[TICK-049]], [[TICK-053]], [[TICK-054]], and [[TICK-056]].
- `Mail/Message.cshtml.cs` overlaps [[TICK-047]], [[TICK-049]], [[TICK-051]], [[TICK-052]], [[TICK-054]], [[TICK-057]], and [[TICK-088]]; their corresponding detail markup changes also require serialization or refresh.
- `MailWorkspaceWebTests.cs` overlaps [[TICK-047]], [[TICK-053]], [[TICK-056]], and [[TICK-057]].
- [[TICK-051]], [[TICK-052]], [[TICK-054]], and [[TICK-088]] are not blockers unless a later accepted action matrix explicitly adds their action to MAIL-08 advice.
- [[AUTO-003]] follows landed Core actions and adds any Automation surface later.
- `docs/capabilities.md` changes only when delivery evidence changes MAIL-08's row. Deployment/current-state updates belong to the release ticket.

## Out of scope

No action beyond the confirmed eligible Move suggestion; no taxonomy, operational-destination, folder-policy, Case-association or action-eligibility duplication; no suggested-action persistence, migration, store, transaction, idempotency key, history, Graph adapter, dynamic registry, plugin/handler framework, AI suggestion, bulk action, MCP tool, inline move, external write, deployment, or live mailbox mutation.
