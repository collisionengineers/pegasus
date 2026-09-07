---
id: ADR-0039
status: accepted
date: 2026-09-07
supersedes: [ADR-0037]
superseded_by: []
related_capabilities: [OPS-10, OPS-24]
related_frd: []
tags: [deployment, windows, linux, release]
---

# ADR-0039: Windows and Linux release workstations

## Status

Accepted through DELIV-048 on the operator's explicit 7 September 2026
requirement to develop and deploy on either Windows or Linux.

## Context

ADR-0037 coupled the release workstation to the deployed Linux runtime. The
existing .NET SDK container-archive publish and portable PowerShell/Azure/ORAS
commands do not need that restriction. Only the migration executable runs on
the workstation and therefore needs its native runtime identity.

## Decision

Use the same direct-terminal release scripts on Windows x64 or Linux x64 with
PowerShell 7. Build and execute a release on one native platform. The existing
platform helper supplies the self-contained EF bundle identity:
`win-x64`/`efbundle.exe` on Windows, `linux-x64`/`efbundle` on Linux. Manifest
schema 3 records that identity and artifact validation requires the matching
workstation and name. Only Linux checks owner-execute permission.

Web and Worker remain Linux x64; the Web OCI archive remains linux/amd64 with
the existing runtime dependencies. No Windows containers, Docker daemon or
second deployment route are introduced. Exact source SHA, immutable artifacts,
manifest approval, migration and permission reconciliation before application
deployment, smoke and rollback retain their existing boundaries.

## Consequences

Operators can choose either supported workstation OS without changing the
deployed architecture. An artifact set cannot switch OS before migration;
its recorded bundle is not renamed or substituted. Windows-only development
tools remain Windows-only where their own supported capability requires it.
This decision establishes support, not native execution or deployment proof.

## Links

- Superseded choice: [ADR-0037](0037-linux-authorised-release-workstation.md).
- Direct terminal: [ADR-0007](0007-direct-terminal-azure-deployment.md).
- Environment boundary: [ADR-0014](0014-local-to-production-deployment.md).
- Procedure: [Runbook](../runbook.md#deployment-and-release).
- Microsoft [EF migration bundles](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
  and [SDK container publishing](https://learn.microsoft.com/en-us/dotnet/core/containers/sdk-publish).
