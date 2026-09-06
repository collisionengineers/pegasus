---
id: ADR-0037
status: accepted
date: 2026-09-04
supersedes: [ADR-0007]
superseded_by: []
related_capabilities: [OPS-10, OPS-24]
related_frd: []
tags: [deployment, linux, release]
---

# ADR-0037: Linux authorised release workstation

## Status

Accepted 2026-09-04 by the Collision Engineers operator through DELIV-047.
Production promotion and Azure or database writes remain separately approved
operations.

## Context

ADR-0007 selects a direct authorised-terminal release with immutable
artifacts, an explicit migration boundary and exact live evidence. ADR-0014
keeps local development and production as the only environments. The deployed
Web and Worker runtimes and their packages are already Linux x64, but the
migration bundle default and working guidance kept the release terminal on
Windows. That split added a workstation handoff without protecting a distinct
production boundary.

Microsoft supports self-contained `linux-x64` Entity Framework migration
bundles. PowerShell 7, Azure CLI, Azure Developer CLI and ORAS support Linux,
and the existing Pegasus release scripts use those portable command-line
interfaces.

## Decision

The sole authorised Pegasus release workstation is Linux x64 on Linux-native
storage. It builds Web, Worker, OCI and the self-contained `efbundle` migration
artifact once from the exact clean release SHA. The release manifest records
`linux-x64` and rejects a Windows bundle or an older manifest contract as a new
release input.

The production route remains ADR-0007's direct terminal sequence. Docker is
not a second deployment route. GitHub Actions deployment, a staging
environment and `azd up` remain excluded. Authentication establishes identity
only; it does not grant promotion or Azure/database write authority.

## Consequences

- Local development and release can use the same WSL filesystem and runtime
  family as the deployed application.
- ORAS is a required release tool because it validates and uploads the OCI
  archive without making Docker a release dependency.
- Retained Windows release artifacts remain historical evidence and rollback
  material. They cannot pass the new-manifest gate for a new release.
- Exact-SHA promotion, manifest approval, migration-before-application,
  runtime-grant reconciliation, live smoke, rollback and current-state
  documentation gates are unchanged.
- A real production cutover still requires fresh `MERGE AUTH GRANTED` and
  exact-target approval for every Azure or database write.

## Links

- Direct terminal and release order:
  [ADR-0007](0007-direct-terminal-azure-deployment.md).
- Environment boundary: [ADR-0014](0014-local-to-production-deployment.md).
- Procedure: [Deployment and release](../runbook.md#deployment-and-release).
