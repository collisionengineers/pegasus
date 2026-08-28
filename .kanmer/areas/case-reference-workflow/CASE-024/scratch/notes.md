## Implementation notes — 2026-08-28

Five commits on `task/case-024-edit-lease-heartbeat`:

1. `76be6c9c` — Core heartbeat seam, `EfCaseWorkflowStore.HeartbeatAsync`, Web
   handler, `site.js` beat, copy deletions, and the intake association fix.
2. `940d476b` — assessment edit mode; shared lease state onto
   `CaseMutationPageModel`.
3. `35a13141` — persistence + assessment tests.
4. `3fee076b` — web heartbeat tests and the copy assertions.
5. `bd08df8a` — FRD-01, FRD-02, capabilities, current-architecture.

### Decisions taken while implementing, beyond the plan

- **`RequireOperationKey` had three identical private copies** (Details, Export,
  Eva/Send). All three page models inherit `CaseMutationPageModel`, so it moved
  there and the three copies went. A fourth copy on the assessment page would
  have been the stop condition the rails name.
- **`RestoreLeaseState` and the claim/release key handling moved to the base
  class** rather than being copied onto the assessment. `LeaseToken`,
  `ClaimLeaseOperationKey`, `ReleaseLeaseOperationKey` and `CanRecoverLease`
  moved with them. `RenewLeaseOperationKey` stayed on `DetailsModel`: only the
  workspace renders a manual renew control.
- **`AssessmentWorkspace` was not widened.** It carries no lease; the page
  already injects `IGetCase`, so `RestoreEditModeAsync` reads
  `CaseDetails.ActiveEditLease` from the query it already depends on.
- **The heartbeat form is a partial posting to no named page**
  (`_EditHeartbeat.cshtml`), so it answers whichever page rendered it and the
  two surfaces share one form.
- **`availableAtUtc`, `WallClock` and `ResolveLondonTimeZone` were deleted**
  from `EditModeDisplay` once no caller passed a time.

### Verification so far

- `dotnet restore --locked-mode`, `dotnet build --configuration Release` — clean.
- Core 1045 passed; Architecture 100 passed.
- Integration, by suite: heartbeat/lease persistence 6, CaseMatch 8,
  Assessment 45, CaseDetailsWeb 26, Triage/Operations/MCP 23 — all passed.
  `AutomationMcpIngressTests` confirms the tool census is unchanged.
- Full `Category!=Corpus` run in progress.

## Verification — 2026-08-28, final code

Run against the branch head after the simplification pass, on Windows +
PowerShell 7, LocalDB.

```
dotnet restore ./Pegasus.slnx --locked-mode                         OK
dotnet build ./Pegasus.slnx --configuration Release --no-restore    OK, 0 warnings
dotnet test ./Pegasus.slnx --configuration Release --no-build \
  --filter "Category!=Corpus"                                       exit 0
```

| Assembly | Failed | Passed | Skipped | Duration |
| --- | --- | --- | --- | --- |
| `Pegasus.Core.Tests` | 0 | 1045 | 0 | 2 s |
| `Pegasus.ArchitectureTests` | 0 | 100 | 0 | 1 m 23 s |
| `Pegasus.IntegrationTests` | 0 | 1023 | 0 | 41 m 2 s |

The integration suite took 41 minutes here, longer than the ~28 usually seen;
`testhost` was confirmed alive and accumulating CPU throughout, and the run
completed with exit 0 rather than being cut short. Log retained at
`artifacts/case-024-full-test.log` in the task worktree — copy it out before the
worktree is removed.

An earlier attempt at this run was killed by a 10-minute tool cap, not by a test
failure; it is not counted as evidence either way. This run replaces it and
covers the post-simplification code.
