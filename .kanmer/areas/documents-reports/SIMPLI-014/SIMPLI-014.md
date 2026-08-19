---
id: SIMPLI-014
type: ticket
title: Integrate CollisionRenderer behind a Core-owned render contract
status: implementing
area: documents-reports
order: 200
assignee: codex-mcp-client
profile: feature
stageEntered:
  backlog: '2026-08-17T12:53:36.226Z'
  preparing: '2026-08-19T08:57:22.253Z'
taken_at: '2026-08-19T09:22:15.273Z'
branch: task/simpli-014-integrate-collision-renderer
worktree: ../pegasus-worktrees/simpli-014-integrate-collision-renderer
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
  - TICK-100
  - TICK-081
refs:
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0028-run-integrated-renderer-in-web-container-app.md
archived: false
created: '2026-08-13T14:38:42.351Z'
updated: '2026-08-19T09:42:35.259Z'
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
