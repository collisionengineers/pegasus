# Post-implementation report — SIMPLI-015

Branch `task/simpli-015-renderer-extractor` @ `f0057da4` on `dev` `5e59f933`. PR #389 (docs-only). Diff: 3 files, +~110/−2.

## What changed

| File | Change | Why |
| --- | --- | --- |
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` (new) | Thin ADR, `status: accepted`, dated 2026-08-17, recording the operator's 2026-08-14 direction: integrate both workspaces behind Core ports when they gain a caller; never extract to standalone repo/package. Context lists the deciding facts (no Pegasus renderer; renderer embeds this repo's design tree and builds from repo root; extractor's only caller-backed reason is `.doc`/`.msg`; single consumer/owner; no package feed; ai-centre precedent not comparable). Decision integrates nothing itself; ADR-0009 activation conditions unchanged. Consequences: 013/014 re-scoped as `Later`; ADR-0001/0003 overlap to resolve in FRD-05 (one PDF path); renderer sub-decisions TICK-203–208, 211–216 remain open; MCPB channel, if kept, is a project here; packaging not foreclosed; integrated code leaves `workspaces/`. Options considered; links. | Ticket step 1. |
| `docs/adr/README.md` | Row for 0025 in the accepted table (related FRD-02, FRD-05, FRD-11). | Index. |
| `workspaces/README.md` | Two integration-status cells cite ADR-0025 and say "not a standalone package"; provenance rows untouched. | Repo tree agrees with the board. |

Board changes (this ticket's Kanmer half): SIMPLI-013 and SIMPLI-014 retitled and rebodied with migration notes, activation conditions and (014) the twelve sub-decision links; SIMPLI-014's stale "standalone" checklist replaced with a history note.

## Deviations from the plan

None. Mechanics deliberately not decided in the ADR (one decision per ADR).

## Verification

- `scripts/Test-DocumentationLinks.ps1`: all relative Markdown links resolve (219 files checked).
- `scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD`: passed.
- No code, project, or workspace file changed.

## Not claimed

No workspace is integrated, referenced, built into the solution, or deployed by this change.
