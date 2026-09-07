---
id: ADR-0015
status: accepted
date: 2026-08-01
supersedes: [ADR-0002, ADR-0007]
superseded_by: []
related_capabilities: []
related_frd: []
tags: [hosting, containerapps]
---
# ADR-0015: Host Pegasus Web on Azure Container Apps Consumption

**Status:** Accepted (2026-08-01)
**Supersedes:** ADR-0002's App Service hosting, tier, and fixed-compute clauses; ADR-0007's Web ZIP deployment mechanism

## Context

The approved local-to-production replacement route originally selected Linux
App Service. UK South exposes service-plan capacity for the subscription only
behind an aggregate App Service quota that is currently zero. A permanently
allocated P0v4 plan would also introduce fixed compute cost for the low-volume
alpha operator workload.

Pegasus.Web is already a stateless ASP.NET Core boundary whose durable state is
held in Azure SQL and Azure Storage. The .NET SDK can build its reviewed source
as a local OCI image archive without Docker or a remote Azure build.

## Decision

Pegasus.Web will run in an Azure Container Apps Consumption environment in UK
South. It will use external HTTPS ingress, one single-active revision, 0.5 vCPU,
1 GiB memory, a minimum of zero replicas, and a maximum of one replica. Cold
start latency is accepted for the alpha route and is an explicit acceptance
test.

A new Basic Azure Container Registry in `rg-pegasus-prod` will hold the Web
image. Registry administration and anonymous pull remain disabled. The Web
user-assigned managed identity receives `AcrPull` and is used both for the
private image pull and the existing runtime permissions.

The release is built once locally as an OCI Linux/AMD64 archive, uploaded with
ORAS, verified against the registry manifest digest, and provisioned from the
template-owned production registry/repository plus an exact `sha256` digest.
Tags are navigation only and never deployment
identity. No placeholder image, Docker daemon, ACR build, `azd up`, remote
build, or release-time rebuild is permitted.

The Container App is created only after the database migration and interactive
Administrator bootstrap have completed. Pegasus.Web, its routes, Core ports,
business policy, authentication behavior, SQL schema, and custody boundaries
do not change.

## Consequences

- The App Service plan, Web App, App Service quota request, Web ZIP deployment,
  and `azurewebsites.net` address leave the active production route.
- `web.zip` remains an immutable bootstrap-only artifact; the deployed Web
  artifact is the OCI archive and its verified registry digest.
- The production infrastructure adds a Container Apps environment and a paid
  Basic registry. Web compute can scale to zero, but the registry retains its
  daily SKU charge.
- The existing authorised-terminal, local-to-production, migration, health,
  smoke, monitoring, recovery, exact-target approval, and acceptance gates
  remain in force.
- This decision proves architecture only. Implementation, deployment, live
  verification, and operator acceptance remain separate evidence states.
