# Operations

This is the last recorded deployed-state and support summary. It is not a fresh
cloud observation. Exact source structure belongs in [architecture](current-architecture.md);
procedures are reached through [the runbook](runbook.md).

## Release 70 — 26 September 2026 (deployment live)

Release 70 deployed [PR 880](https://github.com/collisionengineers/pegasus/pull/880): **Add evidence** on a Case or Triage Case page opens Upload for that Case with the destination declared before the upload, so processing links the files there in the uploader's name, files them straight into the Case's Box folder, runs no identification and makes no Unidentified item, and the operator returns to the Case's Files panel; a staff link (Upload confirm, Link to Case on an Unidentified item, the Inbox link) now files the linked material on the Case and resolves its Unidentified item in the same request; and the upload review shows the photographs pulled out of a PDF. The route was the normal App Service route with one additive migration, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. The intake test estate was wiped immediately before the release (below).

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `6e7235a0db42b4dc9a2b9a0e741c6b737a9f82c5`, promoted atomically to both `dev` and `main` at 16:21:23Z (main was `927482b63`) before the build. Manifest schema 3 SHA-256 `1CA231B7784302F5220AC21987D03907D15DB175D05FBA41885B24B9E67C2AEE`. `web.zip` SHA-256 `578236C39EE944FE04FA44E7AEA624AF6A07BDC8200F7123529D2C0FBFD0E3B5`. `worker.zip` SHA-256 `112B7FF2D4E494E164ED86DAE3BDE25BC6F64B4DF8259B9C2914F30D4C94950F`. Windows `efbundle.exe` SHA-256 `93A478E64CF7C4B72D7BF7321AEABC3E1D2C945975CEE35006079B4085E4CBA1`, built 16:21:44–16:26:23Z and run. |
| Review and verification | PR 880's CI run 36253516429 passed all 11 jobs at its merged head `2d3cc9a0e`. Two earlier runs found what the focused local set had not: the migration first shared the deployed head's timestamp (`20260926090000`) and sorted before it, which the manifest identity would have read as unchanged, so it was renamed `20260926150000_DeclaredUploadDestination` and the two pinned migration lists gained it; and three sweep-recovery tests expected the Worker sweep to resolve a staff-linked item, which the link itself now does, so they assert the link's own resolution and a `(1, 0, 0, 0)` recheck sweep. The `main` push run 36255161301 passed at the release SHA. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. No browser walk ran before release: local development has no Case data. The operator approved the ordinary wipe, granted merge authority for `6e7235a0d`, and approved the exact manifest and targets, proceeding without waiting for the `main` run (26 September 2026). |
| Intake wipe | Ordinary wipe (no test-estate reset) between the promotion and the release. Worker `pegasus-prod-worker-252ow37gij` was stopped at 16:20:37Z and read back `Stopped` at 16:20:51Z. Fresh dry run: 53 blobs (59,324,158 bytes) in `pegcustody252ow37gij/transient-intake`; 129 tables, preserve list 36/36 found (37 effective with `ApprovedMailbox*`), 92 tables to wipe holding 648 rows. Executed at 16:20:55Z: blobs remaining 0; one SQL transaction reported 649 rows affected; wiped tables still holding rows 0; preserved rows after 606; `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` unchanged at 19/9/1 (`TriageSequences` no longer exists); `ValuationPresets` 0/0. Committed mail cutoff `2026-09-26T16:21:02.9380050Z`; mailbox approval and activation times unchanged. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box untouched. The Worker was started at 16:21:38Z and read back `Running` at 16:21:44Z. The wiped estate included U53 and its five Cases from the 25–26 September test round. |
| Schema and grants | Migration **additive**. `20260926150000_DeclaredUploadDestination` adds nullable `DeclaredCaseId` to `IntakeStagedReceipts` and `IntakeReceipts`, each indexed, no foreign key, over `20260926090000_RepairSpecInUseOnCreate`. The bundle applied it 16:35:01–16:35:09Z. Bootstrap verified 707 catalogued permission/denial rows and 493 effective runtime DML rows at 16:35:26Z, unchanged from Release 69. SQL read-back at 16:35:31Z: 169 applied migrations at the manifest identity, and both columns present. |
| Web and Worker deployment | Activation set at 16:36:27Z; `PreProvision` passed (B1 in uksouth limit 3). `azd provision` (deployment `pegasus-prod-1790440616`) updated every resource idempotently in 1 minute 23 seconds, 16:36:47–16:38:10Z. `az webapp deploy` started at 16:38:12Z; OneDeploy `83a3a1ac-1bf4-43d4-9711-dd0560dade64` succeeded at 16:38:46Z with restart onto package `20260926163824.zip`. The first container start crashed at 16:40:10Z with an unhandled `TaskCanceledException` from an HttpClient call during startup (exit code 134 after 62.7 s; the site had recorded the same failure kind before Release 69's start), the platform stopped and recreated the container at 16:41:52Z, the warm-up probe passed at 16:43:28Z and the site started at 16:43:30Z. The CLI's deployment tracker had already marked the deployment failed and reported "site failed to start within 10 mins" at 612 s, so the driver stopped; a direct read-back at 16:49:45Z showed the site `Running` on `DOTNETCORE\|10.0`, `/health/live` and `/health/ready` 200 and `/diagnostics/version` reporting the exact release. The route resumed at 16:51:34Z after that read-back: Worker `config-zip` deployment `60aa3118-c66b-4d4e-b806-b9197db370b1` succeeded at 16:54:21Z. Web outage: 16:38:46–16:43:30Z (4 minutes 44 seconds). The Worker was not stopped for the release. |
| Production smoke | Passed at 16:55:05Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20260926163824.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-26T16:55:03Z`; the active Graph subscription expires `2026-10-02T15:15:00Z`. The release ran no signed-in journey check, and the wiped estate holds no Case yet. The live walk is with the operator once a Case exists: Add evidence on it with `artifacts/scott-williamson.pdf` (the PDF and its photographs appear under Files within seconds, no Unidentified item); then the same PDF through `/Upload`, Find a Case, confirm (the files on the Case and the item Resolved without waiting for the sweep). |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-70-6e7235a0`; driver scripts, phase logs, the wipe log and the Web App's startup logs at `artifacts/releases/release-70-driver`. |

## Release 69 — 26 September 2026 (deployment live)

Release 69 deployed the merge of [PR 875](https://github.com/collisionengineers/pegasus/pull/875) (the Glass's credential is a dialog on Accounts deep-linked with `?glassStaffId=`, the standalone blank-prone page is retired, and both Accounts dialogs resolve their account directly so an account beyond the first list page opens; #872), [PR 877](https://github.com/collisionengineers/pegasus/pull/877) (a repair spec a staff member types in, imports or brings back from Glass's is in use at once; imports take the one enabled labour-rate card; a same-file replay reports the file as already imported and leaves the spec in use unchanged; Accepted/Superseded, the frozen calculation basis and line Status are removed) and [PR 878](https://github.com/collisionengineers/pegasus/pull/878) (Import as repair spec from a Case Files row; a drop reuses a matching confirmed file; a Glass's return lands on a free Case under a fresh lease, and a landing that fails is logged and reports the session rather than an error page). The route was the existing App Service destructive migration route, run from the Linux workstation. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `775a076c27facc1adabb8ce2fb3872c61709334f`, promoted atomically to both `dev` and `main` at 12:19:51Z before the build. Manifest schema 3 SHA-256 `B9BBC773F588A1F0C1C3D753543B372216066B339204E3FA3316FE440912E208`. `web.zip` SHA-256 `0281FDB869617E2135B27D7CE9474077202F98B5537E41A2EFE004278991CF3F`. `worker.zip` SHA-256 `E9FFA59E481BAE6E24C594DCFB5936F8E0E4ECA274BC65FC61B4415817CAAF42`. Linux `efbundle` SHA-256 `22A2633930C5FDA60A8A08E035691B25E3E3BFD9A78910D79F10447F33D1441E`, built and run. |
| Review and verification | Each PR carried an independent review whose findings were fixed before merge: PR 875's first-page-only deep link (both dialogs now resolve their account directly; a regression seeds 101 accounts), PR 877's replay success message (factual now, with an A → B → replay-A regression; the Glass's outcome names the estimate as recorded), and PR 878's Glass callback (the Case read sits inside the landing's try, a landing failure is logged at Warning, and the Documents row's hidden fields share one local function). CI runs 36238469164, 36238149996 and 36238266809 passed all jobs at the merged heads `b9aa6fe8c`, `10b47923f` and `fc16bc52d`; each PR's first run failed one test-only issue, fixed at the cause (an HTML-encoded plus sign, and two Glass sentences that had become prefix and extension). The `main` push run 36241573420 passed all 11 jobs at the release SHA before containment. The Local, Artifact, PreDeploy, PreMigration and both PreProvision gates passed. No browser walk ran before release. The operator granted merge authority, the exact manifest and targets, and an outage window starting once the `main` run was green (26 September 2026). |
| Environment | The workstation's `.azure/pegasus-prod` had not been used since Release 39 and lacked `GLASS_MARKET_VALUE_ASSESSOR_BASE_URI`, `GLASS_ESTIMATOR_BASE_URI`, `GLASS_REPAIR_PROFILE_ID`, `GITHUB_PROBLEM_REPORT_TOKEN_SECRET_URI` and `GITHUB_PROBLEM_REPORT_REPOSITORY`. They were set from the deployed Web App settings (read-only), every other secret URI matched the live apps, and a read-only `azd provision --preview` showed no application-setting change before the release. azd 1.28.0 (the Doctor pin) was installed outside the repository for the run. Two strict-mode faults in the release driver (a `.Count` on empty `git status` output, and a null-conditional in a log line) were fixed mid-run; neither touched Azure state, and the second was resumed after the Web App start it followed. |
| Containment | At 12:23:57Z the old Web reported the approved old SHA `dc56dd95c51e076f573c98f2a4badff6010f1cc9`. The 7-setting Worker census was disabled and smoked from 12:40:03Z, and the new Worker package was staged (deployment `b303f2dc-21ea-4a4a-9140-b0681bac7c05`) at 12:42:41Z with the disabled smoke passing again. The Worker read back `Stopped` at 12:42:50Z. At 12:43:30Z Web read back `Stopped` and `/health/live` was unserved; an explicit probe returned HTTP 403 at 12:43:47Z, and the fresh read-back immediately before SQL passed at 12:43:50Z. |
| Schema and grants | Destructive, forward-only `20260926090000_RepairSpecInUseOnCreate` over `20260925190000_EditLeaseTakeoverHistoryEvents`: Accepted and Superseded rows become Draft, the unused LegacyUnresolved and ApprovedAiProposal routes become Manual, 14 acceptance, supersession and calculation columns (with `VatOverrideReason`) are dropped from `CaseRepairSpecifications` with their check constraints, and `Status` and `CurrentValuesJson` are dropped from `CaseEstimateLines`. Read before SQL: one repair spec (Draft, Glasses, not Current) and 26 provisional estimate lines on 5 Cases, so no stored value changed meaning. The first `DropCheckConstraint` was the forward-only recovery boundary. The bundle ran 12:43:56–12:44:03Z. Bootstrap verified 707 catalogued permission/denial rows and 493 effective runtime DML rows, unchanged from Release 68. SQL read-back at 12:44:13Z: 168 applied migrations at the manifest identity, the one spec still Draft/Glasses/not Current with its 26 lines, and the dropped columns gone. |
| Web and Worker deployment | The new `web.zip` was deployed to the stopped Web App (OneDeploy `587ddc93-5d96-4c7c-b474-1629301fe0d0`, 12:44:41Z) and the site stayed `Stopped`. Provision with the Worker disabled (1 minute 26 seconds) and its smoke passed at 12:46:26Z. The activation provision took 1 minute 16 seconds; the Web App was started at 12:48:06Z and served the exact SHA on the third probe at 12:50:15Z. The Worker read back `Stopped` after the activation provision, was started, and read back `Running` at 12:50:18Z. Outage: Worker 12:42:50–12:50:18Z (7 minutes 28 seconds), Web 12:43:30–12:50:15Z (6 minutes 45 seconds). |
| Production smoke | Passed at 12:50:38Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20260926124427.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-26T12:40:03Z`; the active Graph subscription expires `2026-09-28T14:30:00Z`. The release ran no signed-in journey check. The live walk is with the operator: drop an Audatex file in Case edit and see it in use at £80; a Glass's Save & Exit landing in use; Case Files → Import as repair spec; Accounts → Manage login and its Back to account. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-69-775a076c`; driver scripts, phase transcripts and `state.json` at `artifacts/releases/release-69-driver`. |

## Release 68 — 25 September 2026 (deployment live)

Release 68 deployed [PR 876](https://github.com/collisionengineers/pegasus/pull/876), which fixes Case edit mode after the QDOS26019 report:

- **Take over works.** Taking over a colleague's Case lease records its history line without colliding with the Case's own version event. Production had never recorded a successful takeover before this release.
- **The holder resumes.** A staff member who returns to a Case they are still editing resumes their own lease instead of being offered Take over. One-off claims made elsewhere stay fail-closed.
- **Leaving frees the Case.** Leaving a Case by a link releases its lease, after the existing unsaved-changes question.
- **Record scopes.** Triage and Image Intake Edit replace the viewer's own scope without a takeover.

The route was the approved normal route with one additive migration. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `dc56dd95c51e076f573c98f2a4badff6010f1cc9`, promoted atomically to both `dev` and `main` before the build. Manifest schema 3 SHA-256 `AAB98EDA74E1386A3A958D4968194A93100EE2C53D68AD82BB429F9A70387CCA`. `web.zip` SHA-256 `E56DD7CF54C8542374C0DA2FD2801BFD12CD55043E7DC554F966CD56A22AF969`. `worker.zip` SHA-256 `B8E1DAA3D3301F54C7D84022FF7F578F27C1A45B7497F2B82A0FF67FD8527336`. Windows `efbundle.exe` SHA-256 `B56373105572ED166E6F8D33908CFDEDC0C21E9BFAC18E48CE08403D7670D0C2`, built and run. |
| Review and verification | PR 876's CI run 36181532548 passed all 11 jobs at its merged head `656568cfe`. The first run failed five tests, fixed at the cause: two migration-list tests, two refusal tests that now hold the lease their scenario needs, and one read test that now ends its earlier edit session. An independent review found no blockers. Its two major findings were fixed before merge: one-off claim routes could take and end the holder's edit session, and the Automation Actor could resume a lease. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. No browser walk ran before release: local development has no Case data, and no automated harness covers the Case page JavaScript. The operator approved the release on green CI (25 September 2026). |
| Schema and grants | Migration **additive**. `20260925190000_EditLeaseTakeoverHistoryEvents` re-creates `IX_CaseWorkflowEvents_CaseId_AfterVersion` with `edit_lease_taken_over` excluded from its filter, the same shape as Release 54's `20260917014000_EstimateDocumentPreviewEvents`. The bundle applied it at 20:16:10–20:16:18Z over `20260925150000_RemovePerFieldConfirmation`. SQL read-back: 167 applied migrations at that head, and the index filter carries the new exclusion. Bootstrap verified 707 catalogued permission/denial rows and 493 effective runtime DML rows at 20:16:32Z, unchanged from Release 67. |
| Web and Worker deployment | `azd provision` found no changes (pre-flight: B1 in uksouth limit 3). OneDeploy `38051666-b3f0-4ab3-936f-8aa1756ad565` succeeded at 20:19:08Z with restart. The site started in 164 s and read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source and version on the first probe at 20:22:13Z. Worker `config-zip` deployment `c2c3d337-acf5-4f79-89cb-6dd1d3eddd5b` succeeded. |
| Production smoke | Passed at 20:25:58Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20260925201757.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-25T20:25:03Z`; the active Graph subscription expires `2026-09-28T14:30:00Z`. The release ran no signed-in journey check. The live walk is with the operator: leave a Case and return, leave with unsaved changes, and a colleague takes over. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-68-dc56dd95`; driver scripts and logs at `artifacts/releases/release-68-driver`. The QDOS26019 diagnosis is recorded on PR 876: Application Insights shows duplicate-key exceptions from 18:11:25Z to 18:11:34Z. |

## Release 67 — 25 September 2026 (deployment live)

Release 67 deployed the merge of [PR 869](https://github.com/collisionengineers/pegasus/pull/869) (the Upload aside beside the picker removed), [PR 871](https://github.com/collisionengineers/pegasus/pull/871) (Glass Resume validated, and Case edits preserved through the popup return) and [PR 873](https://github.com/collisionengineers/pegasus/pull/873) (per-field review removed from the assessment record, #837). The route was the existing App Service destructive migration route. Web and Worker came up Running on the approved release, and full production smoke passed. The session that ran the release ended before it wrote this record, so it was written afterwards, during Release 68, from the retained driver logs.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `d74de3ee2a5f387326f836a50b82c6df0765af02`. The combining PR's CI run 36165700943 and the `main` push run 36169619594 both succeeded at that SHA. Manifest schema 3 SHA-256 `99410E6ADB9673C5CDBFCBBDAF85E1F40014DFFF9025D58B841B5FFC6715322F`. `web.zip` SHA-256 `E8A1C0BDD3E9BAD9275ADEF60EF933FDC4AE62901A2286CCBEA01B152E33863B`. `worker.zip` SHA-256 `8AF1763E1DAA2041324E94755AB52B0CADF42EC00392B4063ECB2A7260BE452F`. Windows `efbundle.exe` SHA-256 `EDEEAB6D6E1F90845548AB8CE97B55A8B2E8A27EB7F7596242DCE0C2EEA2D186`. |
| Containment | At 17:50:46Z the old Web reported the approved old SHA `f8e54e11627cc2b2fff18079091fd9e226269c3b`. The 7-setting Worker census was disabled, and the new Worker package was staged at 17:51:10Z with disabled smoke. The Worker read back `Stopped` at 17:54:21Z. At 17:54:39Z Web read back `Stopped` and `/health/live` returned HTTP 403. |
| Schema and grants | Destructive, forward-only `20260925150000_RemovePerFieldConfirmation` over `20260925120000_RetireInstructionDate`. The bundle ran 17:55:16–17:55:26Z. Bootstrap verified 707 catalogued permission/denial rows and 493 effective runtime DML rows. The head read back as the manifest identity at 17:55:51Z. |
| Web and Worker deployment | The new `web.zip` was deployed to the stopped Web App (OneDeploy `84e34894-aa88-4785-93dc-77c542458584`, 17:56:54Z). The Web App stayed `Stopped` until 17:57:05Z. Provision with the Worker disabled, and its smoke, passed at 17:59:09Z. After the activation provision, the Web App started at 18:01:19Z and served the exact SHA at 18:03:37Z. The Worker read back `Running` at 18:03:45Z. |
| Production smoke | Passed at 18:04:43Z. Active Web package `20260925175632.zip` equals the approved `web.zip`. Intake liveness: last completed poll `2026-09-25T18:04:06Z`. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-67-d74de3ee`; driver scripts and phase logs at `artifacts/releases/release-67-driver`. |

## Release 66 — 25 September 2026 (deployment live)

Release 66 deployed [PR 867](https://github.com/collisionengineers/pegasus/pull/867): [PR 858](https://github.com/collisionengineers/pegasus/pull/858) (unused `IsEditable`/`IsTriage` members removed), [PR 862](https://github.com/collisionengineers/pegasus/pull/862) (Reports period filter reads From/To), [PR 856](https://github.com/collisionengineers/pegasus/pull/856) (legacy localStorage seed removed), [PR 863](https://github.com/collisionengineers/pegasus/pull/863) (an Audit's Original report cells fill from the filed report at acceptance and at Mark), [PR 853](https://github.com/collisionengineers/pegasus/pull/853) (dead code, orphaned UI and retired tooling removed; CI routing fixes), [PR 866](https://github.com/collisionengineers/pegasus/pull/866) (the receipt asset read and two test fixtures that #853 removed and #863 uses) and [PR 859](https://github.com/collisionengineers/pegasus/pull/859) (sign-in B, Work Centre B and Upload E, v30 Stage 2). The route was the approved normal route with the migration identity unchanged. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `f8e54e11627cc2b2fff18079091fd9e226269c3b`, promoted atomically to both `dev` and `main` at 13:40Z. Manifest schema 3 SHA-256 `365249FFC4DE4190611C6BC0C16A33461E68618EEF36BDFAA0FFE016D82CFF76`. `web.zip` SHA-256 `7A04DD38F94501D077285053EA5FD4599315A28BAD30F4C477F2A742ED9C55E2`. `worker.zip` SHA-256 `38A945152B8C4113AC63760E58218837AE8F5C143FDD6591CC55D321680B85B9`. Windows `efbundle.exe` built and not run. |
| Review and verification | Every task PR passed its required CI at its merged head and automated security review found no findings; PRs 856, 858, 862 and 863 also carry a full code review. PR 859's shard-6 failure was a test still asserting the pre-v30 Upload heading; PR 863's was a one-in-a-thousand duplicate Principal code in a test seeding helper; both were fixed at the cause. After PRs 863 and 853 merged, `dev` did not compile (each PR's merge ref lacked the other); PR 866 restored the port member and fixtures. The merged tip built clean in Release and passed PR 859's Playwright walks (63/63 and 53/53 checks) and a guardrail walk of PR 853's outstanding surfaces (72/72 checks, no console errors, no horizontal spill at 1580 and 760). The `Invoke-LocalDevelopment` Offline cycle could not complete on a pre-existing Worker-launch race in the script ([issue 868](https://github.com/collisionengineers/pegasus/issues/868)). PR 867's own run was green; the Local, Artifact, PreDeploy and PreProvision gates passed. The operator granted merge and deployment approval for the fixed production targets in the task statement (25 September 2026). |
| Schema and grants | Migration identity unchanged at `20260925120000_RetireInstructionDate`; no migration, bootstrap or grant step ran. |
| Web and Worker deployment | `azd provision` found no changes. OneDeploy `ed8c7f0f-31d0-41af-84f2-2ee538a549e3` succeeded at 13:47:33Z with restart; the site started in 126 s and read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source and version on the first probe at 13:49:53Z. Worker `config-zip` deployment succeeded at 13:52Z; the Worker read back `Running` with every Disabled value `false`. |
| Production smoke | Passed at 13:53Z. Active Web package `20260925134718.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-25T13:50:03Z`; the active Graph subscription expires `2026-09-28T14:30:00Z`. No signed-in journey check was run on production; CI, the Playwright walks and the guardrail walk at the tip are the behaviour evidence. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-66-f8e54e11`; driver and logs at `artifacts/releases/release-66-driver`; walk records under the `upload-flow-five-designs` worktree's `artifacts/ui-baseline-review/`. |

## Release 65 — 25 September 2026 (deployment live)

Release 65 deployed PRs 852, 854 and 855. Report fields now have one owning input path, vehicle lookup facts follow the Case's current registration, and the obsolete instruction-date field is retired in favour of the Case received date. The operator-approved ordinary intake wipe ran first, followed by the existing App Service destructive migration route. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `32dabfc59a2e13b8556c1ecfaa0590617422e8df`, promoted atomically to both `dev` and `main`. Manifest schema 3 SHA-256 `128A5AC50C2BB7FF64A4742576A882771D86C1746ECF30DBCA8E67ED6123E1B0`. `web.zip` SHA-256 `EB1DDC2385029C49D33DC69D0BF40E091AFEDD7D3A137ED1010E5C643061E2E8`. `worker.zip` SHA-256 `A47A4554448F16634E6F6A6447C643073F1EBB5C741493B8C87833E9FF808ACA`. Windows `efbundle.exe` SHA-256 `F177E96F469B7D51F585D4796F0152DC830544FFCCA1C1BCFDFFE34801CD78FE` (run). |
| Review and verification | PRs 852, 854 and 855 passed their required CI and automated security review found no findings. The operator granted wipe, stop, merge and deployment approval for the fixed production targets and immediate window. The release build and Local, Artifact, PreDeploy, PreMigration and both PreProvision gates passed. |
| Intake wipe | At 08:05Z, with Worker `Stopped`, the ordinary wipe cleared 0 blobs from `pegcustody252ow37gij/transient-intake` and 25 inventoried rows across 93 non-preserved tables in SQL `pegasus` on `pegasus-prod-sql-252ow37gij`; the batch reported 26 effects including its cutoff write. Post-run checks found zero blobs and zero wiped tables still holding rows. The committed mail cutoff is `2026-09-25T08:05:37.7098509+00:00`; 590 preserved rows remain. `CaseSequences` 14, `ImageIntakeSequences` 9 rows and `UnidentifiedSequences` 1 were unchanged; `ValuationPresets` remained 0/0. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No `-ResetTestEstate` was used. |
| Containment | The canonical Worker Disabled census was set `true` and smoked, the approved `worker.zip` was staged disabled and smoked again, then Worker and Web both read back `Stopped`; `/health/live` was unserved. The immediate pre-SQL read-back repeated all three checks. The old Web reported the approved source `773a9787eab8b7caa11dab4a4a25bf0063b0dec5`. |
| Schema and grants | Migration identity changed from `20260924180000_CaseWorksAndTriageCases` to `20260925120000_RetireInstructionDate`. The bundle applied additive `20260925090000_VehicleLookupDerivedFacts`, then destructive, forward-only `20260925120000_RetireInstructionDate`, which removed `instruction_date` Case data and dropped `InstructionDrafts.InstructionDate`. Bootstrap verified 715 catalogued permission/denial rows and 499 effective runtime DML rows. The live head read back exactly as the manifest identity. |
| Web and Worker deployment | OneDeploy `63de4ec2-888f-45da-b28e-6f70718e69a4` succeeded against the stopped Web App with restart and status tracking disabled; the site remained `Stopped`. Disabled-Worker provisioning and explicit compatible activation provisioning both succeeded. Web then read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and exact source/version. Worker read back `Running` with every Disabled value `false`. |
| Production smoke | Passed after activation. Active Web package `20260925081308.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-25T08:20:33Z`, and the active Graph subscription expires `2026-09-28T14:30:00Z`. The wipe left no Case for a focused signed-in journey check, so CI is the behaviour evidence for the changed report-field and instruction-date journeys. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-65-32dabfc5`. |

## Release 64 — 24 September 2026 (deployment live)

Release 64 deployed [PR 836](https://github.com/collisionengineers/pegasus/pull/836), the Case referencing rework: an Inspection + Audit Case keeps its Audit as a second work on the same Case, the Audit report carries the `a.` reference, and Triage is a Case type with a `t.` Case/PO. It also deployed [PR 838](https://github.com/collisionengineers/pegasus/pull/838) (one refresh button), [PR 827](https://github.com/collisionengineers/pegasus/pull/827) (Correspondence popup Reply, Reply all and Forward), [PR 828](https://github.com/collisionengineers/pegasus/pull/828) (forced password change asks only for the new password) and [PR 824](https://github.com/collisionengineers/pegasus/pull/824) (one Save for the Case page). The route was the approved destructive route: the old Web and Worker were stopped and read back before SQL, and the release was activated explicitly afterwards. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `773a9787eab8b7caa11dab4a4a25bf0063b0dec5` (the PR 836 merge), promoted atomically to both `dev` and `main` at 18:12Z. Manifest schema 3 SHA-256 `6837E9A8F3DCE5CC77AB576A4A2D29666ABF1741A5CD342AC24FB88C7D754B5B`. `web.zip` SHA-256 `6F46D47B52A4A0C64070A145F7820F1306CA9C98A5242E145AF555E11F52B3FE`. `worker.zip` SHA-256 `C696490DF88B00385F4D524A8BB09F692115843EFF65DE372B01DDA216DD7E47`. Windows `efbundle.exe` SHA-256 `B213398E4245804379C859EABE1E2FE14CCF3670B66C031C43F0D3671398A3AD` (run). |
| Review and verification | PR 836 needed three CI rounds for test fixtures after its merge with `dev`; [its final PR run](https://github.com/collisionengineers/pegasus/actions/runs/36033430053) passed every job at `9e7f9c0bb`. [PR 841](https://github.com/collisionengineers/pegasus/pull/841) (dev to main) [passed every job](https://github.com/collisionengineers/pegasus/actions/runs/36036679766) at the exact release SHA. The [main CI run](https://github.com/collisionengineers/pegasus/actions/runs/36039592914) at the same SHA was cancelled on its first attempt when `actions/checkout` hung for five minutes, and passed every job on a whole-run rerun. The operator granted merge and deployment for this run, chose to read the migration's preconditions back rather than wipe again, and chose the window immediately after promotion. The release build and the Local, Artifact, PreMigration, PreDeploy and both PreProvision plan gates passed; the offline migration-grants guard passed over 163 migrations. |
| Containment | The Worker's canonical Disabled census was set `true` and smoked at 18:31Z, the approved `worker.zip` was staged disabled and smoked again, the Worker read back `Stopped` at 18:34:23Z, and the Web App read back `Stopped` with `/health/live` unserved at 18:34:31Z, then again immediately before SQL. The old Web reported source `4c41d5a56a8a9d3341fc571c585d0b778779da8f`, the exact approved old SHA. |
| Schema and grants | Migration identity **changed** from `20260923180000_ValuationCardFiguresOptional` to **`20260924180000_CaseWorksAndTriageCases`**. The bundle applied `20260924090000_IntakeAssetBoxParentFolder` (additive, PR 824) and `20260924180000_CaseWorksAndTriageCases` (destructive: `CaseWorks` created with a primary work per Case; `CaseId` renamed to `WorkId` on ten per-work tables plus `CaseReportGenerations`; `Cases.AuditOfCaseId`, `CaseEngineerFindings` and `TriageSequences` dropped; `Triage` keyed by Case id) between 18:34:50Z and 18:35:08Z. Its fail-closed preconditions were read back at 18:34:40Z as 0 linked Audit Cases, 0 Triage rows, 0 engineer findings and 0 Cases in total, so no wipe preceded it. Bootstrap verified 714 catalogued permission/denial rows and 498 effective runtime DML rows, and the live head read back as the manifest identity at 18:35:27Z. |
| Web deployment | OneDeploy `8f4a0a10-50c7-435f-b70e-8a6330192424` succeeded at 18:36:28Z against the stopped site with `--restart false`, which stayed `Stopped`. `azd provision` succeeded twice, with the Worker disabled (1 minute 23 seconds) and for activation (1 minute 21 seconds). The site was started at 18:40:12Z and read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source and version on the fourth attempt at 18:42:44Z. |
| Worker deployment | The approved package staged before SQL was activated by configuration only. The Worker was started at 18:42:46Z and read back `Running` at 18:42:52Z with every Disabled value `false`. |
| Production smoke | Passed at 18:43:28Z. Active Web package `20260924183614.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-24T18:43:11Z`, and the active Graph subscription expires `2026-09-28T14:30:00Z`. Smoke is unauthenticated; the new Case page (views, Create audit) and the Triage Case await a signed-in look. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-64-773a9787`. The phase drivers and logs are under `artifacts/releases/release-64-driver`. |

## Intake data wipe — 24 September 2026

- Approved ordinary intake wipe: Worker `pegasus-prod-worker-252ow37gij`
  stopped for the maintenance window, then resumed and read back `Running`.
  The wipe cleared 92 blobs (38,474,936 bytes) from
  `pegcustody252ow37gij/transient-intake` and deleted 758 rows from 93
  non-preserved tables in `pegasus` (759 affected rows including the mail
  boundary update). The committed mail cutoff is
  `2026-09-24T13:17:38.8851348+00:00`; 38 effective tables and 584 rows
  remain preserved. Every value in `CaseSequences` (12 rows),
  `ImageIntakeSequences` (9 rows), `TriageSequences` (3 rows), and
  `UnidentifiedSequences` (1 row) was unchanged; `ValuationPresets` remained
  0/0. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook,
  and Box were untouched. Post-run verification reported zero blobs remaining
  and zero wiped tables holding rows.

## Release 63 — 23 September 2026 (deployment live)

Release 63 deployed [PR 825](https://github.com/collisionengineers/pegasus/pull/825), two operator requests on the Case's Correspondence tab. **Open message** stays on one line. It now opens the message in a dialog over the Case, with sender, received time, recipients, text and attachment names, fetched on first open from a new read-only Content handler on the Inbox message record. **Open full message** leads to the record. The route was the approved normal route with the migration identity unchanged. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `4c41d5a56a8a9d3341fc571c585d0b778779da8f` (the PR 825 merge), promoted atomically to both `dev` and `main` at 23:34Z. Manifest schema 3 SHA-256 `4B98AE5212A833D527E134DE55EA51642A897539C59B664599FD5D7102A2E124`. `web.zip` SHA-256 `E883B68696584C621DBFED08E0C75C5E714D31DADE3A23931853220E4BBD51A2`. `worker.zip` SHA-256 `75577EDBC46DF50E0BE715823CF43CAC3CBC17E5002D1B0A771EADF20F37DAB0`. Windows `efbundle.exe` SHA-256 `9E5D478E8D1D0FCD51E0104CEA8B331926F33C108BD0A0A9EBDADCA744880DD5` (not run). |
| Review and verification | [PR CI](https://github.com/collisionengineers/pegasus/actions/runs/35932407759) passed every job at the PR head `ef8e8582f`, whose merge into an unmoved `dev` is the release source. An automated code review of PR 825 found one low-severity defect: a modified click on Open message opened the dialog instead of a new tab. It was fixed in `ef8e8582f` before merge. The operator granted merge and deployment for this run. [Main CI](https://github.com/collisionengineers/pegasus/actions/runs/35934296288) passed every job at the exact release SHA. On its first attempt, SQL integration shard 6 hit a SQL execution timeout while migrating a fresh test database in `CaseWorkflowMigrationTests`, an unrelated runner flake. It passed on rerun at 00:16Z. The release build and the Local, Artifact, PreDeploy and PreProvision plan gates passed. |
| Schema and grants | Migration identity **unchanged**: `20260923180000_ValuationCardFiguresOptional`. No migration or bootstrap ran. `azd provision` found no changes at 23:40:16Z (pre-flight: B1 in uksouth limit 3). |
| Web deployment | OneDeploy `68b7aa64-d174-4e7e-96ad-e36c259994d5` succeeded at 23:40:38Z. As in Release 62, `az webapp deploy` stopped tracking the site start at about 600 seconds and reported failure while the site was already serving the release. It read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source and version on the first attempt at 23:51:05Z. |
| Worker deployment | ZIP deployment completed successfully after trigger synchronization and the platform health check. The canonical Disabled-setting census passed as `approved-live-worker`. |
| Production smoke | Passed at 23:54Z. Active Web package `20260923234024.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-23T23:50:03Z`, and the active Graph subscription expires `2026-09-28T14:30:00Z`. An unauthenticated request to the new Content handler redirects to sign-in. Smoke is unauthenticated; the Correspondence dialog awaits a signed-in look. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-63-4c41d5a5`. The drivers, phase logs and PR 825 verification (browser captures and probe scripts run against the local visual host) are under `artifacts/releases/release-63-driver`. |

## Release 62 — 23 September 2026 (deployment live)

Release 62 deployed [PR 823](https://github.com/collisionengineers/pegasus/pull/823), which fixes two regressions the operator reported after Release 61. A Case's Correspondence tab now lists every email linked to the Case, whatever its classification, including the email the Case was created from. The tab had listed only query and billing mail plus uploaded `.eml` files, so a mailbox instruction email never appeared and its `.eml` sat on Documents instead. The Administration Accounts Delete confirmation takes clicks again: the settings dialog had marked the native confirmation `inert`. The route was the approved normal route with the migration identity unchanged. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `446c3ce2f060008db57ce24388fe1196e3d34fb8` (the PR 823 merge), promoted atomically to both `dev` and `main` at 18:58Z. Manifest schema 3 SHA-256 `04B634419A7602C65A5237ADB334F0D01002A3C61D2EBF4419190AF78B98254A`. `web.zip` SHA-256 `FE78A3440ADDB51611BA751C6C154E365DBF2F345EBD1C72186D2D6B5CC0ECC3`. `worker.zip` SHA-256 `B9532120B1912B71C36CA6E9E6160BC2051FFB8500233AD8C4C11C0A092C4C68`. Windows `efbundle.exe` SHA-256 `225CD82C74A4D0B28C74B387482594F22915659219A84F71F059331860E81241` (not run). |
| Review and verification | [PR CI](https://github.com/collisionengineers/pegasus/actions/runs/35902573439) passed every job at the PR head `bf547c7cc`, whose merge into an unmoved `dev` is the release source. An automated code review of PR 823 found no defects; the operator granted merge and deployment for the fix. [Main CI](https://github.com/collisionengineers/pegasus/actions/runs/35906134367) passed every job at the exact release SHA. SQL integration shard 5 hung for its 45-minute timeout on its first attempt after an unrelated EF migration-lock error in `AzureSqlRuntimeRoleMigrationTests`, and passed on rerun at 20:44Z. The first release build was stopped by host memory pressure before any Azure write and was rebuilt from a clean worktree. The release build and the Local, Artifact, PreDeploy and PreProvision plan gates passed. |
| Schema and grants | Migration identity **unchanged**: `20260923180000_ValuationCardFiguresOptional`, read back as the production head before deployment. No migration or bootstrap ran. `azd provision` found no changes at 20:12:33Z (pre-flight: B1 in uksouth limit 3). |
| Web deployment | OneDeploy `07fb6510-44b3-466a-b92b-a1ed0891eddb` succeeded at 20:12:56Z. `az webapp deploy` stopped tracking the site start after 600 seconds and reported failure. The site was already serving the release: it read back `Running`, `DOTNETCORE\|10.0`, HTTP 200 readiness and the exact source and version on the first attempt at 20:23:29Z. |
| Worker deployment | ZIP deployment completed successfully after trigger synchronization and the platform health check. The canonical Disabled-setting census passed as `approved-live-worker`. |
| Production smoke | Passed at 20:27Z. Active Web package `20260923201242.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-23T20:25:02Z`, and the active Graph subscription expires `2026-09-28T14:30:00Z`. A read-only SQL check of the new projection lists QDOS26011's originating mailbox email as correspondence and matches its `OriginalSource` `.eml` by SHA-256, so Documents no longer shows it. Smoke is unauthenticated; both fixes await a signed-in look. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-62-446c3ce2`. The drivers, phase logs and PR 823 verification are under `artifacts/releases/release-62-driver`, including before-and-after browser captures of the Delete confirmation. |

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
