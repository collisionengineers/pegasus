---
id: ADR-0007
status: accepted
date: 2026-07-25
supersedes: [ADR-0002]
superseded_by: [ADR-0014, ADR-0015, ADR-0037]
related_capabilities: []
related_frd: []
tags: [deployment]
---
# ADR-0007: Direct authorised-terminal Azure deployment

- Status: Accepted for direct authorized-terminal deployment. Environment
  clauses are superseded by ADR-0014, Web ZIP/hosting clauses by ADR-0015,
  and the Windows-only release-workstation restriction by ADR-0037.
  `superseded_by` records these partial relationships; the retained principle
  does not prove a current release or recovery run.
- Date: 2026-07-25
- Supersedes: ADR-0002 deployment mechanism only

## Context

Pegasus has committed Bicep and an `azd` service manifest, but neither
proves a runnable release route. GitHub Actions/OIDC deployment is a permanent
`Not planned` boundary. Production needs an explicit migration boundary, package
identity, and a recoverable direct deployment appropriate to the F1/B1 topology.

## Decision

Azure development/integration and production deployments are made only from an
authorised terminal, using committed Bicep and `azd`, after exact approval for
the target environment and intended writes. Local development, isolated Azure
development/integration, and production remain separate boundaries.

The intended production order is:

1. locally validate, then create Web, Worker, and migration bundles once and
   record their hashes/provenance;
2. preview/provision the approved infrastructure change;
3. apply the explicit immutable migration bundle;
4. deploy the hashed Web package, prove live and ready endpoints;
5. deploy the hashed Worker package and record smoke evidence.

`azd up` is not a production release route because it combines provision,
package, and deploy without the required explicit migration boundary. GitHub
Actions/OIDC deployment, deployment slots/S1, and a staging environment are not
introduced as substitutes. Rollback redeploys the prior application package; it
does not down-migrate or delete data-bearing resources.

## Current limitations

This is a target design, not a runnable procedure. `azure.yaml` has no migration
step; `dotnet ef` is not pinned or available for a migration bundle;
`AZURE_PRINCIPAL_NAME` requires preflight; and the least-privilege Entra
directory-resolution route for `CREATE USER ... FROM EXTERNAL PROVIDER` remains
unresolved. Target runtimes, pinned dependencies/tools, package paths,
hash/provenance recording, build-once/deploy-same-artifact proof, and the
`SCM_DO_BUILD_DURING_DEPLOYMENT=true` conflict also require a separate
infrastructure implementation. No deployment command in this ADR is authorised
or ready to run.

## Recovery boundary

`0.1.0-alpha.1` requires a one-time backup/restore proof meeting the 15-minute RPO and
four-hour RTO targets. Recurring quarterly recovery exercises are `Not planned`; that
does not waive the `0.1.0-alpha.1` proof or its evidence.

## Consequences

- ADR-0002 continues to govern the modular monolith and Azure runtime.
- The release owner must preserve exact target, approval, artifact hashes,
  migration identity, probe result, and rollback evidence.
- The missing package, migration, identity, and script work needs its own
  implementation plan before Azure deployment can be attempted.

## Sources

- [Azure Developer CLI reference](https://learn.microsoft.com/azure/developer/azure-developer-cli/reference)
- [EF Core migration bundles](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying#bundles)
- [CREATE USER (Transact-SQL)](https://learn.microsoft.com/sql/t-sql/statements/create-user-transact-sql)
