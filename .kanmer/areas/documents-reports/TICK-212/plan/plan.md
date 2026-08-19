# Plan — TICK-212: Add report-renderer package lock files

## Approach

Treat TICK-212 as a decision-only prerequisite already subsumed by [[SIMPLI-014]], not as a separate lock-file implementation. Current `dev` has project-local `packages.lock.json` files for every Pegasus production and test project; the shared build action hashes those files and restores `Pegasus.slnx` with `--locked-mode`. The renderer workspace has no locks, and its API/CLI/MCP/test project boundaries are being retired. Therefore the correct outcome is not to add six workspace locks: SIMPLI-014 adds only the renderer dependencies actually used by existing Pegasus owning projects and regenerates those projects' existing canonical locks. An independent TICK-212 branch would edit the same project and lock files claimed by SIMPLI-014.

## Governing docs

- **Meets — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** The renderer becomes application code in existing Pegasus project boundaries and does not survive as a standalone product/package/project set. Updating existing project locks, while adding none for retired workspace hosts, follows that decision. No ADR change is required.
- **Meets — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.** Lock-file placement does not alter report behaviour. The resolved dependency graph supports the single Infrastructure renderer adapter while FRD-11's Core-owned policy and fail-closed behaviour remain unchanged. No FRD modification is authorized or needed.
- **Shared EPIC-004 constraint.** A second renderer project/package boundary is prohibited. Package locks must describe the integrated monolith dependency graph, not preserve the workspace topology.

## Steps

1. Confirm that SIMPLI-014's final plan/checklist retains the TICK-212 disposition: add only required Scriban/Playwright/PDFsharp references to existing owning Pegasus projects, regenerate their existing project-local locks, use canonical locked restore, and do not add locks or host-only dependencies for retired CollisionRenderer API/CLI/MCP projects. Reuse SIMPLI-014 as the sole implementation owner.
2. After SIMPLI-014's independently reviewed PR is merged, inspect its exact merged project/lock-file diff: dependency additions are minimal and attached to actual production/test callers; corresponding existing locks changed deterministically; no `workspaces/report-renderer/**/packages.lock.json` survives; and ModelContextProtocol or other retired host-only packages did not enter application locks.
3. Validate the merged dependency graph using the repository's canonical `dotnet restore ./Pegasus.slnx --locked-mode`, Release build/test evidence, and advisory output recorded by SIMPLI-014. Confirm the shared build action still keys all relevant `src/**/packages.lock.json` and `tests/**/packages.lock.json` inputs without a renderer-specific cache path.
4. Record a no-code post-implementation report and outcome linking the SIMPLI-014 PR, merge commit, lock-file diff, restore/build evidence, and proof. State that TICK-212 was subsumed and created no repository branch, worktree, commit, PR, deployment, or cloud action; then complete its remaining Kanmer gates from that evidence.

## Verification

The post-implementation report and eventual proof will cite SIMPLI-014's exact merged PR/commit and record read-only checks on merged `dev`:

- enumerate Pegasus project-local `packages.lock.json` files and identify precisely which existing locks changed;
- compare each changed project reference with its locked dependency graph and confirm only caller-backed renderer/test dependencies were introduced;
- verify no workspace renderer lock file or retired API/CLI/MCP host-only dependency remains;
- cite successful `dotnet restore ./Pegasus.slnx --locked-mode`, Release build/tests, CI cache/restore checks, and package advisory output from SIMPLI-014;
- confirm TICK-212 itself has no repository commit, PR, worktree, deployment, or cloud action.

The final lock graph cannot be accepted until SIMPLI-014's dependency migration is merged. TICK-212 owns the disposition and acceptance slice only; SIMPLI-014 owns all project, lock, source, and CI changes.

## Risks / open questions

- **Active overlap:** TICK-212's expected project and lock files are explicitly claimed by SIMPLI-014. Mitigation: no independent worktree or diff.
- **Transitive drift:** regenerating a lock can change unrelated packages. Mitigation: inspect the exact lock diff against direct project references and require an explanation for unrelated graph movement.
- **Retired dependency leakage:** host-only MCP/API/CLI packages could enter production locks during mechanical migration. Mitigation: focused negative checks for those packages and caller-based review of every new direct reference.
- **Native/runtime assets:** Playwright/PDF dependencies may expand platform assets. Mitigation: retain SIMPLI-014's build/render tests and leave deployed runtime proof to PLAT-007.
- **Operator questions:** none remain; existing repository locked-restore convention resolves the technical choice.


## Simplification pass — 2026-08-19

n/a — zero-diff subsumption. The independently reviewed SIMPLI-014 implementation already owns the project and lock-file changes. Creating renderer-workspace locks, a renderer-specific cache path, or a second dependency owner would recreate the retired boundary.
