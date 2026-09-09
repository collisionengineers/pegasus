---
kind: proof-record
schema: 2
merged_sha: "c3219cd28c69530441e2bba7357063372628ff37"
environment: "CEALEX-May25; PowerShell 7.6.5; .worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37 (clean detached exact merge)"
verified_at: "2026-09-09T01:12:28.947Z"
result: FAIL
failure_class: implementation
receipts: []
attempts:
  - attempted_at: "2026-09-09T00:59:21.220Z"
    command: "pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Release workstation and manifest contract passed (win-x64); Windows/Linux mappings checked.\r\nPegasus platform LocalDB state classification passed.\r\n"
  - attempted_at: "2026-09-09T00:59:22.635Z"
    command: "pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "WARNING: A new Bicep release is available: v0.46.1. Upgrade now by running \"az bicep upgrade\".\r\nAzure deployment plan validation passed (Local; Worker Disabled settings render 'true').\r\n"
  - attempted_at: "2026-09-09T00:59:31.950Z"
    command: "pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "All relative Markdown links resolve (141 files checked).\r\n"
  - attempted_at: "2026-09-09T00:59:34.278Z"
    command: "pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c -Head c3219cd28c69530441e2bba7357063372628ff37"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Markdown placement passed for 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c..c3219cd28c69530441e2bba7357063372628ff37.\r\n"
  - attempted_at: "2026-09-09T01:00:00.216Z"
    command: "$ErrorActionPreference='Stop'; $paths=@(git diff --name-only 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c HEAD -- '*.ps1'); foreach($path in $paths){$tokens=$null;$errors=$null;[void][System.Management.Automation.Language.Parser]::ParseFile((Join-Path (Get-Location) $path),[ref]$tokens,[ref]$errors);if($errors.Count){throw \"$path parse errors: $errors\"}}; $total=0; foreach($path in @('.agents/skills/pegasus-release/SKILL.md','.agents/skills/pegasus-release/references/database-migration.md')){$body=Get-Content -Raw -LiteralPath $path; $fences=[regex]::Matches($body,'(?ms)^[ \\t]*```(?:powershell|pwsh)[^\\r\\n]*\\r?\\n(.*?)^[ \\t]*```');if($fences.Count -eq 0){throw \"No fences: $path\"};foreach($fence in $fences){$tokens=$null;$errors=$null;[void][System.Management.Automation.Language.Parser]::ParseInput($fence.Groups[1].Value,[ref]$tokens,[ref]$errors);if($errors.Count){throw \"$path fence parse errors: $errors\"};$total++}}; Write-Output \"Parsed $($paths.Count) changed PowerShell files and $total recipe fences with zero errors; no parsed code executed.\""
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Parsed 4 changed PowerShell files and 19 recipe fences with zero errors; no parsed code executed.\r\n"
  - attempted_at: "2026-09-09T01:00:13.265Z"
    command: "git diff --check 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c HEAD"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Exact integrated diff whitespace validation: no errors"
  - attempted_at: "2026-09-09T01:10:51.748Z"
    command: "dotnet restore tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --locked-mode"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37"
    exit_code: 0
    result: "PASS"
    authority: "supporting"
    summary: "Architecture project and dependencies locked restore passed; no lock edits."
  - attempted_at: "2026-09-09T01:11:02.062Z"
    command: "dotnet build tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-restore -nodeReuse:false"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37"
    exit_code: 0
    result: "PASS"
    authority: "supporting"
    summary: "Release architecture build passed, 0 warnings/errors,46.18seconds."
  - attempted_at: "2026-09-09T01:12:28.947Z"
    command: "dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --filter FullyQualifiedName=Pegasus.ArchitectureTests.WorkerActivationReleaseContractTests.WorkerActivationReleaseValidationUsesTheSameExactCensusAndStopsUnsafeDisable"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-c3219cd28c69530441e2bba7357063372628ff37"
    exit_code: 1
    result: "FAIL"
    authority: "authoritative"
    failure_class: "implementation"
    summary: "1failed,0passed,0skipped.31ms. WorkerActivationReleaseContractTests line305 Assert.Equal ExpectedFunctions versus Actual[]. Its regex scans productionSmoke for quoted literal AzureWebJobs.<function>.Disabled names, removed by PR711 canonical helper refactor; omitted affected contract test was not updated."
---
# Exact integrated-result verification — PLAT-046

Verifier: `/root/verify_711_712`, distinct from implementation and review roles. PR711 is MERGED into configured `dev`, GitHub mergeCommit `c3219cd28c69530441e2bba7357063372628ff37`. Exact SHA ancestry to origin/dev passed. The source, board and implementation worktrees were not switched or modified.

