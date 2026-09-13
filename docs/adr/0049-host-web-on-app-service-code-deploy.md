---
id: ADR-0049
status: accepted
date: 2026-09-13
supersedes: [ADR-0015, ADR-0028]
superseded_by: []
related_capabilities: [EXT-08]
related_frd: [frd-12]
tags: [hosting, appservice, deployment]
---

# ADR-0049: Host Pegasus Web on an Azure App Service Web App by code deployment

## Status

Accepted. This record supersedes ADR-0015 (Container Apps hosting, the
registry, the OCI archive and digest-pinned activation) and the Container App
consequence of ADR-0028 (the renderer's execution boundary is now decided by
ADR-0050).

## Context

ADR-0015 moved Web from the Linux App Service route of ADR-0002 to Container
Apps Consumption because the subscription's aggregate App Service quota in UK
South was zero. ADR-0028 then made the Web container load-bearing by placing the
Chromium report renderer inside its image.

On 2026-09-13 a read-only `Microsoft.Web/validate` dry run for Linux `B1`,
`P0v3` and `P1v3` plans in `uksouth` returned success for this subscription and
`az appservice list-locations` lists UK South for each, so the quota block no
longer applies. ADR-0050 replaces Chromium with a pure .NET renderer, so no
native browser dependency remains in the Web boundary. The operator has asked
to leave container hosting: the OCI build, ORAS upload, registry and revision
mechanics are the largest part of the release procedure and exist only to carry
that image.

Pegasus.Web is already host-agnostic: health endpoints, forwarded headers,
Data Protection keys in Blob Storage, a user-assigned managed identity and
environment-variable configuration.

## Decision

Pegasus.Web runs on a Linux Azure App Service plan (`B1`, Always On) in UK
South as a code-deployed Web App on the platform `DOTNETCORE|10.0` stack. The
release artifact is `web.zip` from `dotnet publish`, deployed with
`az webapp deploy --type zip` and run from package. The Web App keeps the
existing user-assigned identity, role assignments, Key Vault references,
`/health/ready` as its health-check path, HTTPS only, TLS 1.2 minimum and FTPS
disabled. The `webActivation` fail-closed gate remains: the site is created
only when explicitly approved.

The Container Apps environment, the Container App, the container registry and
the `AcrPull` assignment leave the template in the release after cutover. The
Worker's Flex Consumption hosting is unchanged.

## Consequences

- The release skill loses the OCI archive, ORAS, digest and revision steps;
  activation is a zip deployment and the destructive-migration containment
  uses `az webapp stop`/`start` for Web.
- The public hostname changes to `azurewebsites.net`. Entra redirect URIs, the
  Graph mail webhook notification URL, public upload links and the Provider
  API base address are re-pointed at cutover; a custom domain is a separate
  operator decision.
- Fixed compute replaces scale-to-zero; the plan can be resized within the
  Basic tier without redeploying.
- Minor and patch .NET runtime versions follow the platform's rollout rather
  than a pinned base image.
- This decision proves architecture only; deployment, live verification and
  operator acceptance remain separate evidence states.
