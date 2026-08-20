# Files — TICK-047 / MAIL-05 folder recommendation

*Surveyed on current `origin/dev` (`b36c6666`) and `origin/main` (`2325ed4a`). Implementation must wait for [[TICK-064]] and refresh these paths from its merged branch.*

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Extend the existing exact-message read model and `GetRetainedMail` use case with the current MAIL-23 logical-folder/binding result: configured recommendation plus policy/binding provenance, or an explicit unavailable/no-recommendation outcome. Re-derive on every read; add no persisted recommendation or write contract. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Keep the staff caller thin and expose the Core result already returned by `GetRetainedMail`; accept no folder input and add no Graph or mapping logic. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Display the designated folder and provenance, or the honest unavailable/no-recommendation reason, beside classification and operational destination. Add no confirmation/move form; that belongs to MAIL-07. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` | Prove authorization and exact-message/mailbox scope; current classification plus current binding resolves; reclassification/rebinding re-derives; Unidentified, ambiguity, missing binding and wrong mailbox fail closed; and `No action` is not mistaken for no recommendation. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Prove the authenticated `/Inbox/{id}` caller renders the configured exact recommendation/provenance and unavailable states without changing classification history, approved-mailbox state, retained mail, or any external mailbox. |

## Context files

| Path | What it tells the implementer |
|---|---|
| MAIL-23's landed Core folder-policy file beside `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` | The sole detailed-classification → logical-folder/no-recommendation owner. Use its actual merged symbol; do not recreate the table in retained mail or Razor. |
| `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` | MAIL-02's separate application-destination policy and current message-detail convention. It is not a folder recommendation policy. |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | MAIL-22's canonical category/subtype/Other vocabulary and validation. |
| MAIL-23's landed binding port in `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | The approved mailbox-scoped logical-folder → exact Outlook identity source. Consume this port after MAIL-23; do not query Infrastructure directly. |
| MAIL-23's landed `EfApprovedMailboxStore`/binding persistence | Establishes configuration version, exact mailbox scope and unavailable behavior. MAIL-05 reads through the Core port and does not modify this store. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Existing operational-destination labels and any logical-folder labels landed by MAIL-23; one display list per concept. |
| `src/Pegasus.Web/Mcp/MailMcpTools.cs` | A second existing caller of `GetRetainedMail`; do not expand its public tool contract here because [[AUTO-003]] owns Automation completion. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Governs the three separate facts, 13 logical folder types, current-policy recommendation, explicit later confirmation and no arbitrary destination. |
| EPIC-006 `context.md` | One Core implementation and no local-alpha Outlook mutation. |
| [[TICK-044]], [[TICK-046]], and [[TICK-064]] research/proof | Prove the operational policy/caller, current correction dossier, and prerequisite folder policy/binding boundary. |

## Ripple effects and exact overlaps

- Hard dependency: [[TICK-064]] now structurally blocks this ticket. It must land first; then MAIL-05 research/file map and plan refresh to its actual symbols.
- `RetainedMail.cs` overlaps [[TICK-049]], [[TICK-050]], [[TICK-053]], and downstream [[TICK-056]].
- `Mail/Message.cshtml.cs` overlaps [[TICK-049]], [[TICK-050]], [[TICK-051]], [[TICK-052]], [[TICK-054]], [[TICK-057]], and [[TICK-088]].
- `MailWorkspaceWebTests.cs` overlaps [[TICK-053]], [[TICK-056]], and [[TICK-057]].
- MAIL-23 owns approved-mailbox policy/persistence/admin/migration/Graph-resolution files. MAIL-05 deliberately avoids them after merge, reducing overlap to the consuming read model/UI.
- MAIL-07 is the downstream confirmation/move ticket and must consume the recommendation/provenance produced here. [[AUTO-003]] follows the landed Core use cases.
- `docs/capabilities.md` changes only when delivery evidence changes MAIL-05's row. Deployment/current-state updates belong to the release ticket.

## Out of scope

No taxonomy or operational-queue change; no approved-mailbox binding/configuration work; no recommendation persistence or migration; no correction, confirmation, operation key, concurrency write, history append, folder move, retry, Graph call, arbitrary folder input, bulk action, MCP tool, generic action framework, deployment, or live Outlook mutation.
