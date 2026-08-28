# MAIL-019 plan — post-release smoke asserts inbox intake liveness

## Verified premises (read-only, 2026-08-27)

- `scripts/Invoke-ProductionSmoke.ps1` has no SQL access today; it reads
  Worker app settings via `az` and probes Web over HTTP. Its `-WorkerOnly`
  path is exercised by `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs`
  with a fake `az` on PATH; the full path (`-BaseUri …`) is only run live.
- SQL auth pattern already in the repo: `az account get-access-token
  --resource https://database.windows.net/` + `Invoke-Sqlcmd -AccessToken`
  (`scripts/Invoke-AzureDatabaseBootstrap.ps1:363-377`, runbook restore
  drill). Prod target is hardcoded in the runbook as server
  `pegasus-prod-sql-252ow37gij`, database `pegasus`; the smoke already
  hardcodes the Worker identity the same way.
- Live schema (queried prod): `ApprovedMailboxes.State` nvarchar
  (`Approved`), `AllowInboundIntake` bit, `ActivatedAtUtc` datetimeoffset;
  `ApprovedMailboxSubscriptions.LifecycleState` nvarchar (`Active`),
  `ExpiresAtUtc`; `ApprovedInboxPollStates.LastCompletedAtUtc`,
  `LastFailureCode`, `ActivatedAtUtc`. Current rows: one mailbox Approved,
  activated 10:20:33Z; one Active subscription expiring 2026-09-02; last poll
  completed 14:30:00Z (5-minute recovery cadence, `ApprovedInboxPollSchedule`).
- Grace: `RetainedMail.StaleAfter` = 15 min = three recovery intervals; the
  smoke uses the same number so the release gate and the workspace chip agree.
- Assumed (not re-verified): the release terminal has the `SqlServer`
  module, as the bootstrap script already requires.

## Change

1. `scripts/Invoke-ProductionSmoke.ps1` — in the full (non-`WorkerOnly`)
   path, after the Web checks, add one read-only query against
   `tcp:pegasus-prod-sql-252ow37gij.database.windows.net,1433` / `pegasus`
   using the bootstrap token pattern, and FAIL (throw) when:
   - any mailbox is `Approved` with `AllowInboundIntake = 1` and
     `ActivatedAtUtc IS NULL` (the release-33 defect); or
   - no mailbox has an `Active`, unexpired subscription; or
   - no `ApprovedInboxPollStates.LastCompletedAtUtc` within the last 15 min.
   Success prints one line naming the newest poll time and subscription
   expiry. No new parameters; `-WorkerOnly` behaviour and the mocked tests
   are unchanged.
2. `docs/runbook.md` — extend the smoke description (~:932) and the
   post-release smoke paragraph (~:1074) to name the intake-liveness gate.
3. `.agents/skills/pegasus-release/SKILL.md` step 9 — one sentence naming
   the new gate. `docs/operations.md` unchanged (release records are written
   per release, not per script change).

## Verification

- `pwsh -NoProfile -Command "[scriptblock]::Create((Get-Content -Raw scripts/Invoke-ProductionSmoke.ps1)) | Out-Null"`
- `dotnet test tests/Pegasus.ArchitectureTests --filter WorkerActivationReleaseContractTests`
  (the mocked smoke path) and `scripts/Test-AzureDeploymentPlan.ps1`'s
  regex assertions on the script stay green.
- Dot-source the new query block against prod (read-only) and record the
  output.

## Simplification pass

Recorded after implementation.

## Simplification pass — 2026-08-27

Diff: `scripts/Invoke-ProductionSmoke.ps1` (+50), `docs/runbook.md` (+8/-2),
`.agents/skills/pegasus-release/SKILL.md` (+5/-2). Lenses: reuse,
simplification, efficiency, altitude.

- Reuse: token + `Invoke-Sqlcmd -AccessToken` copied from
  `Invoke-AzureDatabaseBootstrap.ps1:360-377`; the bootstrap's
  `Invoke-AzureSqlQuery` is a script-local function (not a module), so one
  inline call is the smaller change than extracting a shared module for two
  callers. Applied.
- Simplification: single SQL statement, all clock comparison on the database
  clock (`SYSDATETIMEOFFSET()`), so no local-clock skew. Applied.
- Efficiency: one round trip. `EarliestSubscriptionExpiryUtc` is a second
  subquery used only for the pass line — kept, because that line is the
  release-record evidence (not applied, reason recorded).
- Altitude: no new parameters, switches, or modes; `-WorkerOnly` and the
  mocked Architecture tests untouched. Server/database are hardcoded like the
  Worker app name already is (`Test-AzureDeploymentPlan.ps1` asserts that
  style). Applied.
- "Identities bound" from the brief is not a schema fact (no identities
  table; `ApprovedMailboxFolderBindings` exists) — the hard-FAIL condition is
  `Approved + AllowInboundIntake + ActivatedAtUtc IS NULL`, which is the
  release-33 shape.

## Verification record — 2026-08-27

- Parse: `[scriptblock]::Create(...)` OK.
- Live read-only block: `Inbox intake liveness smoke passed (last poll
  2026-08-27 14:30:00Z, subscription expires 2026-09-02 10:25:00Z).`
- `dotnet test tests/Pegasus.ArchitectureTests -c Release --filter
  FullyQualifiedName~WorkerActivationReleaseContractTests`: 14/14 passed,
  exit 0 (first run built and hit a transient "test host process crashed"
  after 7 passes; `--no-build` rerun clean).
