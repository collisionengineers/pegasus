---
id: SIMPLI-014
type: ticket
title: Integrate CollisionRenderer behind a Core-owned render contract
status: done
area: documents-reports
order: 410
assignee: codex-mcp-client
profile: feature
stageEntered:
  backlog: '2026-08-17T12:53:36.226Z'
  preparing: '2026-08-19T08:57:22.253Z'
  review: '2026-08-19T09:56:33.525Z'
  verifying: '2026-08-19T10:29:35.071Z'
  done: '2026-08-19T10:33:08.662Z'
labels: []
groups:
  - EPIC-002
  - EPIC-004
links:
  - TICK-221
blocks:
  - DOCS-001
  - TICK-096
  - TICK-097
  - TICK-081
  - TICK-092
refs:
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0028-run-integrated-renderer-in-web-container-app.md
commits:
  - b10aba3d1266e302c534f6d5e88d8da5aa51585c
  - b6db5f4d73ee17b3b488e8e65a694dbf06d96260
  - cdb50cd2bbeb84fe69172407adaca06298a437a2
  - b548b674e31d05de6f43eeb285a25dedd7d2a768
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/415'
deployment: production
archived: false
created: '2026-08-13T14:38:42.351Z'
updated: '2026-09-01T14:44:32.033Z'
---

## What

Bring `workspaces/report-renderer/` (CollisionRenderer: Scriban templates + headless-Chromium/Playwright + PDFsharp) into the application as the Infrastructure adapter behind a `Pegasus.Core`-owned render contract, so Pegasus can produce its reports (`RPT-01`–`RPT-05`, `1.1.0`).

## Why

Direction: [ADR-0025](docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md) — integrate, do not extract. Pegasus has no renderer; the renderer already embeds this repository's design tree (`docs/design/assets/report-renderer/**`, `docs/design/brand/**`) and builds from this repo root; its templates are product behaviour that must co-version with FRD-11 and Core policy.

## Migration note (2026-08-17)

Was "Make report renderer standalone" (replacement for [[TICK-221]]; its checklist carried the container-build and MCPB proofs migrated from [[TICK-209]]/[[TICK-210]]). Re-scoped by [[SIMPLI-015]] to the integration direction the operator set on 2026-08-14. Not alpha-cutover work; scheduled with `RPT-01`–`RPT-05` (`Later`, `1.1.0`).

## Approach

- Core owns the render contract and report policy; `CollisionRenderer.Core` becomes the Infrastructure adapter; templates, `report.css`, logo and signatures move to embedded resources under the design authority (pin logical names; verify the complete set); retire `CollisionRenderer.Api` (Pegasus.Web replaces it); consolidate MCP tools into Pegasus.Web's existing MCP host or defer per [[SIMPLI-012]] — a win32 MCPB, if it survives, is a separately buildable project in this repository.
- Open sub-decisions to resolve at activation (linked below): [[TICK-203]] MCP design vs Automation Actor, [[TICK-204]] outcome variants, [[TICK-205]] repair-spec vs dual-Audit-spec, [[TICK-206]] template→capability mapping, [[TICK-207]] Audit template, [[TICK-208]] Sent evidence through correction, [[TICK-211]] analyzer strictness, [[TICK-212]] package lock files, [[TICK-213]] density scope, [[TICK-214]] MCPB host boundary, [[TICK-215]] production execution location (Worker container with the Playwright base image is the obvious candidate), [[TICK-216]] unaccepted wording/signature assets behind a closed gate.
- Activation conditions are ADR-0009's; home under `src/`; update `Pegasus.slnx`, `DependencyDirectionTests`, `.github/workflows/workspaces.yml`, `workspaces/README.md`, `TreatWarningsAsErrors` reconciliation; retain the workspace ADRs as history until the integration deliberately supersedes a mechanism.

## Verification

- [ ] A real Pegasus caller renders at least one accepted report variant end to end through the composed Web/Worker path, with the design-tree assets pinned and verified.
- [ ] Architecture tests updated; the workspace no longer exists as a non-caller import; the sub-decision tickets above are closed or superseded.

## Outcome

Integrated CollisionRenderer into the existing Pegasus Core/Infrastructure/Web monolith and retired the separate workspace/API/CLI/MCP/MCPB/container boundary. The active surface is the approved rendererref1 assessment plus fee note for four Core-owned outcomes, with Andy Patterson as the only complete selectable engineer tuple and unsupported families/wording fail closed.

Merged by [PR #415](https://github.com/collisionengineers/pegasus/pull/415) to `dev` at `b548b674e31d05de6f43eeb285a25dedd7d2a768` on 2026-08-19. Verified locally and in green CI at the integrated source/Web-composition tier. Automatic triggering, durable report identity/custody/correction remain with [[DOCS-001]]; Azure runtime/deployment proof remains with [[PLAT-007]]. No deployment or `main` update occurred.
