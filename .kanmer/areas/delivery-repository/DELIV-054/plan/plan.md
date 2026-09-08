# Plan — DELIV-054: include hidden runtime directories in release ZIPs

## Objective

Restore hidden runtime directories to release ZIPs while retaining the current
portable workstation mapping, schema-3 manifest, Linux deployment targets, and
Web OCI archive behavior.

## Starting state

The builder at current `dev` HEAD `7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae`
uses `Compress-Archive` with a wildcard for both ZIPs. Commit
`80acaf56e65d45c53f46bda75924f1a5f0dd3ed2` supplies the isolated
`ZipFile::CreateFromDirectory` correction, while current code already carries
ADR-0039's portable migration-bundle behavior. Estimated diff: three scripts,
about 40 lines. Evidence:
`research/research.md`@`b719d6ff1276e8a4`,
`files/files.md`@`f5b6c5e7cdea91de`.

## Governing docs

- `docs/adr/0039-windows-and-linux-release-workstations.md` — Meets:
  preserve its Windows/Linux workstation mapping, schema-3 manifest, and
  Linux-x64 deployed application target; no ADR modification.
- `.agents/skills/pegasus-release/SKILL.md` — Meets: preserve its
  four-artifact release route and artifact-validation boundary; no procedure
  modification.

## Required changes

- Replace only the two glob-based ZIP calls with `ZipFile::CreateFromDirectory`
  using `includeBaseDirectory = $false`, so hidden directories are retained at
  archive root.
- Extend the existing `Test-ArtifactManifest` owner to require
  `.azurefunctions/` in `worker.zip` and `.playwright/` in `web.zip`
  before OCI inspection.
- Make the existing `Test-PegasusPlatform.ps1` manifest fixture construct
  valid ZIPs with those roots, retaining its artifact hash and OCI-stub
  assertions.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `scripts/Build-ReleaseArtifacts.ps1` | Package Web and Worker publish roots without dropping hidden directories. |
| Modify | `scripts/Test-AzureDeploymentPlan.ps1` | Validate required hidden-root entries in supplied release ZIP artifacts. |
| Modify | `scripts/Test-PegasusPlatform.ps1` | Supply real hidden-directory ZIP fixtures to the existing validator contract. |

## Do not modify

- `AGENTS.md`
- `.agents/**`
- `docs/**`
- `scripts/PegasusPlatform.ps1`
- `infra/**`
- `src/**`
- `tests/**`
- `artifacts/**`
- `.worktrees/deliv-048/**`

## Constraints

- Reuse only the `ZipFile` approach from `80acaf56`; do not import its
  unrelated release-skill or Linux-policy changes.
- Preserve ZIP root layout, the four-artifact manifest census, native bundle
  identity, OCI layout, and manifest schema 3.
- No packages, configuration, cloud operations, deployment, compatibility
  fallback, or new verification framework.
- Do not run tests, builds, packaging, browser/capture hosts, or verification
  scripts in this execution. The parent will schedule the sole host verifier
  after DELIV-053 releases its reservation.
- Do not change `AGENTS.md`; an unexpected need to do so is a parent
  coordination stop.

## Ordered steps

### Step 1 — Package publish roots without glob filtering

- Preconditions: the current builder still uses two `Compress-Archive` calls
  with `Join-Path <publish-root> '*'`.
- Files: `scripts/Build-ReleaseArtifacts.ps1`.
- Change: replace those two calls with `ZipFile::CreateFromDirectory`,
  `CompressionLevel::Optimal`, and `$false` for base-directory inclusion.
- Preserved behaviour: Web and Worker archive paths, root layout, publish
  inputs, OCI generation, native migration bundle, and schema-3 manifest stay
  unchanged.
- Forbidden: Linux-only workstation policy, generated artifacts, or changes to
  release commands.
- Negative cases: a dot-prefixed directory must not be filtered out; an extra
  top-level `web/` or `worker/` directory must not be introduced.
- Tests: host verification only; `scripts/Test-PegasusPlatform.ps1` and
  artifact-mode deployment-plan validation after an isolated package build.
- Commands: none in this execution.
- Expected output: source diff changes only the two ZIP construction calls.
- Done when: both ZIPs are created by `ZipFile` with their publish roots as
  archive roots.
- Deviation stop: the current builder differs materially from the researched
  shape, or the correction requires a fourth source file.

### Step 2 — Validate hidden roots in release artifacts

- Preconditions: `Test-ArtifactManifest` has already verified the four
  artifact names, sizes, hashes, and native bundle identity.
- Files: `scripts/Test-AzureDeploymentPlan.ps1`.
- Change: add one local ZIP-entry check that requires
  `.azurefunctions/` in `worker.zip` and `.playwright/` in `web.zip`
  before OCI inspection.
