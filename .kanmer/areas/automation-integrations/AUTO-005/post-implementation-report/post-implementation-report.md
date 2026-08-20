# Post-implementation report — AUTO-004 / AUTO-005

## Summary

Restored ordinary-casework parity for the Automation Actor through one shared branch and PR. The configuration-gated `/mcp` surface now registers 35 governed tools, including complete Unidentified list/detail/source/resolve access and a typed Triage read/source/lifecycle/evidence/Case-association surface. Both adapters invoke existing Core owners, retain the distinct Automation identity and `automation.intake` scope, and add real HTTP caller evidence without introducing a second policy engine. No deployment was performed or claimed.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | modified | Registers the previously orphaned Unidentified tools and the new Triage adapter under the existing composition gate. |
| `src/Pegasus.Web/Mcp/UnidentifiedMcpTools.cs` | modified | Adds canonical reference validation, receipt/group source projection, exact grouped-member validation, and retained-source download while preserving `IResolveUnidentified` as mutation owner. |
| `src/Pegasus.Web/Mcp/IntakeSourceMcpContent.cs` | added | Shares bounded inline-content formatting between the two concrete intake-source callers while delegating integrity and custody to `IDownloadIntakeSource`. |
| `src/Pegasus.Web/Mcp/TriageMcpTools.cs` | added | Adds typed list/detail/source, Awaiting information, finding/supersession, response evidence, complete/cancel/reopen, and lease-guarded Case link/unlink tools over existing Core queries and commands. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | modified | Makes all 35 tool names part of the exact governed runtime inventory. |
| `tests/Pegasus.IntegrationTests/AutomationIntakeParityIngressTests.cs` | added | Exercises real HTTP Unidentified list/detail/source and Triage list/source/lifecycle attribution. |
| `tests/Pegasus.IntegrationTests/AutomationConnectorAuthorizationTests.cs` | modified | Stops duplicating the inventory count; connector authorization now proves reachable tools while the exact inventory remains owned by one test. |
| `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | modified | Allows derived MCP application factories to drain their own queued intake without creating a second host. |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | modified | Records the authorised Unidentified/Triage same-Core parity contract, retained-source behavior, and separation of actor from explicit Engineer assignment. |
| `docs/capabilities.md` | modified | Records the source-tier MCP-03 inventory without claiming deployment; INTK-019 remains assignment owner. |
| `docs/current-architecture.md` | modified | Updates the as-built 35-tool source inventory and shared Core boundaries. |

## Governing docs

- **FRD-10:** Met and explicitly updated as authorised. The real HTTP caller proves discovery and representative success; existing scope-denial/validation/history fixtures remain active. Typed Unidentified and Triage tools use the existing `automation.intake` scope and resolved Automation actor.
- **FRD-02:** Met. Receipt identity, source metadata, group membership, exact member selection, bytes, hashes, and integrity checks remain owned by existing intake ports. U-references do not substitute for other domain identities.
- **FRD-03:** Met. Triage uses the established states, findings, response evidence, reasons, replay/version rules, and Case lease boundary. It remains distinct from Unidentified. Assignment is not reimplemented; INTK-019 owns the explicit named-Engineer replacement.
- **ADR-0011:** Met. Web and MCP call the same Core use cases; no second policy engine, staff impersonation route, management authority, scope, store, or runtime was added.
- **ADR-0021:** Met. Automation retains exactly `PerformCasework`, direct-write logging parity, version/replay guards, and explicit professional-finding/report/send exclusions.

## Risks / follow-ups

- [[INTK-019]] owns retirement of actor-relative “Assign to me” and the shared explicit named-Engineer assignment contract; this PR intentionally does not pre-empt it.
- [[AUTO-003]] remains the owner of broader classified-mail parity.
- No production deployment or cloud write was authorised. `docs/operations.md` therefore remains the deployed-state owner and was not changed. Release verification must not infer that the 35-tool source inventory is live.
- Full IntegrationTests initially reported the now-corrected connector count assertion and one unrelated Playwright navigation timeout. The connector/task-focused rerun passed 4/4 and the exact browser case passed alone on rerun.

## Verification hand-off

On merged `main`, run:

- `dotnet restore Pegasus.slnx --locked-mode` — expected success.
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — expected zero warnings/errors.
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — pre-merge 758/758 passed.
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — pre-merge 98/98 passed.
- Focus `AutomationIntakeParityIngressTests`, the exact inventory test, connector authorization, and `QdosTriageIntegrationTests` — pre-merge focused sets passed 4/4 and 15/15.
- `pwsh ./scripts/Test-DocumentationLinks.ps1` — pre-merge 192 files passed.
- Full IntegrationTests — pre-merge aggregate 797 passed, 14 corpus-gated skips, with the two reported failures independently rerun green after correction/isolation.
