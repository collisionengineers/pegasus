---
id: ADR-0080
status: proposed
date: 2026-10-22
supersedes: []
superseded_by: []
related_capabilities: [MCP-01, MCP-02, MCP-03, MCP-04, MCP-05, MCP-06]
related_frd: [frd-10]
tags: [mcp, automation, oauth]
---

# ADR-0080: One Automation MCP server per scope

## Status

Proposed on 22 October 2026; not accepted. Issue #1487 asks for a new ADR
on splitting the Automation MCP server. This record refines ADR-0011,
ADR-0026, ADR-0027 and ADR-0059 and replaces none of them: the one server at
`/mcp` is source structure that no ADR decided. Every decision clause below
is a proposal. It takes OQ-134's recommendation (21 October 2026), which is
a recommendation, not an operator ruling: "an ADR proposing one server per
scope with `Instructions`, tools filtered by token scope, no new tools until
the split lands". Acceptance is the operator's. The answers to the questions
under Decision are written in at acceptance, with their ruling, and the code
waits for them.

## Context

Observed at `95a607711`. Pegasus hosts one MCP server, `pegasus-automation`,
which `AddPegasusAutomationMcp` registers as stateless streamable HTTP and
`MapPegasusAutomationMcp` maps at `AutomationMcp.McpEndpointPath` (`/mcp`).
Its 17 tool classes register 65 tools;
[FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#tool-inventory)
owns the inventory. By the scope each tool's call requires: cases 8, intake
22, documents 10, assessment 10, mail 6, jobs 4 and send 2, plus the three
edit-lease tools, whose scope follows `recordKind` (cases for a Case, intake
for a Triage Case). `CaseEditMcpTools` holds four cases tools and the three
valuation tools of assessment, so a class is not a unit of assignment. A
scope groups tools, not records: `pegasus_triage_case_link` (intake) also
claims the instruction Case's lease.

Every token lists all 65 tools in `tools/list`, whatever scopes it carries.
`AutomationActorResolver.RequireAsync` checks the scope when a tool is
called and records a refusal as `automation_scope_denied`; the `/mcp`
endpoint policy needs the audience and any one scope. The server sets no
`ServerInstructions` and maps no resource or prompt.

The rest of the ingress is single too: one client registration, holding all
seven scopes; one audience, `AutomationMcp.Audience`, which every token
carries in place of a path; one RFC 8707 resource, the `/mcp` URI,
registered with OpenIddict and permitted to the client; one
protected-resource metadata document, whose `scopes_supported` lists all
seven scopes; one feature gate and kill switch; one rate-limit policy, 120
requests a minute per IP address. Consent is per scope on the Administrator
consent page, and each approval is a new grant whose refresh token lasts
fourteen days without sliding
([ADR-0027](0027-authorization-code-for-external-mcp-connectors.md)).
`Program.cs` keys three behaviours on a path that starts with `/mcp`: the
rate-limit reason code, the ingress middleware that records transport
denials, and machine-surface error handling. A server path under `/mcp/`
keeps all three.

Callers of `/mcp`: in the repository, the mail classifier agent (it holds
`automation.mail` only and reads `PEGASUS_MCP_URL`), the release skill's
step that re-adds the connector at a new URL, and the integration tests,
through `AutomationMcpTestSupport.PostMcpAsync` and the inventory
`AutomationMcpIngressTests` pins; outside it, the claude.ai connector,
Claude Desktop's AI-job loop and any plugin skill.

Description weight, counted statically over the tool classes' `[Description]`
text with shared constants resolved (JSON schemas and titles not counted):
about 61,500 characters in 477 descriptions. The mail tools carry about 6%,
so the classifier lists about 94% text for tools it cannot call. Text
repeating an earlier description is about 9,400 characters (15%); the rest
is per-tool prose, which server instructions cannot replace and only a
shorter list removes.

The SDK, `ModelContextProtocol.AspNetCore` 2.2.0, read from the package's
documentation and not exercised: `McpServerOptions.ServerInstructions` is
sent at initialization; `ConfigureSessionOptions` sets server options from
the request's `HttpContext` and, in stateless mode, runs on every request;
`AddAuthorizationFilters` honours `[Authorize]` on tools, filters list
results and adds a call-tool authorization checkpoint;
`OnResourceMetadataRequest` varies the metadata per request, and with
`ResourceMetadataUri` left null the handler mirrors the requested resource
path. That one host serves more than one server configuration is not proved.

The boundary the split keeps.
[FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary)
names one Automation Actor and no number of servers; ADR-0021 and ADR-0077
name `/mcp` only to say what is gated and what is compressed. The Actor has
"no Administrator, configuration, credential, cloud, release, deletion, or
other management authority", and "Any future expansion of MCP access to
people or another automation identity requires a new accepted decision and
explicit Core authorization contract"
([ADR-0011](0011-restrict-mcp-to-automation-actor.md)). Pegasus "does not
create a new Pegasus-side tool-permission policy or take ownership of the
external MCP client's client-selection or tool-access policy"
([ADR-0026](0026-enable-automation-mcp-by-explicit-deployment-configuration.md)).
`automation.send` exists "so a grant can hold casework without sending"
([ADR-0064](0064-automation-actor-staff-casework-parity.md) decision 9).

## Decision

Each clause is a proposal.

1. **Servers (proposed).** The servers are the rows of one table keyed by
   scope set. Each row has its own path under `/mcp/`, its own
   `ServerInfo.Name`, its own `ServerInstructions`, its own endpoint policy
   and its own protected-resource metadata. The default is one row per scope
   in `AutomationMcp.Scopes`, seven servers; the named alternative is a
   bundle, a row holding several scopes (Q1). A tool is on the server whose
   scope set holds the scope its call requires, assigned tool by tool; the
   three edit-lease tools are on the cases and the intake servers.
2. **Instructions (proposed).** A server's `ServerInstructions` restate only
   cross-tool rules FRD-10 already gives for that server's tools: the
   one-command lease and the edit lease token, `expectedVersion`,
   `operationKey`, paging and native content. They make no rule. The words
   are agent-facing copy, shown to the operator for approval in the code PR.
   This record shortens no tool description; the code PR measures the text
   the descriptions repeat at its base and puts any shorter wording to the
   operator.
3. **Listing (proposed).** On every server, `tools/list` holds only the
   tools whose scope the token carries. The list is disclosure, not
   authority: `AutomationActorResolver.RequireAsync` stays the call gate,
   with its refusal and its `automation_scope_denied` record. The list and
   the gate read one tool-to-scope table, so a scope is declared once, and a
   filtered list is built once per scope set, not on each request. Two
   stages: Stage 1 filters the list on today's `/mcp`; Stage 2 is the split
   (Q4).
4. **Shared plumbing (proposed).** The one client registration, audience,
   token endpoint, kill switch, rate-limit policy, Administrator consent
   page, feature gate and the two content routes stay as they are. Each
   server adds its own registered resource, the client's permission for it,
   and metadata whose `scopes_supported` lists only that server's scopes. A
   server's endpoint policy requires the shared audience and one of its own
   scopes: scope, not audience, separates the servers.
5. **No Administration or read-only server (proposed).** An Administration
   server needs authority ADR-0011 and FRD-10 withhold from the Actor
   (ADR-0064 decision 1), so it needs its own accepted decision and Core
   authorization contract. A read-only server is not read-only under today's
   scopes, since one `automation.cases` token reads `pegasus_case_get` and
   writes `pegasus_case_update_details`; it needs read scopes, a new
   Pegasus-side tool-permission policy, which ADR-0026 says Pegasus does not
   create. The split along today's scopes creates none: each server lists
   tools by the scope their call already requires.
6. **Migration (proposed).** `/mcp` is replaced, not aliased, in the release
   that updates its consumers
   ([project principles](../../AGENTS.md#project-principles)). In the
   repository: the classifier's `PEGASUS_MCP_URL` (its Bicep setting,
   `config.ts` and `.env.example`), the release skill's connector step and
   the tests that name `/mcp`. Outside it: the claude.ai connector, which an
   Administrator re-adds once per server and consents to once for each;
   Claude Desktop's AI-job loop; and any plugin skill, which the operator
   lists. Today's grants name the one registered resource, `/mcp`, while
   tokens carry the shared audience, not the path: whether a token or
   refresh token issued before the split works on a new server is stated
   from a test in the code PR, not assumed.
7. **Order (proposed).** No new tool until the split lands (OQ-134). The
   issue's further tools, resources and prompts wait behind it; Q7 asks how
   far the rule reaches.

### Questions for the operator

Each question is in the operator's decisions register. The default is what
this proposal takes until the operator answers.

- **Q1 (OQ-134).** Which servers, and named `pegasus-` or `pegasus_`? Seven
  by scope, or fewer with bundled rows? A documents, assessment or send
  server is not usable without the cases server: their descriptions take
  Case ids and vocabularies from `pegasus_case_get`, `pegasus_case_search`
  and `pegasus_vocabulary_get`. Jobs name no cases tool, and mail needs none
  (`pegasus_mail_link_destinations` returns the Case ids and versions).
  `pegasus_edit_begin`, which the write descriptions name, is optional: a
  write without its token holds the lease for one command. A bundle keeps
  such a dependency inside one server. Default: seven,
  `pegasus-<scope word>` at `/mcp/<scope word>`.
- **Q2 (OQ-197).** Does any admin tool need a separate audience, or only a
  scope? Default: no admin server (decision 5); one audience.
- **Q3 (OQ-198).** Does the consent page list servers, scopes or both?
  Default: scopes; the page is unchanged.
- **Q4 (OQ-199).** Filter first and split later, or stop after filtering?
  Filtering alone changes nothing for a connector that holds all seven
  scopes: today's metadata advertises all seven and consent grants what the
  connector asks for, so it still lists 65 tools (assumed, not verified:
  that claude.ai asks for the advertised scopes). It shrinks the list only
  for a client that asks for fewer, such as the classifier, which would
  list the six mail tools. A smaller view for an all-scope connector comes
  from each server's own `scopes_supported`, which only the split gives.
  Default: both stages; the operator may stop after Stage 1.
- **Q5 (OQ-200).** Keep `/mcp` as an alias for a period after the split?
  Default: no.
- **Q6 (OQ-201).** Duplicate the shared reads, `pegasus_case_get` and
  `pegasus_vocabulary_get`, onto each scope server? Default: no.
- **Q7 (OQ-202).** Do issue #1474's new tools and issue #1488's `set_type`
  wait for the split? Default: #1474's tools wait, unless OQ-133 is answered
  with a route that adds no tool; `set_type` is an action value of an
  existing tool, not a tool, and does not wait.

## Consequences

- An agent that works a Case end to end connects up to seven servers: up to
  seven connectors, each consented by an Administrator at least fortnightly.
  A bundle reduces both.
- The reads every writer needs, `pegasus_case_get` and
  `pegasus_vocabulary_get`, stay on the cases server, which a documents,
  assessment or send client connects beside its own (Q1, Q6).
- One audience stays, so scope remains the only boundary between servers:
  the split cuts context weight, not authority. A grant without
  `automation.send` already sends nothing (FRD-10); a separate send server
  saves that server's text and adds a consent, but adds no isolation.
- The rate limit is per IP address and shared by every server, so each
  extra initialize and list request spends it; the code PR counts them
  against the 120 a minute.
- `ConfigureSessionOptions` runs on every request in stateless mode, and
  `AddAuthorizationFilters` adds a call checkpoint that refuses a call
  before `RequireAsync` records the refusal. Whichever the code uses, a
  request costs no more than a lookup of a prebuilt tool set, and the
  resolver stays the call gate or the code PR states the change.
- FRD-10 stays the inventory's owner and, at the split, names each server's
  tools; `docs/current-architecture.md` changes with the source, and
  `docs/operations.md` at the release.
- No data changes and no migration. The client id stays the Actor's
  identity in history; only the server names and paths change.
- Live evidence is owed at the code release and recorded in operations: a
  connector re-added per server honouring its `scopes_supported`, the
  instructions reaching the client, the classifier on its new URL, and the
  `tools/list` size before and after.

## Options considered

**One endpoint, filtered list only (the issue's mechanism 2).** The
cheapest: no URL or client change. It gives no smaller server to connect, no
separate consent, and nothing for a connector holding all seven scopes (Q4).
Kept as Stage 1 and as a stop point the operator may choose.

**Bundled servers.** Fewer connectors and consents, and a dependency on the
cases server stays inside one server; a bundle still filters its own list by
the scopes held. The named alternative under Q1: membership is a table row,
so it can change later without a redesign.

**By pipeline stage (intake, Case, send).** A particular bundle of today's
scopes, so Q1 can choose it; not a separate design.

**By audience (worker and admin servers).** The admin half needs authority
the Actor does not hold (decision 5): a new decision and Core authorization
contract, not an ingress change.

**By safety (a read-only server).** 25 of the 65 tools are marked read-only,
but no scope is: a read-only server needs read scopes, a new Pegasus-side
tool-permission policy (decision 5).

## Links

- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [ADR-0011](0011-restrict-mcp-to-automation-actor.md),
  [ADR-0026](0026-enable-automation-mcp-by-explicit-deployment-configuration.md),
  [ADR-0027](0027-authorization-code-for-external-mcp-connectors.md),
  [ADR-0059](0059-native-mcp-file-content-and-consolidated-tool-inventory.md),
  [ADR-0064](0064-automation-actor-staff-casework-parity.md)
- [Current architecture](../current-architecture.md)
