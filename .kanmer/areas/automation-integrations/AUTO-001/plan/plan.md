# Plan — AUTO-001: enable the Automation MCP configuration

## Approach

This is a runtime-configuration activation, not an application rebuild. Retain the deployed Web image and configure the existing production Container App with the OAuth client-secret reference and Automation MCP settings. The matching IaC change records those same settings so the gate remains enabled on the next normal deployment.

Claude Desktop controls connector and tool access. Pegasus supplies the existing remote MCP endpoint, OAuth client validation, inventory, permanent history, rate limit, and Administrator kill switch. No new tool policy, scope design, Core behavior, migration, or source/test change is in scope.

## Governing docs

- **FRD-10**: the live Claude Desktop caller must evidence the existing inventory’s success, authorization failure, validation failure, and permanent history.
- **ADR-0021**: retain the accepted single-client, direct-write tool boundary and excluded confirmation/approval/dispatch tools. This activation does not modify either document.

## Steps

1. Retain the IaC changes that declare the versioned Automation MCP secret URI, Container App secret reference, and non-secret feature/client/public-origin settings for future deployments.
2. In Key Vault `pegasusprodkv252ow37g`, create `automation-mcp-client-secret` without returning or recording its value. Assign the existing Web managed identity (`pegasus-prod-web-id-252ow37gij`) **Key Vault Secrets User** only on that new secret, matching its two existing secret-level assignments.
3. Update Container App `pegasus-prod-web-252ow37gij` directly—without rebuilding its image—to reference that secret and set `Features__AutomationMcp=true`, `AutomationMcp__ClientId=pegasus-automation`, `AutomationMcp__ClientSecret`, and its current public HTTPS origin. Read back only setting/secret-reference names and revision health.
4. Configure the Claude Desktop custom remote connector with the public `/mcp` URL and OAuth client ID/secret. Record its existing fifteen-tool success, authorization-denial, validation-denial, permanent-history, kill-switch, and closed-route rollback evidence.
5. Update current-state documentation from the fresh readback and complete the PR/report. No .NET build or test is required for this configuration-only activation.

## Verification

- Read-only Key Vault/Container App configuration and secret-reference census; never read the secret value.
- New Container App revision is healthy; `/health/live`, `/health/ready`, metadata, OAuth token, and MCP endpoint exhibit the expected state.
- Claude Desktop evidence exercises the existing fifteen-tool inventory plus denial, validation, history, kill switch, and rollback.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| The Web identity cannot resolve the new secret. | Create only one secret-level Key Vault Secrets User assignment, matching existing Box-secret access. |
| Future deployment removes the configuration. | Keep the equivalent IaC settings in this ticket. |
| Runtime configuration is incompatible with the deployed revision. | Check new revision health immediately and roll the gate back to its closed state if it fails. |
| Secret exposure. | Generate/store it directly in Key Vault; suppress command output and never retrieve, log, or commit it. |

## Live outcome — 2026-08-18

The configuration-only hypothesis was tested and disproved on the approved production target. The configured revision exited with `Features:AutomationMcp requires the DevelopmentOffline runtime profile`; it never became ready. The gate was immediately rolled back to `false`, producing healthy revision `pegasus-prod-web-252ow37gij--0000003` with the MCP routes closed.

The exact deployed source SHA (`aecad2479f52dadfedca109413a458c60c85323e`) contains that guard. A source change and replacement image are therefore required to activate the existing endpoint. Do not retain an IaC setting that turns the gate on until that code is deployed.
