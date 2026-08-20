# Files — UI-14 post-merge map

Base: origin/dev 4baae5f0.

## Change files

| Path | Narrow change and reuse |
| --- | --- |
| src/Pegasus.Core/Intake/RetainedMail.cs | Extend MailWorkspaceScope with zero-or-one destination/detail filter; validate through ListRetainedMail. |
| src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs | Expose one policy-owned query criterion per aggregate destination and have Map consume it; no second mapping list. |
| src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs | Translate the policy criterion/exact category against existing decision columns before SQL count/paging; project current classification/destination after paging with EfIntakeReceiptStore.MapMailClassificationDecision. |
| src/Pegasus.Web/Pages/Mail/Index.cshtml.cs | Parse one queue key using existing MailClassificationSelection and pass the Core scope; preserve the key in refresh context. |
| src/Pegasus.Web/Pages/Mail/Index.cshtml | Accessible queue/detail navigation, active filter and row classification/destination; preserve queue through message links and pagination. |
| src/Pegasus.Web/Pages/Mail/Message.cshtml.cs | Bind and carry the originating queue key through existing redirects/return-context helpers only. |
| src/Pegasus.Web/Pages/Mail/Message.cshtml | Preserve queue through Back, correction, move and association links/forms; no new action. |
| tests/Pegasus.Core.Tests/Intake/Classification/MailOperationalDestinationPolicyTests.cs | Prove criteria and Map agree, especially Unidentified versus Triage. |
| tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs | Prove destination/detail filtering, corrected current decision, mailbox/folder scope and SQL count/page behavior. |
| tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs | Authenticated accessible navigation, visible row projection, invalid-filter refusal and exact list/detail context preservation. |
| docs/capabilities.md | Reconcile UI-14 wording/evidence to the delivered local read-only slice. |
| docs/design/README.md | Replace the remaining operator-facing broad Needs sorting wording only where it means Unidentified. |

## Context files

| Path | Constraint |
| --- | --- |
| docs/frd/frd-08-email-mailbox-and-background-processing.md | Canonical catalogue, distinct facts, filter preservation and SQL-honest counts. |
| docs/operator-notes.md; docs/prd/pegasus-product.md | Unidentified replaces only broad Needs sorting; Triage remains distinct. |
| src/Pegasus.Web/Presentation/MailClassificationSelection.cs | Existing canonical presentation list/parser; do not add another. |
| src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs | MailTaxonomy is the category/subtype owner. |
| landed TICK-053/049/050/051/052 symbols | Preserve search, folder, advisory and Case-association context. |

## Out of scope

No persisted destination, EF model/migration, taxonomy edit, second mapping, new store/service/framework, mailbox mutation, action matrix, Automation/MCP, deployment or live evidence.
