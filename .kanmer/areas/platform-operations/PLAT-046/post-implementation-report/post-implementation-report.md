# Post-implementation report — PLAT-046

## Summary

Implemented the planned destructive-migration shutdown procedure on branch `PLAT-046-destructive-migration-shutdown`, at `7e5aff7cf9c2bb69710ec962c4b5e18ded9fde08` (base `9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c`).

The release skill now distinguishes unchanged/additive and destructive routes. The destructive route stages the approved new Worker with every trigger disabled while the old schema is intact, proves the old Worker is stopped and the exact old Web revision inactive with zero replicas before SQL, verifies migration/grants/head, provisions the approved new Web while the new Worker remains disabled, and explicitly re-enables and smokes the compatible runtime. It fails closed on unknown, malformed, or drifting state and prohibits old-runtime recovery after destructive SQL begins.

## Changed files

- `.agents/skills/pegasus-release/SKILL.md` — planning classification, route selection, containment, provision read-back, and explicit activation.
- `.agents/skills/pegasus-release/references/database-migration.md` — destructive SQL boundary after proven containment.
- `docs/adr/0046-destructive-migration-runtime-shutdown.md`, `docs/adr/0030-non-additive-schema-changes-before-cutover.md`, and `docs/adr/README.md` — decision and partial supersession linkage.
- `docs/runbook.md` and `AGENTS.md` — operational and repository guidance aligned with the decision.
- `scripts/PegasusPlatform.ps1`, `scripts/Test-AzureDeploymentPlan.ps1`, and `scripts/Invoke-ProductionSmoke.ps1` — one canonical Worker Disabled-setting census and its two consumers.
- `scripts/Test-PegasusPlatform.ps1` — offline contracts for the census wiring and fail-closed semantics.

## Verification

Exact-head PASS at `7e5aff7cf9c2bb69710ec962c4b5e18ded9fde08`:

- `pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1`
- `pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`
- `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` (141 files)
- `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c -Head 7e5aff7cf9c2bb69710ec962c4b5e18ded9fde08`
- bounded PowerShell-fence parser: 19 fences, 0 parse failures.
- `git diff --check` passed before the commits (line-ending advisories only).

Retained results: the predecessor commit `bbae334ca33c1f89617dfe458d8d7ac45dff24a0` failed `Test-AzureDeploymentPlan -Mode Local` because its test still parsed a duplicate literal setting census; `7e5aff7` corrects that assertion. The first parser-harness invocation on `7e5aff7` failed before parsing because two paths were passed as one native argument; the later authorized parser-only retry passed. No result is erased.

## Risk and hand-off

No Azure, SQL, package, browser, or live migration operation was run or authorized. The short outage, exact targets, and post-release usage-window approval remain release-time operator responsibilities. The destructive route is forward-only once SQL starts.

Independent review should assess this exact head and the scoped verification record. After merge, `kanmer-verify` should re-run the merged-result scoped script/documentation checks and record the proof; no deployment is a condition of ordinary Done.
