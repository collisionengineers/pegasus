# Research — SIMPLI-001: Make AI Centre a standalone repository

## Question

What must be preserved, repointed, and approved to extract the imported AI Centre source workspace from Pegasus without changing its source, activating an AI capability, or moving private data into Git?

## Findings

- `workspaces/ai-centre/` is a source-only workspace, not a Pegasus project, caller, deployment unit, or policy owner.
  - `workspaces/README.md`, `workspaces/AGENTS.md`, `workspaces/ai-centre/README.md`, and ADR-0009 all require it to remain independent of `Pegasus.slnx` and production composition until a separately accepted contract and real caller exist.
- The current tracked extraction set is 266 files: 224 under `skills/`, 35 under `services/`, five under `docs/` or `ml-ops/`, and the root `README.md` and `.gitignore`.
  - `services/collision-brain/` is a standalone locked .NET solution; `skills/tools/test_pack_skill.py` validates the reusable agent-skill packages.
- `workspaces/ai-centre/skills/` contains protected external source. Root `AGENTS.md` forbids modifying, deleting, renaming, regenerating, or normalising each named package without prompt-specific authorisation that names the exact package and operation.
  - A repository extraction must therefore preserve those files byte-for-byte; a later Pegasus removal needs explicit authorisation for every protected package affected.
- The source has functional Pegasus-relative documentation links: `workspaces/ai-centre/README.md`, `docs/architecture.md`, and Collision Brain operations documentation point to root Pegasus documentation, `reference/`, `workspaces/report-renderer`, `corpus/ai-centre/`, and `artifacts/`.
  - Those links and the corpus/artifact custody contract need a deliberate cross-repository replacement; copying any corpus, archive, credential, model weight, local setting, or generated output is forbidden.
- Root CI and the runbook currently own AI Centre validation.
  - `.github/workflows/workspaces.yml` restores, builds, and tests Collision Brain and runs the Python skill-pack test; `docs/runbook.md` provides the same local commands. `scripts/Test-DocumentationLinks.ps1` validates tracked relative Markdown links.
- Git history contains the AI Centre path from its import onward, including `b53447d0` (AI Centre import) and later source/documentation corrections.
  - A path-filtered clone can preserve only this path's history; a snapshot transfer is possible but deliberately loses that history.

## Implications

The extraction must be an approval-gated repository migration, not a source refactor. It needs a new-repository target, an explicit history decision, a new home for repository-scoped controls and CI, and a reviewed Pegasus retirement change. It must preserve the source-only and no-private-data boundaries, and it must not turn AI Centre into an application dependency or a deployed service.

## Open questions

- What organisation, repository name, visibility, maintainers, branch protection, and transfer owner are authorised for the new remote?
- Is a history-preserving filtered repository required, or is a documented snapshot acceptable?
- Does `corpus/ai-centre/` remain owner-provisioned outside both repositories, move to a separately approved AI Centre-local custody root, or use another approved configuration?
- Which exact protected skill packages, if any, are authorised to be removed from Pegasus after cutover?
- Should the new repository carry a superseding architecture record, or should Pegasus record the decision in a new ADR that supersedes the AI Centre portion of ADR-0009?
