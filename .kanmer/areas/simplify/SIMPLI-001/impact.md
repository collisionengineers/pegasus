# Impact — SIMPLI-001

The files and modules this change touches, surveyed BEFORE planning.

| File / module | Change | Risk |
|---|---|---|
| New AI Centre repository root | Receive the extracted source, independent repository controls, CI, and ownership metadata. | No remote target or authority is currently supplied; external creation/push is not authorised. |
| `workspaces/ai-centre/**` | Extract intact, then remove from Pegasus only after independent validation and explicit protected-source authorisation. | `skills/**` is protected external source; private corpus and generated/local material must never be transferred. |
| `workspaces/ai-centre/README.md` | Replace broken relative Pegasus references with approved cross-repository documentation links/configuration. | Must preserve the Core-owned policy, no-caller, approval, renderer, and data-custody boundaries. |
| `workspaces/ai-centre/docs/architecture.md` and `services/collision-brain/docs/operations.md` | Repoint root-relative requirements/design/operations links and document the approved corpus/artifact boundary. | Invalid links or a changed corpus location could weaken custody rules. |
| `workspaces/ai-centre/services/collision-brain/CollisionBrain.slnx`, projects, locks, Docker assets | Retain as a standalone service; validate in the new repository. | Do not run Docker, a model, or an external connector without exact approval. |
| `workspaces/ai-centre/skills/**` and `skills/tools/test_pack_skill.py` | Preserve the packages verbatim; retain their independent packaging test. | No source edits, normalisation, or package regeneration without per-package authorisation. |
| `.github/workflows/workspaces.yml` | Remove the two AI Centre validation steps only after equivalent validation lands in the new repository. | The remaining document-extraction and report-renderer workspace jobs must stay intact. |
| `docs/runbook.md`, `docs/architecture.md`, `workspaces/README.md`, and root `AGENTS.md` | Remove or repoint the retired workspace references; the workspace register/provenance rows must no longer imply a local import. | Root documentation and safety constraints could become stale; the immutable ADR is not edited. |
| `docs/adr/0009-adopt-pegasus-monorepo-workspaces.md` | Remains immutable historic record. | The extraction needs an accepted superseding ADR rather than retroactive mutation. |
| `scripts/Test-DocumentationLinks.ps1` | Use to prove no broken relative Markdown links remain in the Pegasus retirement change; establish an equivalent check in the new repository. | Relative cross-repository links will fail unless deliberately converted. |

## Ripple effects

- Existing temporary plans may mention `workspaces/ai-centre/`; they are historic/supporting material, not a reason to leave stale active production documentation.
- The new repository must independently run Collision Brain's locked restore/build/test and the skill-package Python test before Pegasus removes its copies.
- Pegasus must continue to have no project reference, runtime load path, deployment package, or application caller for AI Centre.
- The ignored immutable corpus remains an owner-controlled local-data operation; the migration cannot copy it, upload it, or place it under source control.

## Out of scope

- Implementing or activating an AI agent, model, desktop application, connector, or Pegasus adapter.
- Moving private corpora, Outlook/Box data, credentials, model weights, build outputs, caches, or generated artifacts.
- Changing the contents of any protected package in `workspaces/ai-centre/skills/`.
- Editing ADR-0009 or rewriting Pegasus history.
- Creating a remote repository, changing external access, or pushing source before exact external-write approval.

## Repository-wide affected-file inventory

This is the current Pegasus-side scope, derived from tracked references and CI; it is a planning inventory, not permission to remove the workspace.

| Path | Planned disposition |
|---|---|
| `workspaces/ai-centre/**` (266 tracked files: 224 under `skills/`) | Copy or filter into the approved standalone repository byte-for-byte. Remove from Pegasus only in the separately approved retirement PR and only with prompt-specific authority for each protected skill package. |
| `.github/workflows/workspaces.yml` | Move the AI Centre restore/build/test and skill-pack validation into the standalone repository; then remove only those two AI Centre steps from Pegasus, retaining the other workspace jobs. |
| `workspaces/README.md` | Remove the AI Centre and AI Centre skills integration-register and provenance rows; retain the other workspace records and explain the historic extraction without claiming an active local import. |
| `workspaces/AGENTS.md` | Replace the AI Centre-specific ownership statement if needed while preserving the generic source-workspace constraints for remaining imports. |
| `docs/architecture.md` | Remove the current-state statement that AI Centre and its skills are merged beneath the local workspace path; retain the no-caller boundary as historic/standalone context where appropriate. |
| `docs/runbook.md` | Remove the local AI Centre validation commands and the local pgvector prerequisite reference, after their standalone equivalents are live and verified. |
| `AGENTS.md` | Retire the local protected-package path rule only as part of the authorised workspace removal; preserve any successor handling/instruction needed for the independent repository. |
| `.gitignore` | Remove the AI Centre-specific ignored paths only after the source has left Pegasus; do not add corpus, secrets, model, cache, or output files to Git. |
| `scripts/Test-DocumentationLinks.ps1` | Revisit its AI Centre skill-path exclusion after removal; keep the link checker for Pegasus and add an equivalent checker to the standalone repository. |
| New `docs/adr/####-*.md` plus `docs/adr/README.md` | Add an accepted superseding decision before extraction; ADR-0009 itself remains immutable. The number/title are assigned only through the repository’s ADR process. |
| `docs/adr/0009-adopt-pegasus-monorepo-workspaces.md`, `CHANGELOG.md`, and `docs/temp-plans/**` | Historical evidence: do not rewrite merely to erase prior paths. Assess any surviving operationally-active link separately. |

### Confirmed non-code boundary

- No change is planned to `Pegasus.slnx`, `src/**`, or `tests/**`: current searches show no AI Centre project, caller, runtime-load, deployment, or test dependency to migrate.
- No changes are authorised within `workspaces/ai-centre/skills/**` during planning. Any eventual Pegasus removal is an explicit protected-source operation, not normal cleanup.
- The target standalone repository's root controls and CI are new files, but their final paths and cross-repository links depend on the approved remote target and migration mode.

### Sequencing dependency

The standalone-repository handover (source manifest, independent CI, documentation repoints, clean validation, independent review) precedes the Pegasus retirement PR. Until that handover is accepted and reachable, this inventory is read-only and no path is removed from Pegasus.
