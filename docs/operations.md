# Operations

This is the last recorded deployed-state and support summary. It is not a fresh
cloud observation. Exact source structure belongs in [architecture](current-architecture.md);
procedures are reached through [the runbook](runbook.md).

## Retired Container Apps resources — 15 September 2026

After the Release 50 App Service cutover and the subsequent successful Releases
51 and 52, the operator approved final removal of the retained Container Apps
rollback resources. The retired `pegasus-prod-web-252ow37gij` Container App was
already unserved, with no ingress, active revision or replica. The authorised
cleanup deleted it, then the empty
`pegasus-prod-aca-env-252ow37gij` managed environment, then the obsolete
`pegasusprodacr252ow37gij` registry. Every deletion exited zero and an
independent Azure inventory read-back found none of those three resource types;
the same-name `Microsoft.Web/sites` App Service remains.

Two earlier cleanup-script preflights exited one before any Azure write: the
first constructed the version URI incorrectly and the second queried the
Function App state at the wrong Azure CLI property path. The corrected dry run
passed every precondition and reached `ShouldProcess` before the successful
authorised execution.

After deletion, the App Service `/health/ready` endpoint returned 200 and
`/diagnostics/version` reported source
`38051586856eb2b4a00b964de842a2e7bcdee555`, version `0.1.0-alpha.1`. The Flex
Consumption Worker remained `Running`. Full production smoke exited zero: the
deployed Web package SHA-256 matched Release 52, Worker activation was
`approved-live-worker`, and intake liveness passed with the last poll at
`2026-09-15T10:05:03Z`. No application package, schema, configuration, mailbox,
storage, SQL, Box or Outlook state changed.

## Test-estate reset — 15 September 2026

The operator-approved production reset removed 56 blobs (10,633,488 bytes)
from `pegcustody252ow37gij/transient-intake` and 345 rows across 91
intake/case tables. The checked SQL transaction reported 397 affected rows,
left all 91 target tables empty, and committed the mailbox cutoff
`2026-09-15T08:37:24.1701441Z` without changing mailbox approval or activation
times.

The reset retained the sole `alex` Administrator account and removed `andrew`,
`claudeuiverification`, `engineertest`, and `test1`, including four role rows
and 42 attributable security events. Post-checks found no remaining account
traces. The QDOS 2026 counter changed from 9 to 0, so the next allocation is
`QDOS26001`; Image Intake remained at seven sequence rows, Triage remained
empty, and Unidentified remained at one sequence row. All 37 preserve-list
entries were present, 38 tables were effectively preserved, and 481 preserved
rows remained after the transaction.

The Worker was stopped for the operation and returned to `Running` afterward.
Independent read-back found zero target blobs and rows, one `alex` account,
zero removed-account traces, and QDOS still at zero. Authenticated Web checks
showed zero cases requiring attention, zero recent cases, and zero Inbox
messages. The authentication ring, `box-links`, `pegtrans252ow37gij`, Outlook,
Graph, and Box were untouched.

## Release 52 — 15 September 2026 (deployment live)

Release 52 deployed the 14–15 September live-walk batch (PRs 749–758) to the
Linux App Service Web host and the Flex Consumption Worker by the normal
route with an additive migration. Full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and package | Version `0.1.0-alpha.1`, source `38051586856eb2b4a00b964de842a2e7bcdee555` (dev = main); manifest SHA-256 `D366CE129E2D7D245B7A9EAD1CE69878216DF153E2CB42AFC93892912E001771`; `web.zip` SHA-256 `74EC9599CA3949BB1DD21DD675F6CBABB1F28BA1A74A2D7EAA77D764A9AD376E`; `worker.zip` `B2E6AB40D08B775823D025855F3F0748DC9917A6A18DF2930EB7FEFB6F956F94`; bundle `efbundle.exe` (win-x64). |
| Promotion and CI | PRs 749–758 each passed review-by-operator instruction and full CI on `dev`; the operator waived the browser walk. Main and dev were atomically fast-forwarded from `bdc85085c` to the source. |
| Schema | Additive: `20260914150656_UploadedCorrespondenceMailbox` (RetainedMailboxMessages.MailboxId nullable; unique index re-created filtered on non-null) applied over `20260914100000_WidenDocumentContentCacheVariant`. Bootstrap verified 705 catalogued permission/denial rows and 487 effective runtime DML rows. |
| Provision | `azd provision` succeeded (1 m 28 s) with Web activation `approved` and Worker `approved-live-worker`; it applied the alert tuning: `pegasus-prod-web-http5xx` threshold 1 (fires above one 5xx in five minutes), `pegasus-prod-application-exceptions` severity 2 — both read back. |
| Deployment | Web deployment `ca09bf40-c04d-48db-8a4a-d123bc7fc69c` (OneDeploy) succeeded at `2026-09-15T08:16:15Z`; the site took 144 s to start and the release skill's two-minute read-back wait expired before it was ready, then `/health/ready` 200 and `/diagnostics/version` reported the source. Worker deployment `d26124a8-d872-44eb-b3de-caa0eb6877cd` succeeded; Worker `Running`, every Disabled setting `false`. |
| Smoke | Passed at 08:27Z: Web App `Running` on `DOTNETCORE|10.0`, deployed package `20260915081601.zip` SHA-256 equals the approved `web.zip`, intake liveness (last poll `2026-09-15T08:25:03Z`, subscription expires `2026-09-20T13:10:00Z`). |
| Behaviour shipped | Damage image strip removed; tag picker closes on outside click; crop toolbar no longer overlaps; Cancel discards without the dialog; faulted heartbeats no longer end edit mode; Accounts Delete removes the row; uploaded `.eml` files are Correspondence; Valuation is one route through per-source entry cards (no Add valuation; no provider connected yet); Editing column on the Case list and Search; Estimate pill removed; Intake pending-custody 404; download 409 race fixed; transaction-scoped SQL retry. Not walked in a browser before release by operator instruction. |
| Evidence | `artifacts/releases/release-52-38051586` retains the manifest, ZIPs, bundle, build and deploy logs and the migration/deploy scripts. |

## Release 51 — 14 September 2026 (deployment live; recovery validated)

