---
ticket: MAIL-015
merged_main: 1ec65dc894f121f4bb5b31ae82c818a401d08beb
pr: 566
release: 34
proof_type: command-log
date: 2026-08-27
---

# Proof — MAIL-015

Written on merged `main` (`1ec65dc8`), deployed as release 34.

- PR #566 (`f01fed3f`) merged as `dd3afd97`; `origin/main:infra/modules/platform.bicep:540`
  reads `'0 */5 * * * *'`.
- Release 34 `azd provision` (2026-08-27 09:29Z) applied it. Live read-back:
  `ApprovedInboxPollSchedule = 0 */5 * * * *` (six fields); every sibling
  schedule unchanged; all seven `AzureWebJobs.*.Disabled = false`.
- After the Worker `config-zip` deployment (`3757b0c0`, complete/active),
  `az functionapp function list` returns seven functions including
  `InboxRecoveryFunction` with `isDisabled = False` — the host indexes the
  timer again.
- `Invoke-ProductionSmoke.ps1`: Worker activation and production smoke
  passed against `1ec65dc8`.
- Not proved here: an observed timer execution (App Insights daily cap makes
  this unreliable to read in working hours; see PLAT-036).

Verdict: **PASS**.
