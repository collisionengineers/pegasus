# Files — MAIL-05

## Change surface

| Path | Purpose / risk |
| --- | --- |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | Core-owned contract/policy; reuse existing vocabulary and avoid a second business implementation |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs` | Persistence or external adapter boundary; preserve mailbox scope, idempotency and durable history |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Real staff or Automation caller; thin orchestration only |
| `tests/Pegasus.Core.Tests/Intake/Classification/MailTaxonomyTests.cs` | Focused acceptance and regression evidence |
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