Release 51 deployed the reviewed mailbox-reactivation correction to the Linux
App Service Web host. Mailbox recovery, the generation-3 subscription and poll,
and full production smoke passed. The delivery trace reported zero records for
the pause; recent delivery records can lag, so this does not guarantee that no
mail arrived.

| Observation | Value |
| --- | --- |
| Source and package | Version `0.1.0-alpha.1`, source `bdc85085cb5478b5bdf013a30b95059bff451b59`; manifest SHA-256 `54C5FB94B6FA4F880210070A85C5AE74F20EFE207225792780387ADE8C8519B7`; server package `20260914130353.zip` SHA-256 `2DFE8E88CD54950A11C88E23A0485FA6543CDA1EF4ABAA226DA4C02C70343852`, equal to the approved `web.zip`. |
| Public origin | `https://pegasus-prod-web-252ow37gij.azurewebsites.net/` on the existing UK South B1 Linux plan; the Worker remains Flex Consumption. |
| Promotion and CI | Main and dev were atomically fast-forwarded to the source. PR 748 merged at `2026-09-14T12:55:54Z`; CI `34841859467` passed every SQL group and coverage check on the exact deployed tree. Its earlier CI `34841313504` failed with `CS0136` local-name conflicts; the correction was included in the passed source and the failure log remains retained. Main CI `34846154877` passed its distinct “Require main history to be contained in dev” check. No local tests duplicated the passing PR CI. |
| Schema and preflight | The schema remained at `20260914100000_WidenDocumentContentCacheVariant`; B1 UK South remained limit 3. Packaging and read-only preflight Build, Artifact, and PreProvision each exited 0. |
| Deployment | The Release 51 driver exited 0 at `2026-09-14T13:09:05Z`. Web deployment `808f1ecc-3bdb-4afa-a65a-804cc184c4f5` succeeded at `2026-09-14T13:04:09Z`; the Web App is `Running` on `DOTNETCORE|10.0`. Worker deployment `e9b5082a-6b48-4a7e-b9f9-9845364e9edc` and its configuration smoke passed. |
| Mailbox | The UI re-enable succeeded with state `Approved`, inbound `true`, sent `false`, and staff send `false`, proving fresh Web Inbox access. The mailbox is version 8, generation 3, activated at `2026-09-14T13:09:31.4060206Z`; the UI shows `Approved` and last completed at 14:10 UK. |
| Subscription and poll | Subscription `8ae31eda-21a9-4558-8e21-29b16dc09888` is generation 3 `Active`, maintained at `2026-09-14T13:10:00.178085Z`, and expires at `2026-09-20T13:10:00.178085Z`. The generation-3 poll completed at `2026-09-14T13:10:06.1157293Z`; its start boundary equals activation and it had no failures. Worker URL: `https://pegasus-prod-web-252ow37gij.azurewebsites.net/hooks/microsoft-graph/mail`. |
| Smoke | All production smoke checks passed, exiting 0 at `2026-09-14T13:12:24.6169651Z`. |
| Pause trace | The complete pause from `2026-09-14T11:37:22.417Z` to `2026-09-14T13:09:31.4060206Z` lasted 5,528.989 seconds (1 hour 32 minutes 8.989 seconds). Exchange reported zero delivery records at `2026-09-14T13:13:41.8177179Z`; this has the explicit recent-delivery latency caveat. No backfill was performed. |
| External clients | The server URL and MCP metadata are verified. External MCP client reconnections are operator-owned and outside this task, and do not block the release record. |
| Evidence | `artifacts/releases/release-51-bdc85085` retains the approved packet, manifest, ZIPs, bundle, CI/readiness/approval evidence, `deployment-readback.json`, `deployment-result.json`, `mailbox-ui-recovery.json`, `mailbox-recovery-readback-20260914T131113680Z.json`, `mailbox-pause-delivery-readback.json`, and `smoke-result.json`. |

## Release 50 — 14 September 2026 (historical App Service cutover)

Release 50 moved the Web host from the retiring Container App to the Linux App
Service Web App. It completed the destructive schema route and initial App
Service activation. The interrupted mailbox refresh below was subsequently
recovered by Release 51.

| Observation | Value |
| --- | --- |
| Candidate and approval | Source `3ce266fecd1ecf710d0abbdad2241717f2b54978`, version `0.1.0-alpha.1`; procedure commit `da7036dec0d4837acadc9aee124d9ad977e50bcd`. Alex approved the exact packet, targets, manifest, and a 30-minute Web/Worker outage. |
| Manifest and evidence | Manifest SHA-256 `D18501F7AF0B32D1E2857C139C86D7A28F03F8A49D854F5EE81A6D4CE2D3579D`; schema 3 with `win-x64` / `efbundle.exe`. The approved packet, manifest, artifacts, phase logs and interrupted-refresh evidence remain at `artifacts/releases/release-50-3ce266fe`. |
| Containment and migration | The retiring Container App source was `37d00f4c2fc6f106554e69ab4e8c3590939c25b7`, with approved active revision `pegasus-prod-web-252ow37gij--37d00f4c2fc6`. Fresh containment left no active old revisions or replicas, ingress disabled, and the old URL unserved. All 13 migrations through `20260914100000_WidenDocumentContentCacheVariant` applied. The route was destructive/non-additive because `LinkedAuditCase` replaced the unfiltered Case sequence uniqueness index with the linked-Audit filtered form. Runtime bootstrap verified 705 catalogued permission/denial rows and 487 effective runtime DML rows. |
| Outage and staging | Worker outage began `2026-09-14T11:06:50.9505668Z`; polling resumed at `2026-09-14T11:28:22Z`, about 21 minutes 31 seconds later. Web outage began `2026-09-14T11:10:33.8157183Z`; startup was observed at `2026-09-14T11:27:49.183Z`, about 17 minutes 15 seconds later. Both disabled Worker smokes passed and `worker.zip` deployment `37477644-fb2f-409c-b883-a68cab04496b` staged before containment. These initial outages were within the approved 30-minute window. |
| Provision and activation | Phase 4 provision `pegasus-prod-1789384475` created the UK South B1 Linux plan and Web App. Web ZIP deployment `b639667d-e0bc-463c-b143-1ef0dce051b0` completed successfully while the Web App was intentionally stopped. The Phase 4 wrapper exited 1 only because CLI status tracking waited for that intentionally stopped app; server-side completion was confirmed, its owned poller ended, and no ZIP was reuploaded. Activation deployment `pegasus-prod-1789385054` then succeeded. |
| Hostname read-back | `Glass__CallbackBaseUri` and `AutomationMcp__PublicOrigin` read `https://pegasus-prod-web-252ow37gij.azurewebsites.net/`. MCP protected-resource metadata named that origin for resource and authorization server; `azd-web-output-readback.json` verified the same four Web output keys in primary and release environments. |

