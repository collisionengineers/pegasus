# Files — DELIV-054

## Where the change lands

| Path | Why |
|---|---|
| `scripts/Build-ReleaseArtifacts.ps1` | Replace the two glob-based ZIP calls with `ZipFile::CreateFromDirectory`, keeping entries at the archive root. |
| `scripts/Test-AzureDeploymentPlan.ps1` | Extend the existing artifact validation to require Worker `.azurefunctions/` and Web `.playwright/` archive entries. |
| `scripts/Test-PegasusPlatform.ps1` | Update the existing manifest-validator fixture to create valid ZIPs that contain the two required hidden directory roots. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/adr/0039-windows-and-linux-release-workstations.md` | Workstation support changes only the migration bundle identity; deployed Web and Worker remain Linux x64 and manifest schema remains 3. |
| `.agents/skills/pegasus-release/SKILL.md` | The release route consumes `worker.zip` through config-zip and validates a four-artifact schema-3 manifest; this ticket does not execute it. |
| `scripts/PegasusPlatform.ps1` | The builder's existing native migration bundle mapping must remain the only platform policy owner. |
| `scripts/Test-AzureDeploymentPlan.ps1` | `Test-ArtifactManifest` is the current artifact validation owner and its ZIP checks must remain before OCI inspection. |
| `git show 80acaf56 -- scripts/Build-ReleaseArtifacts.ps1` | Supplies the exact established `ZipFile` approach and root-layout argument, without importing PR #676's unrelated release-skill edits. |
| `DELIV-048` | Is already Verifying with retained historical workspace; this successor must neither reuse nor edit it. |

## Ripple effects

The change preserves the existing release command and manifest artifact census.
A host verifier, not this implementation phase, will run the existing focused
platform and deployment-plan contracts plus ZIP-entry inspection. No application
caller, package, runtime artifact format, documentation, deployment, cloud
state, or database schema changes.

## Out of scope

- ADR-0039, `PegasusPlatform.ps1`, migration bundle policy, schema-3 manifest
  fields, Web OCI archive construction, native bundle content, and release
  procedure edits.
- Linux-only policy restoration or any compatibility path.
- DELIV-048, PR #676's unrelated documentation changes, release execution,
  deployment, and historical-record rewriting.
