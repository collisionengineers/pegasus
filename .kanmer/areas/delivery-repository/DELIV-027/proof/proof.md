---
ticket: DELIV-027
release: 34
source: 1ec65dc894f121f4bb5b31ae82c818a401d08beb
image_digest: sha256:b04bad2c2ee8109d3309eb99b3d6610aca8f1319869f92db7c12e17fcb9d2bf0
manifest_sha256: B3984E24EC795C2E12A641039868341D116D3E84BC286133EF1D0031EA821CE2
web_revision: pegasus-prod-web-252ow37gij--1ec65dc894f1
worker_deployment: 3757b0c0-63dd-47f2-a8ad-92c748495dd0
migration: none (20260826151807_ApprovedMailboxStableIdentityAndSubscriptions unchanged)
docs_pr: 570
proof_type: command-log
date: 2026-08-27
---

# Proof — DELIV-027 (release 34)

Tier: **production**.

1. Promotion: atomic `--force-with-lease` fast-forward of `main` and `dev`
   to `1ec65dc8` at 09:22Z (`MERGE AUTH GRANTED` given with the approved
   plan); read-back equal.
2. Artifacts built from a clean detached worktree at that SHA;
   `Test-AzureDeploymentPlan` Local, Artifact and PreProvision all passed.
   Retained at `artifacts/releases/release-34-1ec65dc8/`.
3. `oras cp` to ACR reported digest `sha256:b04bad2c…` = manifest.
4. azd env corrected before provisioning: digest/suffix set for this release;
   missing `GRAPH_CHANGE_NOTIFICATION_CLIENT_STATE_SECRET_URI` set from the
   live Worker's Key Vault reference (version `37d5dee5…`).
5. `azd provision --no-prompt`: SUCCESS in 1 m 21 s. Read-back: revision
   `--1ec65dc894f1` Healthy, RunningAtMaxScale, 100 % traffic;
   `ApprovedInboxPollSchedule=0 */5 * * * *`; seven `Disabled=false`;
   `Graph__ChangeNotificationClientState` binding intact.
6. Worker `config-zip`: deployment `3757b0c0` complete/active 09:32Z; seven
   functions indexed including `InboxRecoveryFunction`.
7. `Invoke-ProductionSmoke.ps1`: passed (and passed again after PLAT-045).
8. Docs: PR #570 (`776364d4`) merged to `dev` as `a9184315` after an
   independent APPROVE; `main` (`1ec65dc8`) is contained in `dev` — the one
   docs commit ahead is expected.

Verdict: **PASS**.
