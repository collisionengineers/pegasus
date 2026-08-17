# Post-implementation report — KANMER-002

## What changed

- Moved `docs/design.md` to `docs/design/README.md` and moved every tracked file formerly under top-level `design/**` to the same relative path under `docs/design/**`. This includes five renderer templates/styles, the logo and three signatures, three comparison rasters, and the complete design-system package (package files, build script, source, component docs and logo).
- Retargeted every live consumer of those paths:
  - `.design-sync/config.json` and `.design-sync/NOTES.md`;
  - `.gitattributes` and `.gitignore`;
  - `workspaces/report-renderer/Dockerfile`, `CollisionRenderer.Core.csproj`, NOTICE, renderer docs and workspace ADR references;
  - `docs/index.md`, `docs/current-architecture.md`, PRD/FRD headers and design links, `docs/open-decisions.md`, document-extraction workspace docs, reference index, Web comments and CSS comments;
  - `docs/design/system/scripts/build.mjs`, whose repository-root traversal gained one level after the move.
- Retired all 21 tracked files under `docs/temp-plans/**`. Completed task detail remains in Git/Kanmer; the still-live renderer integration direction and work list were copied into [[SIMPLI-015]] research/files before deletion.
- Updated `AGENTS.md` and `docs/index.md` so transient task research, plans, checklists, reviews and proof live in Kanmer ticket documents. Removed the retired-directory exemption from `scripts/Test-DocumentationLinks.ps1`.
- Deleted `reference/workproviders-and-repairers/contacts/providers.xlsx`; it was byte-identical to `reference/workproviders-and-repairers/providers.xlsx` (SHA-256 `25F7E2C6893F741A743F5C22FDF619032DC63D6B7AA92D24B3F842CC04E40E5F`) and had no inbound tracked reference.
- Audited the ignored local `artifacts/` root. Previously identified obsolete planning/audit candidates were already absent. The remaining `artifacts/tools` is the sole dotnet-ef installation, not a duplicate, so no local artifact was deleted. Active intake, evaluation, local-development, acceptance, release, staging and test evidence was preserved.
- No tracked empty directory existed; after deletion Git has no `docs/temp-plans/` tree.

## Plan and governing-doc alignment

All checklist scope was implemented. Repository process remains owned by AGENTS.md and navigation by `docs/index.md`; no PRD, FRD or ADR was invented or behaviorally changed. Existing PRD/FRD references were path-retargeted only. `docs/operator-notes.md` was deliberately untouched because its earlier proposed retirement was outside the ticket body.

## Verification

- `pwsh ./scripts/Test-DocumentationLinks.ps1` — PASS, all relative links resolve across 214 Markdown files.
- `npm ci` then `npm run build` in `docs/design/system` — PASS; bundled `dist/index.js`, copied the real Web stylesheet and emitted declarations. npm reported one moderate dependency advisory; no dependency version was changed by this path-only task.
- `dotnet restore src/CollisionRenderer.Core/CollisionRenderer.Core.csproj --locked-mode` — PASS.
- `dotnet build src/CollisionRenderer.Core/CollisionRenderer.Core.csproj --configuration Release --no-restore` — PASS, 0 warnings and 0 errors.
- `dotnet test tests/CollisionRenderer.Core.Tests/CollisionRenderer.Core.Tests.csproj --configuration Release` — PASS, 173 passed, 0 failed, 0 skipped.
- JSON parse of `.design-sync/config.json` — PASS.
- Exhaustive live search excluding historical CHANGELOG and generated node_modules — zero `docs/design.md`, `docs/temp-plans`, or `docs/docs/design` hits.
- `git diff --check` — PASS.

## Risks and follow-up

- External Claude Design sync has not been invoked; its tracked config/build paths were retargeted and the package build passed.
- The npm advisory is pre-existing dependency-health work, not caused or remediated here.
- SIMPLI-015 remains the implementation owner for renderer/document-extractor integration; KANMER-002 preserved its required planning context but did not implement it.

## Review brief

The independent reviewer should verify the plan did not omit a ticket-body cleanup area, every deleted plan has durable coverage, the design move has no live stale consumer, the duplicate proof is sufficient, the artifact preservation decision is safe, and the PR contains no product-behavior change.

## Review correction — PR-001

Independent review found that `.design-sync/config.json` retained a pre-move package-relative guideline glob. It and `.design-sync/NOTES.md` now use `../README.md`, which resolves from `docs/design/system` to the existing canonical `docs/design/README.md`. The explicit resolved-path check, documentation-link validation, design-system build and diff check all pass.
