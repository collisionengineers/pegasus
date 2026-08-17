# Proof — SIMPLI-015 (verified on merged `dev`)

## What landed

PR #389 (https://github.com/collisionengineers/pegasus/pull/389), docs-only, merged into `dev` as **`40ee3103`** on 2026-08-17 12:49 UTC. Independent docs-only review: **PASS** (two wording nits fixed in `01f300f9`).

- `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` — `status: accepted` (accepted 2026-08-14 by the Collision Engineers product owner as an operator direction recorded on this ticket; assessment reconfirmed 2026-08-17). One decision: when either workspace gains a real Pegasus caller it is integrated behind a Core-owned port as a project in this repository, never extracted into a standalone repository or NuGet package. Nothing is integrated; ADR-0009's activation conditions stand.
- `docs/adr/README.md` — indexed in the accepted table (related FRD-02, FRD-05, FRD-11).
- `workspaces/README.md` — both integration-status cells cite ADR-0025 and say "not a standalone package"; provenance untouched.

## Verification on `40ee3103` (ticket worktree detached at the merge commit)

| Check | Result |
| --- | --- |
| ADR file present with complete frontmatter (`id`, `status: accepted`, `date`, `supersedes`, `superseded_by`, `related_capabilities`, `related_frd`, `tags`) | yes |
| Indexed in `docs/adr/README.md`; cited twice in `workspaces/README.md` | yes |
| `scripts/Test-DocumentationLinks.ps1` | All relative Markdown links resolve (220 files checked) |
| `scripts/Test-MarkdownPlacement.ps1 -Base 5e59f933 -Head 40ee3103` | Markdown placement passed |
| CI on PR #389 | changes / documentation / reference-data / source-workspaces pass; code lanes correctly skipped |

## Ticket verification lines

- **Accepted ADR records the integration direction and contract for both workspaces** — ADR-0025 merged and indexed; "contract" = ADR-0009's activation conditions restated by reference (Core port, caller-backed proof, parity/security/licence, migration/recovery, operator acceptance), mechanics deliberately deferred to FRDs and implementation tickets.
- **SIMPLI-013 / SIMPLI-014 re-scoped with migration notes** — retitled "Integrate CollisionDocNet behind `IIntakeSourceReader` for `.doc` and `.msg` intake" and "Integrate CollisionRenderer behind a Core-owned render contract"; each body carries the migration note (was "standalone"; replacement for TICK-220/221), the activation conditions, and (014) the twelve sub-decision links; 014's stale standalone checklist replaced with a history note. Both released to Backlog as `Later` work with `refs` → ADR-0025 at closeout.
- **Disposition of the temp-plans renderer content and TICK-203–216 recorded; nothing lost** — the preserved planning content is in this ticket's `research` (written by KANMER-002 before the temp-plans deletion); TICK-203–208 and 211–216 are linked from SIMPLI-014 as the open sub-decisions the ADR names; TICK-209/210 were proof tickets already consolidated into SIMPLI-014 and archived — correctly not in the decision list.

## Not claimed

No workspace is integrated, referenced from `Pegasus.slnx`, built, or deployed. `docs/current-architecture.md`'s workspace section is unchanged because the as-built state is unchanged.
