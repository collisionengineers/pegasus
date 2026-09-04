# Plan — DELIV-047: Make Linux the authorised Pegasus release workstation

## Objective

Make Linux x64 under WSL the sole authorised build and terminal platform for the existing direct production release route, prove immutable release artifacts locally, and preserve every exact-SHA, target, approval, migration, rollback, smoke and documentation gate.

## Starting state

The Web, Worker and OCI outputs are already Linux x64. The build script defaults only the migration bundle to Windows, downstream validation accepts old Windows manifests, the runbook/agent guidance still declares a Windows release terminal, and ORAS is absent from this host despite being an existing artifact dependency. Azure CLI and azd have no active sign-in. Evidence: `research/research.md`@`423978d1b499b0af`, `files/files.md`@`7e03e7ea40f4cbbd`.

## Governing docs

Meets ADR-0007 by preserving its authorised direct-terminal sequence and explicit migration boundary. Meets ADR-0014 by keeping local development and production as the only environments. The operator explicitly requested a Linux or platform-independent route; this plan records a new thin ADR-0037 selecting Linux x64 for the terminal without superseding either existing decision. After the ADR exists, link it to DELIV-047.

## Required changes

Enforce Linux x64 at artifact construction, emit only a schema-3 manifest with `migrationRuntimeIdentifier=linux-x64` and `migrationBundleName=efbundle`, reject older/Windows manifests from upload or migration gates, make ORAS an explicit doctor prerequisite, and align the canonical release skill, agent rules, runbook and current-state docs. Install official ORAS 1.3.4 in the WSL user tool path so the exact artifact path can be exercised. The production route remains the existing azd/Azure CLI route; no Docker deployment wrapper is added.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Add | `docs/adr/0037-linux-authorised-release-workstation.md` | Durable Linux-x64 release-host decision. |
| Modify | `docs/adr/README.md` | Index ADR-0037. |
| Modify | `AGENTS.md` | Replace the obsolete Windows-only release rule. |
| Modify | `docs/runbook.md` | Linux release procedure and `efbundle` command. |
| Modify | `docs/current-architecture.md` | Implemented release-tooling shape, without deployment claim. |
| Modify | `docs/operations.md` | Current readiness and remaining authorization/authentication boundary. |
| Modify | `scripts/Build-ReleaseArtifacts.ps1` | Linux-x64 host and manifest contract. |
| Modify | `scripts/Test-AzureDeploymentPlan.ps1` | Fail-closed schema/runtime/bundle validation and source assertions. |
| Modify | `scripts/Invoke-Doctor.ps1` | Existing tool-health caller for ORAS. |
| Modify | `scripts/PegasusPlatform.ps1` | Single ORAS repair-hint owner. |
| Modify | `scripts/Test-PegasusPlatform.ps1` | Focused platform/repair test. |
| Modify | `.agents/skills/pegasus-release/SKILL.md` | Canonical Linux release preflight and artifact wording. |
| Modify | `.agents/skills/pegasus-release/references/database-migration.md` | Linux bundle execution. |
| Modify | `.zcode/skills/pegasus-release/SKILL.md` | Forward stale duplicate guidance to the canonical skill. |

## Do not modify

- `src/**`
- `tests/**`
- `infra/**`
- `.github/**`
- `docs/operator-notes.md`
- `corpus/**`
- `.worktrees/kanmer/**`

## Constraints

No new package dependency, deployment unit, CI route or product feature. Use PowerShell 7 and existing helpers. Keep secrets out of code, commands, board documents and proof. Local artifact generation is authorized; `dev` to `main` and every Azure/database write are not. Prior Windows release artifacts remain retained evidence/rollback material but are not accepted as new release inputs.

## Ordered steps

### Step 1 — Enforce the Linux artifact contract

- Preconditions: DELIV-047 is taken on its exact `origin/dev` worktree and official ORAS 1.3.4 is installed outside the repository.
- Files: `scripts/Build-ReleaseArtifacts.ps1`, `scripts/Test-AzureDeploymentPlan.ps1`, `scripts/Invoke-Doctor.ps1`, `scripts/PegasusPlatform.ps1`, `scripts/Test-PegasusPlatform.ps1`
- Change: reuse `Get-PegasusPlatform`; require Linux x64; emit schema 3 with fixed Linux bundle identity; reject missing/old/Windows identity; doctor-check ORAS through the existing repair table.
- Preserved behaviour: exact clean SHA, locked restores, Linux Web/Worker/OCI identity, hashes, migration identity and Azure target validation remain unchanged.
- Forbidden: Windows fallback, host-path constants, new dependency manager, Docker production route or weakened manifest checks.
- Negative cases: non-Linux host, missing ORAS, schema 2, `win-x64`, `efbundle.exe`, missing artifact or hash mismatch fail explicitly.
- Tests: `scripts/Test-PegasusPlatform.ps1` and `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`.
- Commands: `pwsh ./scripts/Test-PegasusPlatform.ps1`; `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`.
- Expected output: both exit 0 and name the passing Linux/Local contracts.
- Done when: one enforced Linux artifact contract owns build and validation.
- Deviation stop: stop if an existing release script requires a Windows-only API or the manifest change would prevent retained artifacts from being used for rollback.

