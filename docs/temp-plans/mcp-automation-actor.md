# MCP Automation Actor ingress (MCP-01/02/03/04)

Status: design stage. The worktree, claim, and this design are in place;
implementation starts only after the operator decisions in
[section 1.10](#110-decisions-requiring-operator-sign-off) are made. Parts 2
and 3 are research/design proposals only — they claim no capability, caller,
or activation, and their durable outcomes would be promoted to
open-decisions lines or ADRs before this plan file is deleted post-merge.

---

## Part 1 — Automation Actor MCP server: full design

### 1.1 Scope

Build the management/development-controlled MCP ingress for one named,
vendor-neutral Automation Actor that invokes existing `Pegasus.Core` use
cases, per [ADR-0011](../adr/0011-restrict-mcp-to-automation-actor.md) and
[ADR-0013](../adr/0013-qdos-alpha-implementation-contract.md) clause 10:

- MCP-01: the ingress itself — actor identity, authentication, scopes,
  rate limits, attribution, permanent history.
- MCP-02: Automation Actor Case actions through the same Core use cases as
  staff.
- MCP-03: Automation Actor intake-queue actions through the same Core use
  cases as staff.
- MCP-04: Automation Actor document actions through the same Core use
  cases as staff.

Out of scope for this task:

- MCP-05 (broader classified-email workspace actions) — `Next`/`0.3.0`,
  blocked on the email workspace itself.
- AI-09 (`Send to AI` proposal transport) — separate `1.3.0` capability;
  the Automation Actor's tools invoke ordinary operational Core actions
  only and return no AI proposal.
- The external client itself (the "externally scheduled automation client
  scanning an approved network-drive scope"): that client, its schedule,
  and the filesystem it reads stay outside Pegasus per requirements;
  custody begins only at an authenticated accepted MCP submission. This
  task builds the ingress the client would call, not the client.
- Vendor/tool selection, credential provisioning, deployment, or live
  activation. Production exposure remains configuration-gated off until
  separately approved.
- Any Administrator, configuration, credential, cloud, release, or
  deletion authority for the Actor (excluded by ADR-0011).

### 1.2 Why this is not greenfield

A full per-staff-OAuth MCP surface was built and merged to `dev`
(`a8d5991`, 2026-07-29) and deliberately deleted (`4c7e0ac`, 2026-07-30)
when ADR-0011 rejected the per-staff actor model. The deleted code
(`src/Pegasus.Web/Mcp/*McpTools.cs`, `StaffMcpAuthorization.cs`,
`StaffMcpExtensions.cs`) remains useful prior art for tool-schema shape
and DI wiring (`AddMcpServer().WithTools<T>()`, `MapMcp("/mcp")`,
`ModelContextProtocol.AspNetCore` package, stateless HTTP transport,
rate-limited endpoint), but its authorization layer — per-staff OAuth
scopes resolving the HTTP user into a staff `ActionActor` — is exactly
what ADR-0011 forbids rebuilding.

### 1.3 Deployment shape

- Remote streamable-HTTP MCP server hosted inside `src/Pegasus.Web/` (the
  existing HTTP composition root) at `/mcp`, per architecture.md's
  "Provider API and Automation MCP are separate Web ingress boundaries."
  No new project or deployment unit (would need an ADR; none is
  justified — Web already carries HTTP ingress, auth, and rate limiting).
- Official C# MCP SDK, `ModelContextProtocol.AspNetCore` NuGet package.
  Current stable is 2.0.0 (released 2026-07-28): a breaking release aligned
  to the newer MCP spec revision, stateless-by-default HTTP, and hardened
  OAuth metadata handling (explicit PKCE S256, RFC 9207 issuer
  validation). Recommended: pin 2.0.0 via `packages.lock.json`. Its
  deprecations (Roots/Sampling/Logging surfaces) do not affect a
  tools-only server; fall back to the mature 1.4.1 line only if 2.0.0
  shows instability during implementation. (Prior art used 1.4.1;
  `workspaces/ai-centre` reference code already uses 2.0.0.)
- Stateless HTTP transport (stateless is the 2.0.0 default; the prior art
  opted in explicitly), avoiding server-held session affinity. Server-to-
  client sampling needs stateful transport, but this ingress makes no
  sampling requests.
- The entire ingress is composition-gated: endpoints are mapped only when
  an explicit configuration flag enables them (default off in every
  profile except local development evidence runs). The application today
  "fails closed by exposing no such ingress" (operations.md); the flag
  preserves that posture in production until separately approved
  activation.

### 1.4 Actor identity (Core changes)

- Add `ActorKind.Automation` to
  `src/Pegasus.Core/Identity/IdentityContracts.cs` and a factory
  `ActionActor.Automation(string actorId)`. The durable actor identity is
  data (the registered client's stable actor id), not a hard-coded name,
  mirroring how `SystemWorker("approved-inbox-poller")` works today —
  but exactly one Automation client registration is expected and the
  design assumes no multi-actor fan-out (ADR-0011: "one named,
  vendor-neutral Automation Actor").
- Fail-closed authorization: extend the single authorization switch in
  `src/Pegasus.Core/Identity/StaffAuthorization.cs` (or a sibling
  `AutomationAuthorization` if mixing kinds muddies the switch — decide at
  implementation, same file ownership either way) so the Automation kind
  is granted only the ordinary operational surface (the
  `PerformCasework`-shaped set) and is explicitly denied
  `ManageStaffAccounts`, `ReviewStaffAccess`, `AssignStaffRoles`,
  `ManageOrganizationsAndPrincipals`, `ManageWorkflowConfiguration`,
  `ManageApprovedMailboxes`, `ExecuteSystemWork`, and
  `SubmitRequestUpload`. Anything not explicitly granted is denied.
- Use cases that self-check `actor.Kind` keep doing so; where a use case
  in the approved inventory must accept the Automation kind, that check is
  extended deliberately per use case, never globally.

### 1.5 Authentication

Recommended: OAuth 2.0 client credentials issued by OpenIddict hosted in
`Pegasus.Web`, validated on the `/mcp` endpoint.

- The dormant OpenIddict tables from migration
  `20260729150000_DocumentCustodyAndRequests` become live for exactly one
  client registration class: the Automation Actor client (client id +
  secret; only the secret hash stored; rotation and revocation
  supported). This mirrors the accepted provider-API credential shape in
  ADR-0004's surviving half.
- The staff role access matrix already grants Administrators "accepted
  OAuth-client registration/revocation", so client administration is an
  Administrator staff action with permanent history; no new role is
  invented. A minimal Administrator surface (list/register/revoke, no
  secret display after creation) ships with this task or the registration
  is seeded by migration + configuration — decision 1.10(b).
- Scopes: one coarse `pegasus.automation` scope minimum; optionally
  per-area scopes (`automation.cases`, `automation.intake`,
  `automation.documents`) so the token itself cannot exceed the approved
  inventory. Recommendation: ship per-area scopes now — they are cheap
  and make the approved-inventory boundary token-enforceable.
- Endpoint wiring follows the SDK's documented protected-server shape:
  JWT bearer validation as the authenticate scheme (OpenIddict-issued
  tokens validate directly) plus the SDK's `AddMcp` challenge scheme
  serving RFC 9728 protected-resource metadata, so an unauthenticated
  call gets `401` with `WWW-Authenticate: Bearer resource_metadata="…"` —
  the discovery handshake Anthropic clients document. Clients that
  support static bearer headers (Claude Code `--header` /
  `headersHelper`) can also present the token directly. Exact client
  mechanics: 1.10(c).
- A staff browser cookie/identity is never accepted on `/mcp`
  (operations.md: "A staff browser identity is not a substitute for that
  actor"). Antiforgery does not apply (no cookies); the endpoint is
  bearer-only.

### 1.6 Tool inventory (one tool per action)

Pattern: one tool per action. The action space is small (< 15), each tool
wraps exactly one existing Core use-case interface, and tool schemas
mirror the command/query shape. No search+execute indirection, no batch
tools, no parallel policy.

The exact approved inventory is an operator decision (requirements:
"invokes only its approved ordinary operational Core-action inventory").
Proposed v1 inventory for approval, enumerated from existing Core
interfaces at implementation time:

| Area (capability) | Tool (proposed name) | Wraps | Annotations |
| --- | --- | --- | --- |
| Case (MCP-02) | `pegasus_case_search` | existing case search/query use case | read-only, idempotent |
| Case (MCP-02) | `pegasus_case_get` | existing case detail query | read-only, idempotent |
| Case (MCP-02) | `pegasus_case_edit_begin` / `pegasus_case_edit_end` | the server-owned edit-lease acquire/release | not read-only, idempotent |
| Intake queue (MCP-03) | `pegasus_intake_queue_list` | existing intake/`Needs sorting`/`Blocked intake` queue queries | read-only, idempotent |
| Intake queue (MCP-03) | `pegasus_intake_submit` | the Core intake receipt use case (immutable source occurrence submission — the document-action route the external scan client would use) | not read-only, idempotent per source occurrence |
| Documents (MCP-04) | `pegasus_document_add` | `IAddCaseDocument` | not read-only, idempotent per `OperationKey` |
| Documents (MCP-04) | `pegasus_document_download` | `IDownloadCaseDocument` | read-only, idempotent |
| Documents (MCP-04) | `pegasus_document_export` | `IExportCaseDocuments` | read-only, idempotent |

Inventory rules:

- Case mutations go through the same edit-lease and version guard as Web
  (requirements: "Web and MCP Automation Actor callers use the same
  guard"), which is why lease acquire/release are tools: a mutation tool
  call presents the lease token and expected Case version like any staff
  save. A missing/expired/stale presentation fails closed with the same
  Core refusal.
- Case lifecycle transitions (close, reopen, hold, review) are excluded
  from v1 pending explicit operator approval of each named action.
- Every mutation tool takes an explicit idempotency `OperationKey`
  (prefixed `mcp:`), mirroring the existing command contracts.
- File content crosses the boundary base64-encoded with the same
  size/type limits the Web upload path enforces; oversized content fails
  closed with a typed error, no partial custody. Download/export results
  also respect client-side tool-result caps (Claude Code defaults to
  25k tokens per tool result, raisable via `MAX_MCP_OUTPUT_TOKENS` or
  per-tool via `_meta["anthropic/maxResultSizeChars"]` up to 500k chars;
  claude.ai/Desktop ~150k characters), so large content returns a
  bounded summary plus a retrieval handle rather than overflowing
  silently.
- Tool annotations use the C# SDK's verified `[McpServerTool]` attribute
  properties — `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld`,
  plus `UseStructuredContent` for typed results — which the SDK maps
  onto the MCP `ToolAnnotations` hints.

### 1.7 Attribution, history, and telemetry

- Every tool call resolves the authenticated client principal to
  `ActionActor.Automation(actorId)` before touching Core; the Core use
  cases already write `IActionHistoryWriter` entries carrying that actor,
  outcome, correlation id, and before/after evidence — no parallel audit
  path.
- Authorization failures and token rejections on `/mcp` write
  `ISecurityEventWriter` events (material denial is attributable
  permanent history per requirements); routine token issuance/refresh and
  transport mechanics stay content-safe telemetry.
- Rate limiting: a dedicated ASP.NET rate-limiter policy on the MCP
  endpoint group (prior art had this; ADR-0013 clause 10 names rate
  limits as part of the boundary).
- MCP registration, tool schema, or endpoint presence is asserted
  nowhere as proof; the evidence plan (1.9) is the only claim route.

### 1.8 Implementation slices and file-level changes

1. Core actor identity: `IdentityContracts.cs` (`ActorKind.Automation`,
   factory), authorization switch extension, unit tests proving the
   denied-set fails closed.
2. AuthN plumbing: OpenIddict server + validation wiring in
   `Pegasus.Web`, client registration path (per 1.10(b)), configuration
   gate, integration tests for token issuance and rejection.
3. MCP endpoint: `src/Pegasus.Web/Mcp/` tool classes wrapping the
   approved inventory, `AddMcpServer().WithTools<...>()`,
   `MapMcp("/mcp").RequireAuthorization(...).RequireRateLimiting(...)`,
   package reference + lock-file update.
4. Tests: Core authorization tests; integration tests driving the MCP
   endpoint over HTTP (handshake, tool list, success call, authorization
   failure, validation failure, action-history assertion);
   `Pegasus.ArchitectureTests` project-reference pin updated for the new
   package (and still excluding `workspaces/*`).
5. Docs: `docs/architecture.md` and `docs/operations.md` updated from
   "not implemented" to the implemented-but-gated state, with the exact
   evidence recorded; `docs/capabilities.md` untouched (allocation
   already correct).

### 1.9 Verification and evidence plan

- `dotnet restore`, `dotnet build --configuration Release`, focused then
  full `dotnet test`.
- Evidence tier 5 (operations.md "Required evidence tiers"): an exercised
  real caller reaching Core through `/mcp` — expected success,
  authorization failure, and validation failure, each with action-history
  proof. The initial real-client evidence run uses the client selected in
  1.10(c) against a locally run `Pegasus.Web`; integration tests provide
  the repeatable equivalent.
- No deployment, live-verification, or acceptance claim is made by this
  task; the configuration gate stays off outside local evidence runs.

### 1.10 Decisions requiring operator sign-off

a. **Authentication mechanism** — recommended: OpenIddict client
   credentials as in 1.5. Alternative: static long-lived bearer secret
   (rejected: no rotation story, no scope enforcement); authorization-code
   flow with a management-held account (rejected as primary: reintroduces
   a user-shaped login for a machine identity; may still be needed for
   specific clients — see 1.10(c)).
b. **Client registration path** — Administrator UI surface now, or
   migration/configuration-seeded single registration now with the UI
   deferred. Recommended: seeded registration + read-only Administrator
   visibility now; full register/revoke UI as a follow-up line.
c. **Initial evidence client** — recommended: Claude Code. Its
   documented `--header "Authorization: Bearer …"` and `headersHelper`
   (a script run fresh at each connection that exchanges the client
   id/secret for a short-lived token; from Claude Code v2.1.193 a
   401/403 automatically re-runs the helper, reconnects, and retries
   once) fit a machine credential exactly, with no beta gate. Research finding: claude.ai /
   Claude Desktop custom connectors explicitly do **not** support the
   client-credentials grant ("every connection requires user consent");
   their non-OAuth option (`static_headers`, admin-entered bearer) is
   beta/early-access. ADR-0011's "Claude Desktop may provide initial
   client evidence" is permissive wording, and ADR-0013 already allows
   any compatible client, so no doc conflict — but the practical first
   client is Claude Code or a scripted MCP client, and the plan review
   should record that finding.
d. **v1 tool inventory** — approve or amend the table in 1.6, in
   particular whether any Case lifecycle transition belongs in v1.

---

## Part 2 — Proposal A: two-way in-app AI assistant (research)

Maps to AI-01 "In-app staff AI assistant" (`Later`/`0.6.0`,
"individually approved operator AI assistance"; operator truth CAP-015
"Provide in-app AI features. Not in 0.1.0-alpha.1."). This proposal is
paper only: nothing here creates a route, control, placeholder, caller,
credential, or activation.

### 2.1 Binding constraints found

- The assistant is a **named UI absence** today: design/README.md lists
  "an in-app AI assistant" among things with "no alpha control, route or
  placeholder", and every deferred UI capability "must re-enter
  specification, alternatives, independent review, explicit approval,
  visual generation and manual visual review before implementation."
- **Staff must not gain MCP access.** AI-01's requirements anchor is the
  MCP-boundary section itself ("Ordinary staff have no MCP access and use
  the Web UI"); design/README adds "Provider APIs and MCP are non-browser
  boundaries and do not create staff-shell destinations." So the phrase
  "use the automated actor connector" has a narrow safe reading: the
  assistant backend may not become a staff-to-MCP side door, and staff
  work may not be attributed to the Automation Actor (nor the Actor
  impersonate staff — both directions are forbidden).
- **Proposal-only AI**: "no AI caller mutates, approves, or sends
  autonomously"; proposals remain proposals until an authorised human
  accepts through Core; field provenance already models "AI prefill or
  proposal" as an origin.
- **No real-time plumbing exists**: Pegasus.Web has zero JavaScript, no
  API controllers, no SSE/SignalR; ADR-0002 rejected Blazor
  Server/SignalR and SPA "without a demonstrated interaction
  requirement". An assistant is exactly such a requirement — but
  changing that stance is an ADR-level decision, not a design detail.
- **Per-activation gates**: external AI processing needs processor-terms
  confirmation plus data/licence/cost/security approval per operation;
  cost has no fixed ceiling (alert-only £75 Azure budget; material spend
  needs a named expenditure owner). A shared cross-capability AI usage
  ledger is excluded until activation; capacity measurement is
  capability-specific.

### 2.2 Architecture options

**Option 1 (recommended): in-process tool loop with a Core-owned
assistant port.** The ASP.NET Core backend calls the model API directly
and runs the tool loop itself:

- Model host: Anthropic Messages API via the official C# SDK, or Claude
  on Microsoft Foundry — the repo's named intended candidate for AI
  query-response work. Foundry hosts Claude models with Entra ID auth
  and an Anthropic-native endpoint
  (`…services.ai.azure.com/anthropic/v1/messages`, `Anthropic.Foundry`
  NuGet). Foundry "Hosted on Azure" deployments keep prompts/completions
  within Azure but do **not** support the hosted MCP connector (400 by
  design) — irrelevant here because this option manages tools
  client-side, which works on both hostings.
- Tools are **not MCP**: they are the same Core use-case interfaces
  invoked in-process, carrying the signed-in staff `ActionActor` plus an
  AI-assistance provenance marker. This respects staff-no-MCP, keeps
  Core the sole policy owner, needs no public endpoint exposure, and
  makes human-in-the-loop natural — our code sits between the model's
  tool request and execution, so v1 is read-only inventory and any
  mutation surfaces as a proposal for explicit staff confirmation.
- Interaction transport: v1 can be plain request/response into a Razor
  page (no ADR needed); streaming (SSE) is a 0.6.0 design-re-entry
  question with the "restrained live announcements" accessibility rule
  and ADR-0002's stance both in scope.

**Option 2: hosted MCP connector.** The Messages API accepts
`mcp_servers: [{url, authorization_token}]` (beta header
`mcp-client-2025-11-20`) and would reuse the Part 1 ingress with a
client-credentials token — the one hosted path where a machine token is
first-class. Rejected for the staff assistant: it routes staff-triggered
work through the Actor identity (the boundary collision above), requires
`/mcp` publicly reachable from Anthropic egress (160.79.104.0/21),
supports tools only, and has no per-call approval hook (only allow/deny
lists). It remains viable for pure machine flows with no staff in the
loop.

**Option 3: Claude Code channels relay.** Pegasus posts staff queries to
a local channel server beside a persistent `claude` session; replies
return via the channel's reply tool. Research preview, single-machine,
single-session, silent drops, custom channels need
`--dangerously-load-development-channels`, org enablement required on
Team/Enterprise, unavailable on Foundry auth. A useful
management/development experiment; not a staff product surface.

### 2.3 Decision route

Before any implementation: AI-01's 0.6.0 activation evidence (model,
transport, data, cost, evaluation, failure/recovery, real caller,
individual operator approval), full UI design re-entry, an ADR if the
interaction transport changes ADR-0002's stance, and a Core
assistant-port contract (typed evidence/proposal/review identities per
the AI-assistance seam row). Recommended next step when the operator
wants this: one open-decisions line naming Option 1 as the candidate
architecture.

## Part 3 — Proposal B: "Send to Claude" receiver (research)

Two different products hide under this button name; the proposal keeps
them separate:

1. The **allocated end-state** is ADR-0011/AI-09's `Send to AI` domain
   action (`Later`/`1.3.0`): one durable, idempotent, capability-scoped
   Core work request bound to an immutable case/revision; a scoped
   worker leases it and returns only a proposal, evidence, or visible
   failure. ADR-0011 fixes `Send to AI` as the vendor-neutral domain
   action; a `Send to Claude` UI label is permitted wording that does
   not redefine it. It, too, is a named UI absence today, and 1.3.0
   requires transport/lease/recovery proved before any proposal caller.
2. A **near-term operator hand-off**: management/development pushes a
   case pointer into a live Claude working surface. This can be built
   and exercised outside the staff shell without touching design
   authority or capability allocation, and is the practical research
   vehicle.

### 3.1 Receiver options evaluated

**Option 1 (recommended near-term): Claude Code channel webhook
receiver + Automation Actor connector.** A custom channel is a local
stdio MCP server that Claude Code spawns; it listens on a localhost HTTP
port and forwards each POST into the running session as a
`notifications/claude/channel` event (rendered as a `<channel>` tag).
The operator's session also has the Part 1 `/mcp` connector configured
(bearer token via `headersHelper`), so the pushed event carries only a
case reference and Claude fetches the data through the approved
Automation Actor tools. This is coherent with ADR-0011 — the session is
management/development-controlled and the MCP client is the one named
Actor — and the same setup doubles as the MCP-01–04 real-caller evidence
run. Two-way replies and even remote permission approval are supported
by the channel contract.

**Return path — results go back through the same ingress.** Work
product re-enters Pegasus only through the Automation Actor's approved
write tools (`pegasus_document_add`, intake submission, lease-guarded
case mutations), with the Actor's attribution, idempotency
`OperationKey`, and action history — never through the channel. The
channel's reply tool is operator chat (confirmations like "document
added to CE-1234") and permission relay; routing business data back
through the channel server into Pegasus would require a second
authenticated ingress and become exactly the parallel policy path the
architecture forbids. Two consequences:

- Write-back shapes the v1 inventory approval (1.10(d)): the specific
  operational write actions a Send-to-Claude workflow needs must be on
  the approved list, and the lease tools make Claude follow the same
  acquire-edit-release discipline as a staff editor.
- Anything **proposal-shaped** (an AI-drafted finding, specification, or
  query response) has no accepted write path until AI-09's proposal
  contract exists: the model can never issue an accepted case,
  engineering, economic, legal, or report outcome. Whether an AI-drafted
  *artifact* may enter case custody as an ordinary document (clearly
  provenance-labelled, accepted by nobody) is an inventory-approval
  question for the operator, not something this design assumes. Constraints, all documented: research preview;
custom channels run only behind `--dangerously-load-development-channels`
(or an org `allowedChannelPlugins` entry on Team/Enterprise with
`channelsEnabled`); events arrive only while a session is open; delivery
is unacknowledged and silently dropped if the channel is not loaded;
requires claude.ai or Console authentication (not Bedrock/Foundry).

**Option 2: Claude Desktop deep link.** `claude://claude.ai/new?q=…` is
documented: prefills (never auto-sends) up to ~14k characters; Code
sessions via `claude://code/new?q=…&folder=…`; the CLI scheme
`claude-cli://open?…` caps `q` at 5k. No receiver infrastructure and the
human reviews before sending — but one-shot, no response path back to
Pegasus, payload crosses via URL (so send a reference + short summary,
never documents), and pairing it with a connector for payload drags in
Desktop's OAuth model, which does not support client credentials
("every connection requires user consent"; the admin-configured
`static_headers` alternative is beta). A documented web `/new?q=`
equivalent does not exist — do not design against it.

**Option 3: the in-Pegasus durable receiver (allocated end-state).**
AI-09's contract maps directly onto the existing
`ExternalWorkProcessing` seam in Core (`QueuedExternalWork`,
reader/lease/poison/reconcile, Azure queue transport in Worker) plus the
case-edit lease/version guard — a design that reuses that seam stays
inside the architecture; a new store or runtime would need an ADR. The
leasing "scoped worker" could later be a Claude-driven client acting as
the Automation Actor through approved MCP tools (track 1 of the
Send-to-AI open decision), returning a proposal that staff accept or
reject through Core. Nothing in this task builds it.

**Option 4 (not a hand-off): headless `claude -p` / Agent SDK
server-side.** Both terminate in Pegasus's own UI, which makes them
Proposal A architectures, not "Send to Claude" receivers. The Agent SDK
is TypeScript/Python only — a sidecar would be a new runtime/deployment
unit (ADR required) and third-party products must use API-key auth, not
claude.ai login.

### 3.2 Recommendation

Near-term: Option 1 as a management/development research vehicle,
combined with Part 1's evidence run — one setup proves both. The staff
UI button itself remains a deferred UI capability (named absence, full
design re-entry) and nothing near-term touches the staff shell.
End-state: Option 3 under AI-09's own 1.3.0 gates. Deep links (Option 2)
are a zero-infrastructure fallback worth knowing about, not a
foundation.

## Part 4 — Promotion path

- Part 1's settled auth + actor contract is promoted to an ADR before or
  with the implementation PR (it is a durable technical decision;
  ADR-0011 owns the boundary, the new ADR would own the concrete
  identity/authentication/inventory contract).
- Parts 2 and 3, once reviewed, reduce to one open-decisions line each
  (or an amendment to the existing Send-to-AI transport decision) — they
  are not implemented in this task and claim no capability allocation
  beyond what capabilities.md already records.
- Two research findings belong in the Send-to-AI open decision's
  evidence when it is next touched: the realistic first automation
  client is Claude Code (machine-credential headers documented;
  claude.ai/Desktop connectors require user consent and do not support
  client credentials), and the hosted Messages-API MCP connector is the
  one hosted path where a client-credentials token is first-class —
  while Foundry "Hosted on Azure" deployments exclude that connector,
  so a Foundry-hosted assistant implies a client-managed tool loop.
