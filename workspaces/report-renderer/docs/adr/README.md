# Architecture decision records

This directory is the immutable index for Collision Renderer architecture decisions.

## Index rules

- ADR numbers are never reused.
- Accepted ADR bodies are historical records and remain unchanged.
- Corrections or changed decisions are recorded in a new ADR, not by rewriting an old body.
- A superseding ADR must identify the exact decision or detail it replaces. Unmentioned parts of an older ADR remain in force.
- Status changes are recorded in this index and, where repository policy permits, by a new ADR; they do not erase the original rationale.
- This index describes the independent source workspace and does not claim external integration or deployment.

## Immutable index

The existing ADR-0001 through ADR-0010 bodies remain unchanged. Where a retained source does not expose an ADR title, this index deliberately avoids inventing one; the existing ADR body's title and text are authoritative.

| ADR | Status | Indexed subject / authority note |
| --- | --- | --- |
| [ADR-0001](0001-rendering-engine-headless-chromium.md) | Accepted — existing | Headless Chromium rendering engine. |
| [ADR-0002](0002-modular-shared-core-thin-clients.md) | Accepted — existing | Shared Core with thin clients. |
| [ADR-0003](0003-unified-dotnet-8-stack.md) | Accepted — existing | Unified .NET 8 source-workspace stack. |
| [ADR-0004](0004-templating-scriban-plus-csharp-shell.md) | Accepted — existing | Typed model, first-party Scriban body and C#-built common shell. |
| [ADR-0005](0005-reuse-brand-css-design-system.md) | Accepted — existing | Reuse of the retained brand CSS design system. |
| [ADR-0006](0006-page-furniture-chromium-header-footer-paged-media.md) | Accepted — existing | Chromium header/footer and paged-media furniture. |
| [ADR-0007](0007-density-auto-fit.md) | Accepted — existing | Density auto-fit behavior. |
| [ADR-0008](0008-cloud-portability-aspnet-core-api-docker.md) | Accepted, partially superseded | **Only its API authentication detail is superseded by ADR-0011.** Every other decision, rationale and consequence remains in force. |
| [ADR-0009](0009-reference-material-handling.md) | Accepted — existing | Reference material handling: private examples/prior art are not product source or build inputs; sensitive material stays ignored and local. |
| [ADR-0010](0010-accept-scriban-security-advisories.md) | Accepted — existing | Constrained acceptance of Scriban NU1901–NU1904 based on first-party embedded templates and encoded value handling. |
| [ADR-0011](0011-multi-token-sha256-api-authentication.md) | Accepted | API bearer-token compatibility, rotation lists and SHA-256 configuration. Supersedes only ADR-0008's authentication detail. |

## ADR-0011 decision summary

ADR-0011 records the current accepted API authentication contract:

| Setting | Contract |
| --- | --- |
| `CR_API_TOKEN` | Retained compatibility input for one raw bearer token. |
| `CR_API_TOKENS` | Accepted raw bearer-token rotation list. |
| `CR_API_TOKEN_SHA256` | Accepted configuration for one bearer token represented by its SHA-256 value. |
| `CR_API_TOKEN_SHA256S` | Accepted configuration for a rotation list of SHA-256 token values. |

Decision details:

1. Authentication remains optional when none of the four settings is configured.
2. Configuring any supported token source protects every endpoint except `/healthz`.
3. Clients continue to present the raw secret as `Authorization: Bearer <token>`; hash configuration is a server-side secret representation.
4. Presented tokens are evaluated through SHA-256-based, constant-time comparison.
5. `CR_API_TOKEN` remains supported so existing standalone API callers are not broken solely by the addition of rotation and hashed configuration.
6. Rotation is supported through plural raw and hash settings so an old and replacement token can overlap during a controlled change.
7. The executable parser remains authoritative for list syntax and invalid-entry handling; wrappers must not define a conflicting list format.

### Rationale

A single raw environment token is simple but makes rotation disruptive and exposes the accepted secret directly in configuration. Supporting lists enables overlap during rotation. Supporting SHA-256 values allows the API to match presented tokens without requiring the accepted raw token itself in that configuration form. Retaining `CR_API_TOKEN` protects compatibility.

### Scope of supersession

ADR-0011 supersedes **only** the API authentication detail in ADR-0008. It does not supersede ADR-0008's other architectural boundaries, transport choices, host responsibilities, deployment neutrality or any unrelated rationale. ADR-0008's body is not edited.

### Consequences

- Operators may choose compatibility raw-token, raw rotation-list, single-hash or hash-list configuration.
- `/healthz` stays available for unauthenticated liveness checks.
- A token setting that is unintentionally present enables authentication and can cause `401` responses; diagnostics must check all four variables.
- Hash support does not remove the need for TLS, secret management, access controls, logging discipline or request/attachment limits.
- Future authentication changes require a new ADR and must state whether compatibility with these four settings is retained.