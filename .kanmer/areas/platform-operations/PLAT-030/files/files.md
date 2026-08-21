# Files

Committed in `1a86f5db`.

| File | Change |
| --- | --- |
| `src/Pegasus.Worker/host.json` | `extensions.queues.maxPollingInterval: "00:00:02"` — the single biggest win |
| `infra/modules/platform.bicep` (526-528) | `ApprovedInboxPollSchedule` → `*/15 * * * * *`; `IntakeStagedArtifactReconciliationSchedule` → `*/10 * * * * *`; `PendingWorkDispatchSchedule` → `*/5 * * * * *` |
| `src/Pegasus.Worker/local.settings.example.json` | Mirrors the deployed values |
| `scripts/Invoke-LocalDevelopment.ps1` (652) | Mirrors the deployed values |

## Deployment consequence

The bicep change means this ticket needs **`azd provision`**, not a code deploy alone.
`host.json` ships inside the Worker package, which must go via
`az functionapp deployment source config-zip` — `azd deploy worker --from-package`
triggers an Oryx rebuild that crash-loops the host.

Live before this release, confirmed by `az functionapp config appsettings list`:
`ApprovedInboxPollSchedule 45 * * * * *`, `PendingWorkDispatchSchedule */15 * * * * *`,
`IntakeStagedArtifactReconciliationSchedule 30 * * * * *`.