### Interrupted mailbox refresh and recovered route

- The approved mailbox Disable save succeeded at `2026-09-14T11:37:22.417Z`.
  Its re-enable attempt at `2026-09-14T11:40:36.570Z` failed with “The address
  could not be found in the mail system.” Read-only SQL then showed `Disabled`,
  version 7, mailbox generation 2, and the generation-1 subscription `Active`.
  The mailbox existed and its identity was unchanged by migration.
- The Web managed identity received `403` from
  `GET /v1.0/users/instructions%40collisionengineers.co.uk` and had zero
  Microsoft Graph directory app-role assignments. The approved `User.ReadBasic.All`
  role-assignment POST failed `403 Authorization_RequestDenied` at
  `2026-09-14T11:45:38Z`; no successful directory grant was recorded. Two
  device-authentication attempts completed as the Digital Operator without
  Global or Privileged Role Administrator authentication. This directory route
  was disposed and superseded; no further privileged sign-in was pending.
- Exchange read-back showed the Digital Operator's Exchange Administrator route,
  no Web service principal, and a Worker service principal with
  `Application Mail.Read` scoped only to `Pegasus Production Instructions
  Mailbox`, filtered to `instructions@collisionengineers.co.uk`. The scoped
  Exchange Web service-principal registration and `Application Mail.Read` grant
  then completed with exit 0. Its authorization test was in scope for
  `instructions@collisionengineers.co.uk` and out of scope for `desk`; no
  directory, mailbox-write, or mail-send grant was added. Release 51 used that
  route to restore fresh Web Inbox access and re-enable the unchanged mailbox.
- The initial full smoke preceded the failed mailbox toggle and did not prove a
  new mailbox generation or webhook. The previous failure and permission-route
  evidence remains retained in `mailbox-refresh-readback.json`,
  `mailbox-directory-read-approval.json`, `mailbox-directory-read-grant.log`,
  `exchange-mailbox-access-readback.json`, `web-exchange-mailbox-read-grant.json`,
  and `web-exchange-mailbox-read-grant.log`.

## Release 49 — 11 September 2026

Every Glass's session situation handled on the Case record (PR 736): a
session that still holds the account can be closed by its owner, a live
session on another Case is named on every Case the Engineer opens with no
launch offered, a second launch is refused by reading the live session
before anything is inserted, every estimate id a session was launched under
is retained, and page-level refusals are logged. Deployed through the normal
route of the release skill from an isolated worktree at source
`37d00f4c2fc6f106554e69ab4e8c3590939c25b7`, version `0.1.0-alpha.1`.

| Observation | Value |
| --- | --- |
| Promotion | PR 736 fast-forwarded into `dev` after its CI passed; PR 737 ran the full suite on the merged head; `main` = `dev` = `37d00f4c` by atomic fast-forward from `8d4031ff`. |
| Manifest | schema 3, SHA-256 `E32D7F99DE9868A62281773541779DBD274F8A02F2EF064B5DC361ACD998DD0D`, `win-x64` / `efbundle.exe`. |
| Web image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:f7ea8e77d8336b40c5c6d0f1635d9c8f7d11b69528487999dfb49b550b4a6e2a`; remote digest equalled the manifest. |
| Migration | Identity unchanged at `20260911100000_PromoteVehicleLookupSuggestionsToFacts`; no bundle or bootstrap run. |
| Web | `pegasus-prod-web-252ow37gij--37d00f4c2fc6` active, `Healthy`, one replica, at the approved digest. |
| Worker | `worker.zip` deployed with the Worker approved live; state `Running`; every `AzureWebJobs.*.Disabled` setting `false`. |
| Smoke | Full smoke passed on the first run (last poll 20:50:02Z, subscription expiry 17 September 18:10:00Z). |
| Live check | Unauthenticated smoke only. For Alex: the EX10UHD session that has held alex's account since 17:09Z can now be closed from that Case's Estimate section (confirmation and reason), after which a fresh launch and the full Save & Exit round trip are the remaining live proof. The 18:44Z Sev1 `pegasus-prod-application-exceptions` alert was Entity Framework logging the caught duplicate-key refusal of a second launch; that path no longer reaches the index. |
| Artifacts | Retained at `artifacts/releases/release-49-37d00f4c` (ignored) with the phase logs and the driver. |

Authorization: Alex, 11 September 2026 (the approved session-handling
plan through to deployment). No outage: normal route.

## Release 48 — 11 September 2026

The Glass's return is bounced through Pegasus's own origin before it is
read (PR 733): the staff cookie is SameSite=Strict, and the estimator returns
the operator by a cross-site navigation, so the first live round trip
(EX10UHD, 17:09Z, session Active with an MVA vehicle and an ERE id) reached
the callback without a session and was met with the sign-in challenge.
Deployed through the normal route of the release skill from an isolated
worktree at source `8d4031ff051309886d51c6d55ed3404983df695b`, version
`0.1.0-alpha.1`.

| Observation | Value |
| --- | --- |
| Promotion | PR 733 fast-forwarded into `dev` after its CI passed; PR 734 ran the full suite on the merged head; `main` = `dev` = `8d4031ff` by atomic fast-forward from `70877fb6`. |
| Manifest | schema 3, SHA-256 `2DF4576D747C57420051533AF83972E4B6F9C4BE411C8BFAA5F7F40370F42819`, `win-x64` / `efbundle.exe`. |
| Web image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:7b8dd2054e44f167e0fc6056adee194e19d9c7ff985800c13d56a8974ce64183`; remote digest equalled the manifest. |
| Migration | Identity unchanged at `20260911100000_PromoteVehicleLookupSuggestionsToFacts`; no bundle or bootstrap run. |
| Web | `pegasus-prod-web-252ow37gij--8d4031ff0513` active, `Healthy`, one replica, at the approved digest. |
| Worker | `worker.zip` deployed with the Worker approved live; state `Running`; every `AzureWebJobs.*.Disabled` setting `false`. |
| Smoke | Full smoke passed on the first run (last poll 18:30:03Z, subscription expiry 17 September 18:10:00Z). |
| Live check | Unauthenticated smoke only; the full Glass's round trip (launch, Save & Exit, return, import) on a plate MVA can look up remains for Alex. LF62GOC is not such a plate: MVA answers `vrm_lookup=0` with an empty candidate list for it, which the portal reports as vehicle not found. |
| Artifacts | Retained at `artifacts/releases/release-48-8d4031ff` (ignored) with the phase logs and the driver. |

