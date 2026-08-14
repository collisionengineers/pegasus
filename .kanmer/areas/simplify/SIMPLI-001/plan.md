# Plan — SIMPLI-001: Make AI Centre a standalone repository

Written from research.md and impact.md.

## Approach

Extract AI Centre as an independently governed source repository while preserving its present source-only relationship to Pegasus. The recommended migration mode is a path-filtered, history-preserving clone because it retains the imported-source timeline without carrying unrelated Pegasus history. A snapshot is acceptable only if the owner explicitly accepts the lost history. No remote repository, source deletion, data transfer, capability activation, or protected-skill modification is part of this plan until the listed approvals are obtained.

**No Pegasus ADR is created for this extraction.** Moving non-caller workspace source out to its own repository is repository housekeeping, not a Pegasus product or architecture decision — a workspace never owns application policy. ADR-0009 remains the historical record of the original monorepo import (still valid for the workspaces that stay) and is neither edited nor superseded. The extraction is recorded on this Kanmer ticket only.

## Steps

1. Obtain the migration decision and exact authority before changing source: new repository organisation/name/visibility/maintainers, transfer owner, branch and review rules, history-preserving versus snapshot mode, corpus/artifact custody, and the disposition of the Pegasus copy. Obtain prompt-specific authority identifying each protected skill package and the permitted Pegasus removal operation. No ADR is filed.

2. Produce a read-only transfer manifest from `workspaces/ai-centre/`: Git path list, byte hashes, file count by subtree, current source-provenance records, ignored/private-material exclusions, and a list of Pegasus-relative documentation links. Confirm the transfer contains code and tracked documentation only, never `corpus/`, local environments, credentials, caches, generated output, model weights, or nested Git metadata.

3. In an isolated clone or new worktree, create the standalone repository using the approved migration mode. For the recommended mode, filter history to `workspaces/ai-centre/` and rename that path to the new repository root; for a snapshot, initialise one documented import commit that carries the existing provenance. Compare the resulting tree to the manifest before any documentation changes. Do not alter `skills/**`.

4. Establish independent repository ownership and validation. Add repository-level instructions and contribution/security ownership appropriate to the new repository; move or recreate the AI Centre-only CI so it validates Collision Brain's locked restore/build/test and `skills/tools/test_pack_skill.py`. Keep toolchains and dependencies independent of Pegasus and retain the current no-caller/no-deployment boundary.

5. Repair cross-repository documentation only outside protected skill packages. Replace relative Pegasus documentation and report-renderer references with approved stable links or an explicit configured contract location; describe the approved external corpus/artifact custody without moving any data. Preserve the statements that Pegasus Core owns business policy, the engineer remains the decision-maker, and a future integration still requires an accepted contract, real caller, evidence, recovery, and operator acceptance.

6. Validate the standalone repository from a clean checkout: compare its source manifest, run its relative-link check, restore/build/test `services/collision-brain/CollisionBrain.slnx` in Release with locked restore, and run the skill-package Python test. Verify that no Docker profile, model, connector, corpus processing, or external service was run. Obtain an independent review of the extraction boundary and CI.

7. Only after the standalone repository is independently reviewed, green, and reachable to its maintainers, make a separate Pegasus retirement PR. Remove the authorised `workspaces/ai-centre/` source copy, remove only its two validation steps from `.github/workflows/workspaces.yml`, and update `workspaces/README.md`, `docs/runbook.md`, `docs/current-architecture.md`, and root `AGENTS.md` so they no longer claim it is a local workspace. Do not disturb the other workspaces or any historic ADR; ADR-0009 is left exactly as it is.

8. Prove the Pegasus retirement change: run the documentation-link check, confirm no active production-facing documentation or CI path points to `workspaces/ai-centre/`, confirm `Pegasus.slnx` and application projects never reference it, and run the canonical Pegasus restore/build/test profile required by the runbook. Merge only after independent review and green CI; then record cutover evidence and archive the old workspace register/provenance rather than silently erasing source history.

## Verification

