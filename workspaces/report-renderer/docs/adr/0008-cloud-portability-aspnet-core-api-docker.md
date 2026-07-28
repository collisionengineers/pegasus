# 0008 — Cloud portability via ASP.NET Core API + Playwright Docker image

## Status

Accepted

## Context

Beyond the desktop and CLI, the renderer must be callable as a service so other systems can
request documents — and it must deploy to ordinary container hosts (for example Cloud Run).
This is only feasible because `CollisionRenderer.Core` was kept free of Windows-only
dependencies (ADRs 0002, 0003). The remaining work is to expose Core over HTTP and to package
it, with its Chromium dependency and correct fonts, in a portable image, while keeping a
simple optional access control for hosted deployments.

## Decision

Ship `CollisionRenderer.Api`, an ASP.NET Core minimal API wrapping the shared Core renderer,
packaged by a multi-stage `Dockerfile` at the repository root. The runtime stage uses the
official Playwright .NET image (`mcr.microsoft.com/playwright/dotnet`), which bundles the
matching Chromium build and its native dependencies; the image additionally installs
`fonts-liberation` (with a `fonts-dejavu-core` fallback) so the Arial-metric body copy
renders identically on Linux.

Endpoints:

- `GET /healthz`
- `GET /v1/templates`
- `POST /v1/validate`
- `POST /v1/render` (artifact JSON with optional base64)
- `POST /v1/render.pdf` (raw PDF bytes)

Access control is **optional bearer auth**: if the `CR_API_TOKEN` environment variable is
set, every request except `/healthz` must present `Authorization: Bearer <token>`; if it is
unset, the API is open (suited to a trusted/internal deployment).

## Consequences

- The same Core engine that powers the desktop and CLI is reachable as a service, with parity
  guaranteed because the API is just another thin client (ADR 0002).
- The image is portable to any container host; Chromium and the right fonts travel with it,
  so Linux output matches Windows output.
- `/healthz` stays unauthenticated for liveness probes even when a token is configured.
- Bearer auth is a single shared token, not per-user identity — adequate for an internal or
  gateway-fronted service, but not a full authorisation model. Larger Chromium-bearing image
  size is accepted for self-containment.

## Alternatives considered

- **No API (desktop + CLI only):** blocks system-to-system integration, a stated goal.
  Rejected.
- **A full controller-based ASP.NET Core app with a heavy auth stack (e.g. OAuth/JWT
  issuer):** more than this internal service needs; the minimal API plus optional bearer token
  keeps the surface small. Rejected for now, and not precluded later.
- **Hand-built Docker image installing Chromium manually:** duplicates what the official
  Playwright image already provides and is more fragile to keep in step with the Playwright
  version. Rejected.
