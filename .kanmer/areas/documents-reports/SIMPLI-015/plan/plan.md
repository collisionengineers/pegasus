# Plan — SIMPLI-015: record the renderer + extractor integration direction; re-scope SIMPLI-013/014

Diff estimate: docs-only, ~4 files (+~110 / −~6): one new thin ADR, its index row, two register cells in `workspaces/README.md`, plus Kanmer re-scoping (no repository code). No root task plan is needed (docs-only).

## Approach

Write **one thin ADR** recording the one durable decision the operator made on 2026-08-14 and the 2026-08-17 assessment confirmed: the report renderer and the document extractor are **integrated into the Pegasus application when they gain a caller — not extracted into standalone repositories or NuGet packages**. The ADR states the decision and its consequences (which ADR-0009 conditions still gate activation; that the renderer's design-tree coupling and the extractor's `.doc`/`.msg` gap are the concrete reasons; that packaging can be produced *from* this repo later if a second consumer appears). Mechanics — which port, which project folder, execution location, MCP consolidation — are **not** decided here; they belong to the owning FRDs and the two implementation tickets when they activate (`RPT-01…05` are scheduled `Later` / `1.1.0`; the extractor is not alpha-critical). ADR-0009 is not superseded (it anticipated integration behind Core contracts); its stale `ai-centre/` line is out of scope and noted.

Then re-scope [[SIMPLI-013]] and [[SIMPLI-014]] on the board to the integration direction with migration notes, link the renderer sub-decision tickets TICK-203–208, 211–216 to SIMPLI-014 as the open sub-decisions the ADR names, and update the two `workspaces/README.md` register cells so the repo tree agrees with the board.

Governing docs: [ADR-0009](../../../docs/adr/0009-adopt-pegasus-monorepo-workspaces.md) (unchanged, refined); new ADR-0025 (this ticket); `docs/adr/README.md` index. Reuses: ADR-0009's activation conditions verbatim rather than restating new ones.

## Steps

1. Author `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` (frontmatter per AGENTS.md; `status: accepted` — the direction is an operator decision recorded on this ticket on 2026-08-14; `related_capabilities: [RPT-01…RPT-05, INT-10, INT-11, INT-12]`; `related_frd: [frd-11, frd-05, frd-02]`); add its row to `docs/adr/README.md`.
2. `workspaces/README.md` — the two "Integration status" cells cite ADR-0025 (direction: integrate; still no caller/deployment/acceptance).
3. Kanmer: retitle/rebody SIMPLI-013 → "Integrate CollisionDocNet behind `IIntakeSourceReader` for `.doc` and `.msg` intake" and SIMPLI-014 → "Integrate CollisionRenderer behind a Core-owned render contract"; each body carries the migration note (was "standalone"), the activation conditions, and the sub-decisions; link SIMPLI-014 → TICK-203–208, 211–216 (`relates`); both stay taken on this branch until this PR merges, then are released back to Backlog (they are `Later` work) with `refs` → ADR-0025.
4. Verify: `scripts/Test-DocumentationLinks.ps1` (or the documentation CI lane) passes; `Test-MarkdownPlacement` accepts the new file (it is under `docs/adr/`); ADR index row present.
5. PR to `dev` (docs-only: reviewer checks diff + description for missing/unauthorised scope); merge; move to done (proof: the merged ADR + index + register + the re-scoped tickets).

## Verification (acceptance — the ticket's own three lines)

- Accepted ADR records the integration direction and contract for both workspaces → ADR-0025 merged, indexed.
- SIMPLI-013 / SIMPLI-014 re-scoped with migration notes → titles/bodies/refs updated, released to Backlog as `Later` work.
- Disposition of the temp-plans renderer content and TICK-203–216 recorded → research already carries the preserved content; TICK links recorded on SIMPLI-014; ADR names them.

## Risks / stop rules

- Do not decide mechanics in the ADR (one decision per ADR; mechanics go to FRD/implementation).
- Do not touch `Pegasus.slnx`, any project reference, or a workspace project file — that is activation work under ADR-0009's conditions, not this ticket.
