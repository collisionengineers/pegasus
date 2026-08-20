# Post-implementation report — MCP-05 (TICK-062)

## What was delivered

The classified-email workspace is now reachable by the Automation Actor through the gated `/mcp` host, as thin tools over the same Core use cases the staff mail pages call, behind a new per-area `automation.mail` scope:

- `pegasus_mail_list` — one page of retained mail (mailbox/folder/page scope) with the mailbox list and workspace freshness; `ListRetainedMail` + `GetRetainedMailFreshness`.
- `pegasus_mail_get` — one retained message: recipients, body text, attachment metadata, thread, folder, and the versioned classification dossier with permanent correction history and the policy-computed operational destination; `GetRetainedMail` + `MailOperationalDestinationPolicy.Map`.
- `pegasus_mail_correct_classification` — the one staff-equivalent mutation, `CorrectRetainedMailClassification`: exact message, expected version, canonical key, reason, `mcp:` operation key; prior decision preserved in permanent history; attributed `automation:pegasus-automation`.

No Outlook, Graph, or mailbox mutation is reachable from any of these tools — reads are of the Pegasus-retained record only, and transport mutation remains a separately approved capability (owning MAIL tickets).

## Files changed

- `src/Pegasus.Web/Mcp/MailMcpTools.cs` (new)
- `src/Pegasus.Web/Mcp/AutomationMcp.cs` (`MailScope` added to the one scope list)
- `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` (`WithTools<MailMcpTools>()`)
- `src/Pegasus.Web/Presentation/MailClassificationSelection.cs` (new — the single classification-selection vocabulary/parser)
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` (delegates to the shared selection helper; behaviour-preserving)
- `tests/Pegasus.IntegrationTests/AutomationMailIngressTests.cs` (new)
- `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` (tool inventory + mail tools)
- `tests/Pegasus.IntegrationTests/AutomationMcpTestSupport.cs` (`AllScopes` + `automation.mail`)

## Commands and results

- `dotnet restore ./Pegasus.slnx` then `dotnet build ./Pegasus.slnx -c Release --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/Pegasus.IntegrationTests -c Release --filter "FullyQualifiedName~AutomationMailIngressTests|FullyQualifiedName~AutomationMcpIngressTests|FullyQualifiedName~MailWorkspaceWebTests"`: 23/23 passed (after one honest fixture-assertion fix: category names are the kebab taxonomy names).

Evidence covered: scope denial with security event, tool inventory, list/detail parity with the staff queries, freshness, content-safe not-found, version-conflict refusal, non-canonical-key refusal, unclassified refusal, operation-key requirement, attributed succeeded/failed action history, and post-correction dossier state.

## Residual risks and qualification

- Local integration evidence only: no deployment, no live Automation MCP client run, no operator acceptance is claimed. The post-deployment live inventory run remains open on the checklist.
- The broader operator inventory (folder move, Case association, read state, flags, delete/restore, compose/send) is deliberately absent: those Core owners have not landed; each follows its owning MAIL capability. Nothing here grants arbitrary folder/delete/send authority.
- No migration, no schema change, no new configuration: the new scope is granted through the existing canonical client descriptor at its next reconciliation.
