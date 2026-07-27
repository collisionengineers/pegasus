# Staff MCP

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Ready V1 case/document/intake plan — classified-email actions V2**

## Purpose

Expose V1 internal staff case, document, and intake-queue actions through one remote MCP endpoint while retaining per-staff identity, current-role enforcement, and the same Core policy as Web. Broader classified-email actions are V2.

## Feature coverage

Primary feature ownership is: `MCP-01`, `MCP-02`, `MCP-03`, and `MCP-04`.
They cover the V1 internal staff OAuth boundary and case, intake-queue, and
document tools that delegate to named Core use cases. `MCP-05` belongs to the
V2 classified-email workspace plan: it must not be pulled into this V1 tool
inventory or used to create a second email policy owner.

## Authority and current boundary

- **Authority:** [remaining requirements](../../../../product/v1-gap.md#3-complete-intake-formats-and-paths) and [ADR-0004](../../../../architecture/decisions/ADR-0004-provider-api-and-staff-mcp-authentication.md#internal-staff-mcp).
- **Policy owner:** existing staff authorization and named Core use cases; Web owns `/mcp`, OAuth metadata and composition.
- **Current implementation:** no staff identity, OAuth server, OpenIddict/MCP package, `/mcp` endpoint, tool inventory or durable key store is registered.
- **Real callers:** planned remote Streamable HTTP MCP client, initially one pre-registered Claude connector; no provider caller may use it.
- **Persistence/adapters:** durable authorizations/tokens and signing/encryption/Data Protection keys are planned in the existing persistence/Key Vault design; Box-backed document actions remain limited by persisted root proof.
- **Dependencies:** staff authentication/roles, named Core case/inbox/document use cases, durable keys and the [Box boundary](box-case-files.md#scoped-box-folder-and-version-custody).
- **Replaces/consolidates:** no local MCPB/stdio bridge, shared static header or separate MCP service/project.

## Shared failure and observability rules

Bearer tokens are accepted only at `/mcp`; interactive cookies are only for staff sign-in/consent. Every request reloads enabled account/current role and validates issuer, resource/audience, lifetime, signature/introspection and scopes before a named policy/tool. Mutations are operation-specific, enter permanent action history with actor/reason/outcome and cannot rely on client approval hints.

## Remote staff OAuth and restricted MCP tool surface

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [ADR-0004](../../../../architecture/decisions/ADR-0004-provider-api-and-staff-mcp-authentication.md#internal-staff-mcp).
- **Confirmed facts:** host remote Streamable HTTP in the existing Web project; pre-register the Claude client/callback; authorization-code flow uses S256 PKCE and exact HTTPS resource/audience. `/mcp` is the current route proposal, not separate product authority. Nothing is implemented or deployed.
- **Decision required before implementation:** verify the current supported, mutually compatible OpenIddict and `ModelContextProtocol.AspNetCore` releases against their primary documentation and record exact versions in dated execution evidence; MCP/Claude enablement and durable key custody require explicit approval before live use.

### Owner and dependencies

- **Policy/implementation owner:** Web authentication/composition owner, with each tool delegating to its existing Core owner; composition owner is the sole merger with provider registration.
- **Independent evaluator:** security-focused test engineer and independent reviewer.
- **Prerequisites:** staff Identity role model, named Core actions/Web policies, durable key storage, revocable authorization/token persistence and Box provenance for document tools.
- **Consumers/unlocks:** authenticated internal staff MCP clients only.

### Caller, contract and change boundary

- **Real or intended caller:** planned remote MCP client sends bearer token to `/mcp`; authorization/consent UI is Web cookie-based.
- **Input/output:** a valid per-staff OAuth token for exact MCP resource invokes only a named tool mapping to an existing/simultaneously delivered Core use case and returns operation-specific result/failure.
- **Ordered decisions and failure behavior:** validate token/resource/scope; reload enabled account/role; apply named action policy/domain invariant; enforce Box persisted-descendant rule where relevant; record the result in permanent action history. Invalid/disabled/revoked/mismatched requests are denied without tool execution.
- **Persistence/migration:** persist/revoke authorizations and rotating refresh tokens; store signing/encryption/Data Protection keys durably with overlap/rotation, never in source.
- **Adapters/side effects:** metadata endpoints, OAuth server and Streamable HTTP endpoint in Web only; no provider authentication or Box arbitrary-ID adapter.
- **Operator surface and observability:** consent/revocation/account-disable effects, tool/action-history outcomes and content-free auth failure telemetry.
- **Documentation affected:** OAuth/client registration and tool inventory; update only after real implementation evidence.
- **Replaces/consolidates:** exclude a local bridge/static shared header/DCR; do not make tools a second business layer.

### Scope

- **Included:** one pre-registered Claude client, OAuth authorization code with S256 PKCE, protected-resource/authorisation-server metadata, and V1 per-staff case/document/intake-queue tools where matching Core/Web actions exist.
- **Excluded:** V2 classified-email tools, provider API authentication, Dynamic Client Registration, accounts/roles/principal/credential administration, Azure/deployment/cloud operations, permanent deletion, Box search/arbitrary IDs, and tools without a real Core owner.

### Implementation checklist

- [ ] Verify compatible package versions, implement durable OAuth/key custody and exact metadata/resource validation in Web.
- [ ] Expose only a reviewed named-tool inventory mapped one-to-one to existing/simultaneously delivered Core use cases and policies.
- [ ] Enforce account reload/current role, revocation, scope/resource checks and persisted-descendant Box restriction on every request.

### Validation checklist

- [ ] Test PKCE, issuer/audience/resource/scope rejection, token expiry/refresh rotation, revoked authorization, disabled account and role change on the next request.
- [ ] Prove restart, scale-out, signing-key overlap and Data Protection key durability.
- [ ] Test forbidden tool inventory, unauthorised/mutating contracts and Box arbitrary/out-of-scope ID denial with no Box call.
- [ ] Exercise an approved remote client through `/mcp` only after enablement approval; run repository check and independent security review.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Valid enabled staff token/current role | only authorised named tool reaches Core and records staff actor | endpoint/authorization integration test | Claude production availability |
| Role change or disabled account | next MCP request denied/changed immediately | persistence/restart test | external IdP lifecycle |
| Token for another resource or forbidden tool | denied before use case/adapter execution | security negative test | general platform security |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** enable the named Claude client, OAuth endpoints and durable keys only after user approval of client/callback/resource/environment; no provider/Box scope is widened.
- **Rollout/activation:** deploy Web endpoint with tool inventory reviewed; prove local OAuth/restart/revocation; approve one client; enable and observe authentication/tool action-history records.
- **Rollback/recovery:** disable `/mcp` routing/client, revoke authorizations/refresh tokens and redeploy prior artifact while retaining permanent action history; do not delete case/document data.
- **Irreversible risk:** issuance of bearer/refresh tokens and externally callable staff actions; durable revocation/key handling is required.

### Deferred-capability impact

- **Named capabilities:** external/customer accounts, broader MCP clients/tools, live Box folders, WhatsApp, EVA API/replacement and cloud operations.
- **Stable seam retained:** staff application identity/current role, named Core use cases, OAuth resource boundary and persisted Box provenance.
- **Future migration/replacement:** another client, DCR or external audience needs separate OAuth/permission/product approval; future Box production scope needs the separate Box decision.
- **Activation boundary:** package/version verification, security evidence and explicit MCP/Claude enablement approval.
- **Deliberately absent:** separate service/project, local stdio bridge, DCR, shared static token/header, provider auth reuse, account/config/cloud/deletion tools and arbitrary Box search.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | planning review | OAuth/tool/approval boundaries | identity implementation, endpoint, remote client, deployment or acceptance |