Authorization: Alex, 11 September 2026 (the Glass's debug task, continued
after the return failure on EX10UHD). No outage: normal route.

## Release 47 — 11 September 2026

Glass's candidate list read again before refusal, with the refusal saying
what the provider answered (PR 729): after Release 46 the launch on
a.QDOS26005 passed the lookup and stopped at `glass.candidates.refused` on
the list's single read. Deployed through the normal route of the release
skill from an isolated worktree at source
`70877fb64a263a6f411382f4e5b14508156bfdb4`, version `0.1.0-alpha.1`.

| Observation | Value |
| --- | --- |
| Promotion | PR 729 fast-forwarded into `dev` after its CI passed; PR 730 ran the full suite on the merged head; `main` = `dev` = `70877fb6` by atomic fast-forward from `70ed12a5`. |
| Manifest | schema 3, SHA-256 `B9831A46FC8306372258EB7760ACAC994F56A871B1EE2AC5BA57FDFD60DA9377`, `win-x64` / `efbundle.exe`. |
| Web image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:9b7b09cd7f7fab5f8ef5e3a2d9eb59fed454011400162117fc276f082bfe7465`; remote digest equalled the manifest. |
| Migration | Identity unchanged at `20260911100000_PromoteVehicleLookupSuggestionsToFacts`; no bundle or bootstrap run. |
| Web | `pegasus-prod-web-252ow37gij--70877fb64a26` active, `Healthy`, one replica, at the approved digest. |
| Worker | `worker.zip` deployed with the Worker approved live; state `Running`; every `AzureWebJobs.*.Disabled` setting `false`. |
| Smoke | Full smoke passed on the first run (last poll 16:30:03Z, subscription expiry 13 September 17:15:12Z). |
| Live check | Unauthenticated smoke only; the Glass's launch on a.QDOS26005 remains for Alex. A candidate refusal now reads the list three times first, and the Web console warning names the lookup's numbers and the list's `success`, keys and `html` length. |
| Artifacts | Retained at `artifacts/releases/release-47-70877fb6` (ignored) with the phase logs and the driver. |

Authorization: Alex, 11 September 2026 (the Glass's debug task, continued
after the `glass.candidates.refused` report). No outage: normal route.

## Release 46 — 11 September 2026

Glass's vehicle lookup follows the portal's own stock rule (PR 726): the
first live launch on a.QDOS26005 failed at `glass.lookup.unavailable`
because the adapter always demanded a fresh search, which a registration the
account has never valued does not answer. Deployed through the normal route
of the release skill from an isolated worktree at source
`70ed12a571bdeadc950bcf21f472e8430a02bd0f`, version `0.1.0-alpha.1`.

| Observation | Value |
| --- | --- |
| Promotion | PR 726 fast-forwarded into `dev` after its CI passed; PR 727 ran the full suite on the merged head; `main` = `dev` = `70ed12a5` by atomic fast-forward from `75120f72`. |
| Manifest | schema 3, SHA-256 `6C1729A25F9A213490BB38179BE6C7B5E4FD0CA4E0BAB04349F0EB35C8AFF675`, `win-x64` / `efbundle.exe`. |
| Web image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:c7073ce830112fdec8d1d241b8bcc61a7492f7612ebad96056ec6f579f47b56d`; remote digest equalled the manifest. |
| Migration | Identity unchanged at `20260911100000_PromoteVehicleLookupSuggestionsToFacts`; no bundle or bootstrap run. |
| Web | `pegasus-prod-web-252ow37gij--70ed12a571bd` active, `Healthy`, one replica, at the approved digest after the previous revision finished deprovisioning (three re-polls); its startup console log holds no Glass or exception line. |
| Worker | `worker.zip` deployed with the Worker approved live; state `Running`; every `AzureWebJobs.*.Disabled` setting `false`. |
| Smoke | Full smoke passed on the first run (last poll 14:15:02Z, subscription expiry 13 September 17:15:12Z). |
| Live check | Unauthenticated smoke only; the Glass's launch on a.QDOS26005 remains for Alex. A refusal now shows `glass.lookup.notfound` (the provider does not know the plate) or `glass.lookup.unavailable`, and the Web console log carries a warning line with `stockcount`, `vrm_lookup` and whether a type number was present. |
| Artifacts | Retained at `artifacts/releases/release-46-70ed12a5` (ignored) with the phase logs and the driver. |

Authorization: Alex, 11 September 2026 (the Glass's debug task, approved
plan through to deployment). No outage: normal route.

## Release 45 — 11 September 2026

Glass's launch configuration (PR 721), the Case Vehicle section with lookup
fills and the derived report mileage code (PR 722), and Administration
actions applying on the click (PR 723). Deployed through the destructive
route of the release skill from an isolated worktree at source
`75120f7299c3b35f517da45ec660c830352f2a4b`, version `0.1.0-alpha.1`.

