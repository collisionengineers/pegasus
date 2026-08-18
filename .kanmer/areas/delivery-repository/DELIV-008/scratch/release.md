## Release 9 transcript — 2026-08-18 (claude-code)

Candidate SHA `f1e116c6eb939f901f32e5f89d58d1d8a4701851` = origin/dev after PRs #393 (TICK-026), #402 (DELIV-007), #403 (AUTO-001) merged. PR #400 checks re-running on it.

Release worktree `../pegasus-worktrees/deliv-008-release-9` @ f1e116c6, clean.

- C1 `dotnet build ./Pegasus.slnx -c Release` → 0 warnings, 0 errors (57 s).
- C2 `Test-AzureDeploymentPlan.ps1 -Mode Local` → pass.
- C3 `Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.1 -SourceRevision f1e116c6…` → `artifacts/releases/0.1.0-alpha.1/{web.zip, web-image.tar.gz, worker.zip, efbundle.exe, release-manifest.json}`.
  Manifest: sourceStatus clean; migrationIdentity `20260814094632_DropBoxFileRequests`; webImage `pegasus/web:f1e116c6…` digest `sha256:63e863242479326d6ef359ef37c0d82ed3db894b6facfe93f2c36d8c489bdd13` (linux/amd64, OCI); tools dotnet 10.0.302 / az 2.88.0 / azd 1.29.0.
- C4 `-Mode Artifact` → pass. Manifest SHA-256 `67A9C17A8ED42F577F3C8F5ACC0184FA454F4D839E7770E86254B86A3B66E324`.
- Pre-checks: ACR authentication-as-arm `enabled`; Worker nine `Disabled` settings all `false`; KV secret `automation-mcp-client-secret` versioned id `…/68ff4a6b656d4768a3d83313f8d80ca9`, Web identity has Key Vault Secrets User on it; azd env refreshed (output keys present).

### Promotion + deploy (2026-08-18, ~11:40–12:55 UTC)

- B: PR #400 checks 10/10 SUCCESS on `f1e116c6`; preflight `merge-base --is-ancestor origin/main origin/dev` OK; `git push --atomic --force-with-lease=refs/heads/dev:f1e116c6… origin f1e116c6…:refs/heads/main f1e116c6…:refs/heads/dev` → `2b0df78c..f1e116c6 main`; readback both heads == f1e116c6. Main-push run 32133221206: all jobs success; guard step "Main history guard passed: 9 new first-parent commit(s); main head is contained in the release branch." PR #400 shows MERGED (commit f1e116c6) automatically.
- C5–C7: azd env refreshed; PreUpload pass; PreMigration pass (after copying the local azd env into the release worktree); PreProvision pass (`Production Worker activation smoke passed (approved-live-worker)`); ACR auth-as-arm already enabled.
- C8/C9: `oras cp` → `pegasusprodacr252ow37gij.azurecr.io/pegasus/web:f1e116c6…`; registry digest `sha256:63e863242479326d6ef359ef37c0d82ed3db894b6facfe93f2c36d8c489bdd13` == manifest.
- C10: `__EFMigrationsHistory` head `20260813025241_StandaloneAuditReportDecision`.
- C11: `efbundle.exe --connection "…Authentication=Active Directory Default…"` — needed the Web host env (`ASPNETCORE_ENVIRONMENT=Production`, `Runtime__Profile=Production`, connection string, identity/storage names, Box URIs + shape-only placeholders for `Box__ConfigJson`/`Box__ClientSecret`) and `AZURE_TOKEN_CREDENTIALS=AzureCliCredential`; run from `src/Pegasus.Web`. Applied `20260814092852_AddWorkerCaseCreationGrants` and `20260814094632_DropBoxFileRequests`; log `artifacts/releases/0.1.0-alpha.1/efbundle-apply.log`. → runbook gap: record this invocation shape.
- C12: `Invoke-AzureDatabaseBootstrap.ps1` → "Verified 459 catalogued permission/denial rows and 306 effective runtime DML rows."
- C13: history head `20260814094632_DropBoxFileRequests`; `BoxFileRequests` table gone.
- C16/C17: first `azd provision` failed creating the revision: Web identity could not fetch `box-config-json`/`box-client-secret` — the local azd env still carried the OLD adopted vaults (`cespkboxkvv76a47`, `cespkenrichkvgi62sd`, now purged) for all six secret URIs. Live web already used `pegasusprodkv252ow37g` (same version ids); the Worker still referenced the old vaults and **all six Worker Key Vault references were unresolved in production**. Set the six inputs to the `pegasusprodkv252ow37g` versioned URIs (both identities hold Key Vault Secrets User there); re-preview; `azd provision` succeeded (deployment `pegasus-prod-1787053759` failed, next one succeeded — see `azd-provision.txt`).
- C18: revision `pegasus-prod-web-252ow37gij--f1e116c6eb93` 100% traffic Healthy; image `…/pegasus/web@sha256:63e86324…`; `Features__AutomationMcp=true`; `/diagnostics/version` sourceSha `f1e116c6…`; live/ready 200; `/Cases` → https sign-in; `/mcp` unauthenticated → 401.
- C19: `azd deploy worker --from-package` failed (remote Oryx build on a pre-published package — same as the 14 Aug log); `az functionapp deployment source config-zip --src worker.zip` → "Deployment was successful." → runbook gap: record the working route.
- C20: `Invoke-ProductionSmoke.ps1` full → "Production Worker activation smoke passed (approved-live-worker). Production smoke passed." Nine functions listed, none disabled. Worker KV references now all `Resolved`.

