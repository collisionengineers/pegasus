# Proof — MCP-07

**Shipped:** PR #446 (`task/tick-104-mcp-07-connector-admin`), merge `154804b2`
**Deployed:** `git merge-base --is-ancestor 154804b2 4111ad29` → **true**. Release 16 runs
revision `pegasus-prod-web-252ow37gij--4111ad291779`, confirmed active by
`az containerapp revision list` on 2026-08-21.

## The capability is reachable, not shipped dark

This is the check that mattered, because the surface sits behind a composition gate and a
gated feature must not be claimed as delivered.

1. **The connector section exists** —
   `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml:120-140` renders the
   "Send to AI connector" panel with base URL, timeout, channel token (held / not held,
   with entry, rotation and removal) and status. Every field the capability names.
2. **It is linked** — `Administration/Index.cshtml:74` links `/Administration/Automation/Index`
   from the Administration landing page. No hidden route, no direct-URL-only access.
3. **The gate that guards the link is open in production** — the card renders only when
   `AutomationComposed` is true (`Index.cshtml.cs:24`), which resolves
   `AutomationClientRegistry`, which `AutomationMcpExtensions` registers only when
   `Features:AutomationMcp` is enabled. Read live:

   ```
   az containerapp show -g rg-pegasus-prod -n pegasus-prod-web-252ow37gij
   Features__AutomationMcp    true
   ```

So an Administrator signing in to the live application reaches this surface through the
navigation, on the deployed revision. That is a genuine operator entry point.

## Activation questions, both resolved before implementation

The ticket's `open-questions` are ticked with operator resolutions, not assumptions:

- *"May MCP-07 be implemented despite its Later / post-alpha designation?"* — resolved by
  the operator on 2026-08-20: all MCP capability tickets are in active implementation
  scope.
- *"Does a real dispatch caller exist for the connector configuration?"* — resolved by a
  read-only check on `dev`: AI-09's `SendCaseToAi` and `ChannelAiHandOffTransport` are
  implemented with round-trip evidence, so the configuration has a concrete consumer. No
  outbound validation ping was built, because no caller for one exists.

The second is the important one: configuration with no consumer would have been exactly the
abstraction-without-a-caller the repository rules forbid.

## Implementation evidence

Checklist 8/8, each with named evidence: Core settings record and rules owner; Infrastructure
columns, mapping and migration; transport resolving effective settings per call with
administration overriding configuration; the Administration section per the design README;
tests for validation bounds, history attribution, token-never-echoed, transport override and
configuration fallback. Recorded runs: Core `AiWorkTests` 31/31, `SendToAiIntegrationTests`
6/6, `Test-MigrationGrants` pass, Release build 0 warnings.

The token is write-only, DataProtection-protected, never displayed or logged; rotation and
every change are attributed permanent history.

## Not claimed

The connector's **configuration** surface is deployed and reachable. Sending a case to AI
through a live non-preview channel is [[TICK-102]]'s activation decision, which remains
unaccepted — see that ticket. Nothing here claims a live AI dispatch has been performed.
