---
id: SIMPLI-013
type: ticket
title: Integrate CollisionDocNet behind IIntakeSourceReader for .doc and .msg intake
status: implementing
area: intake-processing
order: 190
assignee: claude-code
profile: feature
stageEntered:
  backlog: '2026-08-17T12:53:28.687Z'
  preparing: '2026-08-20T03:28:28.884Z'
  implementing: '2026-08-20T03:42:12.960Z'
taken_at: '2026-08-20T03:28:37.656Z'
branch: task/simpli-013-collisiondocnet-integration
worktree: ../pegasus-worktrees/simpli-013
labels: []
groups:
  - EPIC-002
links:
  - TICK-220
blocks: []
refs:
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
commits:
  - c7457628
  - d999277d
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/449'
archived: false
created: '2026-08-13T14:38:42.313Z'
updated: '2026-08-20T04:19:06.314Z'
---

## What

Bring the `workspaces/document-extraction/` (CollisionDocNet) source into the application as a project behind the Core `IIntakeSourceReader` port, so `.doc` (legacy Word) and `.msg` (Outlook) sources are extracted instead of parked for manual sorting.

## Why

Direction: [ADR-0025](docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md) — integrate, do not extract into a standalone package. The only caller-backed reason to bring the extractor in is the `.doc`/`.msg` gap in `MimeKitPdfPigOpenXmlIntakeSourceReader` (they are "retained for manual sorting", `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs:118`).

## Migration note (2026-08-17)

Was "Make document extractor a standalone .NET package" (replacement for [[TICK-220]]). Re-scoped by [[SIMPLI-015]] to the integration direction the operator set on 2026-08-14. Not alpha-cutover work; scheduled with its capabilities (`Later`).

## Approach

- Resolve the ADR-0001/ADR-0003 overlap first, in FRD-05 and here: either scope CollisionDocNet to `.doc`/`.msg` with PdfPig remaining the PDF path, or replace the PDF path with corpus parity evidence — one live PDF implementation, not two.
- Activation conditions are ADR-0009's: Core contract, real caller with proof, parity/security/licence evidence, migration/coexistence and recovery, operator acceptance. Home is under `src/` (a project leaves `workspaces/` when it becomes a caller dependency); update `Pegasus.slnx`, `DependencyDirectionTests`, `.github/workflows/workspaces.yml`, `workspaces/README.md`, `TreatWarningsAsErrors` reconciliation.
- Simplicity rails: reuse `IIntakeSourceReader`; no second extraction pipeline; no interface without a second caller.

## Verification

- [ ] A `.msg` and a `.doc` upload are extracted through the real Web/Worker path and produce receipts with text and assets; PDF/DOCX/EML behaviour unchanged (or replaced with recorded parity evidence).
- [ ] Architecture tests updated to the new project set; the workspace no longer exists as a non-caller import.

## Outcome
