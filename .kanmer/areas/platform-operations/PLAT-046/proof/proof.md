---
kind: proof-record
schema: 2
merged_sha: "d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
environment: "CEALEX-May25; PowerShell 7.6.5; .worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75 (clean detached exact merge)"
verified_at: "2026-09-09T02:03:16.4284974Z"
result: PASS
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
  - attempted_at: "2026-09-09T01:54:30.0000000Z"
    exit_code: null
    result: "INCONCLUSIVE"
    authority: "supporting"
    failure_class: "inconclusive"
    summary: "Observed the prior verifier's unavailable build session at exact d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75. It had reported locked restore PASS, but the Release ArchitectureTests build session 11664 had no recoverable exit result or final log; the build result is INCONCLUSIVE and no test result is inferred. Reported cwd was the same deterministic detached worktree used below."

  - attempted_at: "2026-09-09T01:55:16.9944052Z"
    command: "dotnet restore tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --locked-mode"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Replacement locked restore passed; all projects up to date and no package-lock edit."

  - attempted_at: "2026-09-09T01:55:36.8283250Z"
    command: "dotnet build tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-restore -nodeReuse:false"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Release ArchitectureTests project build passed in 13.17 seconds with 0 warnings and 0 errors."

  - attempted_at: "2026-09-09T01:55:57.5482763Z"
    command: "dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --logger \"trx;LogFileName=plat046-d6ef-architecture.trx\" --results-directory artifacts/verification-plat046-d6ef"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Full architecture project passed: 116 passed, 0 failed, 0 skipped in 32 seconds. TRX confirms every WorkerActivationReleaseContractTests outcome passed, including the formerly failing exact-census test and missing/extra/duplicate/malformed/mixed/case-variant fail-closed checks."

  - attempted_at: "2026-09-09T01:56:41.0910834Z"
    command: "pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Release workstation and manifest contract passed for win-x64; Windows/Linux mappings and Pegasus platform LocalDB state classification passed."

  - attempted_at: "2026-09-09T01:56:51.7336571Z"
    command: "pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Local Azure deployment-plan validation passed; Worker Disabled settings render true. Bicep emitted only its available v0.46.1 upgrade warning; no upgrade was performed."

  - attempted_at: "2026-09-09T01:57:08.1783035Z"
    command: "pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "All relative Markdown links resolved; 141 files checked."

  - attempted_at: "2026-09-09T01:57:18.9121953Z"
    command: "pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base c3219cd28c69530441e2bba7357063372628ff37 -Head d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Markdown placement passed for the corrective exact-merge range c3219cd28c69530441e2bba7357063372628ff37..d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75."

  - attempted_at: "2026-09-09T01:57:41.8311295Z"
    command: "$ErrorActionPreference='Stop'; parse every PowerShell file changed in c3219cd28c69530441e2bba7357063372628ff37..d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75 and all powershell/pwsh fences in .agents/skills/pegasus-release/SKILL.md and .agents/skills/pegasus-release/references/database-migration.md with System.Management.Automation.Language.Parser; execute none"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Parsed 0 changed PowerShell files and all 19 release/migration recipe fences with zero errors; no parsed code executed. The corrective range changes only the architecture test."

  - attempted_at: "2026-09-09T01:58:00.0000000Z"
    command: "$ErrorActionPreference='Stop'; function Require-Text([string]$Body,[string]$Needle,[string]$Label){if(-not $Body.Contains($Needle,[StringComparison]::Ordinal)){throw \"Missing semantic boundary: $Label\"}}; read release skill, migration recipe, runbook, ADR-0046 and canonical Worker consumers; assert the granted semantic boundaries with ordinal literal Contains() and order checks"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 1
    result: "FAIL"
    authority: "supporting"
    failure_class: "plan"
    summary: "Verifier-authored semantic harness invocation failed because literal ordinal Contains() expected a required phrase across a Markdown line break. The phrase exists; this is a brittle verification-command formulation, not a product/procedure failure and not a transient classification. Failure retained; no immediate retry occurred."

  - attempted_at: "2026-09-09T02:00:50.4372769Z"
    command: "$ErrorActionPreference='Stop'; normalize Markdown whitespace with [regex]::Replace($Body,'\\\\s+',' '); read release skill, migration recipe, runbook, ADR-0046 and canonical Worker consumers; retain and assert every substantive classification/outage/containment/pre-SQL/migration-head/reactivation/recovery/census phrase and order"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "After explicit root reauthorization, one corrected attempt normalized Markdown whitespace only and retained every substantive text/order assertion. Destructive classification/outage, disabled staging, exact Worker and Web containment, fresh pre-SQL read-back, migration/grant/head gates, compatible healthy reactivation, forward-only recovery, outside-usage scheduling, and canonical seven-function census wiring all passed."

  - attempted_at: "2026-09-09T02:01:10.0000000Z"
    command: "git diff --check c3219cd28c69530441e2bba7357063372628ff37 d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Corrective exact-merge diff has no whitespace errors."

  - attempted_at: "2026-09-09T02:03:16.4284974Z"
    command: "$expected='d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75'; assert git rev-parse HEAD equals expected; assert symbolic-ref empty; assert git status --porcelain empty; census dotnet/MSBuild/VBCSCompiler/testhost/vstest and assert no active build/test parent; report idle residual servers without killing them"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-plat-046-d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75"
    exit_code: 0
    result: "PASS"
    authority: "authoritative"
    summary: "Exact worktree remained at d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75, detached and clean. No active build/test parent remained. Idle residual MSBuild nodes 23660/23012/24016 and compiler server 16708 were left untouched because no foreign process was killed."