### C21 watch (12:00–12:15 UTC)

- App Insights: worker exception burst 11:48–11:56 UTC (~1.3k `dotnet exited with code 134` / "Failed to start language worker process" / "Exceeded language worker restart retry count … recycling the Functions Host") — the window between the failed `azd deploy worker` remote-build attempt and the successful `config-zip`; none after 11:56.
- **Log Analytics workspace hit its 0.1 GB/day cap at 11:52:46 UTC (`dataIngestionStatus: OverQuota`, resets 2026-08-19 03:00 UTC)** — no telemetry from any role after ~11:56 is the cap, not an outage. Verified the Worker independently: admin host status `Running`, uptime ≈21 min, nine functions loaded/enabled; `ApprovedInboxPollStates.LastCompletedAtUtc = 2026-08-18 12:09:45Z` (23 s before the read), no `LastFailureCode`.
- Web: revision `--f1e116c6eb93` healthy; live/ready 200; version sourceSha f1e116c6; anonymous `/Cases` → https sign-in.
- Pre-existing before this release (now fixed by the corrected secret URIs): `SentEvidencePollFunction` invocations were `success=False` at 11:49–11:50 while all six Worker Key Vault references were unresolved.

### AUTO-001 live evidence (11:59–12:03 UTC, production `/connect/token` + `/mcp`)

- Wrong secret → 401 `invalid_client` (SecurityEvents `automation_token_rejected`).
- Client credentials `pegasus-automation`, scope `automation.cases` → Bearer token, `expires_in` 600.
- `/mcp` without token → 401 with `WWW-Authenticate: Bearer resource_metadata=…` (SecurityEvents `automation_access_denied`).
- `initialize` 200; `tools/list` → 15 tools: assessment_get/update, case_edit_begin/end/renew, case_get, case_search, case_update_details, document_add/download/export, eva_bundle_generate, eva_handoff_status, intake_queue_list, intake_submit.
- `pegasus_case_search` (pageSize 1) → success, structured result (correlationId, page, items…) — ActionHistory `Succeeded` for `pegasus_case_search`, ActorKind Automation, ActorSubjectId pegasus-automation.
- `pegasus_intake_queue_list` with the cases-only token → isError "The 'automation.intake' scope is required for this tool." (SecurityEvents `automation_scope_denied`).
- `pegasus_case_get` with empty id → isError "A non-empty case identifier is required." — ActionHistory `Failed`.
- Kill switch (Administration → Automation as `claudeuiverification`): disable → token endpoint 400 `unauthorized_client`; in-flight token at T+12 s → "The Automation client registration is disabled."; re-enable → new token issued and `pegasus_case_search` succeeds again. Registration left **enabled**.