| Observation | Value |
| --- | --- |
| Promotion | PRs 721, 722 and 723 merged into `dev` as three merge commits after each PR's CI passed (the cancelled shard on 721 was a slow runner and passed on rerun); PR 724 ran the full suite on the merged head; `main` = `dev` = `75120f72` by atomic fast-forward from `955bfa2e` / `6b1a2e5a`. |
| Manifest | schema 3, SHA-256 `FFAC86B80A10753EDA87158793C0DD9CB1BA047F26675EF528907B18F5D7E540`, `win-x64` / `efbundle.exe`. |
| Web image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:e7bd3a0f783c27009b88ad1c838dd2bed1b8292f8c4e62be5b5d16ae2497ddf3`, uploaded with oras 1.3.4; remote digest equalled the manifest. |
| Configuration | azd environment gained `GLASS_MARKET_VALUE_ASSESSOR_BASE_URI`, `GLASS_ESTIMATOR_BASE_URI` and `GLASS_REPAIR_PROFILE_ID` (4063) after `azd env refresh`; the Web revision carries the four `Glass__*` settings with the callback origin derived from its own ingress. |
| Containment | Worker Disabled census set true, approved `worker.zip` staged with the disabled smoke passing before and after; Worker read back `Stopped`; old revision `pegasus-prod-web-252ow37gij--955bfa2ec0ef` deactivated with zero replicas; fresh read-back before SQL. Estate at containment: 4 Cases, 0 edit scopes, 0 Glass sessions. |
| Migration | Destructive `20260911100000_PromoteVehicleLookupSuggestionsToFacts` applied by the bundle: 9 lookup suggestion rows promoted to facts, 0 `vehicle.year` assessment rows to carry, 4 Cases gained a lookup-sourced `vehicle_year` (13 lookup fact rows, 0 suggestions, 4 year rows after); the first bootstrap attempt was refused because the release worktree held an untracked driver script, and passed once removed: 674 catalogued permission rows and 465 effective runtime DML rows; migration head verified. |
| Web | `pegasus-prod-web-252ow37gij--75120f7299c3` active, `Healthy`, `RunningAtMaxScale`, one replica, at the approved digest; the old revision read active while deprovisioning for about a minute, then inactive. Startup passed the Production Glass configuration check; the revision's console log holds no Glass or exception line. |
| Worker | Provisioned back to `approved-live-worker`; state `Running`; every `AzureWebJobs.*.Disabled` setting `false`. |
| Smoke | Full smoke passed after activation (last poll 11:50:02Z, subscription expiry 13 September 17:15:12Z). |
| Live check | Unauthenticated smoke only; authenticated review of the delivered behaviour remains for Alex: Glass's on a.QDOS26005 opens the estimator in its own window and `GlassRepairEstimateSessions` gains an Active row, the Vehicle section shows the filled values with provenance, and Administration actions post on the click. |
| Artifacts | Retained at `artifacts/releases/release-45-75120f72` (ignored) with the phase logs and the driver. |

Authorization: Alex, 11 September 2026 (the review-and-release task: merge the
three PRs to `dev`, promote to `main`, deploy). The outage window ran from
12:53 to 13:02 UK on an estate holding four Cases.

## Release 44 — 10 September 2026

Operator-findings rectification after Release 42 (PR 719): intake image
previews and thumbnails, account dialogs, record edit leases, Action Logs
attribution, administration corrections, the Case header and edit mode, Files
tabs with image tags replacing the Third-party vehicle flag, and vehicle lookup
at Case creation. Deployed through the destructive route of the release skill
from an isolated worktree at source `955bfa2ec0ef9031b61fff827be92e6b3983166f`,
version `0.1.0-alpha.1`.

| Observation | Value |
| --- | --- |
| Promotion | PR 719 fast-forwarded into `dev`; `main` = `dev` = `955bfa2e` by atomic fast-forward from `d395107a` after CI passed every check on that head. |
| Manifest | schema 3, SHA-256 `0252EC97263485468EA6BB90C9A021A7179B96D61551D792DDB32310E534180E`, `win-x64` / `efbundle.exe`. |
| Web image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:dbd43306d4f2b7bef375226f0fa025238196882297b8c16c24e7a18edc091bd5`, uploaded with oras 1.3.4; remote digest equalled the manifest. |
| Containment | Worker Disabled census set true, approved `worker.zip` staged with the disabled smoke passing before and after; Worker read back `Stopped`; old revision `pegasus-prod-web-252ow37gij--d395107ada8a` deactivated with zero replicas; fresh read-back before SQL. |
| Migration | Four migrations `20260910105000_SoftRemoveValuationPresets`, `20260910110000_SecurityEventActingPrincipal`, `20260910111500_DocumentContentCacheVariants` and `20260910120000_CaseImageTags` applied by the bundle; the Third-party conversion found 0 rows; bootstrap verified 674 catalogued permission rows and 465 effective runtime DML rows; migration head verified. |
| Web | `pegasus-prod-web-252ow37gij--955bfa2ec0ef` active, `Healthy`, `RunningAtMaxScale`, one replica, at the approved digest; the old revision read active while deprovisioning for under a minute, then inactive. |
| Worker | Provisioned back to `approved-live-worker`; state `Running`; every `AzureWebJobs.*.Disabled` setting `false`. |
| Smoke | Full smoke passed after activation (last poll 20:40:02Z), and again after the wipe once the first post-wipe poll completed (21:00:03Z). |
| Wipe | Intake wipe #6 immediately after the release, Worker stopped for it and restarted: 77 blobs (30,635,544 bytes) cleared from `pegcustody252ow37gij/transient-intake`; 617 rows deleted across 87 non-preserved tables; 38 tables preserved after adding `ImageTags`, `LabourRateCards`, `ContactRoles` and `ContactPrincipalLinks` to the preserve list; `CaseSequences` 2, `ImageIntakeSequences` 7 rows and `UnidentifiedSequences` 1 unchanged; `ValuationPresets` 1/1; mail cutoff 2026-09-10T20:56:50Z; `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box untouched. |
| Live check | Unauthenticated smoke only; authenticated browser review of the delivered behaviour (tags, Files tabs, Crop in Review, dialogs, take-over, Action Logs, presets, lookup line, EVA exclusion) remains for Alex. |
| Artifacts | Retained at `artifacts/releases/release-44-955bfa2e` (ignored) with the phase, smoke and wipe logs. |

Authorization: Alex, 10 September 2026 (`MERGE AUTH GRANTED` for the promotion
and the destructive route; separate approval naming the wipe targets). The
outage window ran from 21:50 to 22:30 UK on an estate holding one test Case.

## Release 43 — 10 September 2026

