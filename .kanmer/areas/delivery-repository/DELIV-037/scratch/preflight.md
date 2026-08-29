## 2026-08-29 — two corrections to the ticket body, both verified

**1. The migration head has moved, and there are ten pending, not nine.**
The plan recorded `20260828185508_ProviderDeclaredInstruction` as the head. ENG-027
(#621, Case valuations) has since merged and added
`20260829095336_CaseValuations`, which is now the head. Release 36 applied up to
and including `20260827143200_GrantEvaSubmissions` (`docs/operations.md:314`), so
the pending set for release 37 is exactly:

| # | Migration | Grants? |
| --- | --- | --- |
| 1 | `20260828084601_AiJobs` | |
| 2 | `20260828084644_GrantAiJobs` | yes |
| 3 | `20260828104130_PrincipalApiCredentials` | |
| 4 | `20260828104139_GrantPrincipalApiCredentials` | yes |
| 5 | `20260828110108_CaseEditLeaseHolderKind` | |
| 6 | `20260828111707_ProviderSubmissions` | |
| 7 | `20260828111732_GrantProviderSubmissions` | yes |
| 8 | `20260828112103_NamedEstimates` | |
| 9 | `20260828185508_ProviderDeclaredInstruction` | |
| 10 | `20260829095336_CaseValuations` | |

Three grant-carrying, not four. AUTO-013 and AUTO-012 may add more before the
release SHA is fixed — re-derive this table against the promoted SHA rather than
trusting it.

**2. The 2026-08-14 Worker grant hotfix is NOT a risk. It was codified.**
The ticket body lists it as the highest-risk item, on the strength of a
session note saying the live `GRANT` was never captured in a migration and would
regress on the next bootstrap. **That note was stale.** The hotfix was
subsequently written up as
`src/Pegasus.Infrastructure/Persistence/Migrations/20260814092852_AddWorkerCaseCreationGrants.cs`,
which grants `pegasus_worker_runtime_role` the eighteen tables
`EfCaseAcceptanceStore.AcceptOnceAsync` writes in its single batch — `Cases`,
`CaseSequences`, `CaseMatchIndex`, `CaseIntakeLinks`, `CaseHistory`,
`CaseWorkflows`, `CaseDataSnapshots`, `CaseDataFields`, `CaseDueWork`,
`ExternalWorkItems`, `IntakeMutationHistory`, `StandaloneAuditEvidence`,
`Principals`, `PrincipalSequenceLineages`, `Organizations`, `OrganizationRoles`,
`VehicleConfirmations`, `WorkflowConfigurations`.

The migration's own comment records why local testing never caught the original
gap: "Local/LocalDB tests run full-privilege and never exercised the
least-privilege role, so this only ever failed against the deployed estate."
Its grants are additive to the reconciliation baseline, `DELETE` stays denied
everywhere for the Worker, and its `Down` revokes only what it added.

Dated 2026-08-14, it is far below release 36's applied head, so it is already in
production and `Invoke-AzureDatabaseBootstrap.ps1` will not revoke it. **Struck
from the risk list.** The grant census read-back stays in the verification list
as evidence, not as mitigation for a live threat.

## Live state read read-only before any write, 2026-08-29

- `az` on this workstation needs `AZURE_EXTENSION_DIR` pointed at an empty
  directory. One unreadable extension metadata file
  (`.azure/cliextensions/account/azext_account/azext_metadata.json`) breaks the
  whole CLI while it builds the command table — even `az account show`. With an
  empty extension dir everything the release needs works, because
  `containerapp`, `functionapp`, `sql`, `acr` and `rest` are all core commands.
- Web active revision `pegasus-prod-web-252ow37gij--84132d01ccb0`, digest
  `sha256:5ba65f61ad754639185764ed2c7795fc06938e6e397a3a9d5c7f7fe5c01bb032`,
  created 2026-08-28T02:54:27Z, 1 replica — matches the release 36 row exactly.
- Worker census is exactly the expected seven functions.
- Web carries one `Features__*` setting: `Features__AutomationMcp=true`.
  `Features__ProviderApi` is absent and therefore closed.
- `azd`, `oras` and PowerShell 7.6.5 are all present on PATH.

## Docs pass — measured staleness, 2026-08-29

The release skill's step 10 says the release "is unfinished until both
current-state documents match what was actually deployed". Measured against
`dev` before the deploy, they do not come close. Occurrence counts:

| Term | `current-architecture.md` | `operations.md` |
| --- | --- | --- |
| Integrated Operations Workspace | 0 | 0 |
| `AiJobs` | 0 | 0 |
| `NamedEstimates` | 0 | 0 |
| `CaseValuations` | 0 | 0 |
| `VehicleImages` | 1 (as a list page that no longer exists) | 0 |

So the four subsystems this release ships are documented nowhere in the
as-built snapshot, and the one stale mention describes a deleted page.

**Release-number prose is wrong in two places, not one.** `operations.md:296`
reads "the estate currently serves **release 35**" and `:385` says "served
release 35 unchanged throughout", while the release table at `:314` correctly
records **36**. Release 36's commit added its table row and missed the prose —
and release 35's evidently did too. Fix all three, and add release 37's row plus
its prose entry, rather than repeating the same miss a third time.

Also to correct in the same pass:

- `operations.md:121` — "Not implemented: no endpoint, client, credential, or
  caller" for the Provider API. Already stale against source; **actively false**
  once this release deploys the enabled gate. Rewrite it to say the endpoint and
  gate are live and that no credential has been issued, which is what actually
  closes the route.
- `open-decisions.md:57` — "Operations must not imply that `Features:SendToAi`
  or `Features:AutomationMcp` is production enabled." `Features__AutomationMcp`
  has been `true` in `infra/modules/platform.bicep` since ADR-0026, so half that
  sentence is now wrong. `Features:SendToAi` genuinely remains disabled and
  cannot be enabled (`SendToAi.cs:42` throws outside DevelopmentOffline), so the
  constraint survives for SendToAi alone. Split it rather than deleting it.

None of this is written before the deploy. Per the skill it is written **after**,
carrying the observed SHA, manifest hash, image digest, revision and migration
head, then delivered by reviewed PR to `dev` and put on `main` by a **second,
freshly authorised promotion-only pass**.

## Phase 0 complete — two blockers found and closed, 2026-08-29

An independent read-only readiness audit returned **NOT READY**, on two blockers
that every existing gate would have passed. Both are now closed. Each finding
below was re-verified independently against the live estate, not taken on the
auditor's word.

### B1 — the broken `az` extension would have stopped the migration

`az account show` and `az account get-access-token` both died with
`PermissionError: [Errno 13] ... cliextensions\account\azext_account\azext_metadata.json`.
The CLI reads every installed extension's metadata while building its command
table, so one unreadable file breaks the whole CLI, not just extension commands.

**Why that is a release blocker and not an annoyance:** `efbundle.exe` runs with
`AZURE_TOKEN_CREDENTIALS=AzureCliCredential`, and `AzureCliCredential` shells out
to `az account get-access-token`. The migration simply cannot run. It also breaks
`Invoke-AzureDatabaseBootstrap.ps1`'s subscription guard and token fetch, and
`Invoke-ProductionSmoke.ps1`'s intake-liveness token.

**Closed** by exporting `AZURE_EXTENSION_DIR` to an empty directory, which the
child `az` inherits from the process environment, so `efbundle` is covered.
Verified: `az account show` returns the subscription, and
`az account get-access-token --resource https://database.windows.net/` returns a
token. `az containerapp`, `az functionapp`, `az acr` and `az sql` all work this
way because they are core commands, not extensions.

The cleaner repairs both failed: `az extension remove --name account` is denied
on `account-0.2.5.dist-info`, and `icacls /grant` is denied on the same files —
the current user does not own them, so a permanent fix needs elevation. The env
var must therefore be exported in **every** terminal that runs a release script.

### B2 — five deployment parameters were missing, and would have detonated after the promotion

`infra/main.parameters.json` requires five `${VAR}` with **no default** that the
`pegasus-prod` azd environment did not carry:

```
EVA_CLIENT_ID_SECRET_URI
EVA_CLIENT_SECRET_SECRET_URI
EVA_INSTRUCTION_EMAIL
EVA_REQUEST_FROM
GRAPH_CHANGE_NOTIFICATION_CLIENT_STATE_SECRET_URI
```

**`Test-AzureDeploymentPlan.ps1 -Mode PreProvision` does not check them.** It
requires only `AZURE_SUBSCRIPTION_ID`, `AZURE_TENANT_ID`,
`AZURE_RESOURCE_GROUP`, `WORKER_APP_NAME` and `PEGASUS_WORKER_ACTIVATION`
(`Test-AzureDeploymentPlan.ps1:421-427`). So this passes every gate in the
procedure and fails at `azd provision` — which is **step 7, after the promotion
to `main` and after the migrations have already been applied**. Worse, if azd
substituted empty strings rather than failing, Web would lose its EVA secret
references and, per `operations.md:120`, refuse to start — crash-looping the
whole application, not merely the EVA route.

**Closed.** All five values were read from the live estate and verified before
being written:

| Parameter | Verified against |
| --- | --- |
| `GRAPH_CHANGE_NOTIFICATION_CLIENT_STATE_SECRET_URI` | live Container App secret `graph-change-notification-client-state` |
| `EVA_CLIENT_ID_SECRET_URI` | live secret `eva-client-id` |
| `EVA_CLIENT_SECRET_SECRET_URI` | live secret `eva-client-secret` |
| `EVA_REQUEST_FROM` = `COLLENGAPI` | live `Eva__RequestFrom` |
| `EVA_INSTRUCTION_EMAIL` = `digital@collisionengineers.co.uk` | live `Eva__InstructionEmail` |

These are Key Vault *identifiers* and plain configuration, not secret material.

Rather than stop at the five, every parameter was then checked:
**27 of 27 resolve, zero missing.** The three that fall back to a default were
each confirmed to match live — `AUTOMATION_MCP_REDIRECT_URIS`
(`https://claude.ai/api/mcp/auth_callback`, matches live
`AutomationMcp__RedirectUris`), `EVA_BASE_URI`
(`https://sentry.evasoftware.co.uk/api/`) and `EVA_INSPECTION_TYPE`
(`Vehicle Damage Inspection`).

`azd` offered an upgrade to 1.32.0 and it was **not** taken; upgrading azd
mid-release is its own risk.

### The grant question is definitively closed

The auditor proved it against live SQL rather than by reading code. The
bootstrap's own permission query run against production returns **530 rows**;
the matrix derived from `dev` expects **543**. The 13-row difference is exactly
the four pending grant migrations (Web `S/I/U` on `AiJobs`,
`PrincipalApiCredentials`, `ProviderSubmissions`, `CaseValuations`, plus Worker
`SELECT` on `ProviderSubmissions`). **Nothing is live that `dev` does not
expect** — zero drift in either direction.

`Invoke-AzureDatabaseBootstrap.ps1` never revokes anything (all 602 lines read):
its only DDL is idempotent `CREATE USER … IF NULL`, `ALTER ROLE … ADD MEMBER`
and `GRANT CONNECT`. Lines 169-186 *read
`20260814092852_AddWorkerCaseCreationGrants.cs`'s `WorkerGrants` block at
runtime* and fold it into the expected matrix, precisely so that hotfix cannot
drift out.

**The residual risk is inverted from what was assumed:** the bootstrap is an
*equality* gate in both directions, so any manual `GRANT` applied between now
and the release would itself become a stop condition. Do not hand-patch grants.

### Migration risk is lower than feared

Live `__EFMigrationsHistory` holds 76 rows, head `20260827143200_GrantEvaSubmissions`.
`dev` carries 86 files, head `20260829095336_CaseValuations` — 10 pending. The
two structurally risky ones are harmless against current data:

- `20260828112103_NamedEstimates` reshapes `CaseRepairSpecifications` with a data
  `UPDATE` and six new check constraints — but that table and
  `CaseEstimateLines` both hold **0 rows**.
- `20260828185508_ProviderDeclaredInstruction` widens
  `CK_CaseDataFields_FieldName` and `CK_CaseDataFields_SourceKind`. The new lists
  are strict **supersets** of the live ones; live `CaseDataFields` (98 rows) uses
  13 field names and 4 source kinds, all inside both.

Nothing in the set is non-additive in a way that breaks the running release-36
revision during the migration window.

### `/health` does not exist — a smoke trap

`GET /health` returns **302** to `/Account/SignIn`. The real endpoints are
`/health/live` and `/health/ready` (both 200 `Healthy`) and
`/diagnostics/version`, which is what `Invoke-ProductionSmoke.ps1:194-197`
asserts against. Anything that probes `/health` gets a sign-in redirect and a
false negative. Live version today:
`{"version":"0.1.0-alpha.1","sourceSha":"84132d01ccb0afca7af6c6ce519e6f3491aee160"}`.

Also: **the Web's `AppRoleName` is an empty string** — only the Worker sets a
role name — so post-deploy KQL must not filter Web telemetry by `AppRoleName`.

## Four things that need the operator's decision

**1. The `claudeuiverification` Administrator is live and will be re-asserted.**
Production SQL holds two accounts: `alex` and `claudeuiverification`, both
enabled Administrators. `ReconcileVerificationAccountAsync`
(`Program.cs:1145-1180`) re-converges its password and role on **every**
Production start, so release 37 re-asserts it. Its password is a plaintext
literal in a tracked file that ships inside the container image. The file's own
comment says to retire it by replacing `UserName`/`Password` with
`{ "Removed": "claudeuiverification" }`, which deletes the account on next start.
Not changed — this is a go-live decision, not a release step.

**2. "Nothing gated off" cannot include document upload links.** They stay
unavailable in production regardless of any `Features:` flag, because
`Program.cs:241-250` requires `DocumentRequests:AcceptedLimitsVersion` and
production sets none — it is absent from both `platform.bicep` and the live app
settings. The code comment says this is deliberately blocked pending the INT-31
open decision. Opening it is a separate decision, not part of this release.

**3. Worker rollback is not one command on this workstation.**
`./artifacts/releases/release-36-84132d01/worker.zip` does not exist here;
releases 33-36 were run from `C:/Users/Alex/Documents/GitHub/pegasus`, which is
not on this machine. The Web rollback *is* clean — release 36's image
(`sha256:5ba65f61…`, tag `84132d01ccb0…`, pushed 2026-08-28T02:48:25Z) is still
in the ACR, whose retention policy is disabled so nothing auto-purges. A Worker
rollback would have to be rebuilt from source at `84132d01ccb0`.

**4. The Graph subscription expires 2026-09-02T10:25Z.** `Invoke-ProductionSmoke.ps1`
fails unless an unexpired `Active` subscription exists. If the release slips past
that date the smoke fails on a live-estate condition rather than on anything the
release did.
