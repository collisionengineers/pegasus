---
id: ADR-0025
status: accepted
date: 2026-08-17
supersedes: []
superseded_by: []
related_capabilities: [RPT-01, RPT-02, RPT-03, RPT-04, RPT-05, INT-10, INT-11, INT-12]
related_frd: [frd-02, frd-05, frd-11]
tags: [architecture, workspaces, renderer, extraction]
---
# ADR-0025: Integrate the report renderer and document extractor into the application, not into standalone packages

- Date: 2026-08-17
- Status: accepted — records the operator's direction of 2026-08-14; the
  supporting assessment is on Kanmer ticket SIMPLI-015
- Owners: Collision Engineers product owner and Pegasus development team
- Relation: refines [ADR-0009](0009-adopt-pegasus-monorepo-workspaces.md),
  which admitted the two source workspaces and anticipated their later
  integration behind Core contracts; ADR-0009 is not superseded

## Context

`workspaces/report-renderer/` (CollisionRenderer) and
`workspaces/document-extraction/` (CollisionDocNet) are independently
buildable source imports with no Pegasus caller. Two contrary paths were in
flight: extracting each into its own repository and NuGet package for Pegasus
to consume, or integrating each into the application when it gains a caller.

The facts that decide it:

- Pegasus has **no** report renderer; report production is a core product
  capability (`RPT-01`–`RPT-05`, scheduled for `1.1.0`). CollisionRenderer's
  core project already embeds the canonical design assets from this
  repository's design tree (`docs/design/assets/report-renderer/**`,
  `docs/design/brand/**`) and its container build uses this repository as its
  build context. Its templates are Pegasus product behaviour and must
  co-version with the FRDs and Core policy that feed them.
- Pegasus already extracts PDF, DOCX and EML through `IIntakeSourceReader`
  (ADR-0001, ADR-0003) but parks `.doc` and `.msg` for manual sorting;
  CollisionDocNet extracts both. That gap is the only caller-backed reason to
  bring it in.
- Both have a single prospective consumer (Pegasus), the same owner, and are
  maintained in this repository today. The repository has no package feed, no
  central package management, and no `nuget.config`; a standalone package would
  add feed infrastructure and a release-and-bump cycle for every change, and —
  for the renderer — would either split the design authority across
  repositories or duplicate brand assets.
- The AI Centre precedent (extracted to its own repository, SIMPLI-001) is not
  comparable: it is not an application dependency.

## Decision

When either workspace gains a real Pegasus caller, it is **integrated into the
application** — a project in this repository, referenced from
`Pegasus.slnx`, composed by Web or Worker behind a `Pegasus.Core`-owned
port — and is **not** extracted into a standalone repository or consumed as a
NuGet package.

Nothing is integrated by this decision. Activation of each workspace remains
gated by ADR-0009's conditions, unchanged: a Core-owned contract
(`IIntakeSourceReader` for extraction; a Core-owned render contract for
rendering), a real caller with caller-backed proof, representative parity,
security and licence evidence, migration/coexistence and recovery behaviour,
and operator acceptance. Until then both remain non-caller source under
`workspaces/`, and the repository invariant that no workspace joins
`Pegasus.slnx` or is referenced, loaded, or deployed stands.

## Consequences

- SIMPLI-013 and SIMPLI-014 are re-scoped from "make standalone" to
  "integrate behind the Core port" and scheduled with their capabilities
  (`Later`); neither is alpha-cutover work.
- The extractor's activation must resolve its overlap with ADR-0001 ("do not
  implement the PDF file format in Pegasus code") and ADR-0003 (PdfPig): either
  scope CollisionDocNet to `.doc`/`.msg` with PdfPig remaining the PDF path, or
  replace the PDF path with parity evidence — one live PDF implementation, not
  two. That choice is made in FRD-05 and the implementation ticket, not here.
- The renderer's activation carries the open sub-decisions already on the
  board (TICK-203–208, 211–216: template-to-capability mapping, outcome
  variants, MCP/tool consolidation, execution location, analyzer strictness,
  package locks, density, MCPB host boundary, unaccepted wording). Where a
  Claude Desktop MCPB channel survives, it is a separately buildable project in
  this repository, not a separate repository.
- Packaging is not foreclosed: a package can be produced *from* this repository
  later if a second consumer appears. Extraction now would only add friction.
- Once a workspace is integrated it leaves `workspaces/` (which by definition
  holds non-caller imports); its home under `src/` and any new top-level
  directory follow the ADR-0009 rule for new production projects, and
  `.github/workflows/workspaces.yml`, `workspaces/README.md`, and
  `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` are updated in
  the activating change.

## Options considered

- **Standalone repository + NuGet package** for each — rejected: single
  consumer, same owner, no feed infrastructure, release/bump friction, and the
  renderer's design-tree coupling.
- **Leave as frozen workspaces indefinitely** — rejected as a decision; it is
  the status quo until a caller exists, which this ADR preserves.

## Links

- [ADR-0009](0009-adopt-pegasus-monorepo-workspaces.md) — workspace boundary
  and activation conditions.
- [ADR-0001](0001-hybrid-pdf-extraction.md), [ADR-0003](0003-pdfpig-for-first-qdos-slice.md)
  — the PDF path the extractor's activation must reconcile with.
- [`workspaces/README.md`](../../workspaces/README.md) — integration status
  register and import provenance.
- FRD-11 (report correction, finality and post-report work), FRD-05
  (documents, extraction and custody), FRD-02 (intake and source identity).