Fee note inside the report PDF or as a separate document (operator option).
Deployed through the unchanged-identity route of the release skill from an
isolated worktree at source `d395107ada8a721570667726db5bff6d77ddcbdf`,
version `0.1.0-alpha.1`.

| Observation | Value |
| --- | --- |
| Promotion | `main` = `dev` = `d395107a` by atomic fast-forward from `c7e1ad5f` / `84a1d70e` after PR 718 CI passed (infrastructure job skipped: no infrastructure change). |
| Manifest | schema 3, SHA-256 `E8EE7E26F75AC9E87D701E59F3DE8ADCA4FB42F5E836580135226B9DE1282CDA`, `win-x64` / `efbundle.exe`. |
| Web image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:8660252a1a4d712d4d4778ce2237db48e07a9da77590513f2356542878822a90`; remote digest equalled the manifest. |
| Migration | Identity unchanged at `20260910104000_RecordCaseReportViewAndDownloadEvents`; no bundle or bootstrap run. |
| Web | `pegasus-prod-web-252ow37gij--d395107ada8a` active, `Healthy`, one replica, at the approved digest after the previous revision finished deprovisioning. |
| Worker | `worker.zip` deployed with the Worker approved live; state `Running`; every `AzureWebJobs.*.Disabled` setting `false`. |
| Smoke | Full smoke passed on the first run (last poll 12:35:03Z, subscription expiry 13 September 17:15:12Z). |
| Live check | Authenticated review of the Include fee note choice against the deployed bytes remains for Alex; the combined PDF was rendered through the real Playwright path locally and inspected before release. |
| Artifacts | Retained at `artifacts/releases/release-43-d395107a` (ignored) with the phase logs. |

## Release 42 — 10 September 2026

Stage 5–8 residual completion after Release 41. Deployed through the normal
route of the release skill from an isolated worktree at source
`c7e1ad5fa765bf6d24a97de7c9e2da39eff87d27`, version `0.1.0-alpha.1`.

| Observation | Value |
| --- | --- |
| Promotion | `main` = `dev` = `c7e1ad5f` by atomic fast-forward from `13ac6f71` / `3f2f873f` after PR 717 CI passed every check. |
| Manifest | schema 3, SHA-256 `C2D07C5D79E3F3A3E04338240DF86B131D3E43A4C662F356DB821631FB3DD1FD`, `win-x64` / `efbundle.exe`. |
| Web image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:71cac4cc3966ce220948ef79ea2fda685c99cd85cec76092ceead8f00a60a9be`; remote digest equalled the manifest. |
| Migration | Additive: `20260910104000_RecordCaseReportViewAndDownloadEvents` (extends the workflow-event index filter) applied by the bundle; bootstrap verified 660 catalogued permission rows and 458 effective runtime DML rows; live head read back equal to the manifest. |
| Web | `pegasus-prod-web-252ow37gij--c7e1ad5fa765` active, `Healthy`, `RunningAtMaxScale`, one replica, at the approved digest; the previous revision read active while `Deprovisioning` for about 30 seconds during the single-mode switch. |
| Worker | `worker.zip` deployed with the Worker approved live; state `Running`; every `AzureWebJobs.*.Disabled` setting `false`. |
| Smoke | Full smoke passed on the first run (last poll 10:45:14Z, subscription expiry 13 September 17:15:12Z). |
| Live check | Authenticated browser review of the new screens against the deployed bytes remains for Alex; synthetic local captures of the same screens passed before the release. |
| Artifacts | Retained at `artifacts/releases/release-42-c7e1ad5f` (ignored) with the phase logs. |

Authorization: the operator goal of 10 September 2026 (perform the second
deployment when the outstanding work is complete).

## Release 41 — 10 September 2026

Corrective pre-v1 UI and workflow rectification. Deployed through the
destructive route of the release skill from an isolated worktree at
source `13ac6f71581d22544efb71d37b60cb99a6172b3f`, version `0.1.0-alpha.1`.