- Standalone repository: exact pre-repoint source manifest comparison; clean `dotnet restore ./CollisionBrain.slnx --locked-mode`, Release build/test, Python skill-pack test, and a checked set of all cross-repository documentation links.
- Pegasus retirement PR: `pwsh ./scripts/Test-DocumentationLinks.ps1`; targeted searches showing no live `workspaces/ai-centre/` caller, CI, or documentation reference; and the runbook's canonical `dotnet restore`, Release build, and focused/full test profile.
- Review evidence: an independent reviewer verifies that the plan retained every source-only, private-data, protected-skill, and no-activation boundary, and that the new repository's CI covers the validation removed from Pegasus.

## Risks / open questions

- New-repository creation and publishing are external writes and require the exact approved target. No target is currently supplied.
- Removing the source from Pegasus changes protected external packages. The required authority must name the exact packages and removal operation; otherwise the source stays in place after the standalone repository is prepared.
- The current corpus is deliberately outside Git at the Pegasus-root path. Choosing a new custody root is a policy and operational decision, not a mechanical directory move.
- Existing relative links will break after extraction unless they are repointed. Cross-repository URLs cannot be finalised until the target repository is known.
- A filtered history needs a clean, isolated clone and a manifest comparison; it must never rewrite `dev`, `main`, or the existing Pegasus history.

---

## Rectification — taken over by claude-code 2026-08-14

**Discrepancy resolved.** Investigated `origin/task/ai-centre-standalone-repository`
(272 files, −22,892 lines). This branch **deletes the entire `workspaces/ai-centre/`
tree**, including that workspace's OWN internal ADRs
(`workspaces/ai-centre/docs/adr/*`, `.../collision-brain/docs/adr/*`). It creates
**no** Pegasus-root `docs/adr/` ADRs, and PR #374 creates none either. So the
"new ADRs were created" concern is unfounded. **No Pegasus ADR is filed for this
extraction** — moving non-caller workspace source out is repository housekeeping,
not a product/architecture decision; ADR-0009 stays untouched as history. The only
real discrepancy is that several Pegasus-root docs still describe ai-centre as a
**local** workspace.

**Hard dependency (blocked-by [[SIMPLI-006]] / PR #374, now merged):** rebase on the
current `dev` before executing. #374 deleted `docs/requirements.md` and renamed
`docs/architecture.md → docs/current-architecture.md`; step 7 targets
**`docs/current-architecture.md`**.

**Exact remove/change list (Pegasus-root, applied after the ai-centre tree deletion):**

1. **AGENTS.md** (= CLAUDE.md symlink): remove the protected-skills safety rail
   (~L160, "…protected external source under `workspaces/ai-centre/skills/`…Never
   modify…") and update the product-invariant line (~L191, "AI Centre owns AI
   experimentation only…") so neither implies a local ai-centre workspace.
2. **docs/current-architecture.md**: in `### Workspaces`, remove the AI Centre
   register entry ("…merged under `ai-centre/skills/`") and note it is now an
   external standalone repository.
3. **workspaces/README.md**: remove the ai-centre register/provenance row (keep
   document-extraction and report-renderer). Also apply the #374 review reply on
   **workspace provenance** for the document-extraction extraction edits here.
4. **.github/workflows/workspaces.yml**: remove ONLY the two ai-centre steps
   (~L43-51: "Validate AI Centre workspace" collision-brain build/test +
   `skills/tools` unittest). Leave the other workspaces' steps intact.
5. **docs/runbook.md**: remove/repoint the ai-centre references — ~L41 (AI Centre
   pgvector container note) and ~L370-371 (the `workspaces/ai-centre/…` build/test
   commands).
6. **Do NOT touch ADR-0009 or any other ADR, and do NOT add an ADR.** ADR-0009
   adopted multiple workspaces and stays valid/immutable for the ones that remain;
   the extraction is recorded on this ticket, not in the decision log.
7. **Sequence:** ship the standalone ai-centre repo first (independently reviewed,
   green, reachable to its maintainers); only then this Pegasus retirement PR
   removes the tree + references.

**Verification:** `pwsh ./scripts/Test-DocumentationLinks.ps1` green; `git grep -niE
'ai-centre|collision-brain'` returns only git history, ADR-0009 (unchanged), and the
standalone-repo pointer; `source-workspaces` CI passes without the ai-centre steps;
the protected-skills AGENTS rail is gone; the 7 AGENTS.md-marker integration tests
still pass. No new ADR exists.
