---
kind: proof-record
merged_sha: "14c6fd4155cf3dd63b33049b05caa370d5d9b94d"
environment: "Detached verification worktree .worktrees/verify-mail-020-14c6fd4155cf3dd63b33049b05caa370d5d9b94d on Windows 11 / PowerShell 7, .NET 10 SDK, Azure CLI with bicep 0.45.15; LocalDB not used (integration tests deliberately not run — another suite held LocalDB)"
verified_at: "2026-08-27T18:40:00Z"
result: PASS
attempts:
  - attempted_at: "2026-08-27T18:05:00Z"
    command: "gh pr view 576 --json state,mergeCommit,url"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "state MERGED, mergeCommit.oid 14c6fd4155cf3dd63b33049b05caa370d5d9b94d, https://github.com/collisionengineers/pegasus/pull/576"
  - attempted_at: "2026-08-27T18:06:00Z"
    command: "git fetch origin; git worktree add --detach .worktrees/verify-mail-020-14c6fd4155cf3dd63b33049b05caa370d5d9b94d 14c6fd4155cf3dd63b33049b05caa370d5d9b94d; rev-parse HEAD; symbolic-ref --short -q HEAD; status --short --branch"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "HEAD = 14c6fd4155cf3dd63b33049b05caa370d5d9b94d, symbolic-ref empty (detached), status '## HEAD (no branch)' clean"
  - attempted_at: "2026-08-27T18:08:00Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-mail-020-14c6fd4155cf3dd63b33049b05caa370d5d9b94d"
    exit_code: 0
    result: PASS
    summary: "All projects restored under locked mode"
  - attempted_at: "2026-08-27T18:10:00Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-mail-020-14c6fd4155cf3dd63b33049b05caa370d5d9b94d"
    exit_code: 0
    result: PASS
    summary: "0 Warning(s), 0 Error(s)"
  - attempted_at: "2026-08-27T18:20:00Z"
    command: "dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build"
    cwd: ".worktrees/verify-mail-020-14c6fd4155cf3dd63b33049b05caa370d5d9b94d"
    exit_code: 0
    result: PASS
    summary: "Passed 1002, Failed 0, Skipped 0"
  - attempted_at: "2026-08-27T18:21:00Z"
    command: "dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build"
    cwd: ".worktrees/verify-mail-020-14c6fd4155cf3dd63b33049b05caa370d5d9b94d"
    exit_code: 0
    result: PASS
    summary: "Passed 100, Failed 0, Skipped 0 (composition/registration rules)"
  - attempted_at: "2026-08-27T18:22:00Z"
    command: "dotnet test ./Pegasus.slnx --filter Category!=Corpus (integration suite)"
    cwd: ".worktrees/verify-mail-020-14c6fd4155cf3dd63b33049b05caa370d5d9b94d"
    exit_code: null
    result: NOT_APPLICABLE
    summary: "Deliberately not run: another suite was using LocalDB on this machine. The diff touches no code under integration test (Worker telemetry processor, bicep, docs); PR CI run 33098647645 at head 46a21f92 was green on all sql-integration shards and the controller's serial run recorded 987/987."
  - attempted_at: "2026-08-27T18:15:00Z"
    command: "az bicep build --file infra/main.bicep --stdout"
    cwd: ".worktrees/verify-mail-020-14c6fd4155cf3dd63b33049b05caa370d5d9b94d"
    exit_code: 0
    result: PASS
    summary: "Compiled JSON declares variables.telemetryDailyCapGb = [json('0.5')]; workspace workspaceCapping.dailyQuotaGb = [variables('telemetryDailyCapGb')]; child microsoft.insights/components/pricingPlans with cap = [variables('telemetryDailyCapGb')] and planType Basic — both caps 0.5 GB from the one variable"
  - attempted_at: "2026-08-27T18:15:00Z"
    command: "Select-String src/Pegasus.Worker/Program.cs -Pattern AddApplicationInsightsTelemetryProcessor; Get-Content src/Pegasus.Worker/SqlDependencyTelemetryFilter.cs"
    cwd: ".worktrees/verify-mail-020-14c6fd4155cf3dd63b33049b05caa370d5d9b94d"
    exit_code: 0
    result: PASS
    summary: "Program.cs line 16: .AddApplicationInsightsTelemetryProcessor<SqlDependencyTelemetryFilter>() — named production caller. Filter returns only for DependencyTelemetry { Type: \"SQL\", Success: true }; everything else reaches next.Process"
  - attempted_at: "2026-08-27T18:16:00Z"
    command: "az monitor app-insights component billing show --app pegasus-prod-appi-252ow37gij -g rg-pegasus-prod"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Read-only. Live dataVolumeCap.cap = 0.1 GB, resetTime 0 (00:00Z), warningThreshold 90 — the 0.5 GB declared in bicep is NOT yet deployed"
  - attempted_at: "2026-08-27T18:16:00Z"
    command: "az monitor log-analytics workspace show -g rg-pegasus-prod -n pegasus-prod-logs-252ow37gij --query workspaceCapping"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Read-only. Live dailyQuotaGb = 0.1, RespectQuota, next reset 2026-08-28T03:00Z — not yet deployed"
---

# MAIL-020 — proof

Verified at the exact PR #576 merge commit
`14c6fd4155cf3dd63b33049b05caa370d5d9b94d` in a disposable detached
worktree. The `dev`/`main` checkouts, the board worktree and the
implementation worktree were not touched.

## What passed

- Locked restore, Release build (0 warnings), Core tests 1002/1002,
  Architecture tests 100/100.
- `az bicep build` exit 0; the compiled template carries the
  `pricingPlans` child (`cap`) and the workspace `dailyQuotaGb`, both bound
  to `telemetryDailyCapGb = json('0.5')`.
- `SqlDependencyTelemetryFilter` is registered from the Worker composition
  root via the SDK's `AddApplicationInsightsTelemetryProcessor<T>()` and
  drops only successful SQL dependency items.

## Not yet live (next release, operator approval required)

Read-only Azure checks confirm both live caps are still **0.1 GB**
(component `pegasus-prod-appi-252ow37gij`, workspace
`pegasus-prod-logs-252ow37gij`). Neither the cap change nor the Worker
filter is deployed by this ticket. They ship with the next release:
`azd provision` applies the bicep caps (or the operator runs the two `az`
updates named in the plan) and the Worker image carries the filter. Raising
the caps changes billing (≈0.4 GB/day extra ingestion ceiling) and needs
explicit operator approval for those exact targets before that release.
That release must also refresh `docs/current-architecture.md` and
`docs/open-decisions.md` (review finding R3).

Integration tests were not rerun here (LocalDB contended); the diff has no
integration-tested surface and PR CI at the head was green.

## Closeout

- PR: https://github.com/collisionengineers/pegasus/pull/576
- Merged into `dev`: 2026-08-27T17:44:20Z, merge commit
  `14c6fd4155cf3dd63b33049b05caa370d5d9b94d`.
- Closed out 2026-08-27: implementation worktree and branch removed, ticket
  released.
