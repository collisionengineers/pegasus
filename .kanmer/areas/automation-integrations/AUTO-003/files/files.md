# Files — complete MCP-05 email actions

## Change surface

| Path | Purpose / risk |
| --- | --- |
| `src/Pegasus.Web/Mcp/MailMcpTools.cs` | Extend the existing mail tool surface; keep orchestration thin and typed |
| `tests/Pegasus.IntegrationTests/AutomationMailIngressTests.cs` | Prove authorization, exact identity, operation keys, parity and durable attribution |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Keep the canonical tool inventory accurate |
| Owning MAIL tickets' future Core contracts | Reuse only after they land; exact paths must be refreshed before planning |

## Context files

| Path | What it establishes |
| --- | --- |
| `src/Pegasus.Web/Mcp/AutomationMcp.cs` | One existing `automation.mail` scope; do not create per-action scopes without a proven requirement |
| `src/Pegasus.Web/Mcp/AutomationMcpErrors.cs` | Existing content-safe error convention |
| `src/Pegasus.Web/Mcp/AutomationActorResolver.cs` | Existing principal/actor boundary |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Staff caller whose Core behavior the tools must match, not copy |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Mail behavior and external-write constraints |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | Automation authorization, attribution and tool behavior |
| EPIC-006 `context.md` | One Core owner; no unapproved mailbox mutation |

## Ripple and conflict notes

This ticket overlaps the MCP files above but should not overlap the underlying MAIL tickets if those tickets keep business contracts in Core and Web/Infrastructure. Plan only after those tickets land so the final file map names their actual types.

## Out of scope

No generic mail-action framework, duplicate taxonomy/mapping/authorization, direct Graph client, new scope registry, UI implementation, arbitrary folder/recipient input, live Outlook write, or dormant tool for an unlanded Core use case.