- Preserved behaviour: artifact hashes, schema-3 validation, workstation-native
  bundle checks, and OCI inspection ordering remain unchanged.
- Forbidden: manifest schema changes, a second validator, or a non-root entry
  match.
- Negative cases: missing either hidden root fails artifact validation before
  the OCI tool is called.
- Tests: host verification only; existing platform fixture and
  `Test-AzureDeploymentPlan.ps1 -Mode Artifact` against a built artifact.
- Commands: none in this execution.
- Expected output: a precise failure naming the ZIP and required hidden root.
- Done when: validator rejects artifacts missing either required root.
- Deviation stop: the check needs a package, a manifest change, or unrelated
  deployment validator edits.

### Step 3 — Make the existing fixture exercise ZIP-entry validation

- Preconditions: the platform script loads the real
  `Test-ArtifactManifest` function and stubs only ORAS.
- Files: `scripts/Test-PegasusPlatform.ps1`.
- Change: replace placeholder text `web.zip` and `worker.zip` fixtures with
  real ZIPs that contain `.playwright/` and `.azurefunctions/` respectively,
  then calculate the existing artifact hashes from those ZIPs.
- Preserved behaviour: native-bundle platform mapping, fixture isolation,
  cleanup safeguards, manifest-negative cases, and ORAS call census remain
  unchanged.
- Forbidden: a new test project, generated committed artifacts, browser host,
  or package installation.
- Negative cases: the valid fixture must satisfy new checks, while removal of a
  required root is left to the validator's named failure path.
- Tests: host verification only; `pwsh ./scripts/Test-PegasusPlatform.ps1`.
- Commands: none in this execution.
- Expected output: fixture retains a valid manifest and reaches the existing
  OCI-stub assertions.
- Done when: fixture ZIPs prove the new validation contract without a separate
  test framework.
- Deviation stop: the existing fixture cannot create ZIPs with platform
  libraries already supplied by the runtime.

### Step 4 — Handoff source-only change for reserved verification

- Preconditions: the three scoped script changes are complete and only
  expected files differ.
- Files: `scripts/Build-ReleaseArtifacts.ps1`, `scripts/Test-AzureDeploymentPlan.ps1`, and `scripts/Test-PegasusPlatform.ps1` (inspect only).
- Change: inspect the diff and record unrun host-verifier commands in the
  implementation report after the parent supplies their results.
- Preserved behaviour: DELIV-053 remains sole heavy-verification owner until
  released; no test, build, packaging, deployment, capture, or browser host is
  started here.
- Forbidden: executing a verification command, merging, cloud writes, or
  modifying DELIV-048's retained workspace.
- Negative cases: missing host results or a scope expansion leaves the ticket
  in Implementing for the parent to resolve.
- Tests: parent-arranged host-only checks:
  `pwsh ./scripts/Test-PegasusPlatform.ps1`; an isolated release-artifact
  build followed by `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Artifact
  -ManifestPath <manifest>`; and the existing deployment-plan contract
  appropriate to the verifier's scope.
- Commands: none in this execution.
- Expected output: independently recorded PASS/FAIL/INCONCLUSIVE evidence.
- Done when: implementation is ready for verification, not merged or released.
- Deviation stop: verification remains reserved, fails, is inconclusive, or
  requires any unplanned file.

## Acceptance checks

- The production release caller remains
  `scripts/Build-ReleaseArtifacts.ps1`, whose `worker.zip` is consumed by
  the existing config-zip route.
- Worker ZIPs contain root `.azurefunctions/`; Web ZIPs contain root
  `.playwright/`; neither gains a publish-root wrapper directory.
- Existing artifact validation checks the actual archive entries and the
  existing platform fixture covers the validator.
- The diff preserves current portable workstation/native-bundle, OCI, and
  schema-3 manifest contracts.
- Required verification is explicitly deferred to the one host verifier; no
  verification command is run by this implementation worker.

## Commands

No commands may be run in this execution. After DELIV-053 releases the host
reservation, the parent may assign the verifier the Step 4 commands in an
isolated exact-head worktree.

## Failure and deviation rules

Stop and report if a scoped script changes concurrently, a required hidden root
is not part of the publish output, a new dependency/framework is needed, host
verification is unavailable, or verification finds a failure. Do not repair
unrelated release policy, generated artifacts, DELIV-048, or documentation.

## Stop condition

Stop after the scoped code and existing fixture are ready for the parent to
schedule independent verification. Do not run verification, commit, push, open
a PR, merge, release, deploy, or start another ticket until the parent reports
the verifier and simplification outcomes.
