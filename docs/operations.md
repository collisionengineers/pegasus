# Operations

This is the last recorded deployed-state and support summary. It is not a fresh
cloud observation. Exact source structure belongs in [architecture](current-architecture.md);
procedures are reached through [the runbook](runbook.md).

## Release 61 — 23 September 2026 (deployment live)

Release 61 deployed [PR 821](https://github.com/collisionengineers/pegasus/pull/821), the operator's fixes after Release 60. The Decisions pills sit on one line. The Repair Spec full-screen view covers the viewport again: the estimate-import drop target's `position:relative` had overridden it. Every toast has a ×. *On the report* no longer stretches to the Calculation panel. The Accident band loses its tinted fill. The Fee tab records the agreed fee once, with VAT and total beside it. The ribbon Save ends edit mode and releases the lease. The route was the approved normal route with the migration identity unchanged. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `afcba4d45171f3e194b96a2d8761bccfc72be519` (the PR 821 merge), promoted atomically to both `dev` and `main` at 15:00Z. Manifest schema 3 SHA-256 `6C6BF11C0BA092DAA3582207DC16FDD1F2E1FFFFA91D3563309FE5DF5DC3B07A`. `web.zip` SHA-256 `EE237B41C4B9919D65A4BA767C6BEB9D33906E17352312D1EA8284FD21F581DA`. `worker.zip` SHA-256 `BA1ACED021321AB9A1CAA41796FD27665D661A27E0F79BBB4CB41050D0BF1EA1`. Windows `efbundle.exe` SHA-256 `05DB49214104E980FF0CEC1868F4D5B5C5CA2D53B2DBE6DF2363A3C8B0A0EA7A` (not run). |
| Review and verification | [PR CI](https://github.com/collisionengineers/pegasus/actions/runs/35875110056) passed every job at the PR head `da1df2d9f`, whose merge into `dev` is the release source. PR 821 had no formal review; the operator asked for the fix, merge and redeploy. [Main CI](https://github.com/collisionengineers/pegasus/actions/runs/35878395288) passed every job at the exact release SHA, including six SQL integration shards. The release build and the Local, Artifact, PreDeploy and PreProvision plan gates passed. |
| Schema and grants | Migration identity **unchanged**: `20260923180000_ValuationCardFiguresOptional`, applied in Release 60. No migration or bootstrap ran. `azd provision` found no changes at 15:05:57Z (pre-flight: B1 in uksouth limit 3). |
| Web deployment | OneDeploy `213b6712-7c02-4c70-adda-3ecf85b0edf0` succeeded at 15:06:19Z. The site started in 157 seconds. On the first attempt it read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source and version at 15:09:09Z. |
| Worker deployment | ZIP deployment completed successfully after trigger synchronization and the platform health check. The canonical Disabled-setting census passed as `approved-live-worker`. |
| Production smoke | Passed at 15:12Z. Active Web package `20260923150604.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-23T15:10:00Z`, and the active Graph subscription expires `2026-09-28T14:30:00Z`. Smoke is unauthenticated; the fixed Case page surfaces await a signed-in look. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-61-afcba4d4`. The drivers and phase logs are under `artifacts/releases/release-61-driver`. |

## Release 60 — 23 September 2026 (deployment live)

Release 60 deployed [PR 820](https://github.com/collisionengineers/pegasus/pull/820): the Case page shows the same fields in read and edit mode, uses one source-tag system, keeps the damage disc as drawn and clipped, and fills valuation cards in place, with the Case Save recording them. The Repair Spec also uses one layout for both modes. The route was the approved normal route with one additive migration. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `729f4231c28180feb5407c7cce3ae64ca9726546` (the PR 820 merge), promoted atomically to both `dev` and `main` at 13:50Z. Manifest schema 3 SHA-256 `C3522B2FB71F21C3C6169615D0B11E8DB53ECCFD506747CE97AB5BA71875B5EA`. `web.zip` SHA-256 `51DBDDB745FDC97663FC1DF0D4111B86C1ED5DCCB4D0E29ECA7291B58C039F16`. `worker.zip` SHA-256 `8583B48DCE40F824612E95A4332CC028A6963215C87DE6AB6CE7C930358EE0D4`. Windows `efbundle.exe` SHA-256 `268827654DB60105967A90B9689FFB8F7D66D3CDAF701659A85C43BA5DA5719C`. |
| Review and verification | [PR CI](https://github.com/collisionengineers/pegasus/actions/runs/35866013283) passed every job at the PR head `13fe08f0c`, whose merge into `dev` is the release source. PR 820 had no formal review; the operator asked for the merge and release. [Main CI](https://github.com/collisionengineers/pegasus/actions/runs/35869887425) passed every job at the exact release SHA, including six SQL integration shards. The release build and the Local, Artifact, PreDeploy, PreMigration and PreProvision plan gates passed. |
| Schema and grants | Migration **additive**. `20260923180000_ValuationCardFiguresOptional` makes `CaseValuations.Mileage`, `RetailValue` and `TradeValue` nullable. It drops their non-negative checks and re-adds them unchanged. The bundle applied it at 14:03:43–14:03:49Z over `20260923120000_StaffAccountDeletionRuntimePermissions`. SQL read-back: 161 applied migrations at that head; the three columns are nullable with their `>= 0` checks. `CaseValuations` held 0 rows before and after. Bootstrap verified 718 catalogued permission/denial rows and 500 effective runtime DML rows at 14:04:02Z, unchanged from Release 59. There was no infrastructure, dependency or runtime-configuration change: `azd provision` found no changes at 14:04:39Z (pre-flight: B1 in uksouth limit 3). |
| Web deployment | OneDeploy `d2a9a7cc-18ae-4270-af1b-e1c0798f0907` succeeded at 14:05:01Z. The site started in 128 seconds. On the first attempt it read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source and version at 14:07:26Z. |
| Worker deployment | ZIP deployment completed successfully after trigger synchronization and the platform health check. The canonical Disabled-setting census passed as `approved-live-worker`. |
| Production smoke | Passed at 14:10Z. Active Web package `20260923140446.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-23T14:10:03Z`, and the active Graph subscription expires `2026-09-28T14:30:00Z`. Smoke is unauthenticated; the Case page itself awaits a signed-in look. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-60-729f4231`. The drivers, phase logs and migration-head read-backs are under `artifacts/releases/release-60-driver`. |

## Release 59 — 23 September 2026 (deployment live)

Release 59 deployed [PR 819](https://github.com/collisionengineers/pegasus/pull/819). That PR consolidated [PR 817](https://github.com/collisionengineers/pegasus/pull/817) (problem-report issue content and staff-account deletion, #810), [PR 818](https://github.com/collisionengineers/pegasus/pull/818) (the Repair Spec markup that moved Decisions into the side column, #816), [PR 813](https://github.com/collisionengineers/pegasus/pull/813) (verification optimisation) and the local `dev` work. The route was the approved normal route with one additive, grant-only migration. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `dab70182063251680835a1d1885769eec1f0bd12`, promoted atomically to both `dev` and `main` at 09:41Z. Manifest schema 3 SHA-256 `F54C3C82632EEBD1484CCE39003354E941EC810CA662E96C979697216C0BAC5B`. `web.zip` SHA-256 `2330FBD4AAA8C7EB0A772AC5EC5E08B27518B9A42E67C9B5318C9715E29B888B`. `worker.zip` SHA-256 `552FE3FE3563EFCF082CE0E1FEF70A5A50CFB5D975589E353DEAF81A98E302DE`. Windows `efbundle.exe` SHA-256 `03061B4D7DAF8309A78A7C8B856221B524978682FE3CFBF439A8B7ACBD8AC33B`. |
| Review and verification | [Candidate CI](https://github.com/collisionengineers/pegasus/actions/runs/35842161348) passed every job at the exact head, including six SQL integration shards and coverage. PRs 813, 817 and 818 had no formal review; the operator authorised promotion knowing that. The release build and the Local, Artifact, PreDeploy, PreMigration and PreProvision plan gates passed. |
| Schema and grants | Migration **additive** (grant only). `20260923120000_StaffAccountDeletionRuntimePermissions` revokes the Web runtime role's DELETE denial and grants DELETE on `AspNetUsers`, `UserExternalCredentials`, `GlassRepairEstimateSessions` and `StaffNotifications`. The bundle applied it at 09:54:22–09:54:28Z over `20260922225349_ReleaseNoteCreateIdentity`. SQL read-back: 160 applied migrations at that head, and all four tables `GRANT` for the Web role. As merged, PR 817 left the bootstrap census expecting the four denials, which would have failed post-migration verification after SQL; PR 819 extended it. Bootstrap verified 718 catalogued permission/denial rows and 500 effective runtime DML rows at 09:54:40Z (four more than Release 58). No infrastructure, dependency or runtime-configuration change; `azd provision` completed at 09:56:33Z (pre-flight: B1 in uksouth limit 3). |
| Web deployment | OneDeploy `6235bfee-7a8a-46fa-b942-6003678a2376` succeeded at 09:56:59Z. The site started in 158 seconds. On the first attempt it read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source and version at 09:59:54Z. |
| Worker deployment | ZIP deployment completed successfully after trigger synchronization and the platform health check. The canonical Disabled-setting census passed as `approved-live-worker`. |
| Production smoke | Passed at 10:03Z. Active Web package `20260923095645.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-23T10:00:03Z`, and the active Graph subscription expires `2026-09-28T14:30:00Z`. No live account deletion or problem-report issue was created. For those, CI is the behaviour evidence. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-59-dab70182`. The drivers, phase logs and migration-head read-backs are under `artifacts/releases/release-59-driver`. |

## Release 58 — 23 September 2026 (deployment live)

Release 58 closed [PR 808](https://github.com/collisionengineers/pegasus/pull/808)
after its seven review-comment threads were investigated and the validated
findings corrected. The operator-approved ordinary intake wipe ran first,
followed by the existing App Service destructive migration route. Web and
Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `c98c27238cd813cefa681b0bf7d290d8971fbe69`, promoted atomically to both `dev` and `main` at 07:13Z. Manifest schema 3 SHA-256 `AB49921D0965249CCF63C20B1735A6F53AC5806638E20B79DEF1A8A7CD2CB1F0`; `web.zip` SHA-256 `2BB594E335C4408626D65044F1073047FB9862B1B46BAD1FB7CCE6DF6AE449D1`; `worker.zip` SHA-256 `C1DFCDE0D9AF55A64A1BA968A7A0D34D6F040ECBA8CADD49840BDD53D428F2DF`; Windows `efbundle.exe` SHA-256 `07DD30E2ECA87216E1A0648F62925D853FDC1552A4340A2BAC9F5DFD77E6D4A7`. |
| Review and verification | The seven PR issue comments had 33 numbered findings plus documentation and addenda; validated fixes were planned in `artifacts/pr808/` and implemented at the reviewed head. [Candidate CI](https://github.com/collisionengineers/pegasus/actions/runs/35803385195) and [main CI](https://github.com/collisionengineers/pegasus/actions/runs/35830613185) passed every job, including six SQL integration shards and coverage. The release build and Local, Artifact, PreDeploy, PreMigration and PreProvision plan gates passed. |
| Intake wipe | At 07:01Z, with Worker `Stopped` and old Web `Stopped`/unserved (HTTP 403), the ordinary wipe cleared all 9 blobs (15,501,643 bytes) from `pegcustody252ow37gij/transient-intake` and the inventoried 151 rows across 91 non-preserved tables in SQL `pegasus` on `pegasus-prod-sql-252ow37gij`; the batch reported 152 rows affected including its cutoff write. Post-run checks found zero blobs and zero wiped tables still holding rows. The committed mail cutoff is `2026-09-23T07:01:47.1396180+00:00`; 534 preserved rows remain. `CaseSequences` 10, `ImageIntakeSequences` 7 rows, `TriageSequences` 2 and `UnidentifiedSequences` 1 were unchanged; `ValuationPresets` remained 0/0. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No `-ResetTestEstate` was used. |
| Schema and grants | Sixteen **destructive** migrations ran over `20260917153000_CaseClaimSourceContactOverride` after the old Web's exact Release 57 SHA was read back and the new Worker staged with all Functions disabled. Immediately before SQL, Worker and Web were `Stopped` and Web `/health/live` returned HTTP 403. The first destructive SQL statement was the forward-only recovery boundary; old bytes were not restarted afterward. The bundle applied through `20260922225349_ReleaseNoteCreateIdentity`; SQL read-back showed 159 applied migrations at that head. Runtime bootstrap verified 718 catalogued permission/denial rows and 496 effective runtime DML rows. |
| Web and Worker deployment | The approved new Worker ZIP staged successfully under disabled triggers and passed disabled smoke before and after staging. OneDeploy `38822221-6c07-405f-81a8-825ca0884134` succeeded at 07:20:25Z on the stopped Web App with restart and status tracking disabled. Disabled-Worker provisioning succeeded, followed by approved activation provisioning (`pegasus-prod-1790148232`, Succeeded at 07:24:54Z). The activation CLI session was interrupted; the Azure deployment read-back and approved-live-worker smoke established success before Web or Worker activation. Web then read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and exact source/version; Worker read back `Running` with every Disabled setting `false`. |
| Production smoke and scope | Full smoke passed around 07:38Z: active Web package `20260923072012.zip` had the approved Web ZIP SHA-256, the last completed inbound poll was `2026-09-23T07:37:34Z`, and the active Graph subscription expires `2026-09-28T14:30:00Z`. The wipe left no Case for a focused live Case-workflow check, so CI is the behavior evidence for those fixes. Problem reports target public `collisionengineers/pegasus` with only an opaque local report ID in the issue. The operator explicitly chose the existing versioned `github-problem-report-token` secret despite its broad classic `repo` scope, as a release-specific exception to ADR-0055's fine-grained-token guidance; the token value was not printed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-58-c98c2723`; wipe and approval observations are under ignored `artifacts/pr808/`. `docs/current-architecture.md` was updated in the released source for the structural changes. |

## Release 57 — 17 September 2026 (deployment live)

Release 57 deployed the remaining QDOS26010 intake and Case-record fixes (PRs 783, 784 and 786, with the
Release 56 record PR 785) through the approved normal route with two additive migrations. Web and Worker
are running, the deployed Web package matches the approved artifact, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and package | Version `0.1.0-alpha.1`, application source `7b197e1f3df852cf7b14f04a109c8ac083b15180`, promoted atomically to both `dev` and `main` at 18:40Z. Manifest schema 3 SHA-256 `B68747BA93E21AD5B7F51B8A5DC9B28BB40151454F14C0053377B7165DFBED50`; `web.zip` (linux-x64) SHA-256 `F91BA741283683D8167B72C3925E8C6857B28773DFDAD5843B9CE05D3FE5CB04`; `worker.zip` SHA-256 `A74EADAF00B197C3CEA32542D74514D2C680E06CC914C62AE4EAC6DAEB1BD114`; retained `efbundle.exe` (win-x64). |
| Review and verification | PRs 783, 784 and 786 each passed their six-shard CI at their merged head with `dev` merged in, after an independent Codex review (784 needed a post-review fix: the custody and Send to AI acceptance helpers now carry the reviewed draft's inspection date, and the corpus source snapshots follow the policy edits). The main-branch run at the promoted SHA ([35261618999](https://github.com/collisionengineers/pegasus/actions/runs/35261618999)) passed every job on its first attempt, including shard 4, whose grouped-intake race PR 783 fixed. Release build: zero errors; `Test-AzureDeploymentPlan.ps1` passed in Local, Artifact, PreDeploy, PreMigration and PreProvision modes. |
| Schema and configuration | Migrations `additive`: `20260917152000_CaseDueByStaffOverride` (`CaseDueWork.DueBySetByStaff bit NOT NULL DEFAULT 0`) and `20260917153000_CaseClaimSourceContactOverride` (three nullable override columns on `CaseDataSnapshots`), applied by the bundle at 19:16:57–19:17:04Z over `20260917150000_RemoveCaseSequenceCeiling`; head and all four columns read back. Bootstrap verified 708 catalogued permission rows and 490 effective runtime DML rows at 19:17:22Z (unchanged). No infrastructure, dependency or runtime-configuration change; `azd provision` found nothing to change (pre-flight: B1 in uksouth limit 3). |
| Web deployment | OneDeploy `9ad08144-53d7-4217-97a6-52c01badf3b1` succeeded at 19:18:38Z; the site started in 191 seconds and read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source/version at 19:22:07Z on the first attempt. |
| Worker deployment | ZIP deployment completed successfully at 19:24:44Z after trigger synchronization and the platform health check; the canonical Disabled-setting census passed as `approved-live-worker`. |
| Production smoke | The driver process was stopped by workstation memory pressure as the smoke step began (a peer session's test host held 7 GB), so the smoke was rerun on its own from the release worktree and passed at 19:26Z. Active Web package `20260917191823.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed with last completed poll `2026-09-17T19:25:03Z` and active subscription expiry `2026-09-20T13:10:00Z`. |
| Behaviour shipped | QDOS letters now yield the claimant address and contact number from the interleaved CLIENT DETAILS block, and a letter with no inspection date defaults the draft's inspection date (and so `Due by`) to the Europe/London date the instruction was received (PR 784, grammar `Version 10`). Staff can set `Due by` directly on the Case record, kept until cleared; the Case can override its Claim source contact name, telephone and e-mail per field, cleared when the source changes (PR 786). A grouped image intake member no longer strands as `needs_sorting` when its sibling's registration wins the race (PR 783); the pre-fix stranded state was checked on production and no row exists. `QDOS26010` itself keeps its manually entered values; the extraction changes apply to later intakes. |
| Evidence | `artifacts/releases/release-57-7b197e1f` retains the manifest, ZIPs, bundle and phase summary; the drivers and phase logs are under `artifacts/releases/release-57-driver`. |

## Release 56 — 17 September 2026 (deployment live)

Release 56 deployed the three post-QDOS26010 Case-record changes and the case-sequence ceiling removal
(PRs 779, 780, 781 and the Release 55 record PR 782) through the approved normal route with one
additive migration. Web and Worker are running, the deployed Web package matches the approved artifact,
and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and package | Version `0.1.0-alpha.1`, application source `22ef8b2516d0473143f90047c1f3e213b17f5394`, promoted atomically to both `dev` and `main` at 17:17Z. Manifest schema 3 SHA-256 `7D1BEB286FE33EEE2537535818CF632040909D35E3CD69874040EC3A9C26D9C8`; `web.zip` (linux-x64) SHA-256 `020D6FB9EEE4A07A35D03AA55A7DB5C894D5B08A7CD49D787A01A59A4FDDD62D`; `worker.zip` SHA-256 `D4FA9954D127B5B6549291E423DA9D5E2B71DD18A06E63B8FEA3CFB04F38BBDA`; retained `efbundle.exe` (win-x64). |
| Review and verification | PRs 779, 780 and 781 each passed their six-shard CI at their merged head with `dev` merged in, after an independent Codex review (PR 779 was reviewed and repaired by a dedicated review task before merge). The main-branch run at the promoted SHA ([35250212833](https://github.com/collisionengineers/pegasus/actions/runs/35250212833)) failed shard 4 on the load-sensitive `GroupedImageIntakeConcurrencyTests.ConcurrentGroupMembersNeverSplitAcrossRepeatedRuns` (fixed on `dev` by PR 783 after this promotion) and shard 2 on a SQL execution timeout in `VehicleLookupBackfillTests.AnExtractedFactIsNotDisplacedAndNotDuplicated`; both jobs were rerun. Release build: zero errors; `Test-AzureDeploymentPlan.ps1` passed in Local, Artifact, PreDeploy, PreMigration and PreProvision modes. |
| Schema and configuration | Migration `additive`: `20260917150000_RemoveCaseSequenceCeiling` re-creates `CK_CaseSequences_LastAllocatedSequence` as `>= 0` and `CK_Cases_Sequence` as `>= 1` (the `9999` ceiling is gone), applied by the bundle at 17:30:26–17:30:48Z over `20260917140000_GrantWorkerCaseAssessmentFields`; head and both constraint definitions read back. Bootstrap verified 708 catalogued permission rows and 490 effective runtime DML rows at 17:32:01Z (unchanged from Release 55). No infrastructure, dependency or runtime-configuration change; `azd provision` found nothing to change (pre-flight: B1 in uksouth limit 3). |
| Web deployment | OneDeploy `228f97fa-bd9d-4ec6-8c87-0b83d332852d` succeeded at 17:34:12Z; the site read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source/version at 17:36:30Z on the first attempt. |
| Worker deployment | ZIP deployment completed successfully at 17:39:17Z after trigger synchronization and the platform health check; the canonical Disabled-setting census passed as `approved-live-worker`. |
| Production smoke | Passed at 17:41:20Z. Active Web package `20260917173342.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed with last completed poll `2026-09-17T17:40:03Z` and active subscription expiry `2026-09-20T13:10:00Z`. |
| Behaviour shipped | Case references grow past four digits with no allocation ceiling, and wrong-Principal replacement allocates through the shared allocator (PR 779). The Vehicle section's odometer unit is an editable miles/kilometres select (PR 780). All five Engineer sections, including Valuation, are editable from `Not ready` and `Review` as well as `With Engineer`; adopting the Engineer's Value stays an Engineer act, and the "Available With Engineer" chip is gone (PR 781). Not yet shipped: QDOS claimant address and contact extraction with the received-date inspection default (PR 784), and the Due by and Claim source contact overrides (task/case-overview-edits). |
| Evidence | `artifacts/releases/release-56-22ef8b25` retains the manifest, ZIPs, bundle and phase summary; the drivers and phase logs are under `artifacts/releases/release-56-driver`. |

## Release 55 — 17 September 2026 (deployment live)

Release 55 is the hotfix for the vehicle-lookup regression Release 54 introduced: every automatic and
staff DVLA/MOT lookup on the Worker failed with `The SELECT permission was denied on the object
'CaseAssessmentFields'` because the lookup fill now reads the confirmed mileage source and writes the
derived Vehicle type while the Worker's least-privilege role had no grant on that table. Web and
Worker are running, the deployed Web package matches the approved artifact, and full production
smoke passed.

| Observation | Value |
| --- | --- |
| Source and package | Version `0.1.0-alpha.1`, application source `6cade87db86d1104240ada676d1bab5742858e21` (PR 778 plus an `AGENTS.md` update), promoted atomically to both `dev` and `main` at 14:03Z. Manifest schema 3 SHA-256 `4016AA9641DF50FD005061CF18E2ED0D75D93662662D054A946790D2D80BFCA8`; `web.zip` (linux-x64) SHA-256 `671A8C6AD506A877678C6BBD4F5911FE6DADDE7F1036CDE2249B3A39ED762D11`; `worker.zip` SHA-256 `401F08B3A57D6848550C7585CC0F78322322EAB498DE62298119067D97F4F5A3`; retained `efbundle.exe` (win-x64). |
| Review and verification | PR 778 passed its six-shard CI (shard 4 on its second attempt; the failing test was the load-sensitive `GroupedImageIntakeConcurrencyTests.ConcurrentGroupMembersNeverSplitAcrossRepeatedRuns` recorded under Release 54, unrelated to this change). Local: Release build, Integration `VehicleLookup|AzureSqlRuntimeRole|CaseWorkflowMigrationTests|CommittedMigration` (78 passed, including the new Worker-role lookup test), Architecture (121 passed), `Test-MigrationGrants.ps1`, `Test-AzureDeploymentPlan.ps1 -Mode Local`, documentation links. |
| Schema and configuration | Migration `additive` (grant only): `20260917140000_GrantWorkerCaseAssessmentFields` grants SELECT, INSERT and UPDATE on `CaseAssessmentFields` to `pegasus_worker_runtime_role` (DELETE stays denied), applied by the bundle at 14:20:01–14:20:14Z over `20260917014000_EstimateDocumentPreviewEvents`; head read back as `20260917140000_GrantWorkerCaseAssessmentFields` and the Worker role's effective grants read back as INSERT, SELECT, UPDATE. Bootstrap verified 708 catalogued permission rows and 490 effective runtime DML rows at 14:20:44Z (three more than Release 54). No infrastructure, dependency or runtime-configuration change; `azd provision` found nothing to change. |
| Web deployment | OneDeploy `9c6fe5fd-a38c-4b75-ace2-a62aa8bed513` succeeded at 14:22:42Z; the site started in 115 seconds and read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source/version at 14:25:16Z. |
| Worker deployment | ZIP deployment completed successfully at 14:27:59Z after trigger synchronization and the platform health check; the canonical Disabled-setting census passed as `approved-live-worker`. The Worker also now logs an external work failure with its durable id before the queue's retry policy takes over. |
| Production smoke | Passed at 14:29:46Z. Active Web package `20260917142226.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed with last completed poll `2026-09-17T14:25:00Z` and active subscription expiry `2026-09-20T13:10:00Z`. |
| Repair | The two dead lookup work items for `QDOS26010` (one `queue_poisoned`, one stuck `processing`) are not revived by the recovery timer or the automatic-lookup sweep; the repair is one staff press of **Look up DVLA & MOT** on the Case, which creates a new work item under the corrected grants. |
| Evidence | `artifacts/releases/release-55-6cade87d` retains the manifest, ZIPs, bundle and phase logs; drivers under `artifacts/releases/release-55-driver`. |

## Release 54 — 17 September 2026 (deployment live)

Release 54 deployed the sprint 1609 changes (PRs 764, 766/767, 769–776) through
the approved normal route with an additive migration. Web and Worker are
running, the deployed Web package matches the approved artifact, and full
production smoke passed. The operator-decided intake data wipe followed.

| Observation | Value |
| --- | --- |
| Source and package | Version `0.1.0-alpha.1`, application source `24b97fe660cfd8ae53a946d15a0b1f20db6f9158`, promoted atomically to both `dev` and `main` at 11:39Z. Manifest schema 3 SHA-256 `10EA309AD78674C0F8A23BAD5ED105F695B680FE8EB923287735CED14CC5A825`; `web.zip` (linux-x64) SHA-256 `8CBCA9E5D1A85F0053BA53CD2231A7709965F9212C227B303DBC76EF8943B75A`; `worker.zip` SHA-256 `9C45A593A1E79F9A80E4D998AB21A2491B8E637E2DCBA1DDA9EA3148ED168DE3`; retained `efbundle.exe` (win-x64). |
| Review and verification | Every included PR passed its six-shard CI at its merged head after `dev` was merged in; each was reviewed by an independent Codex review before push. The main-branch run at the promoted SHA ([35216598539](https://github.com/collisionengineers/pegasus/actions/runs/35216598539)) passed every job except shard 4, where `GroupedImageIntakeConcurrencyTests.ConcurrentGroupMembersNeverSplitAcrossRepeatedRuns` failed; the same test failed on two of three shard-4 attempts for PR 776 and passes alone locally, so it is recorded as load-sensitive under the six-shard partition and left open. Release build: zero errors; documentation links and placement passed. |
| Schema and configuration | Migration `additive`: `20260916090000_VehicleLookupTypeSignals` (three nullable columns on `VehicleLookupObservations`) and `20260917014000_EstimateDocumentPreviewEvents` (the `IX_CaseWorkflowEvents_CaseId_AfterVersion` filter now also excludes `case_estimate_document_previewed`) applied by the bundle at 11:42:53–11:43:00Z over `20260914150656_UploadedCorrespondenceMailbox`; head read back as `20260917014000_EstimateDocumentPreviewEvents`. Bootstrap verified 705 catalogued permission rows and 487 effective runtime DML rows at 11:43:16Z. New Worker setting `AutomaticEvaReviewSubmissionSchedule` (`0 * * * * *`) provisioned. No dependency, tier or publish-mode change. |
| Provision | Approved `azd provision -e pegasus-prod --no-prompt` exited zero at 11:45:38Z (1 minute 22 seconds). Pre-provision quota read: B1 in uksouth limit 3. |
| Web deployment | OneDeploy `8c1c6eec-4643-421d-b552-fad2cced3ff6` succeeded at 11:46:01Z. The first container start exited with code 134 after 77 seconds with no application telemetry; the platform restarted the container and the site started at 11:53:06Z after a 122-second warm-up, so the CLI's ten-minute start wait reported failure although the package was serving. Read-back: `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source/version. |
| Worker deployment | ZIP deployment completed successfully at 12:07:32Z after trigger synchronization and the platform health check; the canonical Disabled-setting census passed as `approved-live-worker`. |
| Production smoke | Passed at 12:08:06Z. Active Web package `20260917114546.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed with last completed poll `2026-09-17T12:05:16Z` and active subscription expiry `2026-09-20T13:10:00Z`. |
| Behaviour shipped | Six-shard SQL CI and the Case test split; first-use Case paths and thumbnail caching; matched email PDFs and photographs filed on existing Cases; estimate import dialogs and refusals; Inbox scope for resolved Unidentified items; Vehicle type auto-fill; audit quick fixes; report and fee-note freshness; estimate document PDF with specialist-hours fixes; UI guardrails skill; one `a.` Audit prefix with Audit Cases created without their original report (the missing report is an outstanding Case item cleared by Mark as original report). |
| Evidence | `artifacts/releases/release-54-24b97fe6` retains the manifest, ZIPs, bundle and the phase logs; the drivers are under `artifacts/releases/release-54-driver`. |

- Intake data wipe, 17 September 2026 (operator decision of 16 September: the
  Audit prefix change ships without a data migration because the estate is
  test data): Worker `pegasus-prod-worker-252ow37gij` stopped at 12:08:39Z for
  the maintenance window, then resumed and read back `Running` at 12:09:23Z.
  Every blob in `pegcustody252ow37gij/transient-intake` was cleared (zero
  remaining) and 446 rows deleted from 91 non-preserved tables in `pegasus`
  (447 affected rows reported). The committed mail cutoff is
  `2026-09-17T12:09:05.3052004+00:00`; 519 preserved rows remain.
  `CaseSequences` (9), `ImageIntakeSequences` (7), `TriageSequences` (2) and
  `UnidentifiedSequences` (1) were unchanged; `ValuationPresets` remained 0/0.
  `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box
  were untouched. Post-run verification reported zero blobs remaining and zero
  wiped tables holding rows. Full production smoke passed again at 12:16Z after
  the first post-wipe inbound poll (`2026-09-17T12:15:03Z`).

## Release 53 — 15 September 2026 (deployment live)

Release 53 deployed the reviewed performance and Case-read changes through the
approved normal route. Web and Worker are running, the deployed Web package
matches the approved artifact, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and package | Version `0.1.0-alpha.1`, application source `e8efb19779baadc5ea46bd9c62e6c9c54740cac7`, promoted to both `dev` and `main` through [PR 761](https://github.com/collisionengineers/pegasus/pull/761). Manifest schema 3 SHA-256 `D1327BCBB11B3E386DEBA5590696B3386E16F2AB43634DC1165B12B740927CAF`; `web.zip` (linux-x64) SHA-256 `A7E081A4373FFFD7ADB87B09D912F9D70BD9AD173359C9A1752A2B0FC6C10463`; `worker.zip` SHA-256 `267CFA412A546842CD92FAF82CFEEE220288FA887D9724CD8158DD50CC1DD425`; retained `efbundle.exe` (win-x64). |
| Review and verification | Independent review findings were remediated, including Report image controls while Files is deferred. [Candidate CI](https://github.com/collisionengineers/pegasus/actions/runs/35010425465) passed: 4,619 passed, 17 skipped, zero failed; all 2,306 Integration tests were enumerated exactly once. [Main CI](https://github.com/collisionengineers/pegasus/actions/runs/35014174698) also passed. Targeted rerun: 50/50 rows across 45 methods. Release build: zero warnings/errors; documentation links and placement passed. |
| Schema and configuration | Migration unchanged at `20260914150656_UploadedCorrespondenceMailbox`, confirmed by read-only SQL at 21:04Z. No migration/bootstrap, intake wipe or data reset was executed. UK South B1 Web plan remains capacity 1; Worker remains Flex Consumption; SQL remains S0. No dependency, infrastructure tier or publish-mode change. |
| Provision | Approved `azd provision -e pegasus-prod --no-prompt` exited zero at 21:05:05Z and found no changes to provision. Preflight matched the approved resource inventory and activation settings. |
| Web deployment | OneDeploy `09b48b82-6bb4-4332-9c2d-c10ce4af0e9f` succeeded at 21:06:42Z. CLI startup completed after 158 seconds. Subsequent read-back returned `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source/version. |
| Worker deployment | ZIP deployment `ecf3095f-bcbc-4885-9c4d-628f86e135b3` completed successfully at 21:12:51Z after trigger synchronization and the platform health check. Worker read back `Running`; the canonical Disabled-setting census passed as `approved-live-worker`. |
| Production smoke | Passed at 21:13:55Z. Active Web package `20260915210628.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed with last completed poll `2026-09-15T21:10:00Z` and active subscription expiry `2026-09-20T13:10:00Z`. Health, source/version, authentication redirect, CSP and Graph validation checks passed. |
| Focused browser check | The live sign-in page loaded correctly. The operator explicitly skipped the authenticated Work Centre refresh and Case section/image check on 15 September 2026; those live checks were not performed and are not claimed as passed. Exact-source offline browser evidence already covers deferred Files, unsaved edits, image preparation and cancellation. |
| Behaviour shipped | Work Centre automatic refresh applies a bounded fragment and preserves truthful stale/partial state. Case sections use focused reads; Report and Files share image identities; Operations counts extend beyond the display cap. Case-only assets, one maintained Case controller and sampled document-phase telemetry are included. Local measurements are not production latency, capacity or monetary-saving claims. |
| Evidence | `artifacts/releases/release-53-e8efb197` retains the manifest, ZIPs, bundle and release evidence. `artifacts/performance/operator-20260915` retains the approval, preflight, deployment and smoke logs; the [PR review record](https://github.com/collisionengineers/pegasus/pull/761#issuecomment-5686605997) links implementation and verification evidence. |

## Intake data wipe — 16 September 2026

- Approved intake wipe: Worker `pegasus-prod-worker-252ow37gij` stopped for the maintenance window, then resumed and read back `Running`; 123 blobs (183,300,964 bytes) cleared from `pegcustody252ow37gij/transient-intake` and 959 rows deleted from 91 non-preserved tables in `pegasus`. The committed mail cutoff is `2026-09-16T09:22:24.7526521+00:00`; 38 effective tables and 512 preserved rows remain. `CaseSequences` (7), `ImageIntakeSequences` (7), `TriageSequences` (2), and `UnidentifiedSequences` (1) were unchanged; `ValuationPresets` remained 0/0. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook, and Box were untouched. Post-run verification reported zero blobs remaining and zero wiped tables holding rows.

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