| Observation | Value |
| --- | --- |
| Promotion | `main` = `dev` = `13ac6f71` by atomic fast-forward from `07f50e80` after PR 716 CI passed every check. |
| Manifest | schema 3, SHA-256 `5C36A86B22BF7E0AE13EACF97F92F57335AA3B754CD450609AD2C609C023EC73`, `win-x64` / `efbundle.exe`. |
| Web image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:1123186b7c0622040f5fed95affcf8c57a3c9cb1eb2967d408ed533b529bdb42`, uploaded with oras 1.3.4; remote digest equalled the manifest. |
| Containment | Worker Disabled census set true, approved `worker.zip` staged with the disabled smoke passing before and after; Worker read back `Stopped`; old revision `pegasus-prod-web-252ow37gij--c9bd3aa6bba0` deactivated with zero replicas; fresh read-back repeated immediately before SQL. |
| Migration | 14 migrations from `20260909120000_ApprovedMailboxDefaultStaffSend` through `20260910103000_RetainObservedStaffMailSentEvidence` applied by the bundle; bootstrap verified 660 catalogued permission rows and 458 effective runtime DML rows; live head read back equal to the manifest. Pre-SQL inspection found no row tripping any stop check (0 ClaimSources, 4 single-role accounts, 1 Engineer note moved, 0 directory entries, 0 chase reasons). Post-migration counts: 1 Case, 15 Organizations, 15 ContactRoles, 1 moved note, 4 role rows for 4 users. |
| Web | `pegasus-prod-web-252ow37gij--13ac6f71581d` active, `Healthy`, `RunningAtMaxScale`, one replica, at the approved digest; the old revision read active while `Deprovisioning` for under a minute during the single-mode switch, then inactive. |
| Worker | Provisioned back to `approved-live-worker`; state `Running`; every `AzureWebJobs.*.Disabled` setting `false`. |
| Smoke | Worker-only disabled smoke passed after the new Web; the first full smoke failed only the inbox-liveness gate because the newest poll predated the maintenance window (21 minutes against the 15-minute threshold); the rerun 100 seconds later passed in full (last poll 08:36:35Z, subscription expiry 13 September 17:15:12Z). |
| Live check | Unauthenticated: root redirects to sign-in; the sign-in page returns 200 with the Collision Engineers frame. Authenticated browser review of the refined design against the deployed bytes remains for Alex; no staff credentials were used by the agent. |
| Artifacts | Retained at `artifacts/releases/release-41-13ac6f71` (ignored) with the phase logs. |

Authorization: the operator goal of 10 September 2026 (perform the corrective
deployment; merge to main authorized in the handoff). The outage window ran
immediately in working hours on an estate holding one test Case.

## Release 40 — 9 September 2026

The approved corrective release was deployed from Windows PowerShell 7 at
source `c9bd3aa6bba06a81f35b0e4aca36ab8c01ac744b`, version `0.1.0-alpha.1`.
All ten checks in [the exact dev CI run](https://github.com/collisionengineers/pegasus/actions/runs/34312991582)
passed before the whole dev branch was promoted through [PR #675](https://github.com/collisionengineers/pegasus/pull/675).
This includes merged corrections #712, #714 and #715; unmerged saved worktree
branches are excluded.

The schema-3 manifest SHA-256 is
`2C9127990A039C49020053D46A413F1E4064FCF8D27AD6AD71D61D7F4D6C54D6`.
The sole active Web revision is
`pegasus-prod-web-252ow37gij--c9bd3aa6bba0`, observed Healthy,
RunningAtMaxScale and Provisioned at image digest
`sha256:6179de11540614449c3c70d3934e0933a872b992f5487d9d10405efd91676684`.
The manifest's linux/amd64 Web image was uploaded and its remote digest verified;
its exact Worker ZIP was staged by deployment
`d352a2ba-edda-4aeb-841e-a6616ec2e05d`. The native win-x64 `efbundle.exe`
applied the three pending migrations through
`20260909091500_RemoveCaseDocumentOcrOperations`. Runtime bootstrap verified
651 catalogued permission/denial rows and 449 effective runtime DML rows.

The maintenance sequence began with Worker Function disablement and new-package
staging at 05:26 UTC. Worker Stopped and the exact old Web revision inactive with
zero replicas were verified before the intake reset and again before migration.
The reset cleared 122 blobs (156,871,692 bytes) in
`pegcustody252ow37gij/transient-intake` and 448 intake-created rows across 87 SQL
tables. Verification found zero remaining blobs and zero populated reset tables;
34 tables were preserved. The SQL batch reported 449 affected rows including the
mail-state update. The committed receive-time cutoff is
`2026-09-09T05:31:30.9218156+00:00`. Identity, mailbox approval/activation times,
all four reference-sequence tables (zero keyed-value changes) and all five
valuation presets were preserved. The reset did not touch `authentication-ring`,
`box-links`, `pegtrans252ow37gij` contents, Outlook or Box.

Provisioning created `pegasus-prod-ocr-252ow37gij`; its successful state,
disabled local-key authentication and Worker managed-identity Cognitive Services
User assignment were verified. Web and OCR infrastructure were provisioned with
Worker disabled, then the compatible Worker was explicitly activated. The final
checks passed at 05:42:33 UTC: Worker Running, every canonical Function Disabled
setting false, exact Web version/digest and health, Graph validation handshake,
anonymous Case-access denial and schema head. Intake liveness reported a completed
poll at 05:40:40 UTC and an active subscription expiring
13 September 2026 at 17:15:12 UTC. Alex owns live behavioural acceptance; no
intake/OCR canary was uploaded by the agent and no functional acceptance is claimed.

Retained attempts and limitations: the first migration host setup stopped before
SQL because the copied azd environment lacked `BOX_HOLDING_FOLDER_ID`. The existing
Worker value `415928884118`, subsequently confirmed by Alex, was restored along
with existing signing/encryption certificate URI inputs from the old Web revision.
The migration retry succeeded. The initial post-provision Web check reported an
unexpected active revision; a fresh full inventory and the unchanged strict check
then verified the sole exact approved revision and digest. The first activation
smoke stopped on the reset's null poll-completion timestamp; the normal scheduled
poll completed and the full smoke passed on rerun. No old application bytes were
explicitly reactivated after the destructive migration.

The four hash-verified artifacts are retained locally under
`artifacts/releases/release-40-c9bd3aa6/`, with stage transcripts and retry evidence
in its `execution/` directory. The approval packet and preparation evidence are
`artifacts/corrective-release-approval-20260909.md` and
`artifacts/corrective-readiness-20260909.md`. These local artifacts are not
committed. The main checkout's ignored azd environment was synchronized to the
observed deployment inputs; its previous copy is retained with the release.

## Read-only production observation — 6 September 2026

On 6 September 2026, Azure CLI on the Windows development host read the existing
`rg-pegasus-prod` Container App. The sole active revision was
`pegasus-prod-web-252ow37gij--0f0e90ae44ff`, Healthy, with 100% traffic.
Its image digest was
`sha256:b791d9587224d30d68fd6abcbd1e1d5f389f2baefc3702d9ec2d2f37398eef15`,
matching the release-38 record in the
[baseline release ledger](https://github.com/collisionengineers/pegasus/blob/af1625fae8ac8018054c95e988907f6c44fa4639/docs/operations.md). `Features__AutomationMcp` and
`Features__ProviderApi` were both `true`; no explicit `Features__SendToAi`
environment value was present. This read checked revision, image and those
settings only. It did not recheck SQL, provider credentials, external-client
round trips, document rendering or operator acceptance.

Later source changes, old workstation-readiness checks and earlier renderer
canaries are separate evidence. Their exact observations and limitations remain
in the [baseline operations record](https://github.com/collisionengineers/pegasus/blob/af1625fae8ac8018054c95e988907f6c44fa4639/docs/operations.md).
Do not infer present authentication, render readiness or deployment from them.

## Historical release-39 record — 7 September 2026

This is a qualified historical record from the unmerged [PR #676 operations
entry](https://github.com/collisionengineers/pegasus/blob/93255e5c188865e5b0dab95d6c628ab49a902d99/docs/operations.md), not a fresh D59 Azure,
artifact, telemetry or estate observation. It names source
`3da60bd0c270111d5168dc17246dc831882108ea`, image
`sha256:cc1a5077efdc761e359667a1bfc73b7cf8851e88437cd95fec2615fc41b1fe73`,
manifest SHA-256
`F9C64EF7729484EF3BD3EFBF24F0791ADA4F53857EA05F5A80E8453A549D3E08`,
and migration head `20260907100000_RemoveAutomaticEvaSubmission` as PR-recorded
release identifiers. Git history independently establishes only that source
identity. GitHub Actions [run 34132950893](https://github.com/collisionengineers/pegasus/actions/runs/34132950893)
independently shows `test-ui` failed while `browser` succeeded; it does not
confirm the PR's browser-cancelled or two-lane-waiver narrative, and D59 found
no retained waiver-authorisation receipt.

PR #676 records that Kudu rejected the manifest Worker ZIP
`DF651AD0D87850AB7ECD883D06951B8B1844CA4C93BAAD67715D8D075BF4F261` because
Linux glob packaging omitted `.azurefunctions/` and `.playwright/`. It further
records a same-source-SHA replacement Worker package
`0651B1862CA8201C5D70C51577B5614F491A08764D46D5B3536759CF6F1B6BBB` and
`config-zip` deployment `2ce3eb15-0a86-4d14-94f2-ecc1264ea25a`. The replacement
DLLs were not byte-identical to the manifest copies, so this is retained as a
provenance deviation rather than equated with the manifest artifact. The same
historical record attributes the intervening Worker failures and subsequent
smoke/telemetry statements to that release; D59 did not re-read the manifest,
either ZIP, deployment receipt, Azure telemetry or current estate. Later
source-only ZIP correction [PR #703](https://github.com/collisionengineers/pegasus/pull/703)
does not establish a release-39 artifact or deployment.

PR #676 also records seven migrations committing before `V1PlatformFoundation`
failed, leaving release 38 briefly without two `WorkflowConfigurations`
review-flag columns. It records two separately authorised test-data deletions
before the remaining six migrations and bootstrap completed: an intake wipe,
then removal of the conflicting QDOS organisation/principal/lineage and its
sequence row, resetting that seeded lineage. Those partial-migration and reset
facts remain historical PR claims requiring their own past authorisation; they
are not D59 permission to migrate, reset, or infer a present database state.

<a id="approved-box-integration-test-target"></a>


## Approved Box custody root

Box folder `405543781910` ("pegasus") is the production custody root: all case
folders are created only under it, and the deployed configuration carries it.
Folder `392761581105` is the only eligible controlled integration-test
boundary, confined to an approved disposable test subtree; neither folder is
standing write authority. The exact-target approval and invocation checks are
owned by the [runbook's live-operation approval matrix](runbook.md#operational-authority).
The activated production caller is confined to case-scoped objects under the
configured root and has no delete, move, copy, or share operation. Failed
attempts remain visible for authorised staff retry; there is no automatic
business retry.

Production server authentication uses the retained `box-config-json` JWT
configuration and `box-client-secret` Key Vault secrets. The Box SDK obtains and
refreshes short-lived authorization headers at runtime; a static access token is
not an accepted setting or deployment input. Since release 3 both hosts resolve
their own copy of these secrets server-side only — the Worker through app
setting Key Vault references and the Web through Key Vault-backed Container
Apps secrets, each via its own managed identity (see the
[production environment Secrets record](operations.md)) — never
client-side.

The intended application staff accounts are Pegasus Identity accounts. The DevelopmentOffline profile authenticates its deterministic local Administrator fixture and enforces its Administrator role. Application staff identity initialization remains a separately controlled application operation; Entra users must not be assumed. Third-party credentials must never enter tracked settings, command-line arguments, prompts that may be retained, terminal output, telemetry, or business history.


## Support and incident response

Alex provides first-line application support and initially receives security,
availability, failure and cost alerts. Additional recipients are configuration,
not code changes. Critical incidents are acknowledged immediately while Alex is
in the staffed office; outside staffed hours, response is as soon as reasonably
possible. This records the support arrangement, not an invented 24/7 SLA.

Two Azure Monitor rules page the action group (`infra/modules/platform.bicep`):
`pegasus-prod-web-http5xx` (Sev1) fires when the Web App returns more than
one HTTP 5xx in a five-minute window, and `pegasus-prod-application-exceptions`
(Sev2 since 15 September 2026) fires on a correlated Web or Worker exception
signature in a fifteen-minute window. Both auto-resolve. Before the tuning
each fired on a single event and paged several times a day on transient
faults.

Causes seen in the 7–14 September window and their disposition: the Case
list's "Confirmed vehicle fields exist without a confirmed vehicle
registration" throw was removed by `6d51c993d` (Release 44) and last fired on
10 September before that release; the content cache's `409 BlobAlreadyExists`
on a concurrent preview, the Intake Source and Asset `500` while Box custody
was still pending, and unretried transient SQL faults are fixed by the
alert-causes change (15 September). Transient SQL faults retry only outside a
store transaction; inside one they surface as before.

Emergency production access is Alex initially, plus specifically designated
Administrators or Azure operators. Exact credentials and grants are not stored
in this file. Read-only inventory and external mutation have different authority.

## Retained evidence and recovery basis

[The baseline operations record](https://github.com/collisionengineers/pegasus/blob/af1625fae8ac8018054c95e988907f6c44fa4639/docs/operations.md)
retains the prior release ledger, failed attempts, hashes, rollout limitations,
rollback artifacts and old cost observations through release 38. The dated
historical release-39 entry above supplements that ledger with PR #676-recorded
evidence and its explicit limitations; neither record is a fresh observation.
Use the exact applicable release evidence when selecting recovery; do not infer
rollback compatibility or a deployed repair from a later source fix.

A new observation records UTC time, environment, artifact/revision, migration,
observed activation and the checks actually performed. A release can change
deployable infrastructure, dependencies, schema or runtime configuration even
when no file under `src/` changes. Update this summary with observations, not
future requirements or a second ticket-status ledger.

No fresh full-day workload, excluded capacity/soak run, external-provider
acceptance or disaster-recovery exercise is claimed by this documentation change.
Historical evidence remains historical; an unresolved incident stays explicit
until an observation establishes its disposition.

## Current development estate choices

The current Web warm settings are the operator's chosen configuration, not an
architectural minimum. Current data is disposable test data and need not be
preserved during an authorized reset. Neither fact supplies permission to
change settings or clear an estate outside the current task's authorization.
