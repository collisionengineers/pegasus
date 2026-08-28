---
id: DELIV-029
type: ticket
title: >-
  Release 35: mailbox reactivation migration, Audit allocation recovery,
  telemetry filter and 0.5 GB caps, intake-liveness smoke, Mailboxes
  subscription health
status: done
area: delivery-repository
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-27T19:28:05.493Z'
  review: '2026-08-27T20:00:51.449Z'
  verifying: '2026-08-27T20:06:44.514Z'
  done: '2026-08-27T22:04:17.629Z'
labels:
  - release
  - documentation
groups:
  - EPIC-010
links:
  - MAIL-017
  - MAIL-018
  - MAIL-019
  - MAIL-020
  - MAIL-021
  - INTK-044
refs:
  - docs/current-architecture.md
  - docs/operations.md
commits:
  - cb2ab070
  - 68adedafb9159772515b1b4fb9758f0ab2261fe7
prs:
  - '#578'
archived: false
created: '2026-08-27T19:27:58.771Z'
updated: '2026-08-27T22:04:30.578Z'
---

## Purpose

Promote `dev` (`3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f`) to `main` by exact-SHA fast-forward after PRs #571, #572, #573, #575, #576, #577 merged with independent review and green CI; build immutable artifacts; apply migration `20260827100901_ReactivateBoundApprovedMailboxes` (data-only, expected to match zero prod rows) with the runtime-role bootstrap census; provision so the bicep-declared 0.5 GB App Insights component cap and workspace quota reach the estate (operator billing approval given 2026-08-27); deploy Web and Worker (Worker now filters successful SQL dependency telemetry); smoke with the new intake-liveness gate; record release 35 in `docs/operations.md`, refresh `docs/current-architecture.md` (telemetry diagnosis text, deployed shape) and `docs/open-decisions.md` (cap decision), then a promotion-only pass for the docs.

## Authority

Operator on 2026-08-27: "Approved. Merge auth granted. proceed to deployment afterwards, and updating the repository documents." Covers: the `dev` → `main` promotion of `3a1a017c…`, the migration, the image upload, `azd provision` (including the cap raise on `pegasus-prod-appi-252ow37gij` / `pegasus-prod-logs-252ow37gij`), and the Worker deployment.

Operator on 2026-08-27, second grant: approval of the promotion-only plan, given as merge authority for exactly `68adedafb9159772515b1b4fb9758f0ab2261fe7` — the docs promotion-only pass (step 11). No build, no Azure write, no redeploy.

## Verification

Both remote refs equal the release SHA; uploaded digest equals the manifest; `__EFMigrationsHistory` head is `20260827100901_ReactivateBoundApprovedMailboxes` and the bootstrap census passes; active Web revision digest equals the manifest; Worker read-back shows all seven functions enabled and `ApprovedInboxPollSchedule=0 */5 * * * *`; component `dataVolumeCap.cap` and workspace `dailyQuotaGb` read back 0.5; `Invoke-ProductionSmoke.ps1` passes including `Inbox intake liveness smoke passed`; App Insights shows Worker `AppDependencies` volume dropping after deploy. After the docs pass, both remote refs equal `68adedaf` and `main` carries the release-35 record.

## Outcome

Release 35 executed 2026-08-27, full route, all steps PASS. Manifest SHA-256 `CA81E6F7D9A1A63C9CC8460614E728B601E206919CB6653E7CB5A681D9EF10CF`; Web image digest `sha256:694c562f9b686877b73e30015a65d35b52c05e5a4b0c455219388c157a0892c8` (uploaded and matched). Migration `20260827100901_ReactivateBoundApprovedMailboxes` applied (zero rows matched, as expected — mailbox already reactivated). Bootstrap census unchanged (526/359). Active revision `pegasus-prod-web-252ow37gij--3a1a017c8dea`, 100% traffic. Worker `config-zip` deployed; all seven functions enabled. App Insights component cap and workspace daily quota both read back 0.5 GB (raised from 0.1). Smoke passed including the new `Inbox intake liveness smoke passed`. Post-deploy `AppDependencies` observation: 223 Worker records in 14 minutes, all successful, no successful SQL-dependency rows — consistent with the deployed filter, not a controlled before/after comparison. Release artifacts retained at `artifacts/releases/release-35-3a1a017c`.

Docs PR [#578](https://github.com/collisionengineers/pegasus/pull/578) (commit `cb2ab070`) was reviewed and merged into `dev` as `68adedaf` with green `repository-check`. The docs promotion-only pass (step 11) then ran under the second operator grant: atomic fast-forward push, `origin/main` and `origin/dev` both read back `68adedafb9159772515b1b4fb9758f0ab2261fe7`, and `main` now carries the release-35 record in `docs/operations.md`, the refreshed `docs/current-architecture.md`, and the cap decision plus the corrected stale-threshold row (also satisfying [[MAIL-022]]) in `docs/open-decisions.md`. Deployed application code was not touched by that pass. All steps 1–11 complete. Full proof in `proof/proof.md`.
