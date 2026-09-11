# Operations

This is the last recorded deployed-state and support summary. It is not a fresh
cloud observation. Exact source structure belongs in [architecture](current-architecture.md);
procedures are reached through [the runbook](runbook.md).

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
