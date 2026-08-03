# MCP Automation Actor ingress (MCP-01/02/03/04)

Status: scaffolding only — worktree and claim are in place; implementation
is paused pending operator/user direction on the open decisions below. Do
not start implementation from this draft without that direction.

## Scope

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

Explicitly out of scope for this task:

- MCP-05 (broader classified-email workspace actions) — `Next`/`0.3.0`,
  blocked on the email workspace itself, which does not exist yet.
- AI-09 (`Send to AI` proposal transport) — a separate `1.3.0` capability
  and Core work-request contract; the Automation Actor's MCP tools invoke
  ordinary operational Core actions only and return no AI proposal.
- Any external client, schedule, or filesystem for the "externally
  scheduled automation client scanning an approved network-drive scope"
  concept in requirements.md — that client, its schedule, and the
  filesystem it reads stay outside Pegasus per the requirements text;
  custody begins only at an authenticated accepted MCP submission. This
  task builds the ingress the client would call, not the client.
- Vendor/tool selection, credential provisioning, or any live external
  wiring beyond what local development evidence requires.
- Any Administrator, configuration, credential, cloud, release, or
  deletion authority for the Actor (explicitly excluded by ADR-0011).

## Why this is not greenfield

A full per-staff-OAuth MCP surface was built and merged to `dev`
(`a8d5991`, 2026-07-29) implementing this shape but using an *individually
authorized staff* actor model. ADR-0011 (accepted 2026-07-30) rejected that
model in favor of one named, vendor-neutral Automation Actor, and the
per-staff surface was deliberately deleted the same day (`4c7e0ac`) as part
of hardening the QDOS alpha delivery boundary. The deleted code
(`src/Pegasus.Web/Mcp/*McpTools.cs`, `StaffMcpAuthorization.cs`,
`StaffMcpExtensions.cs`) is useful prior art for tool-schema shape and DI
wiring (`AddMcpServer().WithTools<T>()`, `MapMcp("/mcp")`,
`ModelContextProtocol.AspNetCore` package), but its authorization layer
(per-staff OAuth scopes impersonating a staff identity) is exactly what
must not be rebuilt.

## Existing patterns to reuse

- Core use cases are plain interfaces: `Task<TResult> ExecuteAsync(TCommand
  command, CancellationToken ct)` — see
  `src/Pegasus.Core/Documents/DocumentContracts.cs`.
- Commands/queries carry `ActionActor Actor`, an idempotency
  `OperationKey`, and (where applicable) `ExpectedCaseVersion` /
  `EditLeaseToken`.
- `src/Pegasus.Core/Identity/IdentityContracts.cs` defines `ActorKind`
  (`Staff`, `SystemWorker`, `RequestLink` — no automation case yet) and
  `ActionActor` factory methods, plus the shared `IActionHistoryWriter` /
  `ISecurityEventWriter` ports that already record actor, caller, outcome,
  and before/after evidence.
- `src/Pegasus.Core/Identity/StaffAuthorization.cs` is the fail-closed
  authorization switch over `StaffAccessRight`; individual use cases also
  self-check `actor.Kind` (e.g. `MailboxIntake.cs:114-117`).
- `src/Pegasus.Web` is the correct composition root for this ingress per
  `docs/architecture.md`'s "Provider API and Automation MCP are separate
  Web ingress boundaries" — not a new top-level project (no ADR exists
  proposing that, and one would be required by the architecture
  invariants).
- Dormant OpenIddict tables already exist from migration
  `20260729150000_DocumentCustodyAndRequests` — schema presence only, not
  an implemented ingress (`docs/operations.md`'s "Automation MCP remains a
  deferred ingress" section).

## Open decisions requiring direction before implementation starts

1. **Actor identity/authentication mechanism.** `open-decisions.md`'s
   Send-to-AI transport section lists "authentication and Automation Actor
   identity" as still-needed evidence. Candidates: a client-credentials
   flow against the existing dormant OpenIddict tables, or a distinct
   mechanism. This is a security-sensitive design choice, not something to
   infer from the deleted per-staff code.
2. **Exact operational tool inventory.** Which specific Case,
   intake-queue, and document Core use cases belong in the Actor's
   allow-list, and the exact `McpServerTool` names/annotations
   (`ReadOnly`/`Destructive`/`Idempotent`) for each.
3. **New `ActorKind` (or equivalent).** Whether to add an `Automation`
   case to the existing three-value enum, and the exact
   `StaffAccessRight`-equivalent gate that keeps the Actor away from
   Administrator/config/credential/cloud/release/deletion actions.
4. **Initial client evidence.** Whether Claude Desktop remains the
   accepted initial client for exercising the real caller (per MCP-01's
   "Claude Desktop may provide initial accepted client evidence without
   owning the actor identity"), and what that verification looks like
   locally vs. any deployed evidence.

## Planned changes (subject to the above)

- `src/Pegasus.Core/Identity/IdentityContracts.cs`: extend actor identity
  for the Automation Actor case.
- `src/Pegasus.Core/Identity/StaffAuthorization.cs` (or a parallel
  Automation-scoped equivalent): fail-closed allow-list enforcement.
- `src/Pegasus.Web/Mcp/`: new MCP tool classes wrapping existing Case,
  intake-queue, and document `ExecuteAsync` use cases (mirroring the
  deleted tool shape, replacing its authorization layer).
- `src/Pegasus.Web/Pegasus.Web.csproj`: `ModelContextProtocol.AspNetCore`
  package reference.
- `tests/Pegasus.Core.Tests/`: authorization tests (allow-listed action
  succeeds, Administrator/config/credential action fails closed, action
  history records actor/tool/outcome).
- `tests/Pegasus.ArchitectureTests/`: confirm the existing
  `ApplicationSolutionExcludesSourceWorkspaces` /
  `ApplicationProjectsDoNotReferenceSourceWorkspaces` guards still pass
  (no reference to `workspaces/*` MCP reference servers).
- `docs/architecture.md` / `docs/operations.md`: update once a real caller
  exists — these currently correctly state "not implemented."

## Verification

- `dotnet restore`, `dotnet build --configuration Release`,
  `dotnet test` (focused on the new authorization/tool tests, then full
  suite).
- New authorization tests as above.
- An exercised real MCP caller reaching Core (evidence tier 5 in
  `docs/operations.md#required-evidence-tiers`): expected success,
  authorization failure, and validation failure, each with action-history
  proof — not just registration/schema presence
  (`docs/requirements.md`'s "MCP registration, a tool schema, or an
  endpoint file is not proof" line).