### Step 2 — Align governing and operator guidance

- Preconditions: Step 1 contract is concrete and ADR-0007/ADR-0014 remain in force.
- Files: `docs/adr/0037-linux-authorised-release-workstation.md`, `docs/adr/README.md`, `AGENTS.md`, `docs/runbook.md`, `docs/current-architecture.md`, `docs/operations.md`, `.agents/skills/pegasus-release/SKILL.md`, `.agents/skills/pegasus-release/references/database-migration.md`, `.zcode/skills/pegasus-release/SKILL.md`
- Change: add ADR-0037, link it to the ticket, replace Windows commands/claims with Linux `efbundle`, state ORAS and authentication prerequisites, and retain exact approval boundaries.
- Preserved behaviour: direct terminal, promotion lease, immutable manifest, explicit migration, ACR digest, provision/Worker order, smoke, rollback and post-release docs.
- Forbidden: production claims, secret values, CI deployment, staging, operator-notes edits or duplicated release procedures.
- Negative cases: guidance must not imply local proof is deployment proof or that authentication grants write approval.
- Tests: documentation links, Markdown placement, targeted stale-wording census and release-skill link resolution.
- Commands: `pwsh ./scripts/Test-DocumentationLinks.ps1`; `pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD`; targeted `rg`; `git diff --check`.
- Expected output: all exit 0; only historical Windows evidence remains where explicitly retained.
- Done when: all active authorities and agent routes name Linux x64 consistently.
- Deviation stop: stop if the change would alter operator business truth or conflict with a later accepted ADR.

### Step 3 — Prove exact Linux release artifacts and open the PR

- Preconditions: Steps 1 and 2 pass; worktree is clean except this ticket diff; no SQL/browser-heavy process overlaps the build.
- Files: `scripts/Build-ReleaseArtifacts.ps1`, `scripts/Test-AzureDeploymentPlan.ps1`
- Change: commit the implementation, then from the exact clean commit build version `0.1.0-alpha.947`, validate Local and Artifact modes, inspect manifest/file/executable identity, run canonical restore/build/tests, simplify the branch diff, push and open one PR to `dev`.
- Preserved behaviour: generated artifacts remain ignored and are never committed; no Azure sign-in or cloud write is attempted.
- Forbidden: production promotion, ACR upload, migration, provision, deploy, fabricated smoke or deletion of retained release artifacts.
- Negative cases: artifact proof fails on dirty source, wrong SHA, missing ORAS, wrong runtime/bundle/schema, hash mismatch or non-executable bundle.
- Tests: exact artifact build/validation plus canonical repository rail.
- Commands: `pwsh ./scripts/Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.947 -SourceRevision <exact-commit>`; `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`; `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Artifact -ManifestPath ./artifacts/releases/0.1.0-alpha.947/release-manifest.json`; canonical locked restore/build/test; documentation checks; `git diff --check`.
- Expected output: schema-3 Linux manifest with four hash-valid artifacts, executable `efbundle`, linux/amd64 OCI descriptor, and all repository checks exit 0.
- Done when: PR targets `dev`, traceability is recorded, and ticket is in Review.
- Deviation stop: stop on any failed test or if local equivalence needs an Azure write.

## Acceptance checks

- Production caller/route: `.agents/skills/pegasus-release/SKILL.md` remains the one authorised terminal route and calls the existing build, validation, migration/bootstrap and smoke scripts.
- Runtime artifacts: Web OCI is linux/amd64, Web/Worker ZIPs are Linux publishes, and the self-contained executable migration bundle is `linux-x64`; every artifact is hash-bound in schema 3.
- Data boundary: no schema change occurs. The existing migration-before-application and runtime-grant census remain unchanged.
- Local evidence: exact clean commit artifact build plus Local/Artifact validation and canonical solution commands pass on WSL.
- Post-merge production evidence: after fresh Azure authentication, exact `MERGE AUTH GRANTED`, and exact-target write approval, promote the reviewed SHA, build/approve the manifest, execute only required migration/upload/provision/deploy steps, run full smoke, and update both current-state documents. Until then the ticket remains production-unverified.

## Commands

Run the step commands from the recorded Linux ticket worktree. Post-merge live commands are owned by the canonical `pegasus-release` skill and may run only after the explicit authorities named above.

## Failure and deviation rules

Preserve every failed attempt and exit code. Do not weaken a test, accept a Windows/old manifest, infer Azure authentication, relabel local evidence as live, add a second release route, or perform any remote write without exact authority. A failure in artifact identity, migration/grants, live smoke or target readback stops the release.

## Stop condition

Stop pre-merge with one reviewed Linux-route PR open against `dev`. After merge, verification may perform production promotion/release only with fresh `MERGE AUTH GRANTED` and exact-target cloud-write approval; otherwise retain the ticket in Verifying and report the precise authority still required.
