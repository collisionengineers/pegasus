# Plan — SIMPLI-001: Make AI Centre a standalone repository

Written from research.md and impact.md.

## Approach

Extract AI Centre as an independently governed source repository while preserving its present source-only relationship to Pegasus. The recommended migration mode is a path-filtered, history-preserving clone because it retains the imported-source timeline without carrying unrelated Pegasus history. A snapshot is acceptable only if the owner explicitly accepts the lost history. No remote repository, source deletion, data transfer, capability activation, or protected-skill modification is part of this plan until the listed approvals are obtained.

## Steps

1. Obtain the migration decision and exact authority before changing source: new repository organisation/name/visibility/maintainers, transfer owner, branch and review rules, history-preserving versus snapshot mode, corpus/artifact custody, and the disposition of the Pegasus copy. Record an accepted ADR that supersedes ADR-0009 for AI Centre only; do not edit the historic ADR. Obtain prompt-specific authority identifying each protected skill package and the permitted Pegasus removal operation.

2. Produce a read-only transfer manifest from `workspaces/ai-centre/`: Git path list, byte hashes, file count by subtree, current source-provenance records, ignored/private-material exclusions, and a list of Pegasus-relative documentation links. Confirm the transfer contains code and tracked documentation only, never `corpus/`, local environments, credentials, caches, generated output, model weights, or nested Git metadata.

3. In an isolated clone or new worktree, create the standalone repository using the approved migration mode. For the recommended mode, filter history to `workspaces/ai-centre/` and rename that path to the new repository root; for a snapshot, initialise one documented import commit that carries the existing provenance. Compare the resulting tree to the manifest before any documentation changes. Do not alter `skills/**`.

4. Establish independent repository ownership and validation. Add repository-level instructions and contribution/security ownership appropriate to the new repository; move or recreate the AI Centre-only CI so it validates Collision Brain's locked restore/build/test and `skills/tools/test_pack_skill.py`. Keep toolchains and dependencies independent of Pegasus and retain the current no-caller/no-deployment boundary.

5. Repair cross-repository documentation only outside protected skill packages. Replace relative Pegasus documentation and report-renderer references with approved stable links or an explicit configured contract location; describe the approved external corpus/artifact custody without moving any data. Preserve the statements that Pegasus Core owns business policy, the engineer remains the decision-maker, and a future integration still requires an accepted contract, real caller, evidence, recovery, and operator acceptance.

6. Validate the standalone repository from a clean checkout: compare its source manifest, run its relative-link check, restore/build/test `services/collision-brain/CollisionBrain.slnx` in Release with locked restore, and run the skill-package Python test. Verify that no Docker profile, model, connector, corpus processing, or external service was run. Obtain an independent review of the extraction boundary and CI.

7. Only after the standalone repository is independently reviewed, green, and reachable to its maintainers, make a separate Pegasus retirement PR. Remove the authorised `workspaces/ai-centre/` source copy, remove only its two validation steps from `.github/workflows/workspaces.yml`, and update `workspaces/README.md`, `docs/runbook.md`, `docs/architecture.md`, and root `AGENTS.md` so they no longer claim it is a local workspace. Do not disturb the other workspaces or immutable historic ADRs.

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
