# Post-implementation report — DELIV-047

## Result

PR #667 targets `dev` from `DELIV-047-linux-release` at exact head `287fc2e46aeee4999c8bab18349ea44f32b40b4d`. Linux x64 is enforced at release-artifact construction, schema-3 validation rejects old or Windows bundle inputs, ORAS 1.3.4 is an explicit Cloud Doctor prerequisite, and current guidance names the same Linux direct-terminal route. No application, database schema, infrastructure template, CI, Azure or production state changed.

## Reuse and wiring

The change reuses `Get-PegasusPlatform`, `Invoke-Doctor.ps1`, its shared repair-hint table, `Test-AzureDeploymentPlan.ps1`, and the existing azd/Azure CLI release sequence. The canonical caller remains `.agents/skills/pegasus-release/SKILL.md`; Docker remains a local build/database dependency and is not a production deployer.

## Verification

At exact clean head `287fc2e46aeee4999c8bab18349ea44f32b40b4d`, release `0.1.0-alpha.947` built with schema 3, `linux-x64` executable `efbundle`, four hash-valid artifacts and a `linux/amd64` OCI descriptor. Local and Artifact deployment-plan validation passed. Documentation links checked 126 files, Markdown placement passed, and `git diff --check` passed. Canonical locked restore and Release build passed with zero warnings/errors. Tests passed: Core 1225/1225, Architecture 100/100, Integration 1264 passed / 7 intentional skips / 0 failed in 37m34s against a fresh loopback-only pinned SQL Server container.

## Attempts and deviations

The first canonical attempt passed Core and Integration but failed one Architecture fixture because the newly inspected build script was absent from the isolated test fixture. The bounded fixture copy was added without changing assertions; focused, full Architecture and full canonical final-head runs then passed. Earlier ORAS and Cloud Doctor setup attempts and their dispositions remain preserved in `scratch/execution.md`.

## Remaining boundary

Production promotion/deployment is deliberately not performed. It requires fresh Azure authentication, exact-target cloud-write approval, and `MERGE AUTH GRANTED` immediately before the `dev` to `main` update. Current-state documents must be refreshed after that live release.