---
# Exact corrective integrated-result verification — PLAT-046

Verifier: `/root/pegasus_verifier_713`, using the exact project verifier role and distinct from implementation and independent review. PR713 is MERGED into configured `dev` at GitHub merge commit `d6ef56a6a8ce1c5faeed0cbe56644ce033a4ae75`. The deterministic detached worktree remained clean at that exact SHA.

## Receipt classification

Before any Git operation, `get_status.delivery` supplied the default contract `pr.yml` / `verify` / `push`. The configured workflow lookup returned HTTP 404 and the exact-SHA push-run API census returned `[]`. Therefore there is no bound post-integration receipt; `receipts: []` is the complete local-fallback form and every obligation was run locally. PR-head CI is not presented as merge-SHA proof.

## Result

PASS. Replacement locked restore, Release ArchitectureTests project build, all 116 architecture tests, platform contract, Local deployment-plan contract, 141-file documentation links, corrective-range Markdown placement, all 19 release/migration PowerShell fences, semantic boundary validation, corrective diff whitespace validation, and final exact-worktree/process read-back passed. The TRX confirms the corrected `WorkerActivationReleaseValidationUsesTheSameExactCensusAndStopsUnsafeDisable` and the surrounding fail-closed Worker census cases passed at the merge SHA.

The semantic harness's first exit-1 is retained as a verifier-command planning defect: literal `.Contains()` crossed Markdown line wrapping. Root explicitly authorized one whitespace-normalized retry; it changed no substantive assertion and passed. This is not classified as a product transient and does not erase the failed attempt.

## Historical evidence and limits

All prior PR711 exact-merge attempts remain in the typed ledger, including the authoritative c3219 architecture FAIL. They describe the superseded merged SHA and do not contradict the final d6ef result. The prior replacement verifier's lost build session remains an explicit INCONCLUSIVE observation; this run repeated and captured the missing build/test evidence rather than inferring it.

Independent review of PR713 passed at review record `50e080c74f77641f`. Unrelated browser/UI/SQL3 GitHub failures were disposed to INTK-066 during review; this proof does not claim all CI green and does not claim PLAT-046 changed one production architecture file. No full solution, SQL, browser, package, cloud, deployment, migration, Outlook, or Box operation ran. No product file, source branch, implementation worktree, or mutable checkout was changed. The retained ignored logs and TRX are under `artifacts/verification-plat046-d6ef` in the exact detached worktree. Root owns the Verifying-to-Done move and cleanup.