## Receipt classification

Before any Git operation, read `get_status.delivery`: integrationBranch dev; default verification contract pr.yml / verify / push. `gh run list --repo collisionengineers/pegasus --workflow pr.yml --event push --commit c3219cd28c69530441e2bba7357063372628ff37 --limit 5 --json databaseId,headSha,event,status,conclusion,url,createdAt` exited 1: HTTP404, workflow pr.yml not found. This is an absent bound workflow, not a failed test receipt. Read-only workflow inventory returned only repository-check (.github/workflows/ci.yml); the exact-merge push run census returned []. Therefore every packet obligation was missing from receipts and executed locally at the exact detached merge. No PR-head CI is presented as merge-SHA proof.

## Acceptance and semantic verification

The initial six scoped checks passed; they omitted an affected architecture check and did not discharge complete acceptance. The later exact-merge check below FAILS. Platform contracts exercise the canonical Worker Disabled census and missing/extra/duplicate/wrong-value fail-closed semantics; Local deployment validation confirms both real script consumers and exact Bicep census. Four changed PowerShell files and all 19 release/migration PowerShell fences parsed without executing any recipe. Links and placement passed (141 Markdown files).

Manual literal comparison of the integrated release/migration recipe, ADR0046 and runbook to the plan confirms: planning classifies destructive operations and affected capability before approval; explicit short-outage and post-release outside-usage window; exact Single/one-active Web inventory; approved new Worker staging while old schema intact and all Functions disabled; whole Worker Stopped plus exact old Web inactive/zero replicas freshly before SQL; native/JSON/unknown failures stop; manifest migration/grants/head precede new Web and explicit compatible activation; no old bytes revived after destructive SQL. Normal additive route remains separate, with no readiness service, alert weakening, dependency or compatibility path added. This is procedure/source verification, not live-runtime or artifact acceptance.

## Retained prior attempts and limitations

Prior premerge bbae334 Local deployment-plan contract failure and the first 7e5aff7 parser-harness invocation failure remain recorded in scratch/execution and the implementation report; neither occurred at this merge SHA. They were corrected and independently reviewed before merge, not relabeled PASS. This sentence was superseded by the later postmerge failure below; the current result is FAIL. Bicep printed only its available-upgrade warning; no upgrade performed.

The initial six-check run executed no dotnet restore/build/test. The later architecture-only restore/build/test below ran. No production smoke execution, package build, live SQL, deployment or other cloud write ran. Production no-exception acceptance remains conditional on the separately approved release following this procedure. Final exact-merge worktree was clean and host heavy-process census zero. Implementation worktree/branch remain for closeout.

## Newly discovered missed affected check — authoritative FAIL

PR712 current-head CI exposed WorkerActivationReleaseContractTests.WorkerActivationReleaseValidationUsesTheSameExactCensusAndStopsUnsafeDisable failing after incorporation of PR711. This is new evidence invalidating the earlier verification coverage. Root requested reproduction at PR711's exact merge. Read-only reconcile_ticket returned EVIDENCE_INCONCLUSIVE and recommendation:null; no reconciliation mutation was attempted. Gates then permitted Done→Verifying with the explicit omitted-check reason.

Receipt lookup occurred again before Git: configured pr.yml absent404; exactmerge push run census[]. Clean deterministic detached worktree was recreated at the same c3219cd merge. Locked restore and Release architectureproject build passed; the exact named test failed immediately and no retry occurred. The regex at lines295–303 scans Invoke-ProductionSmoke.ps1 for quoted literal Disabled setting names, while PR711 correctly moved that production list into Get-PegasusWorkerDisabledSettingNames. Actual[] is deterministic stale contract parsing, not evidence the running Worker lacks functions. The affected test consumer still belongs to this change and must be reconciled without reintroducing a duplicate production list or weakening intended census validation.

Classification implementation: required verification consumer was omitted from implementation. Earlier PASS attempts remain intact but do not override the final authoritative FAIL. This is neither a waiver nor a transient failure. Root owns routing Verifying→Implementing and kanmer-execute remediation; because PR711 is merged a follow-up PR is needed on this same ticket. No source edits or full-suite run occurred. Three exact owned reusable restore nodes22176/19860/24040 were identity-validated by start,parent5556,executable and command before individual cleanup. The failed-run detached worktree is clean and retained for diagnosis, not removed on non-PASS. Implementation worktree remains untouched.
