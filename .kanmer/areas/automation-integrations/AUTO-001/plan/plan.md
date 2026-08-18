# Plan — AUTO-001: activate the production Automation MCP gate

## Status

**Blocked pending the recorded user decisions in `open-questions`.** This plan deliberately does not authorize any cloud, Key Vault, credential, deployment, or external-client write.

## Approach

Promote the existing composition-gated ingress to a production-capable, still default-off configuration. Preserve the single-client registry, scopes, Core use cases, rate limit, history, and Administrator kill switch. Replace only the local-only assumptions (DevelopmentOffline restriction, ephemeral token keys, relaxed transport) with an approved production token-key and HTTPS design; use the existing versioned-Key-Vault-to-Container-App-secret pattern for the credential. A setting-only change is rejected because current code fails startup in Production and its ephemeral keys invalidate a production token boundary on restart.

## Governing docs

- **FRD-10** (linked): preserve the one Automation Actor boundary and prove real-client success, authorization denial, validation denial, and action history for every approved tool; no staff browser or management authority reaches MCP.
- **ADR-0021** (linked): retain the fifteen-tool direct-write inventory, scopes, leases, replay/version guards, permanent history, and structural absence of confirmation, approval, and dispatch tools. This ticket does not change the business contract.
- **New ADR required before implementation:** record durable production token signing/encryption-key custody, rotation, HTTPS transport, and rollback behavior. This is a technical decision that ADR-0021 explicitly leaves unmade; it must be created through `kanmer-docs` after user approval and linked here.

## Steps

1. Resolve the three recorded approvals: exact production mutation targets, named external actor and minimum scopes, and the durable production token-key/transport decision. Record the answers in `open-questions`.
2. Author and accept the new ADR, then link it to this ticket. It must choose managed rotatable key custody, HTTPS-only token/MCP transport, key rotation, and the closed-state rollback.
3. Refactor the Automation MCP options and composition so the surface remains absent by default, DevelopmentOffline retains the existing ephemeral-key evidence path, and Production becomes available only with every approved non-secret/secret prerequisite. Add focused unit/integration coverage for each rejected configuration and the bearer-only route behavior.
4. Extend existing Azure IaC and release validation with an Automation Actor client-ID parameter, versioned Key Vault secret URI, Container App secret reference, and the required feature/public-origin settings. Reuse the current Web Key Vault-reference pattern; no values are tracked or emitted.
5. Run the canonical build plus focused Automation MCP integration tests, Bicep compile/lint, and release-plan validation. Review the diff for simplicity: no second client registry, policy owner, or parallel tool implementation.
6. After explicit exact-target approval, create the secret in `pegasusprodkv252ow37g`, deploy the signed Web revision to `rg-pegasus-prod/pegasus-prod-web-252ow37gij`, and run the preflight/readback without disclosing secret material.
7. With the named external actor, exercise all fifteen tools for approved scope(s), plus authorization and validation failures, action-history evidence, the Administrator kill switch, and a rollback that leaves the public route closed. Update `docs/current-architecture.md`, `docs/operations.md`, and `docs/runbook.md` with observed facts only.
8. Write the post-implementation report, open the PR to `dev`, and move the ticket to Review. After merge, verify on merged `main` and write proof from the actual approved production evidence.

## Verification

- `dotnet restore`
- `dotnet build --configuration Release`
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter FullyQualifiedName~AutomationMcpIngressTests --configuration Release`
- `az bicep build --file infra/main.bicep`
- Existing release-plan validation and approved production smoke commands, run only after exact-target approval.
- Fresh Azure readback: Container App revision/configuration (no secret value), health endpoints, bearer-only MCP metadata/token behavior, external-client tool ledger, action history, kill switch, and closed-state rollback.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| A flag-only deployment crashes or weakens the token boundary. | Treat production key custody and HTTPS transport as an ADR prerequisite; test fail-closed configuration. |
| Secret leakage. | Receive only a versioned Key Vault URI in deployment inputs; never read, log, commit, or write the secret to ticket documents. |
| Over-broad actor access. | Bind one named actor and the minimum approved existing scopes; preserve server-side per-tool scope checks. |
| A live route cannot be closed quickly. | Prove the existing Administrator kill switch and deploy rollback to the no-route state before calling activation complete. |
| Docs claim stale deployment facts. | Refresh current-state docs from post-deploy readback, including the current version discrepancy found in research. |

## Clarification — external client controls tool use

Claude Desktop is the one OAuth confidential client and holds the client ID/client secret. Its MCP configuration controls the tools it exposes to the actor and therefore its tool-use policy. Pegasus continues to validate bearer tokens and its existing scope claims as a transport boundary, but this task does **not** create a Pegasus-side scope-approval process or ask an operator to choose a tool allow-list.

Step 1 is therefore narrowed to: record exact approval for the production mutation and external evidence run, plus the named Claude Desktop OAuth client configuration. The production key/HTTPS decision remains separate because the code presently uses local-only ephemeral OpenIddict keys and relaxed transport; it is not a tool-permission decision.
