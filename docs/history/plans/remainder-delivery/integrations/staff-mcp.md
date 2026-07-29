# Staff MCP

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Ready `0.1.0-alpha.1` case/document/intake plan — classified-email actions `Next`/`unallocated`**

## Purpose

Expose `0.1.0-alpha.1` internal staff case, document, and intake-queue actions through one remote MCP endpoint while retaining per-staff identity, current-role enforcement, and the same Core policy as Web. Broader classified-email actions are `Next`/`unallocated`.

## Feature coverage

Primary feature ownership is: `MCP-01`, `MCP-02`, `MCP-03`, and `MCP-04`.
They cover the `0.1.0-alpha.1` internal staff OAuth boundary and case, intake-queue, and
document tools that delegate to named Core use cases. `MCP-05` belongs to the
`Next`/`unallocated` classified-email workspace plan: it must not be pulled into this `0.1.0-alpha.1` tool
inventory or used to create a second email policy owner.

## Authority and current boundary

- **Authority:** [remaining requirements](../../../../product/qdos-alpha-gap.md#3-complete-intake-formats-and-paths) and [ADR-0004](../../../../decisions/ADR-0004-provider-api-and-staff-mcp-authentication.md#internal-staff-mcp).
- **Policy owner:** existing staff authorization and named Core use cases; Web owns `/mcp`, OAuth metadata and composition.
- **Current implementation:** staff Identity, OpenIddict application persistence, authorization-code/refresh-token endpoints, exact-resource validation, consent, protected-resource metadata, and local client register/revoke commands are implemented in Web for `DevelopmentOffline` in `Development`. `/mcp`, an MCP package/tool inventory, durable production key custody, and remote-client activation remain absent.
- **Real callers:** the one-shot local client commands call `IOpenIddictApplicationManager` against the Development database; a remote Streamable HTTP MCP client remains planned. No provider caller may use the OAuth surface.
- **Persistence/adapters:** OpenIddict applications, authorizations, and tokens use the existing persistence stream. Development certificates are local only; durable production signing/encryption/Data Protection custody and Box-backed document action proof remain separately gated.
- **Dependencies:** staff authentication/roles, named Core case/inbox/document use cases, durable keys and the [Box boundary](box-case-files.md#scoped-box-folder-and-version-custody).
- **Replaces/consolidates:** no local MCPB/stdio bridge, shared static header or separate MCP service/project.

## Shared failure and observability rules

Bearer tokens are accepted only at `/mcp`; interactive cookies are only for staff sign-in/consent. Every request reloads enabled account/current role and validates issuer, resource/audience, lifetime, signature/introspection and scopes before a named policy/tool. Mutations are operation-specific, enter permanent action history with actor/reason/outcome and cannot rely on client approval hints.

## Remote staff OAuth and restricted MCP tool surface

**Evidence state:** Implemented (local-only source and command surface); no local command or remote-client execution evidence is recorded here.

### Authority and decision gate

- **Requirement/decision:** [ADR-0004](../../../../decisions/ADR-0004-provider-api-and-staff-mcp-authentication.md#internal-staff-mcp).
- **Confirmed facts:** the existing Web project hosts the local OAuth metadata, consent, authorization, token, and revocation endpoints. The deterministic local public client uses authorization-code flow with S256 PKCE and the exact HTTPS resource/audience; its create/update and actual deletion append content-safe `Client` security events. `/mcp` is not registered. The Claude client/callback is not registered or activated.
- **Decision required before remote implementation:** verify the current supported, mutually compatible `ModelContextProtocol.AspNetCore` release against its primary documentation and record its exact version in dated execution evidence. MCP/Claude enablement, canonical issuer/resource, hosted callback, and durable key custody require explicit target-specific approval before live use.

### Owner and dependencies

- **Policy/implementation owner:** Web authentication/composition owner, with each tool delegating to its existing Core owner; composition owner is the sole merger with provider registration.
- **Independent evaluator:** security-focused test engineer and independent reviewer.
- **Prerequisites:** staff Identity role model, named Core actions/Web policies, durable key storage, revocable authorization/token persistence and Box provenance for document tools.
- **Consumers/unlocks:** authenticated internal staff MCP clients only.

### Caller, contract and change boundary

- **Current caller:** `--register-development-mcp-client` and `--revoke-development-mcp-client` invoke the persisted OpenIddict application manager only in the DevelopmentOffline/Development gate; a remote MCP bearer caller remains planned.
- **Input/output:** the local command accepts no client-supplied metadata. It creates or updates only the fixed public S256 PKCE client, exact loopback callback, resource scopes, grant types, and consent setting; revocation deletes only that same client. It never prints a token or credential.
- **Ordered decisions and failure behavior:** outside the exact DevelopmentOffline/Development gate, either command stops before client access. The register command records `development_mcp_client_registered`; revoke records `development_mcp_client_revoked` only after an actual deletion. The absent-client revoke is an idempotent no-op. Remote token/resource/current-role/tool checks remain future `/mcp` work.
- **Persistence/migration:** the existing migration persists revocable applications, authorizations, and refresh tokens. Durable signing/encryption/Data Protection keys with overlap/rotation remain unimplemented production prerequisites.
- **Adapters/side effects:** protected-resource metadata and OAuth server endpoints are in Web only; there is no Streamable HTTP endpoint, provider authentication, or Box arbitrary-ID adapter.
- **Operator surface and observability:** the commands emit content-safe `Client` security events without client secrets or tokens. Consent, account-disable, tool/action-history, and remote authentication observability require their corresponding callers.
- **Documentation affected:** the local register/revoke and external activation gate are owned by [operations](../../../../operations.md#staff-mcp-oauth-offline-replay-and-activation-gate).
- **Replaces/consolidates:** exclude a local bridge/static shared header/DCR; do not make tools a second business layer.

### Scope

- **Included now:** the deterministic DevelopmentOffline public client, OAuth authorization-code/refresh-token endpoint configuration with S256 PKCE, local protected-resource metadata, and persisted client register/revoke callers.
- **Excluded:** remote/Claude client activation, `/mcp` tools, Dynamic Client Registration, provider API authentication, accounts/roles/principal/credential administration through MCP, Azure/deployment/cloud operations, permanent deletion, Box search/arbitrary IDs, and tools without a real Core owner.

### Implementation checklist

- [x] Persist OpenIddict applications/authorizations/tokens and implement DevelopmentOffline-only OAuth metadata, authorization, consent, token, revocation, exact-resource checks, and fixed-client register/revoke callers in Web.
- [ ] Provide approved durable production OAuth/key custody and an approved remote client/callback for the canonical issuer/resource.
- [ ] Expose only a reviewed named-tool inventory mapped one-to-one to existing/simultaneously delivered Core use cases and policies; enforce account reload/current role, revocation, scope/resource checks and persisted-descendant Box restriction on every `/mcp` request.

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
| Implemented (not executed for this archive) | `--register-development-mcp-client` / `--revoke-development-mcp-client` source path | DevelopmentOffline OpenIddict application persistence and `Client` security events | A real local command caller is connected to fixed-client create/update/delete behavior | command execution, OAuth HTTP replay, remote `/mcp`, deployment, hosted callback, durable production keys, or acceptance |
