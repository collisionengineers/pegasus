# Operations

This is the last recorded deployed-state and support summary. It is not a fresh
cloud observation. Exact source structure belongs in [architecture](current-architecture.md);
procedures are reached through [the runbook](runbook.md).

## Release 97 — 9 October 2026 (deployment live)

Release 97 deployed [PR 1138](https://github.com/collisionengineers/pegasus/pull/1138). It merged four PRs into `dev`:

- [PR 1137](https://github.com/collisionengineers/pegasus/pull/1137): the v36 Case page sharpening round, its mockup record under `design/planning-and-old-designs/v36_planning/`, and Stage 2 on the live page: Case details without the ribbon's three facts, Storage per day and Recovery charge on Decisions with selects for recorded choices, Get valuation centred at each card's foot, aligned Repair Spec figures with Glass's included operations drawn indented, the Statement of truth folded, Held's step offering Release Hold, blocker rows without a how sentence, busy link buttons, Workflow and Closure actions returning to their section.
- [PR 1133](https://github.com/collisionengineers/pegasus/pull/1133): a contact Save is no longer refused by the Replace dialog's implicit-required key; Linked principals is hidden for a Principal-only contact.
- [PR 1134](https://github.com/collisionengineers/pegasus/pull/1134): a tag, untag or In report that clears a report blocker redraws the other sections as a save does, so the Report head shows Generate report without a reload.
- [PR 1136](https://github.com/collisionengineers/pegasus/pull/1136): an Actions-menu item taken inside the edit session keeps it.

The route was the normal App Service route with the migration identity unchanged, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. The Web App was out of service for about four minutes while it started the new package. An ordinary intake wipe followed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `7c5d8f462c2437a61af1f852301b7dde67219fef`. This is the merge of PR 1138 into `dev`; its tree equals the PR head `4e45cbc91` that CI tested. Promoted atomically to both `dev` and `main`; `main` was `14e9f7272`. Manifest schema 3 SHA-256 `852C35E56B811572B9B09242BACB897FEB5F73F53FAAA0DA81C294E7A05D1BBD`. `web.zip` SHA-256 `BE65B9BCB6913A7E91F008EEA46E07ACD5F7D675B782729EA90A3E1D6A36D837`, 107,545,472 bytes. `worker.zip` SHA-256 `BD25069200ED8F466346B96A15208B3AC31DC77DCA95457F30E136C667BAB572`. Windows `efbundle.exe` SHA-256 `376638C3920000C0400FACA9419D589A0C368ADAAC9F3947F615B847A4966C9A` (built, not run). |
| Review and verification | No PR had review feedback. PR 1137 first conflicted with `dev` in `CaseVehicleWebTests.cs` (Release 96's no-MOT-history test beside the v36 Experian seam helper) and, once merged, failed eight integration tests that asserted the pre-v36 page; `4c502cf01` updated them and narrowed item O so reading lists only the applied value increases (the 8 October 2026 ruling) while keeping the edit geometry. The train then merged without conflicts; git auto-merged FRD-16, `case-workspace.js` (PRs 1134 and 1137) and `CaseMutationPageModel.cs` (PRs 1136 and 1137), and the lost-work audit passed (recorded in PR 1138's body). <br>**CI:** PR 1137 run 37929486756 passed at `4c502cf01`; PR 1138 run 37933025206 passed all 10 jobs at `4e45cbc91` (infrastructure skipped). The Local, Artifact, PreDeploy and PreProvision gates passed. <br>**Operator approval (9 October 2026):** the mockup conversion, the combination, both merges, the release and the ordinary wipe, with all merge, deployment and wipe authority granted in the one request. |
| Schema and grants | Unchanged. The manifest's migration identity `20261008174505_CaseListPresets` equals the head Release 94 applied; the range adds no migration. No bundle or bootstrap ran. |
| Deployment | `azd provision` with Web `approved` and Worker `approved-live-worker` found no changes. B1 quota in `uksouth` read 3. `web.zip` was deployed with restart (OneDeploy `7206b3c6-d78e-4ee5-9a19-e5c63016e653` succeeded 13:30:39Z, package `20261009133019.zip`); the site answered the exact SHA on the first read-back at 13:34:54Z. `worker.zip` was deployed by config-zip (deployment `6707524b-d7d5-4de2-b328-e100ca93381e`, succeeded 13:36:45Z). Web outage: at most 13:30:43–13:34:54Z. The Worker was not stopped for the release. |
| Production smoke | Passed at 13:37:39Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261009133019.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-09T13:35:00Z`; the active Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | Ordinary intake wipe after the release, run with the released script, with the Worker stopped for it (`Stopped` 13:38:33Z) and started again after verification (`Running` 13:39:05Z); the Web App stayed Running. The fresh dry run and the execution both read 117 blobs / 101,592,718 bytes in `pegcustody252ow37gij/transient-intake` and 86 tables / 1,323 rows (8 Cases, 1 Triage, 47 documents, 8 retained mails; the earlier dry run at 13:21Z read 113 blobs and 1,299 rows); the batch reported 1,324 rows affected (the cutoff row). Blobs remaining 0; wiped tables still holding rows 0; preserved rows after 792; `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` 98/12/1 unchanged; valuation presets 4/4, e-mail templates 3/3, built-in image tags 4/4. Committed mail cutoff `2026-10-09T13:38:45.41Z`. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box untouched. |
| PR states | PRs 1133, 1134, 1136, 1137 and 1138 read Merged. No GitHub issue was linked to any of them. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1137: conformance screenshots of the routed Case page against the v36 shots (read and edit at 1580, 1440 and 760 px), and the two parts left open in the round's notes (item T's catch-up double redraw and first-save defaults; item W's locked-section sentence, which needs approved copy).<br>• PR 1134: with the Overview image as the only blocker in edit mode, tagging an image Overview shows Generate report without a reload.<br>• PR 1136: Edit, then Actions → Assign Engineer, comes back still editing and a later field edit saves.<br>The Release 81 to 96 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-97-7c5d8f46`; the build, deploy and wipe drivers and their logs, with the combination's PR body, at `artifacts/releases/release-97-driver`. |

## Release 96 — 9 October 2026 (deployment live)

Release 96 deployed [PR 1132](https://github.com/collisionengineers/pegasus/pull/1132). It merged two PRs into `dev`:

- [PR 1130](https://github.com/collisionengineers/pegasus/pull/1130): Cazana is connected as a guide valuation source through a Key Vault-held API key (ADR-0066).
- [PR 1131](https://github.com/collisionengineers/pegasus/pull/1131): the mileage box reads "No MOT history" when the lookup found no MOT test.

The route was the normal App Service route with the migration identity unchanged, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. The Web App was out of service for about eight minutes while it started the new package (see Deployment).

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `14e9f727290dcc69fe0675e73303835f4dd52bee`. This is the merge of PR 1132 into `dev`; its tree equals the PR head `0c04c5f88` that CI tested. Promoted atomically to both `dev` and `main`; `main` was `9e85b82fa`. Manifest schema 3 SHA-256 `FED63F62A5E36FD27F84F943B85064C3DF0EAADC6C54FA3A6C5CD87CB964F35B`. `web.zip` SHA-256 `1914A1BD12293DD2ECC9AB7565B2F8C7A11524D09665F7815AD5B2FBC61D0942`, 107,530,747 bytes. `worker.zip` SHA-256 `5F639A4C14AC07535DD777DDECF73832733E95978E45AF8AF6E9FD07A9E8ABF9`. Windows `efbundle.exe` SHA-256 `E59FD31A20E3929C03E6501CBE8B96AE0E91E005AA64B72860B6DE1002C54E5D` (built, not run). |
| Review and verification | Neither PR had review feedback. They share `CaseWorkspaceLabels.cs`, which merged without conflict; the lost-work audit passed (recorded in PR 1132's body). <br>**CI:** PR 1132 run 37907845950 passed all 11 jobs at `0c04c5f88`. The Local, Artifact, PreDeploy and PreProvision gates passed. <br>**Operator approval (9 October 2026):** the combination, its merge and the release, with all merge and deployment authority granted in the request. |
| Schema and grants | Unchanged. The manifest's migration identity `20261008174505_CaseListPresets` equals the head Release 94 applied; the range adds no migration. No bundle or bootstrap ran. |
| Configuration | `Cazana__ApiKey` is a Key Vault reference to `cazana-api-key` version `6db98eae219f4ff197b6a9989af117c4` in `pegasusprodkv252ow37g`, read back `Resolved` through the Web App's user-assigned identity, which holds Key Vault Secrets User on that secret alone. The secret, its grant and `CAZANA_API_KEY_SECRET_URI` in the azd environment were in place before the release. |
| Deployment | `azd provision` (deployment `pegasus-prod-1791538059`) with Web `approved` and Worker `approved-live-worker` succeeded in 1 minute 33 seconds, 09:27:31–09:29:05Z. B1 quota in `uksouth` read 3. `web.zip` was deployed with restart; OneDeploy `ff6475fd-b02e-4a39-bc2e-b1e04fdb57df` succeeded at 09:30:36Z with package `20261009092930.zip`. The first container start logged a transient SQL login failure (`SqlException`, TCP Provider error 35) at 09:30:46Z, terminated at 09:31:24Z and failed the startup probe after 231 s. The platform restarted it, the warm-up probe passed and the site started at 09:37:51Z. The CLI's tracker had already reported "site failed to start within 10 mins" at 619 s, so the driver stopped. A direct read-back showed `/health/live`, `/health/ready` and `/health/warm` 200 and `/diagnostics/version` reporting the exact release, and the route resumed at 09:42:20Z as in Release 71. `worker.zip` was deployed by config-zip (deployment `2ef56952-3e70-45d0-b5b2-157c4d7e5717`, succeeded 09:42:58Z). Web outage: at most 09:29:38–09:37:51Z (8 minutes 13 seconds). The Worker was not stopped. |
| Production smoke | Passed at 09:45:04Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261009092930.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-09T09:40:00Z`; the active Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | None. |
| PR states | PRs 1130, 1131 and 1132 read Merged. No GitHub issue was linked to either constituent. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1130: Get valuation from Cazana fills a test Case, and a registration Cazana holds no data for shows the information sentence.<br>• PR 1131: a vehicle with no MOT test shows "No MOT history" in the mileage box.<br>The Release 81 to 95 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-96-14e9f727`; the build, deploy and resume drivers and their logs at `artifacts/releases/release-96-driver`. |

## Cazana API key — 9 October 2026 (not yet deployed)

Prepared for connecting Cazana as a guide valuation source
([ADR-0066](adr/0066-cazana-valuation-through-a-key-vault-held-api-key.md)).
No release carries it yet; the next release's Bicep hands the Web App the
reference.

| Observation | Value |
| --- | --- |
| Key | Taken from Infisical (`dev` environment, `cazana_api_key`). A request without a VRM answered 400 `vrm or vin is required` on `https://api.cazana.com` with the key as a Bearer header, 403 on the UAT endpoint, and 401 without the key: it is a Production key. |
| Secret and grant | Secret `cazana-api-key` (version `6db98eae…17c4`) created in `pegasusprodkv252ow37g`. The Web identity `pegasus-prod-web-id-252ow37gij` was granted Key Vault Secrets User at that secret's scope (assignment `5541b295…5bc2`). `CAZANA_API_KEY_SECRET_URI` was set in the workstation's `pegasus-prod` azd environment. |
| Still owed | After the release: `Cazana__ApiKey` reads `Resolved`, and Get valuation on a test Case's Cazana card fills Retail and Trade. |

## Release 95 — 8 October 2026 (deployment live)

Release 95 deployed [PR 1128](https://github.com/collisionengineers/pegasus/pull/1128). It merged four PRs into `dev`:

- [PR 1118](https://github.com/collisionengineers/pegasus/pull/1118): the Case ribbon gives the registration its own cell after the Case reference; chips that do not fit beside the facts take their own row.
- [PR 1119](https://github.com/collisionengineers/pegasus/pull/1119): the Release 94 record.
- [PR 1120](https://github.com/collisionengineers/pegasus/pull/1120): every Work Centre Activity figure shows Today and This week.
- [PR 1125](https://github.com/collisionengineers/pegasus/pull/1125): the intake wipe keeps `CaseListPresets`.

The route was the normal App Service route with the migration identity unchanged, run from the Windows workstation, with no outage. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `9e85b82fa634cb176404bc40e2aee2a14d77e0cf`. This is the merge of PR 1128 into `dev`; its tree equals the PR head `bff3ddcca` that CI tested. Promoted atomically to both `dev` and `main`; `main` was `8df124b82`. Manifest schema 3 SHA-256 `A3FF2E55DB9EE1DAA187C930B8FA36C1914A73ADCA2223533F9713C4A4230DC9`. `web.zip` SHA-256 `4AF2A1B90CAEC75E1FCCBF1115F11F8F961B0A54D1878C2F08EB66AA31D357DA`, 107,521,599 bytes. `worker.zip` SHA-256 `312E0AD5CC78F2433B0208AFBADB51A79BF84BAF7791D1755A1FC6261771C25D`. Windows `efbundle.exe` SHA-256 `E38E2A8C53C5FAF73F5A54A7AC14F20E08710EC65089553224D63C256ECF97B7` (built, not run). |
| Review and verification | No PR had review feedback and no two PRs touched the same file; the train merged without conflicts and the lost-work audit passed (recorded in PR 1128's body). <br>**CI:** PR 1128 run 37843139844 passed all 10 jobs at `bff3ddcca` (infrastructure skipped). The Local, Artifact, PreDeploy and PreProvision gates passed. <br>**Operator approval (8 October 2026):** the combination, its merge and the release, with all merge and deployment authority granted in the request. |
| Schema and grants | Unchanged. The manifest's migration identity `20261008174505_CaseListPresets` equals the head Release 94 applied and verified; the range adds no migration. No bundle or bootstrap ran. |
| Deployment | `azd provision` with Web `approved` and Worker `approved-live-worker` found no changes. B1 quota in `uksouth` read 3. `web.zip` was deployed with restart (deployment `1a14366c-e593-48e9-8803-8665c0ee203f` succeeded 21:29:18Z, package `20261008212859.zip`; the site took 197 s to start); the site answered the exact SHA at 21:33:13Z. `worker.zip` was deployed by config-zip (deployment `8a8a8a26-f93d-44cc-a33d-fb3148dba4af`). |
| Production smoke | Passed at 21:35:39Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261008212859.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-08T21:35:03Z`; the active Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | None. |
| PR states | PRs 1118, 1119, 1120 and 1125 read Merged when PR 1128 landed. No GitHub issue was linked to any of them. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1118: the Case ribbon's Registration cell and its chip row on a real Case above 1100 px, including a total-loss Case in edit mode.<br>• PR 1120: the Work Centre Activity panel shows all ten figures.<br>• PR 1125: the next wipe dry run reads `CaseListPresets` on the preserve list.<br>The Release 81 to 94 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-95-9e85b82f`; the build and deploy drivers and their logs at `artifacts/releases/release-95-driver`; the combination's audit and PR body at `../pegasus-worktrees/merge-1118-1125-audit.md` and `merge-1118-1125-pr-body.md`. |

## Release 94 — 8 October 2026 (deployment live)

Release 94 deployed [PR 1117](https://github.com/collisionengineers/pegasus/pull/1117). It merged seven PRs into `dev`:

- [PR 1110](https://github.com/collisionengineers/pegasus/pull/1110): reading the Valuation section, an empty Engineer's Value and an absent guide month read as a dash, and an unrecorded calculation draws no label.
- [PR 1111](https://github.com/collisionengineers/pegasus/pull/1111): the Release 93 record.
- [PR 1112](https://github.com/collisionengineers/pegasus/pull/1112): the Repair Spec's Target % of value slider starts at the spec's own share of the PAV and goes no higher; Core drops the 100 % bound.
- [PR 1113](https://github.com/collisionengineers/pegasus/pull/1113): the Case aside's Next action holds one step, and Report not ready is its own folded card (v35 design 8).
- [PR 1114](https://github.com/collisionengineers/pegasus/pull/1114): the Work Centre's Needs attention references link to their record.
- [PR 1115](https://github.com/collisionengineers/pegasus/pull/1115): a regenerated report's fee note gets a new operation key (a.QDOS26082).
- [PR 1116](https://github.com/collisionengineers/pegasus/pull/1116): Administration Reports becomes Management Reports; Report types goes; the new MI-04 Case list with shared column presets (`CaseListPresets`).

The route was the normal App Service route after an additive migration, run from the Windows workstation, with no outage. Web and Worker are Running on the approved release, and full production smoke passed. An ordinary intake wipe followed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `8df124b82b8a67093286478b578586e0309e1b1e`. This is the merge of PR 1117 into `dev`; its tree equals the PR head `95fe438fa` that CI tested. Promoted atomically to both `dev` and `main`; `main` was `6be875542`. Manifest schema 3 SHA-256 `7FF7896D033B405DF5C877FDCE8E1BBC04EFA1B1E0A2D9DECD2FD93DA6384DDB`. `web.zip` SHA-256 `D3066318989221F58601D77E7AC130FFD26D246EF5CA058EE4C37C0AB2F3F1F5`, 107,518,953 bytes. `worker.zip` SHA-256 `E9EE9396C2ECB6B508CA260E17B10126FA2A6AB4F73D8E61F0B5EDEC02B8CBC7`. Windows `efbundle.exe` SHA-256 `7FA3705CAB757C8BF12025BB9ED14AB8F7AC154463CF5A076463DE3D29F65075`. |
| Review and verification | No PR had review feedback. The train merged without conflicts; git auto-merged the five files two PRs shared, and the lost-work audit passed. One fix on the integration branch: `95fe438fa` adds PR 1116's `CaseListPresets` grants to the bootstrap's hand-kept permission matrix, without which the post-migration bootstrap would have refused production (recorded in PR 1117's body). <br>**CI:** PR 1117 run 37832688690 passed all 11 jobs at `95fe438fa`. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. <br>**Operator approval (8 October 2026):** the combination and release as asked; `MERGE AUTH GRANTED` for the promotion; then the additive route on the exact manifest and targets; then the ordinary wipe on the fresh dry run's counts. |
| Schema and grants | Additive. The deployed head was `20261008090000_DropAutomationWorkflowEventTimeIndex`. The bundle applied `20261008174505_CaseListPresets` at 20:31:27Z; it creates `CaseListPresets` (present 0 before, 1 after) with its filtered unique name index, grants the Web SELECT, INSERT and UPDATE and denies DELETE to both runtime roles. Release 93 never names the table, so it kept serving beside it. Bootstrap verified 699 catalogued permission/denial rows and 487 effective runtime DML rows (694 and 484 before). Live head `20261008174505_CaseListPresets`, verified 20:31:48Z. |
| Deployment | `azd provision` with Web `approved` and Worker `approved-live-worker` found no changes. B1 quota in `uksouth` read 3. `web.zip` was deployed with restart (deployment `91610298-a5ca-4c82-9bc5-f08e99072d0a` succeeded 20:33:02Z, package `20261008203244.zip`; the site took 208 s to start); the site answered the exact SHA at 20:37:03Z. `worker.zip` was deployed by config-zip (deployment `b19d5f2a-5b25-44b1-b9ae-d2b52bbe7175`). |
| Production smoke | Passed at 20:39:26Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261008203244.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-08T20:35:00Z`; the active Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | Ordinary intake wipe after the release, run with the released script, with the Worker stopped for it (`Stopped` 20:41:02Z) and started again after verification (`Running` 20:41:57Z); the Web App stayed Running. Approved dry run and the fresh dry run both read 245 blobs / 320,323,314 bytes in `pegcustody252ow37gij/transient-intake` and 87 tables / 1,786 rows (9 Cases, 2 Triage, 97 documents, 10 retained mails); the batch reported 1,787 rows affected (the cutoff row). Blobs remaining 0; wiped tables still holding rows 0; preserved rows after 780; `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` 90/12/1 unchanged; valuation presets 4/4, e-mail templates 3/3, built-in image tags 4/4. Committed mail cutoff `2026-10-08T20:41:22.74Z`. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box untouched. `CaseListPresets` is not on the preserve list; it held no rows. |
| PR states | PRs 1110–1116 read Merged when PR 1117 landed. No GitHub issue was linked to any of them. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1110: the Valuation section read with an empty Engineer's Value and guide month.<br>• PR 1112: the slider starts at the spec's share on a real Repair Spec, and Apply there leaves the spec unchanged.<br>• PR 1113: conformance screenshots of the routed Case page aside.<br>• PR 1114: a Needs attention reference opens its record.<br>• PR 1115: Generate report after a change files the fee note. a.QDOS26082 was removed by the wipe.<br>• PR 1116: the UI guardrail browser check and a Case list export with a saved preset.<br>The Release 81 to 93 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-94-8df124b8`; the build, migration and deploy drivers and their logs at `artifacts/releases/release-94-driver`; the combination's audit and PR body at `../pegasus-worktrees/merge-1110-1116-audit.md` and `merge-1110-1116-pr-body.md`. |

## Release 93 — 8 October 2026 (deployment live)

Release 93 deployed [PR 1108](https://github.com/collisionengineers/pegasus/pull/1108). It merged three PRs into `dev`:

- [PR 1103](https://github.com/collisionengineers/pegasus/pull/1103): the report files as `{REF}_report.pdf`; grid images print unframed at the column's full width and their own shape, two to a row, capped at 160 mm.
- [PR 1104](https://github.com/collisionengineers/pegasus/pull/1104): the repo skill `pegasus-corpus-holding` copies mailbox mail into the append-only `corpus/holding/`.
- [PR 1107](https://github.com/collisionengineers/pegasus/pull/1107): Get valuation fills the Glass's card's Trade box again. Release 92's card put both figure hooks on both boxes, so Glass's trade figure overwrote Retail and Trade stayed blank.

The route was the normal App Service route with the migration identity unchanged, run from the Windows workstation, with no outage. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `6be8755424d8ccdc211c4a5987a9c155b3ae216d`. This is the merge of PR 1108 into `dev`; its tree equals the PR head `3fd5ab776` that CI tested. Promoted atomically to both `dev` and `main`; `main` was `ece2ede61`. Manifest schema 3 SHA-256 `8833599727220CD9BC4684157177F5981FBA99418D38CB20C84290E32A0CFE9E`. `web.zip` SHA-256 `5243E0CE64BAADE382C8F901760020C2F0C98AB2E2A86BB8397C86B3595CB594`, 107,207,968 bytes. `worker.zip` SHA-256 `68BD9EA9CC1427FA391243A20846195FDB88A91CC5E5538894EC72312C6C295D`. Windows `efbundle.exe` SHA-256 `38BADA8FA66A5ECAD10AD6FB6C6C56EA165DA817E35421DF8F505AB894BB3892` (built, not run). |
| Review and verification | No PR had review feedback and no two PRs touched the same file; the train merged without conflicts and the lost-work audit passed (recorded in PR 1108's body). <br>**CI:** PR 1108 passed all 10 jobs at `3fd5ab776` (infrastructure skipped). The Local, Artifact and PreProvision gates passed. <br>**Operator approval (8 October 2026):** landing PR 1108 when CI was green; then `MERGE AUTH GRANTED` and the normal route on the exact manifest and targets. |
| Schema and grants | Unchanged. The live head read `20261008090000_DropAutomationWorkflowEventTimeIndex` before release, equal to the manifest's migration identity; no bundle or bootstrap ran. |
| Deployment | `azd provision` with Web `approved` and Worker `approved-live-worker` found no changes. B1 quota in `uksouth` read 3. `web.zip` was deployed with restart (deployment `e75add02-dfb1-4258-8d6d-24643c86c199` succeeded 14:02:52Z, package `20261008140232.zip`; the site took 206 s to start); the site answered the exact SHA at 14:06:39Z. `worker.zip` was deployed by config-zip (deployment `ef8545d5-709a-4669-97f1-21f923139235`). |
| Production smoke | Passed at 14:09:02Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261008140232.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-08T14:05:00Z`; the active Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | None. |
| PR states | PRs 1103, 1104 and 1107 read Merged when PR 1108 landed. No GitHub issue was linked to any of them. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1107: run Get valuation and Save again on QDOS26086 and on any other Case valued since Release 92; their Retail likely holds Glass's trade figure.<br>• PR 1103: a generated report files as `{REF}_report.pdf`, and its image pages print portraits and landscapes at full column width, two to a row; no rendered PDF has been inspected.<br>• PR 1104: a first export into `corpus/holding/` under the operator's sign-in.<br>The Release 81 to 92 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-93-6be87554`; the build and deploy drivers and their logs at `artifacts/releases/release-93-driver`; the combination's audit and PR body at `../pegasus-worktrees/_merge-1103-1107-audit.md` and `_merge-1103-1107-body.md`. |

## Release 92 — 8 October 2026 (deployment live)

Release 92 deployed [PR 1100](https://github.com/collisionengineers/pegasus/pull/1100). It merged five PRs into `dev`:

- [PR 1095](https://github.com/collisionengineers/pegasus/pull/1095): the Linked cases finding no longer clips in the aside.
- [PR 1096](https://github.com/collisionengineers/pegasus/pull/1096): the Work Centre's New cases lists new Cases only; the Automation workflow-event time index goes.
- [PR 1097](https://github.com/collisionengineers/pegasus/pull/1097): reading the Valuation calculation lists only the applied value increases.
- [PR 1098](https://github.com/collisionengineers/pegasus/pull/1098): MCP `pegasus_valuation_get` runs Get valuation for automation; an Automation edit lease lasts until `pegasus_edit_end`; `damage.impacts` publishes its format. 61 → 62 tools.
- [PR 1099](https://github.com/collisionengineers/pegasus/pull/1099): Valuation as guide cards (v34); a click on a card uses it; the report's Retail and Trade follow the chosen card and are refused as typed fields.

The route was the normal App Service route after an additive migration, run from the Windows workstation, with no outage. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `ece2ede61f8580885e58695b6ab29eabfaa002fb`. This is the merge of PR 1100 into `dev`; its tree equals the PR head `bf7cd18a0` that CI tested. Promoted atomically to both `dev` and `main`; `main` was `850a8c48e`. Manifest schema 3 SHA-256 `8D34304C5B7ECBBE015B57AF79D9A7A4D737212B07FAAEB20F140EC44B1A6C44`. `web.zip` SHA-256 `B755A6BCC453D2356031F66E38A7E2C5196383946DAAE61282A971CA15F06A06`, 107,206,695 bytes. `worker.zip` SHA-256 `76A766F315293425CFEB9610F436A0F1EBE9EFF5D483D400115D24CBC64794C5`. Windows `efbundle.exe` SHA-256 `A51A86DE97307A6126D5A50425302679D2508AD7CB25A3174CD2BFA71DD68715`. |
| Review and verification | No PR had review feedback. The train conflicted only at PR 1099 against PR 1097, in three files; PR 1099 carries PR 1097's ruling, so PR 1099's side was kept hunk by hunk. Two fixes were needed on the integration branch: `f8ace1e4a` states PR 1099's Retail/Trade rule in PR 1098's `pegasus_valuation_get` text and FRD-10; `bf7cd18a0` fixes six SQL-shard failures that were red at PR 1099's own head, among them a connected guide card rendered `data-valuation-not-connected=""`, which would have kept Glass's "unavailable" notice standing after a Get valuation. The lost-work audit's FAIL and REVIEW lines each map to one of those, recorded in PR 1100's body. <br>**CI:** PR 1100 run 37768212249 passed all 12 jobs at `bf7cd18a0`. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. <br>**Operator approval (8 October 2026):** `MERGE AUTH GRANTED, MANIFEST APPROVED. proceed when CI passes` for landing PR 1100, the promotion and the additive route on the exact manifest and targets. |
| Schema and grants | Additive. The deployed head was `20261007184000_RemoveEva`. The bundle applied `20261008090000_DropAutomationWorkflowEventTimeIndex` at 11:54:05Z; it drops `IX_CaseWorkflowEvents_ActorKind_OccurredAtUtc` only (present 1 before, 0 after), which no code names, so Release 91 kept serving beside it. Bootstrap verified 694 catalogued permission/denial rows and 484 effective runtime DML rows, unchanged. Live head `20261008090000_DropAutomationWorkflowEventTimeIndex`, verified 11:54:31Z. |
| Deployment | `azd provision` with Web `approved` and Worker `approved-live-worker` found no changes. B1 quota in `uksouth` read 3. `web.zip` was deployed with restart (deployment `49c35dff-514d-4929-95f0-33a480b1f2ec` succeeded 11:55:51Z, package `20261008115518.zip`); the site answered the exact SHA at 12:00:11Z. `worker.zip` was deployed by config-zip (deployment `e1f903d6-ae84-4778-92ce-0a8791bf9f3f`). |
| Production smoke | Passed at 12:02:47Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261008115518.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-08T12:00:00Z`; the active Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | None. |
| PR states | PRs 1095–1099 read Merged when PR 1100 landed. No GitHub issue was linked to any of them. Follow-up [issue 1105](https://github.com/collisionengineers/pegasus/issues/1105): the valuation refusal still says "Press Use this value again" after PR 1099 removed the button; its wording awaits the operator. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1095: a long Linked cases finding wraps in the aside.<br>• PR 1096: New cases lists new Cases and no Automation workflow events.<br>• PR 1097 and PR 1099: a signed-in walk of the Valuation section in both views, in Scroll and Tabs, with a real save: cards, a card click, Selected, Retail and Trade following the card, the two-column increases with Add 20 % VAT, the previous total loss switch, and Get valuation on Glass's hiding its notice on success.<br>• PR 1098: a live MCP walk from Claude Desktop: `edit_begin`, several writes under one token, `edit_end`; an impacts write in the published format; `pegasus_valuation_get` on a test Case with the figures matching the card and the Values Only PDF filed.<br>The Release 81 to 91 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-92-ece2ede6`; the build, migration and deploy drivers and their logs at `artifacts/releases/release-92-driver`; the combination's audit and PR body at `../pegasus-worktrees/merge-1095-1099-audit.md` and `merge-1095-1099-pr-body.md`. |

## Release 91 — 7 October 2026 (deployment live)

Release 91 deployed [PR 1093](https://github.com/collisionengineers/pegasus/pull/1093). It merged nine PRs into `dev`:

- [PR 1084](https://github.com/collisionengineers/pegasus/pull/1084): the Principal API takes each file as a name and content; intake owns idempotency; `PrincipalSubmissions` loses five columns.
- [PR 1085](https://github.com/collisionengineers/pegasus/pull/1085): Case sections that show another section's value follow a landed save.
- [PR 1086](https://github.com/collisionengineers/pegasus/pull/1086): the hosted local test instance: live-integration opt-ins, Linux lifecycle fixes and the verification walk.
- [PR 1087](https://github.com/collisionengineers/pegasus/pull/1087): QDOS26080 / t.QDOS26079: the Triage ribbon shows the claimant, the Case gets a Linked cases card, and a Triage finding fills the Case's empty findings once.
- [PR 1088](https://github.com/collisionengineers/pegasus/pull/1088): Audatex PDF imports convert work units to hours by the printed time basis; paint hours and materials land where Glass's puts them.
- [PR 1089](https://github.com/collisionengineers/pegasus/pull/1089), [PR 1090](https://github.com/collisionengineers/pegasus/pull/1090) and [PR 1092](https://github.com/collisionengineers/pegasus/pull/1092): the Automation Actor has staff casework parity (ADR-0064): findings, valuation, estimates, notes, Case details, lifecycle, reports, documents, queues, jobs, intake, sending, inspection address, report wording and image preparation. 36 → 61 tools; new `automation.send` scope.
- [PR 1091](https://github.com/collisionengineers/pegasus/pull/1091): EVA is removed; every standard Case keeps one Export case download (ADR-0065). The EVA API send, its Worker timer, the per-Principal report-generation route, the EVA dialog and the `Eva:*` settings are gone.

The route was the destructive App Service route: the old Worker and Web were stopped before SQL and only the approved new bytes ran against the new schema.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `850a8c48eae1129114a037b1c789e6cc88828cfd`. This is the merge of PR 1093 into `dev`; its tree equals the PR head `9932b7df0` that CI tested. Promoted atomically to both `dev` and `main`; `main` was `19b330ed2`. Manifest schema 3 SHA-256 `993BA82D084FDB204C198EE0DC6ECAE192881852FB78A813FB0DCD18B6C203F7`. `web.zip` SHA-256 `1398B2AC73B379F2AD808C00553C445465339A54DAE97A35A8CAF08786A5717D`, 107,215,684 bytes. `worker.zip` SHA-256 `1C619604514AF33626BEADDD1143625B667C22E20FAD500CEB52B0D568D50733`. Windows `efbundle.exe` SHA-256 `36F0652966E9D2F03C137151E1CA919B0942D9D089EA136B8C39EF7AF33F1A2D`. |
| Review and verification | No PR had review feedback. Each PR merged cleanly against `dev` alone; the train conflicted at PRs 1087, 1089, 1092 and 1091 and was resolved as a union of intents, recorded in PR 1093's body. Two fixes were needed on the integration branch: PR 1091's ADR-0064 became ADR-0065 (PR 1089 holds 0064), and PR 1087's and PR 1091's migrations moved from timestamp `20261007180000` (also PR 1084's) to `183000` and `184000`. PR 1086's optional-EVA branches were dropped because PR 1091 removes EVA. PR 1092's own CI had three red SQL shards (an inspection address supplied by the Actor inherited a "keyed by staff" label; the image preparation tool passed the stored `Kind:SubjectId` stamp through; a test read an omitted null member); fixed on the integration branch in `9932b7df0`. The lost-work audit's FAIL and REVIEW lines each map to one of those resolutions. The operator accepted PR 1084's four behaviour changes and PR 1092's three consent-copy strings on 7 October 2026. <br>**CI:** PR 1093 run 37673568626 passed all 11 jobs at `9932b7df0`. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. <br>**Operator approval (7 October 2026):** the wipe; then `MERGE AUTH GRANTED, run now` for the promotion and every Azure and SQL write on the exact manifest, targets and old Web SHA `19b330ed2`, with the window at about 20:00Z. |
| Schema and grants | Destructive. The deployed head was `20261007160000_RemoveReportDateOverride`. Before SQL (20:07:29Z, read-only): 0 Cases, 0 `PrincipalSubmissions`, 0 rows in the three EVA tables, 0 Principals with a non-default report route, 0 `StaffMailSendOperations`, 0 settled-address receipts. The bundle applied `20261007180000_SimplifyPrincipalSubmissions`, `20261007181000_InspectionAddressSettlerKind`, `20261007182000_StaffMailSendActorKind`, `20261007183000_GrantWorkerTriageFindings` and `20261007184000_RemoveEva` between 20:07:40Z and 20:07:47Z. Bootstrap verified 694 catalogued permission/denial rows and 484 effective runtime DML rows (Release 90: 708 / 496; the EVA tables' rows and the `PrincipalSubmissions` UPDATE grants left, the Worker's `TriageFindings` SELECT arrived). Live head `20261007184000_RemoveEva`, verified 20:08:05Z. |
| Containment and deployment | Pre-step (20:04:07Z): the stale `AzureWebJobs.AutomaticEvaReviewSubmissionFunction.Disabled` setting was deleted from the Worker, because the release's exact five-function census no longer names it; the schedule setting stayed until provisioning removed it. Old Web read `19b330ed2` Running on `DOTNETCORE\|10.0`; Worker update strategy `Recreate`. Census set to disabled and `worker.zip` staged disabled (smoke passed both times); Worker `Stopped` 20:06:53Z; Web App `Stopped` and unserved 20:07:02Z; fresh read-back before SQL 20:07:28Z. `web.zip` deployed to the stopped site (deployment `f22c3687-cb25-4ee0-9fe6-6db472269520`, OneDeploy, package `20261007200849.zip`, 20:09:08Z). `azd provision` with the Worker disabled (1 min 58 s), then with `approved-live-worker` (1 min 23 s); B1 quota in `uksouth` read 3. Web App started 20:13:28Z and answered the exact SHA at 20:17:28Z (attempt 7); Worker `Running` 20:17:35Z. Outage ≈ 20:06:53Z–20:17:28Z. |
| Production smoke | The first full smoke (20:17:35Z) passed every gate except inbox liveness: the newest poll was 19 minutes old because the Worker had just started. Rerun passed at 20:20:28Z: Worker activation `approved-live-worker`, Web App Running on `DOTNETCORE\|10.0`, active Web package `20261007200849.zip` SHA-256 equals the approved `web.zip`, last completed poll `2026-10-07T20:20:03Z`, Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | Ordinary intake wipe before the release, with the Worker stopped for it (`Stopped` 19:12:28Z) and started again after verification (`Running` 19:13:00Z); the Web App stayed Running. Approved dry run and the fresh dry run both read 90 blobs / 46,306,606 bytes in `pegcustody252ow37gij/transient-intake` and 89 tables / 815 rows (4 Cases, 1 Triage, 34 documents, 5 retained mails); the batch reported 816 rows affected (the cutoff row). Blobs remaining 0; wiped tables still holding rows 0; preserved rows after 759; `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` 81/12/1 unchanged; valuation presets 4/4, e-mail templates 3/3, built-in image tags 4/4. Committed mail cutoff `2026-10-07T19:12:41.09Z`. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box untouched. |
| PR states | PRs 1084, 1085, 1087, 1088, 1089 and 1091 read Merged when PR 1093 landed; PR 1086 (based on `main`) read Merged at promotion; PRs 1090 and 1092 (stacked bases) were closed by hand with the merge SHA and ancestry proof. No GitHub issue was linked to any of the nine PRs. |
| Operator follow-up | Outside the repository, each needing its own approval: delete the EVA secrets from Key Vault `pegasusprodkv252ow37g` (`EVA_CLIENT_ID_SECRET_URI`, `EVA_CLIENT_SECRET_SECRET_URI`) and clear the azd `EVA_*` values in `pegasus-prod`; the new Bicep no longer reads them. Re-add the MCP connector's consent for the new `automation.send` scope where the connector needs it. |
| Still owed | Live proofs of the changes themselves, each on a Case created after the wipe:<br>• PR 1084: a Principal API submission with `fileName` + `contentBase64`, an Audit's top-level `originalReport`, and a replayed idempotency key answering `replayed: true`.<br>• PR 1085: set the Engineer's Value, then open Settlement without a reload; strip and salvage slider present; typing during a redraw; Repair Spec full screen.<br>• PR 1086: none in production (local instance).<br>• PR 1087: a new QDOS Triage → instruction pair: claimant on the Triage ribbon, Linked cases card on the Case, findings filled with the Triage tag, and a finding recorded after the link.<br>• PR 1088: discard QDOS26078's "Audatex 1" spec and re-import `AI026078_BD65OAZ.pdf`; hours 0.7/0.3/0.1/0.9/0.1, paint hours 1.2/0.9/1.0/0.7/0.3/0.5, £961.75 materials on the first paint line.<br>• PRs 1089–1092: a live MCP walk: findings, a note, a details edit that keeps unnamed facts, an estimate without a job, a priced To-be-confirmed line, a lease take-over, the vocabulary read, a Case action, a report generation and approval, a document tag, an intake accept with a supplied address, a report send and a mail send under `automation.send`, wording and image preparation.<br>• PR 1091: Export case on a standard Case in each state downloads `{reference}.zip` with the JSON and `Images/`; one `case_exported` history row; Work Centre "Sent to Engineer" counts once. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-91-850a8c48`; the wipe, pre-step, build, containment, migration, deploy and smoke drivers and their logs at `artifacts/releases/release-91-driver`; the combination's audit and resolution tooling at `../pegasus-worktrees/_merge-1084-1092-tools`. |

## Release 90 — 7 October 2026 (deployment live)

Release 90 deployed [PR 1081](https://github.com/collisionengineers/pegasus/pull/1081). It merged three PRs into `dev`:

- [PR 1076](https://github.com/collisionengineers/pegasus/pull/1076): Search's State filter lists With Engineer once and finds both of its states.
- [PR 1079](https://github.com/collisionengineers/pegasus/pull/1079): Glass's VIN fills the Case's empty VIN, from Get valuation and from any Glass's estimate import.
- [PR 1080](https://github.com/collisionengineers/pegasus/pull/1080): the Report date is one box. A recorded date prints; an empty one prints the generation day. Override report date is removed.

The release also carried the docs-only [PR 1082](https://github.com/collisionengineers/pegasus/pull/1082) (Principal API OpenAPI description), merged to `dev` while PR 1081's CI ran. The route was the normal App Service route after an additive migration, run from the Windows workstation, with no outage. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `19b330ed2558b37d2a22fbade0d7f5e71f19a1d5`. This is the merge of PR 1081 into `dev`; its tree equals the PR head `9c32d7a91` that CI tested plus PR 1082's one documentation file. Promoted atomically to both `dev` and `main`; `main` was `15aa9e95b`. Manifest schema 3 SHA-256 `B7FE09580DBC224D66DB41A2692B5872467C657E001D7B2874F92144405B5AA8`. `web.zip` SHA-256 `EDCBF8AD862BAD0E715DBB513B3375ADD8CD7286BDDAFE2A39912F2064977B86`, 107,053,382 bytes. `worker.zip` SHA-256 `28122ECA7B02FD88597F27935BF27434E41AADDC912994F596BBF056038B18D1`. Windows `efbundle.exe` SHA-256 `62AD31B395095D7F78BD936742E89A2ACC05D4DBD3DD91A65CA84B23CB9DAF30`. |
| Review and verification | No PR had review feedback. The three PRs touch no common file and merged cleanly in a simulated merge train, each alone and in every pair. The lost-work audit passed, and no fix commits were needed. No remaining use was found of the symbols the PRs removed or renamed: `CaseSearchFilters.State`, `ReportDateOverridden`, `StampReportDateAsync`, `report.date_override`. Production grants already let the Worker write every table #1079's VIN fill writes. <br>**CI:** PR 1081 run 37613051267 passed all 11 jobs at `9c32d7a91`; PR 1082 passed its path-filtered checks. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. <br>**Operator approval (7 October 2026):** landing PR 1081 once green, then the promotion and the additive route on the exact manifest and targets. |
| Schema and grants | Additive data change, with no schema change. The deployed head was `20261007140000_MarketResearchDocumentRole`. The bundle applied `20261007160000_RemoveReportDateOverride` at 11:59:42Z; it deletes stored `report.date_override` rows (0 before SQL, 0 Cases after the wipe), which Release 89 read as the switch off, so the old bytes kept serving beside it. Bootstrap verified 708 catalogued permission/denial rows and 496 effective runtime DML rows. Live head `20261007160000_RemoveReportDateOverride`. |
| Deployment | `azd provision` with Web `approved` and Worker `approved-live-worker` found no changes. B1 quota in `uksouth` read 3. `web.zip` was deployed with restart (deployment `2285f708-6052-413b-8626-54e85d9d8b4c` succeeded 12:01:58Z, package `20261007120138.zip`); the site answered the exact SHA at 12:07:43Z. `worker.zip` was deployed by config-zip (deployment `52627739-203f-4e29-a1bf-279d9dec5680`). |
| Production smoke | Passed at 12:10:42Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261007120138.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-07T12:10:03Z`; the active Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | Ordinary intake wipe before the release, with the Worker `pegasus-prod-worker-252ow37gij` stopped for it (`Stopped` 11:19:55Z) and started again after verification (`Running` 11:20:34Z); the Web App stayed Running. The approved dry run found 185 blobs and 1,339 rows. The fresh dry run immediately before the execute found 186 blobs (79,733,496 bytes) in `pegcustody252ow37gij/transient-intake` and 1,364 rows across 89 non-preserved tables in SQL `pegasus` on `pegasus-prod-sql-252ow37gij` (3 Cases); the batch reported 1,365 affected rows including the mail-boundary update. The committed mail cutoff is `2026-10-07T11:20:09.1535967+00:00`; 39 effective tables and 747 preserved rows remain. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were unchanged at 77/12/1. `ValuationPresets` 4/4, `EmailTemplates` 3/3 and built-in image tags 4/4 before/after. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No `-ResetTestEstate` was used. Post-run script verification reported zero blobs remaining and zero wiped tables holding rows. |
| Still owed | Live proofs of the changes themselves, each on a Case created after the wipe:<br>• PR 1076: Search shows one With Engineer option and finds Cases in both of its states.<br>• PR 1079: a Get valuation on a real plate and a Glass's estimate return each fill an empty VIN, and neither replaces an existing one.<br>• PR 1080: a report generated with no Report date prints the generation day and the Report section read cell shows it; a typed date prints and stales the report.<br>The Release 81 to 89 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-90-19b330ed`; the wipe, build, migration and deploy drivers and their logs at `artifacts/releases/release-90-driver`. |

## Release 89 — 7 October 2026 (deployment live)

Release 89 deployed [PR 1077](https://github.com/collisionengineers/pegasus/pull/1077). It merged seven PRs into `dev`:

- [PR 1062](https://github.com/collisionengineers/pegasus/pull/1062): the Report Attach tickboxes take the 15px choice style.
- [PR 1063](https://github.com/collisionengineers/pegasus/pull/1063): the Repair Spec stays full screen across Glass's return.
- [PR 1064](https://github.com/collisionengineers/pegasus/pull/1064): report images. The Overview prints at place 1, beside the damage diagram, and the Close-up leads the image pages. Images print whole, order numbers are shown on the tiles, and clicking a thumbnail opens the viewer.
- [PR 1072](https://github.com/collisionengineers/pegasus/pull/1072): report sends reach Sent and the Case's Correspondence. The attachment hash is now matched without regard to hex case.
- [PR 1073](https://github.com/collisionengineers/pegasus/pull/1073): Market research is a document role, not an image tag.
- [PR 1074](https://github.com/collisionengineers/pegasus/pull/1074): completed Triages leave the queue, and Reply with finding goes once it is sent. A reply to a staff forward goes to the original sender. Fixes #1042, #1044 and #1047.
- [PR 1075](https://github.com/collisionengineers/pegasus/pull/1075): Generate report makes the report and then its separate fee note; Generate fee note is removed.

The route was the destructive App Service route, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `15aa9e95b17ea4bfa19a5f36da67712d04a2594b`. This is the merge of PR 1077 into `dev`; its tree equals the PR head `4e989351b` that CI tested. Promoted atomically to both `dev` and `main`; `main` was `66f007dfc`. Manifest schema 3 SHA-256 `AD68EF33343476A8608163195E1626F92634989C1D7387AF6F0DAD981C43E696`. `web.zip` SHA-256 `676633CFAB9CAC25DAA02C7FF7BC07D6DDBECEDB276155C21727B64E6175AE77`, 107,038,147 bytes. `worker.zip` SHA-256 `881CE0F59FADBE6B38FA6A67BEAF3AB1F7339ABDAC97DD819F8FB34817092D27`. Windows `efbundle.exe` SHA-256 `7E19664171CF8D2FAD1D0536F10E627FFB5AA032625A66B73B8517515B7B4394`. |
| Review and verification | No PR had review feedback. All seven merged cleanly in a simulated merge train, each alone and in every pair. The lost-work audit passed, and no fix commits were needed. No remaining use was found of the symbols the PRs removed: `MarketResearchId`/`MarketResearchName`, `CloseUpImageRequirement`, `GenerateFeeNote`/`FeeNoteGenerated`. <br>**CI:** PR 1077 run 37599748469 passed all 11 jobs at `4e989351b`. The Local, Artifact, PreMigration, PreDeploy and PreProvision gates passed. <br>**Operator approval (7 October 2026):** the merge of PR 1077, promotion, the destructive route on the exact manifest and targets, and the outage at 11:06 UK time, inside typical usage. |
| Schema and grants | Destructive data change, with no schema change. The deployed head was `20261006160000_RepairSpecificationGlassEstimate`. The bundle applied `20261007140000_MarketResearchDocumentRole` at 10:10:09Z. Before SQL it would update 0 market-research document occurrences and remove 0 tag assignments, so its only deletion was the built-in Market research tag row `…17a5`. Old bytes could not run against the new data: the old Worker attaches that tag when a Market research job completes. Built-in image tags now read 4. Bootstrap verified 708 catalogued permission/denial rows and 496 effective runtime DML rows. Live head `20261007140000_MarketResearchDocumentRole`. |
| Outage and deployment | The old Web reported `66f007dfc` before containment. The Worker `Disabled` census was set to `true`, and the new `worker.zip` was staged disabled. The Worker update strategy is `Recreate`. Worker `Stopped` 10:09:06Z; Web App `Stopped` and unserved 10:09:22Z. After SQL, `web.zip` was deployed to the stopped site (deployment `4a55171f-31f9-48ec-88a0-d98ef9bc2b17` succeeded 10:11:14Z, package `20261007101057.zip`). It was then provisioned with the Worker disabled and again with `approved-live-worker`. B1 quota in `uksouth` read 3. The Web App started 10:15:02Z and answered the exact SHA at 10:19:26Z; Worker `Running` 10:19:36Z. The outage (10:09–10:19Z) was inside typical usage, by the operator's approval; the test estate held 1 Case. |
| Production smoke | Passed at 10:20:22Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261007101057.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-07T10:20:03Z`; the active Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | None. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1062: the Report Attach tickboxes in the delivery form.<br>• PR 1063: the Repair Spec stays full screen after Glass's returns.<br>• PR 1064: a generated report with the Overview on page 1, the Close-up leading the image pages, and every image printed whole; tile order numbers; the thumbnail opening the viewer.<br>• PR 1072: a report send reaching Sent and listed in the Case's Correspondence.<br>• PR 1073: a Market research job's findings file carrying the Market research role in Documents.<br>• PR 1074: a completed Triage leaving the queue; Reply with finding gone once sent; a reply to a staff forward going to the original sender.<br>• PR 1075: Generate report making the report and its separate fee note; an existing confirmed report without a separate fee note offering Generate report again, which adds only the fee note.<br>Issues #1042, #1044 and #1047, fixed by PR 1074, did not close on promotion and were closed by hand after the release. The Release 81 to 88 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-89-15aa9e95`; the build, containment, migration and deploy drivers and their logs at `artifacts/releases/release-89-driver`. |

## Release 88 — 6 October 2026 (deployment live)

Release 88 deployed [PR 1060](https://github.com/collisionengineers/pegasus/pull/1060). It merged six PRs into `dev`:

- [PR 1052](https://github.com/collisionengineers/pegasus/pull/1052): from the a.QDOS26070 examination. Estimate line ids follow an Estimate save, so a second save on the same page is accepted. A held Glass's estimate lands under the returning staff member's own session, and a Glass's import keeps the lease. Edit is accepted on a page behind the Case.
- [PR 1053](https://github.com/collisionengineers/pegasus/pull/1053): a report is sent in one step with **Send report**; delivery preparation and the `CaseReportDeliveryIntents` table are removed.
- [PR 1054](https://github.com/collisionengineers/pegasus/pull/1054): the Upload pages no longer draw broken image placeholders. The CSP allows `img-src 'self' blob:`, and a review tile links its image only after custody is confirmed.
- [PR 1056](https://github.com/collisionengineers/pegasus/pull/1056): Register images works on an upload group's Unidentified item.
- [PR 1057](https://github.com/collisionengineers/pegasus/pull/1057): every report ends with its fee note, and a separate fee note can always be generated; the Include fee note choice is removed.
- [PR 1058](https://github.com/collisionengineers/pegasus/pull/1058): a Glass's estimate belongs to its repair spec by stock vehicle (ADR-0063). Glass's on that spec reopens the same estimate, and its return updates the spec in place. The Resume button is removed.

The route was the destructive App Service route, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `66f007dfc3ba222ff1452a431b80a95323c849df`. This is the merge of PR 1060 into `dev`; its tree equals the PR head `b54ed409a` that CI tested. Promoted atomically to both `dev` and `main`; `main` was `f8ae02cf8`. Manifest schema 3 SHA-256 `BD69FD835770D2E44692DD8BFC44D638B6BB8D9076C07CBBB6B2FDD2F43B889E`. `web.zip` SHA-256 `468A1033923C9589E40E2992483499B13C9AF671C1AC579947A99BAEF2ECBFCF`, 107,022,263 bytes. `worker.zip` SHA-256 `2987CA8C5B6595C0A44935C8D8442717C7FEEEB4C1413452D2A19893DBB9708A`. Windows `efbundle.exe` SHA-256 `50D8905D98881F808BACF2983D1131715571009DDECFB1B087E3A00594DC13B0`. |
| Review and verification | No PR had review feedback. One merge conflict (#1053 × #1057, `CaseReportDeliveryWebTests.cs`) was resolved by keeping #1053's one-step send without #1057's removed `includeFeeNote` argument. Two fixes went on the integration branch. `66c106ab8`: #1053 and #1058 both used migration timestamp `20261006150000`, so #1058's became `20261006160000`. `b54ed409a` fixed #1058's own head, which had failed six SQL tests: an in-place Glass's update numbered its Imported snapshot 1 again, a double-click on Glass's restarted the estimator, and one test assertion was stale. The lost-work audit's FAIL and REVIEW lines all map to those resolutions. <br>**CI:** PR 1060 run 37537844643 passed all 11 jobs at `b54ed409a`. The first run's coverage collector hung on a stale merge ref, so the PR was closed and reopened. The Local, Artifact, PreMigration, PreDeploy and PreProvision gates passed. <br>**Operator approval (6 October 2026):** the combination, merge to `dev`, promotion to `main`, deployment and the intake wipe were authorised in advance with the request. |
| Schema and grants | Destructive. The deployed head was `20261005150000_PrincipalDefaultFee`. The bundle applied `20261006150000_DropCaseReportDeliveryIntents` (drops `CaseReportDeliveryIntents`, 0 rows after the wipe; forward-only) and the additive `20261006160000_RepairSpecificationGlassEstimate` (six nullable `Glass*` columns on `CaseRepairSpecifications`) by 22:39:41Z. Bootstrap verified 708 catalogued permission/denial rows and 496 effective runtime DML rows. Live head `20261006160000_RepairSpecificationGlassEstimate`. |
| Outage and deployment | The old Web reported `f8ae02cf8` before containment. The Worker `Disabled` census was set to `true`, and the new `worker.zip` was staged disabled. The Worker update strategy is `Recreate`. Worker `Stopped` 22:38:39Z; Web App `Stopped` and unserved 22:38:46Z. After SQL, `web.zip` was deployed to the stopped site (deployment succeeded 22:41:02Z, package `20261006224042.zip`), then provisioned with the Worker disabled and again with `approved-live-worker`. B1 quota in `uksouth` read 3. The Web App started 22:44:27Z and answered the exact SHA at 22:48:36Z; Worker `Running` 22:48:43Z. The short outage (22:38–22:48Z) was outside typical usage. |
| Production smoke | Passed at 22:49:33Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261006224042.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-06T22:35:00Z`; the active Graph subscription expires `2026-10-12T13:50:00Z`. |
| Wipe | Ordinary intake wipe before the release, with the Worker `pegasus-prod-worker-252ow37gij` stopped for it (`Stopped` 21:29:21Z) and started again after verification (`Running` 21:30:02Z); the Web App stayed Running. The fresh dry run immediately before the execute found 176 blobs (158,421,484 bytes) in `pegcustody252ow37gij/transient-intake` and 2,136 rows across 90 non-preserved tables in SQL `pegasus` on `pegasus-prod-sql-252ow37gij` (5 Cases); the batch reported 2,137 affected rows including the mail-boundary update. The committed mail cutoff is `2026-10-06T21:29:34.1443902+00:00`; 39 effective tables and 737 preserved rows remain. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were unchanged at 74/11/1. `ValuationPresets` 4/4, `EmailTemplates` 3/3 and built-in image tags 5/5 before/after. The wiped rows included QDOS's one `PrincipalApiCredentials` row, which was issued, rotated and revoked at 12:01–12:02Z the same day; that table is not on the preserve list. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No `-ResetTestEstate` was used. Post-run script verification reported zero blobs remaining and zero wiped tables holding rows. |
| Still owed | Live proofs of the changes themselves, each on a Case created after the wipe:<br>• PR 1052: a second Estimate save on an open page; a Glass's return after a hand-off and re-claim; Edit within seconds of a Case's creation.<br>• PR 1053: a Report section walk in edit mode, and one live send on a test Case, sent twice.<br>• PR 1054: the Upload selection and review pages showing image previews in production.<br>• PR 1056: Register images on a multi-file upload group's Unidentified item.<br>• PR 1057: a generated report ending with its fee note, and a separate fee note generated beside it.<br>• PR 1058: the six-step walk in its PR body. Also owed: operator approval of its new refusal sentence ("The Case registration or mileage has changed since this Glass's estimate was started. Restore the original vehicle details to reopen it."), and proof that a second Glass's login can open another login's stock record.<br>The Release 81 to 87 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-88-66f007df`; the wipe, build, containment, migration and deploy drivers and their logs at `artifacts/releases/release-88-driver`. |

## Release 87 — 6 October 2026 (deployment live)

Release 87 deployed [PR 1049](https://github.com/collisionengineers/pegasus/pull/1049). It merged ten PRs into `dev` with no conflicts and no fixes of its own:

- [PR 1023](https://github.com/collisionengineers/pegasus/pull/1023): every Glass's page pattern has a one-second match budget, and a timeout settles as the stage's own refusal with `regex=timeout`, never as `glass.transport.failed`. It fixes [issue 1021](https://github.com/collisionengineers/pegasus/issues/1021).
- [PR 1036](https://github.com/collisionengineers/pegasus/pull/1036): Resume and Fetch again re-prove a vehicle whose estimate already exists, and Resume reopens the estimator as the portal does (`ere_id` 0). It fixes [issue 1026](https://github.com/collisionengineers/pegasus/issues/1026).
- [PR 1037](https://github.com/collisionengineers/pegasus/pull/1037): identity refusals name the control that was missing, a newly created vehicle is read once more before the identity check, and a rejected password is its own code `glass.login.rejected`. It fixes [issue 1030](https://github.com/collisionengineers/pegasus/issues/1030).
- [PR 1038](https://github.com/collisionengineers/pegasus/pull/1038): both estimate readers (XML and calculation sheet) take every section, the paint level and set-up time the CE account prints, and every labour time unit. It fixes [issue 1027](https://github.com/collisionengineers/pegasus/issues/1027) and [issue 1029](https://github.com/collisionengineers/pegasus/issues/1029).
- [PR 1039](https://github.com/collisionengineers/pegasus/pull/1039): a refused export is kept rather than discarded, an export that fails after the relay can be fetched again, and a login page at the relay is looked up instead of reported as a landing. It fixes [issue 1031](https://github.com/collisionengineers/pegasus/issues/1031).
- [PR 1040](https://github.com/collisionengineers/pegasus/pull/1040): a Glass's valuation report is filed only when the PDF names the Case registration. It fixes [issue 1032](https://github.com/collisionengineers/pegasus/issues/1032).
- [PR 1041](https://github.com/collisionengineers/pegasus/pull/1041): a system write (custody confirmation, lookup fill) advances the Case version without ending the staff edit session; Renew is removed and a lapsed, unclaimed lease is picked up silently; the total-loss, unroadworthy and contract-sum save refusals are dropped (readiness still lists them).
- [PR 1043](https://github.com/collisionengineers/pegasus/pull/1043): in Review, the Next action opens the Assign Engineer dialog instead of jumping to a section; "Hand to Engineer" is renamed Assign Engineer. It fixes [issue 1025](https://github.com/collisionengineers/pegasus/issues/1025).
- [PR 1045](https://github.com/collisionengineers/pegasus/pull/1045): the Valuation section is v33 design D, one Engineer's Value beside its calculation, with the recorded calculation named as the figure's source.
- [PR 1048](https://github.com/collisionengineers/pegasus/pull/1048): the Cases rail is one continuous list (Not ready, Review, With Engineer, Query, Triage, Awaiting instruction, Held, Unidentified) with no groups, dividers, exception tint or Completed queue; `/Cases?tab=complete` is Not found and a Completed Case is found through Search. It fixes [issue 1046](https://github.com/collisionengineers/pegasus/issues/1046).

The route was the normal App Service route with an unchanged migration identity, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `f8ae02cf81f9437076c65322e9c74ffc22f3a32d` (the merge of PR 1049 into `dev`; its tree equals the PR head `27c5ca52e` that CI tested). Promoted atomically to both `dev` and `main` at 11:30:45Z; `main` was `e50fbd6c8`. Manifest schema 3 SHA-256 `DE7D6390B766B7919FA67597D1CD9F310E6002877456357B488E76DD92A815AD`. `web.zip` SHA-256 `3945C1EDDBE9F1B5F6EFA1E62DD4818144AC728F74848052FD3A43B279878273`, 107,051,157 bytes. `worker.zip` SHA-256 `D4D49910FD525F4119795FA3A78C716A5BD14EC4146F7A49AE1CF5370D596AA9`. Windows `efbundle.exe` SHA-256 `A2424B1325273FA3D88B932422D73B7584E4A54CFC4ABF69FBF42A9B36A57D22`. |
| Review and verification | No PR had review feedback. The merge train (1023, 1036, 1037, 1038, 1039, 1040, 1041, 1043, 1045, 1048) merged with no conflict at any step or pair; the lost-work audit's only REVIEW lines are two PR 1036 lines that its stacked children 1037, 1039 and 1040 revise. A Release build of the combined tip had 0 errors and 0 warnings. <br>**CI:** PR 1049 passed all 11 jobs at `27c5ca52e`, and each PR passed CI at its head. The Local, Artifact, PreDeploy and PreProvision gates passed. <br>**Operator approvals (6 October 2026):** the combination and merge with the request, the wipe with its fresh counts, then merge authority, and the exact manifest and deployment targets. |
| Schema and grants | Unchanged. The manifest's `migrationIdentity` is the deployed head `20261005150000_PrincipalDefaultFee`; no bundle, bootstrap or grant step ran. |
| Web and Worker deployment | Provision (11:31:34–11:31:46Z) reported no changes. B1 quota in `uksouth` read 3, and the Worker `Disabled` settings rendered `false`. `az webapp deploy` (deployment `0277d329-7066-449c-ab84-651fe10ba19e`, package `20261006113154.zip`) started 11:31:46Z. The exact SHA answered `/health/ready` and `/diagnostics/version` at 11:36:30Z. The Worker ZIP was deployed by 11:38:57Z (deployment `11245f67-f501-4b21-8d8f-894e5ffb07c8`). |
| Production smoke | Passed at 11:39:41Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261006113154.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-06T11:35:00Z`; the active Graph subscription expires `2026-10-08T13:10:00Z`. |
| Wipe | Ordinary intake wipe before the release (approved 6 October 2026), Worker `pegasus-prod-worker-252ow37gij` stopped for it (`Stopped` 10:50:58Z) and started again after verification (`Running` 10:51:48Z); the Web App stayed Running. The fresh dry run immediately before the execute found 76 blobs (122,800,525 bytes) in `pegcustody252ow37gij/transient-intake` and 552 rows across 90 non-preserved tables in SQL `pegasus` on `pegasus-prod-sql-252ow37gij` (2 Cases); the batch reported 553 affected rows including the mail-boundary update. The committed mail cutoff is `2026-10-06T10:51:15.9236604+00:00`; 39 effective tables and 727 preserved rows remain. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were unchanged at 69/11/1. `ValuationPresets` and `EmailTemplates` were 0/0 and 3/3 before/after, and built-in image tags 5/5. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No `-ResetTestEstate` was used. Post-run script verification reported zero blobs remaining and zero wiped tables holding rows. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1036: Resume before and after a save, and Fetch again, on a Case whose estimate exists.<br>• PR 1039: one estimator session longer than 30 minutes returning its export.<br>• PR 1041: a Case edit continuing through a custody confirmation without the "case action was not applied" notice.<br>• PR 1043: the Review Next action opening the Assign Engineer dialog.<br>• PR 1045: a signed-in walk of the Valuation section in both views with a real save.<br>The Release 81 to 86 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-87-f8ae02cf`; the build and deploy drivers and their logs at `artifacts/releases/release-87-driver`. |

## Intake data wipe — 5 October 2026

- Approved ordinary intake wipe: Worker `pegasus-prod-worker-252ow37gij`
  stopped for the maintenance window and read back `Stopped`, then resumed and
  read back `Running` after verification; the Web App stayed Running. The fresh
  dry run immediately before the execute found 309 blobs (345,513,862 bytes)
  in `pegcustody252ow37gij/transient-intake` and 2,450 rows across 90
  non-preserved tables in SQL `pegasus` on `pegasus-prod-sql-252ow37gij` (10
  Cases); the batch reported 2,451 affected rows including the mail-boundary
  update. The committed mail cutoff is `2026-10-05T21:48:47.0472336+00:00`;
  39 effective tables and 717 preserved rows remain.
  `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were unchanged
  at 67/11/1. `ValuationPresets` and `EmailTemplates` were 0/0 and 1/1
  before/after, and built-in image tags 5/5.
  `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were
  untouched. No `-ResetTestEstate` was used. Post-run script verification
  reported zero blobs remaining and zero wiped tables holding rows.

## Release 86 — 5 October 2026 (deployment live)

Release 86 deployed [PR 1034](https://github.com/collisionengineers/pegasus/pull/1034). It merged three PRs into `dev` together with the fixes from the PR 1024 review:

- [PR 1024](https://github.com/collisionengineers/pegasus/pull/1024): the Work Centre Owner column uses one word. A named person shows as their name. An empty person slot shows "Unassigned", and a row not held by a person shows "No owner". Assign to me is removed from the open row, Assign Engineer and Hand to Engineer; to take work, staff choose a person in the dialog. It fixes [issue 1014](https://github.com/collisionengineers/pegasus/issues/1014).
- [PR 1028](https://github.com/collisionengineers/pegasus/pull/1028): Laird's total-loss report is recognised as an original report (`laird/2` signature, extraction `/4`; salvage value and category are read as source rows only).
- [PR 1033](https://github.com/collisionengineers/pegasus/pull/1033): every Principal has a required default fee (seeded £180.00). A new Case writes it as `fee.agreed_fee` on acceptance, manual creation and wrong-Principal replacement.

The route was the normal App Service route with an additive migration, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `e50fbd6c88e85498250d93c6b76a563e006b15d1` (the merge of PR 1034 into `dev`; its tree equals the PR head `af73c6200` that CI tested). Promoted atomically to both `dev` and `main`; `main` was `7e96fd3de`, and [PR 1035](https://github.com/collisionengineers/pegasus/pull/1035) (`dev` to `main`) reads merged. Manifest schema 3 SHA-256 `C9256636ED79FC8D8F3E45FE1EE11845C7EB317C43A0793E046AA667073FB0A7`. `web.zip` SHA-256 `F713B587E920E8FACF70E1CE517B37227F9B1533338C326307A3820527C36D01`, 106,997,677 bytes. `worker.zip` SHA-256 `D864724D1DA99483BF4122E7FA195D97EF9DF29BA83B46A0D0BB78207AD5E30E`. Windows `efbundle.exe` SHA-256 `BEABE0FFA7F42A59B0F5BB959959D69328F4334528E515DBBAFCB43B15EA0254`. |
| Review and verification | PR 1024 had a code review ([review 5417875202](https://github.com/collisionengineers/pegasus/pull/1024#pullrequestreview-5417875202)). Fixes were made in PR 1034 (`af73c6200`): <br>• An assigned Review row named its Engineer "Former staff", because Review Engineer ids were never resolved. This predates PR 1024, which made it visible. <br>• The empty Owner word is now decided by row kind in one place. <br>• `NeedsAttentionItem.Owner` is non-nullable, and the duplicate Web "No owner" label is removed. <br>Two findings are left open: Find matches the placeholder words, and self-assignment depends on the dialog's first 100 staff accounts. PRs 1028 and 1033 had no review feedback. The three PRs merged without conflict, and the lost-work audit's only differences are the replaced review-fix lines. <br>**CI:** PR 1034 passed all 11 jobs at `af73c6200`, and each PR passed CI at its head. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. <br>**Operator approvals (5 October 2026):** merge authority with the request, then the exact manifest, migration and deployment targets. |
| Schema and grants | Additive. The deployed head was `20261005120000_GrantWorkerCaseManualChases`. The bundle applied `20261005150000_PrincipalDefaultFee` (18:09:39Z to 18:09:49Z): `Principals.DefaultFee` `decimal(18,2)` NOT NULL with default `180.0`, and `CK_Principals_DefaultFee` (`> 0`). There was no grant change; bootstrap verified 713 catalogued permission/denial rows and 499 effective runtime DML rows, as in Release 85. Live head `20261005150000_PrincipalDefaultFee`. |
| Web and Worker deployment | Provision (18:10:32–18:10:44Z) reported no changes. B1 quota in `uksouth` read 3, and the Worker `Disabled` settings rendered `false`. `az webapp deploy` (deployment `dabede02-2f88-4bd8-b6d3-ee89656a6f0a`, package `20261005181052.zip`) started 18:10:44Z. The exact SHA answered `/health/ready` and `/diagnostics/version` at 18:15:27Z. The Worker ZIP was deployed by 18:17:58Z (deployment `60a40472-cc70-4c5c-a37c-8dd3bacf84f6`). |
| Production smoke | Passed at 18:20:00Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261005181052.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-05T18:15:00Z`; the active Graph subscription expires `2026-10-08T13:10:00Z`. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1024: the Work Centre Owner column on an assigned Review row shows the Engineer's name; the open row and dialogs carry no Assign to me.<br>• PR 1028: the QDOS26062 Laird report must be filed again, or marked by staff, before its Original report cells fill.<br>• PR 1033: Contacts › Report generation shows £180.00 for each Principal, and the next new Case starts with that agreed fee, tagged Principal.<br>The Release 81 to 85 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-86-e50fbd6c`; the build, migrate and deploy drivers and their logs at `artifacts/releases/release-86-driver`. |

## Release 85 — 5 October 2026 (deployment live)

Release 85 deployed [PR 1022](https://github.com/collisionengineers/pegasus/pull/1022). It merged four PRs into `dev` together with the fixes their reviews and CI found:

- [PR 1016](https://github.com/collisionengineers/pegasus/pull/1016):
  - Compose and the message-page reply have one Case / PO field; Reconcile is gone.
  - The Sent poll reads a staff send's operation from its Message-ID, retains every Sent item under Sent Items, and writes `correspondence_sent` for an observed general correspondence send.
  - Case Correspondence lists the Case's Sent items.
- [PR 1018](https://github.com/collisionengineers/pegasus/pull/1018): the Triage Case page is v31 C (contact sheet, Record finding, Reply with finding, a Correspondence tab under Files).
- [PR 1019](https://github.com/collisionengineers/pegasus/pull/1019): the Work Centre is design A, with one Dismiss on every row and the Activity figures. It closes [issue 1017](https://github.com/collisionengineers/pegasus/issues/1017).
- [PR 1020](https://github.com/collisionengineers/pegasus/pull/1020): Send chaser in the Case Actions menu opens the composer addressed, titled and worded from the Case chaser template. A chaser that reaches Sent records the Case's chase. The Cases list drops the Audit pill.

The route was the normal App Service route with an additive migration, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. [PR 1015](https://github.com/collisionengineers/pegasus/pull/1015) is held by operator order and is not included.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `7e96fd3dea1053c9867ae1ee6c55fcd7591b6013` (the merge of PR 1022 into `dev`; its tree equals the PR head `26d1b31f4` that CI tested). Promoted atomically to both `dev` and `main` at 12:32:57Z; `main` was `389484dfd`. Manifest schema 3 SHA-256 `C66E421D1570D9C7BC4CEC50D6A7854776A6F26DDDF06BCC62ED29549F4A2C52`. `web.zip` SHA-256 `BCF7140367467409580FD5EE7796184940C3C6629C20F5704CF15BED23E44C33`, 107,016,697 bytes. `worker.zip` SHA-256 `0BE972C2BC009B7184E153618721031D9FA1F197274596A771F9152B39A99F28`. Windows `efbundle.exe` SHA-256 `D8FC5614F7AE0EF69CE558E894F8722BA9E0C9B5108C9B473F194338F78EF586`. |
| Review and verification | Each PR had a code review. **Fixes on the PR branches:** <br>• **PR 1016**, shard 6 (`StaffCorrespondenceWebTests.RetainedReplyRejectsAStaleCaseContextWithoutSending`): a reply whose Case / PO field was blank sent against the message's Case at its live version. It is now held to the version the page showed. <br>• **PR 1016**, review findings: the EF model and snapshot now key `RetainedMailboxMessages.MailboxId` to `ApprovedMailboxes`, as the migration's SQL does. The Message-ID is unique per folder, so a Sent copy of a received message is its own Sent row. A contradicting Sent item is quarantined rather than holding the cursor. Sent Items takes no category, its search reads the retained subject, sender and text, and its empty list no longer shows the Inbox sentences. <br>• **PR 1018**, review findings: a reload on Files › Correspondence reopens it (`#triage-files-<tab>`). FRD-19 and FRD-16 no longer offer Crop and Tag on a Triage Case. The PRD and architecture map say Reply with finding. <br>• **PR 1019:** three `WorkCentreWebTests` were updated to design A's markup and the Activity section. Shard 5's `glass.transport.failed` pair was the known cold-runner sign-in regex timeout. <br>• **PR 1020:** the standalone Audit list test listed a Case that existed only in a fake search, so the quick detail's read failed and the page said "Cases are unavailable". The test now seeds a real Case. <br>**Merge into PR 1022:** 1016 conflicted with 1018 (`EfCaseQueryStore`, `Details.Triage.cs`, `_TriageCase.cshtml`) and with 1020 (`Compose`, `_ComposeForm`, `EfStaffMailSendStore`, current-architecture). The conflicts were resolved in PR 1022 and its local build had 0 errors. <br>**Least-privilege check before release:** the Worker role had no permission on `CaseManualChases` and no read of `AspNetUserRoles`/`AspNetRoles`, so the first Case chaser observed in Sent would have failed with error 229. PR 1020 added migration `20261005120000_GrantWorkerCaseManualChases` (SELECT, INSERT; DELETE stays denied) with its bootstrap rows and a grant test. The chase now takes the roles the prepared send recorded in `ActionHistory`. The Web role already holds SELECT on every table the Activity figures and chaser recipients read. <br>**CI:** PR 1022 passed all 11 jobs at `26d1b31f4`, and each PR passed CI at its head. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. <br>**Operator approvals (5 October 2026):** merge authority, then the merge, release and deployment, without further approvals. |
| Schema and grants | Additive. The deployed head was `20261002105641_WorkCentreDismissals`. The bundle applied `20261005090000_CorrespondenceSentEvent` and `20261005120000_GrantWorkerCaseManualChases` (12:41:55Z to 12:42:07Z). The first re-keys the retained message's mailbox key to `ApprovedMailboxes`, makes the Message-ID unique per mailbox and folder, and adds `correspondence_sent` to the `CaseWorkflowEvents` per-version exemption. Each replaced key or index is equal or weaker, no data or column was removed, and the old bytes run against it unchanged. Bootstrap verified 713 catalogued permission/denial rows and 499 effective runtime DML rows. Live head `20261005120000_GrantWorkerCaseManualChases`. Read-back: the Worker holds SELECT and INSERT on `CaseManualChases` and is denied DELETE; `IX_RetainedMailboxMessages_MailboxId_FolderScope_CanonicalInternetMessageIdentity` is unique and filtered; `FK_RetainedMailboxMessages_ApprovedMailboxes_MailboxId` exists. |
| Web and Worker deployment | Provision (12:43:01–12:43:11Z) changed nothing: B1 quota in `uksouth` read 3, and the six-name `Disabled` census stayed `false`. `az webapp deploy` (deployment `55c48a41-ecd6-4b13-9ac2-e6468bb95e00`, package `20261005124319.zip`) started 12:43:11Z. The site started after 196 s, and the exact SHA answered `/health/ready` and `/diagnostics/version` at 12:47:27Z on the first read. The Worker ZIP was deployed by 12:50:06Z (deployment `3f17d5d5-4ff6-4a38-9dd1-f2c2ecd752e7`). |
| Production smoke | Passed at 12:51:54Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261005124319.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-05T12:50:00Z`; the active Graph subscription expires `2026-10-08T13:10:00Z`. |
| Still owed | Live proofs of the changes themselves:<br>• PR 1016: one Compose send on `a.QDOS26059` with the Case / PO typed in full, reaching `Submitted` and then `Sent` within a minute, listed under Sent Items and on the Case's Correspondence and Notes. The two earlier 5 October operations stay `Submitted`.<br>• PR 1018: a walk of the Triage Case page (Record finding with Complete Triage, Reply with finding, Correspondence reload).<br>• PR 1019: Dismiss on each Work Centre tab, and the Activity figures against known counts.<br>• PR 1020: one Send chaser from a Not ready Case reaching Sent and recording the chase as the Worker.<br>An empty Sent Items list shows only its count: there is no approved sentence for it, and the operator decides whether it needs one. The Release 81 to 84 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-85-7e96fd3d`; the build, migrate and deploy drivers and their logs at `artifacts/releases/release-85-driver`. |

## 5 October 2026 — first staff sends reach Submitted and stop; Reconcile fails

With the Exchange grant in place the operator sent two Compose messages from `instructions@collisionengineers.co.uk` on Case `a.QDOS26059` (07:08:59Z and 07:12:09Z). Both arrived. Pegasus showed neither under Sent Items, under the Case's Correspondence or Notes, and the composer's Reconcile did nothing visible.

| Observation | Value |
| --- | --- |
| Send journal (SQL) | `StaffMailSendOperations` `2d081866…` and `e101bc7b…`: `Submitted`, stage `ObserveSent`, `LastError` null, never `Sent`. `ActionHistory` holds prepared, draftcreating, draftready, sending, submitted for each. |
| Sent poll (SQL) | `ApprovedSentPollOutcomes` holds both items (07:10:00Z and 07:14:00Z), `Discovered`, `Unmatched`, `AuthoritativeCaseIdentitiesJson = []`, `InternetMessageIdentity = <{operation id:N}@pegasus.invalid>`. The Worker ran `SentEvidencePollFunction` every minute (261 runs in 24 h to the telemetry cap at 04:04Z). |
| Cause of Submitted never becoming Sent | `Unmatched` with no failure code is reached only when the item carries no operation marker: the `X-Pegasus-*` headers were not on the Sent item's MIME while the Message-ID Pegasus assigned was. Not read back directly (no mailbox read from the workstation); the fix reads the operation from the Message-ID as well, so it does not depend on which. |
| Reconcile (HTTP, console) | `POST /Inbox/Compose?handler=Reconcile` at 07:09:08Z and 07:12:33Z both `500`. The Web process ran the Worker's Sent poll in-process and `EfSentEvidencePollStore.RecordOutcomeAsync` failed with SQL error 229 (INSERT denied): `pegasus_web_runtime_role` holds SELECT only on `ApprovedSentPollOutcomes` (migration 20260729183000). |
| Sent Items and the Case | Nothing wrote `RetainedMailboxMessages.FolderScope = 'sent'`; Correspondence read intake receipts only; Notes read `CaseWorkflowEvents`, which a send never wrote. |
| Code change | PR `task/inbox-composer-sent-evidence`: the composers' two Case fields become one Case / PO field that searches as staff type and takes a typed reference; Reconcile is removed from Compose, the message page and the Triage reply panel; the Sent poll reads the operation from the Message-ID, retains every Sent item under the Sent Items scope, and an observed general correspondence send writes `correspondence_sent` to the Case's history (migration `20261005090000_CorrespondenceSentEvent`: the index filter gains the event, and the retained message's mailbox key moves from `ApprovedInboxPollStates` to `ApprovedMailboxes`, since a Sent-only mailbox has no Inbox poll state; no data changes); Case Correspondence lists the Sent items of the Case's sends. The Inbox's Sent rows carry no processing chip and no "Not yet processed" classification. |
| Still owed | After release: one Compose send to `digital@collisionengineers.co.uk` on `a.QDOS26059` using the one field, typed in full, reaching `Submitted` and then `Sent` within a minute, listed under Sent Items and on the Case's Correspondence and Notes. The two 5 October operations stay `Submitted`: the poll cursor has passed their items and nothing re-reads them. |

## 2 October 2026 — staff send refused by the mailbox, and the Exchange grant

The operator enabled staff-send on `instructions@collisionengineers.co.uk` at 13:08:25Z (`AllowStaffSend`, `IsDefaultStaffSend`, verified limit 100,000,000 bytes) and then tried to send. Both attempts failed; the Release 80 Compose dialog could only say "No confirmation received".

| Observation | Value |
| --- | --- |
| Send journal (SQL) | `StaffMailSendOperations` holds two operations, both `Failed` at stage `CreateDraft` with `LastError = graph_rejected_403`: 13:58:12Z (`CaseReport`, `a.QDOS26059`) and 17:01:59Z (`GeneralCorrespondence`). `ActionHistory` records prepared, draftcreating, failed for each. Telemetry showed nothing: the workspace reached its daily cap at 03:52Z. |
| Exchange read-back before the grant | The Web service principal `f3b032cc-7591-4ea8-bd68-d165578c576f` held one Exchange Application RBAC assignment, `Application Mail.Read`, scoped to `Pegasus Production Instructions Mailbox`; `Test-ServicePrincipalAuthorization` on `instructions@` returned `Mail.Read` only. The Worker service principal is the same. Neither identity holds any Microsoft Graph directory app role, as intended. |
| Cause | Compose creates a Graph draft in the mailbox and then sends it. Both need the scoped `Application Mail.ReadWrite` and `Application Mail.Send` that the runbook requires for staff sending; the 14 September grant added read only. |
| Tenant change (done) | The operator granted `EXCHANGE GRANT AUTH` for exactly two scoped assignments, run as the Digital Operator (Exchange Administrator) through the Exchange admin API: `Application Mail.ReadWrite` (`Pegasus Production Web Instructions Mail ReadWrite`, created 19:31:25Z) and `Application Mail.Send` (`Pegasus Production Web Instructions Mail Send`, 19:31:27Z), both for the Web service principal on the existing scope `Pegasus Production Instructions Mailbox`. Read-back: the Web identity now holds `Application Mail.Read`, `Mail.ReadWrite` and `Mail.Send` on that scope and nothing else; `Test-ServicePrincipalAuthorization` lists all three as `InScope True` for `instructions@collisionengineers.co.uk` and `InScope False` for `desk@collisionengineers.co.uk`. The Worker still holds `Application Mail.Read` only. Log at ignored `artifacts/exchange-grant-2026-10-02/exchange-grant.log`. |
| Code change | PR 1006 (Release 82) already shows a refused send as Failed. PR `task/compose-send-feedback` adds the reason in operator words under that state on both composers, logs each refusal with its operation id and failure code, and corrects the Compose dialog: the focus ring stays inside the scroll area and the Message box absorbs spare height instead of scrolling. |
| Still owed | Release 82 is live with the grant in place, so the PR 1006 proof can run now: one Compose send to `digital@collisionengineers.co.uk` reaching `Submitted` and then `Sent` with the Sent item linked to its Case. After this PR's release: the dialog screenshots at 1580×1000 and a smaller desktop height. |

## Release 84 — 3 October 2026 (deployment live)

Release 84 deployed [PR 1012](https://github.com/collisionengineers/pegasus/pull/1012), the operator's option D for busy buttons:

- The busy spinner is the Refresh icon's mechanism: the Lucide `icon-loader` glyph turned by the one `pegasus-spin` keyframe at 1 s, in place of the Release 83 border ring, which the operator saw sitting still as a half-circle while Refresh turned.
- After five seconds the busy words gain the Still word ("Still saving…").
- An action answered in place that succeeds holds a tick and its done word (Saved, Generated, Downloaded, Exported, Valuation received) or its own label for 1.4 s on the pressed button, or on the button drawn in its place when the Case redraws. Failures and pages that reload show no tick.

The route was the normal App Service route with the migration identity unchanged, run from the Windows workstation; no SQL step ran. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `389484dfd9029c2719ab32d28bfea1409722b181` (the merge of PR 1012 into `dev`). Promoted atomically to both `dev` and `main` at 11:44:34Z; `main` was `be444d33b`. Manifest schema 3 SHA-256 `2B035E69C0BC3063B815BEDCC4DF4ADE975EA877934BE3642C2A93A34D60BC55`. `web.zip` SHA-256 `56F89500CCAC4AB5A20A4B34D2367FC4BDD6D10140BC141C3CF8F67B8F07810D`, 106,939,409 bytes. `worker.zip` SHA-256 `F46B23DF0ECDEEAEB27CE594668363DC9FD1D9C3431476E73524A573C26314B0`. Windows `efbundle.exe` SHA-256 `6A51DAC6627A0F4E34C7152FC04AA559619DFCA2482389330442BA0CFAE1D61C`, built and not run. |
| Review and verification | PR 1012 passed every CI job at its head `8dc401dd8` (changes, invariants, unit, six SQL shards and coverage). Before the PR, a headless-Chrome harness over the real `site.js` and `site.css` with virtual time (32 checks, retained at ignored `artifacts/0310-busy-option-d`) showed the glyph's `pegasus-spin` running at 1 s, no animation under forced reduced motion, the Still words at 5 s, the tick on the pressed and on a redrawn id-less form's button, restore after the hold, and an icon-only button keeping its accessible name. The Local, Artifact, PreDeploy and PreProvision gates passed. The operator ordered the merge to `dev` and the release in one message (3 October 2026). |
| Schema and grants | Unchanged. The manifest identity `20261002105641_WorkCentreDismissals` equals the deployed head; no migration or bootstrap ran. |
| Web and Worker deployment | Provision ended at 11:54:01Z and changed nothing: B1 quota in `uksouth` read 3, the six-name `Disabled` census stayed `false`. `az webapp deploy` (OneDeploy `b1b50429-f478-411b-8991-57873ffac24f`, package `20261003115412.zip`) started 11:55:32Z; the site started after 226 s and the exact SHA answered `/health/ready` and `/diagnostics/version` at 11:59:39Z on the first read. The Worker ZIP was deployed at 12:02:58Z (deployment `cf10ab2f-82d6-467f-b74b-44149a6327a7`). |
| Production smoke | Passed at 12:06:49Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261003115412.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-03T12:05:00Z`; the active Graph subscription expires `2026-10-08T13:10:00Z`. |
| Still owed | The operator's live walk of option D: Generate report, a Send and a Reports download, watching the glyph turn, the Still words on a slow action and the tick on success; and a DevTools look at why the Release 83 ring looked still on the operator's machine, since its CSS carried the same keyframe. The Release 81 to 83 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-84-389484df`; the build and deploy drivers and their logs at `artifacts/releases/release-84-driver`. |

## Release 83 — 2 October 2026 (deployment live)

Release 83 deployed [PR 1011](https://github.com/collisionengineers/pegasus/pull/1011), which merged two PRs into `dev` together with the fixes their reviews found:

- [PR 1009](https://github.com/collisionengineers/pegasus/pull/1009): a refused staff send names its reason under the Failed state on both composers, and the refusal is logged (event 7401). The Compose dialog stays inside its frame.
- [PR 1010](https://github.com/collisionengineers/pegasus/pull/1010): every action button shows a busy label and ring until its result arrives; slow preview links and downloads do too. The Case-page EVA ZIP export now returns its file.
- Review fixes in PR 1011: a Case action queued behind a second commit returns to idle when that commit is refused; a failed download shows operator wording, not the browser's fetch error; the Compose Message box no longer shrinks below its textarea; the content-invalid and authorisation-lost reasons name their real causes.

The route was the normal App Service route with the migration identity unchanged, run from the Windows workstation; no SQL step ran. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `be444d33ba55fec4c6ef92ca2509d17161a41866` (the merge of PR 1011 into `dev`; its tree equals the PR head `cb9c0f970` that CI tested). Promoted atomically to both `dev` and `main` at 21:59:36Z; `main` was `60bfdb832`. Manifest schema 3 SHA-256 `A59F5F51A724EC2300E956155480AB5B24D43B8738544A9DBFE688919195406B`. `web.zip` SHA-256 `7DB3A664A403981B35CED63C4E911B2718CF308176A4FFC470E164F5DA214384`, 106,934,509 bytes. `worker.zip` SHA-256 `268DF66B9C2986BE49E79D7F97394CC6849D3057F95ED16B70E07F5AFF84D5D8`. Windows `efbundle.exe` SHA-256 `8CC148477835A77227D94F07C852DDDE3C1DCF7A48C264E776364230C558F6E7`, built and not run. |
| Review and verification | PRs 1009 and 1010 each had an independent code review; neither found a blocker. PR 1009 passed CI at its head. PR 1010's shard 3 failed twice on `CaseValuationV26WebTests.GlassesGetValuationAnswersTheFiguresAndFilesItsReportAfterwards` ("Collection was modified"): the test read the Glass fake's request list while the background report fetch was still adding to it. The test now reads it after filing completes. PR 1011's first run then failed once on `MailWorkspaceWebTests.MessageDetailShowsTheBodyAttachmentsThreadOutcomeAndTheWayBack`, because a random operation key contained `2048`; that check now matches the byte count only when it stands alone. CI at `cb9c0f970` passed all jobs. The Local, Artifact, PreDeploy and PreProvision gates passed. The operator granted merge authority and ordered the release (2 October 2026). |
| Schema and grants | Unchanged. The manifest identity `20261002105641_WorkCentreDismissals` equals the deployed head; no migration or bootstrap ran. |
| Web and Worker deployment | Provision took 13 seconds (22:07:21–22:07:34Z) and changed nothing: B1 quota in `uksouth` read 3, the six-name `Disabled` census stayed `false` and the four schedules unchanged. `az webapp deploy` (OneDeploy `cfc920a7-6263-4b75-ac5e-e4324f7cd300`, package `20261002220749.zip`) restarted the site; the exact SHA answered at 22:12:38Z. The Worker ZIP was deployed at 22:15:25Z. |
| Production smoke | Passed at 22:17:04Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261002220749.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-02T22:15:03Z`; the active Graph subscription expires `2026-10-08T13:10:00Z`. |
| Still owed | The live proofs of the changes themselves:<br>• PR 1009: one Compose send to `digital@` reaching Sent now that the Exchange `Mail.ReadWrite`/`Mail.Send` grant exists, and Compose dialog screenshots.<br>• PR 1010: a live walk of busy buttons on the Case page, Triage, the composers and Glass's paths, and one EVA ZIP export from the Case page.<br>The Release 81 and 82 proofs remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-83-be444d33`; the build and deploy drivers and their logs at `artifacts/releases/release-83-driver`. |

## Release 82 — 2 October 2026 (deployment live)

Release 82 deployed [PR 1008](https://github.com/collisionengineers/pegasus/pull/1008), which merged five PRs into `dev` together:

- [PR 1004](https://github.com/collisionengineers/pegasus/pull/1004): the Case aside's Report not ready list runs in page order, section by section and field by field (FRD-16).
- [PR 998](https://github.com/collisionengineers/pegasus/pull/998): a plate Glass's does not know is `glass.lookup.notfound` after one search; the launch inserts a placeholder vehicle (ADR-0062) and the return rule accepts an absent or Case plate and mileage. Get valuation on such a plate stays unavailable.
- [PR 1005](https://github.com/collisionengineers/pegasus/pull/1005): the Next action rail states the viewed work's step; Create audit is the next step after the sent Inspection report; the Audit view's Report section shows the Audit report alone.
- [PR 1007](https://github.com/collisionengineers/pegasus/pull/1007): the Inspection view edits the Inspection's own work under the one Case lease; an Inspection edit runs no Case-level effect once the Audit exists; the sent Inspection report can be generated, prepared and sent again. Glass's, Vehicle lookup, AI market research and Send to AI are still omitted in that view.
- [PR 1006](https://github.com/collisionengineers/pegasus/pull/1006): Compose Send reaches its handler (the button's `formaction` is honoured only when the attribute exists); Find a Case is an autocomplete picker in Compose and the Inbox message panel; a refused send is shown as failed instead of a 500; a preparation whose send failed or was cancelled can be prepared again.

The route was the normal App Service route with the migration identity unchanged, run from the Windows workstation; no SQL step ran. Web and Worker are Running on the approved release, and full production smoke passed. PR 998 had been held after Release 81 for a stricter review; the operator's instruction of 2 October to merge every open green PR covered it, and its assumptions to confirm stay listed on the PR.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `60bfdb832a6ceb71efbcd42f24f171606eb19b9d` (the merge of PR 1008 into `dev`; its tree equals the PR head `46cf3d03b` that CI tested). Promoted atomically to both `dev` and `main` at 16:11:22Z; `main` was `c86ddd207`. Manifest schema 3 SHA-256 `65B654CD37499241081A15E557735951978C9F9E8187B348318182516F4633F9`. `web.zip` SHA-256 `9341452A1E0F4F1C64182870193904907311CBB0F4D7BF608E32AA6664BF7A1C`, 106,903,786 bytes. `worker.zip` SHA-256 `922BE0A6D637D4D319F53B28147D1D5843FE3C751FBC79F9E14A12C50F615506`. Windows `efbundle.exe` SHA-256 `6E71FDB1F33266C1397AB283B6290D6ACC6189508EE025372DB5690ED4D83C19`, built and not run. |
| Review and verification | PRs 998, 1004 and 1005 passed CI at their heads. The first run on PR 1008 (`5f2b8ac28`) was red on two shards with the same failures as the heads of PRs 1006 and 1007: `StaffCorrespondenceWebTests.ComposeCaseOptionsListMatchingCasesAsSelectActions` (the Case picker listed Cases for a blank term; fixed in `e6d8958b9`, the normaliser now refuses a blank term in Compose and the message page) and the two `CaseVehicleSaveWebTests` pins (PR 1007 made the save write the requested work and stale that work's report alone, 34 commands instead of 35; pinned in `d41cdbb2a`). Both fixes were merged into PR 1008; CI run 37028067251 at `46cf3d03b` passed all 11 jobs, and the fixed heads of PRs 1006 and 1007 passed too. One merge conflict in `_CaseReport.cshtml` (PR 1007's dropped `inspectionOpen` term against PR 1006's `!preparationSpent`) was resolved keeping both. `Invoke-Verification.ps1 -WhatIf` on the union listed 26 owning classes (51.9 minutes) and left them to CI; 569 docs' links resolved. The Local, Artifact, PreDeploy and PreProvision gates passed. The operator granted merge authority and the Azure writes in separate approvals (2 October 2026). PR 1007 was closed by hand after the merge: GitHub refused to retarget it to `dev` because every commit was already there. |
| Schema and grants | Unchanged. The manifest identity `20261002105641_WorkCentreDismissals` equals the deployed head; no migration or bootstrap ran. |
| Web and Worker deployment | Provision took 11 seconds (16:26:40–16:26:51Z) and changed nothing: B1 quota in `uksouth` read 3, the six-name `Disabled` census stayed `false` and the four schedules unchanged. `az webapp deploy` (OneDeploy `aa235b5b-31b9-41e4-918b-f161afa566ed`, package `20261002162659.zip`, sync polling from 16:27:34Z, site started after 237 seconds) restarted the site; one 30-second probe timed out during the start and the exact SHA answered at 16:32:32Z. The Worker ZIP was deployed (deployment `b7927919-0ed9-4ab5-9526-9760daef56f7`, 16:35Z). |
| Production smoke | Passed at 16:35:56Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261002162659.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-02T16:35:03Z`; the active Graph subscription expires `2026-10-08T13:10:00Z`. |
| Still owed | The live proofs of the changes themselves:<br>• PR 1005/1007: a browser walk of the Inspection view on an Inspection + Audit Case (Edit, Save, Generate report again, the report opening on arrival), and whether the operator's "Generate report does not open the report" reproduces.<br>• PR 1006: one Compose send and one report send observed as Sent by the Worker poll, once the Exchange `Mail.ReadWrite`/`Mail.Send` grant exists; until then every send shows as failed.<br>• PR 998: the first live return on an unknown plate (read the "returned a placeholder estimate identified as type number" log line).<br>• PR 1004: the aside's blocker order on a Case with blockers across sections.<br>The Release 81 proofs (vehicle-age sentence, Add evidence JPEG, Dismiss on each tab, reason bank layout) remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-82-60bfdb83`; the build and deploy drivers and their logs at `artifacts/releases/release-82-driver`. |

## Release 81 — 2 October 2026 (deployment live)

Release 81 deployed [PR 1003](https://github.com/collisionengineers/pegasus/pull/1003), which merged four PRs into `dev` together:

- [PR 999](https://github.com/collisionengineers/pegasus/pull/999): in Settlement edit mode the unroadworthy reason bank sits under the reason box in the field column (one scoped CSS rule).
- [PR 1000](https://github.com/collisionengineers/pegasus/pull/1000): Get valuation on a vehicle Glass's does not value because of its age shows the approved blue sentence (`glass.valuation.vehicle_age`) instead of the red unavailable notice; every other failure is unchanged.
- [PR 1001](https://github.com/collisionengineers/pegasus/pull/1001): a JPEG or PNG added to a Case with Add evidence, or linked to one from Unidentified or the upload review, is filed as an `Image` and drawn on the Images tab; the role rule checks the format before the kind.
- [PR 1002](https://github.com/collisionengineers/pegasus/pull/1002): every Work Centre row has Dismiss. A dismissal applies for everyone, hides only the current occurrence of that record in all three tabs, and the row returns when the record next qualifies (FRD-15). New table `WorkCentreDismissals`.

The route was the normal App Service route with an additive migration, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. The intake test estate was wiped just before the release (below). [PR 998](https://github.com/collisionengineers/pegasus/pull/998) (Glass's unknown plate) is held open for a stricter review by operator order; it was rebased onto this `dev` and lists its HAR evidence in a PR comment.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `c86ddd2077466ba87d3dca15e95a899c3f1e57a4` (the merge of PR 1003 into `dev`; its tree equals the PR head `09a3231eb` that CI tested, plus the prose commit `e081e15a5`). Promoted atomically to both `dev` and `main` at 12:26:58Z; `main` was `3c0e5ec36`. Manifest schema 3 SHA-256 `557784F6CDB02C6D9D19441225BC4774249C60F7A32938915290CCA7F007B231`. `web.zip` SHA-256 `48B4CA12422063A2D86DA6133388A8D76F4F046E634D8B4A67BD782E9FA29A8D`, 106,883,144 bytes. `worker.zip` SHA-256 `2C35DDBA2937C17722B5C15A2D8B4E70513937684E10D17B544EFA08B77A68BA`. Windows `efbundle.exe` SHA-256 `A15D1F9D5FC3416750F74015FEC51110BA716F1608AFA628B7B2A66BBF6F63EF`, built and run. |
| Review and verification | Each PR passed CI at its own head. PR 1000's `sql-integration (5)` failed once on `GlassRepairEstimateGatewayTests.ADifferentCallbackQueryForTheSameSessionIsRefusedAndChangesNothing` with `glass.transport.failed` before the callback; the diff does not touch the launch path, the scripted provider never throws, and the likely cause is the 100 ms match timeout on the sign-in `CsrfToken()` regex on a cold runner (`RegexMatchTimeoutException` is a `TimeoutException`). The rerun passed. CI run 37003329247 on PR 1003 passed all 11 jobs at `09a3231eb`. CI run 37006723650 on the push to `main` had every job green except SQL shards 1 and 6, still running when this was recorded. `Invoke-Verification.ps1` on the union: 568 docs' links, change classification and 184 migrations' grants passed; the 16 owning classes (23 minutes) were left to CI. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. The operator granted the dev merge, the wipe, merge authority and the five Azure writes in separate approvals (2 October 2026). Codex security review posted no findings on any of the five PRs. |
| Intake wipe | Ordinary wipe (no test-estate reset), run at 12:24Z with the Worker read back `Stopped` at 12:24:10Z; the Web App stayed Running. The fresh dry run found 225 blobs (237,472,209 bytes) in `pegcustody252ow37gij/transient-intake` and 128 tables, with the preserve list 38/38 found (39 effective), and 89 tables to wipe holding 1,875 rows (8 Cases). Blobs remaining: 0. One SQL transaction reported 1,876 affected rows, including the cutoff update. Wiped tables still holding rows: 0. Preserved rows after: 686. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were unchanged at 57/10/1. `ValuationPresets` and `EmailTemplates` were 0/0, and built-in image tags 5/5. Committed mail cutoff: `2026-10-02T12:24:29.8423510+00:00`; mailbox approval and activation times were unchanged. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. The Worker read back `Running` at 12:24:57Z. |
| Schema and grants | Additive. The deployed head was `20261001110000_StaffNotificationCancellationCause`; the bundle applied `20261002105641_WorkCentreDismissals` at 12:40:54Z (one `CreateTable` and its grants). `Invoke-AzureDatabaseBootstrap.ps1` verified 711 catalogued permission/denial rows and 497 effective runtime DML rows. The live head read back equal to the manifest identity. `sys.database_permissions` on `WorkCentreDismissals`: `pegasus_web_runtime_role` GRANT SELECT, INSERT, UPDATE and DENY DELETE; `pegasus_worker_runtime_role` DENY DELETE. |
| Web and Worker deployment | Provision took 13 seconds (12:42:53–12:43:06Z) and changed nothing: B1 quota in `uksouth` read 3, the six-name `Disabled` census stayed `false` and the four schedules unchanged. `az webapp deploy` (OneDeploy `28348314-5454-4e85-ba7a-8bc5ef9e29bb`, package `20261002124312.zip`, sync polling from 12:43:36Z) restarted the site, and the exact SHA answered at 12:47:09Z. The Worker ZIP was deployed (deployment `bf918a0d-d240-4863-bb4e-1d64a071ffb4`, 12:49Z). |
| Production smoke | Passed at 12:50:27Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261002124312.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-02T12:50:00Z`; the active Graph subscription expires `2026-10-06T16:05:00Z`. The wiped estate holds no Case yet. |
| Still owed | The live proofs of the changes themselves, each on a Case created after the wipe:<br>• Get valuation on a vehicle older than Glass's values: the blue sentence with no Report a problem link, and `glass.valuation.vehicle_age` in the log. If the live `errormsg` lacks "due to the age", the red unavailable sentence shows instead and the wording needs capturing.<br>• Add evidence with a JPEG: the file has role `Image` and appears as an Images tile.<br>• Dismiss on a Needs attention, New cases and AI jobs row: the row leaves every tab and returns when the record next qualifies.<br>• Settlement edit mode at 1580 and 860 px: the reason bank wraps under the reason box.<br>PR 998 awaits its stricter review; its first live return on an unknown plate is the export evidence it still lacks. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-81-c86ddd20`; the build, migration and deploy drivers, their logs and the wipe log (`wipe-run.log`) at `artifacts/releases/release-81-driver`. |

## Intake data wipe — 2 October 2026

- Approved ordinary intake wipe: Worker `pegasus-prod-worker-252ow37gij`
  stopped for the maintenance window and read back `Stopped` at 09:08:23Z,
  then resumed and read back `Running` at 09:09:29Z. The fresh dry run found
  40 blobs (69,899,921 bytes) in `pegcustody252ow37gij/transient-intake` and
  454 rows across 89 non-preserved tables in SQL `pegasus` on
  `pegasus-prod-sql-252ow37gij` (3 Cases); the batch reported 455 affected
  rows including the mail-boundary update. The committed mail cutoff is
  `2026-10-02T09:08:51.6449061+00:00`; 39 effective tables and 686 preserved
  rows remain. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences`
  were unchanged at 49/10/1. `ValuationPresets` and `EmailTemplates` remained
  0/0, and built-in image tags 5/5. `authentication-ring`, `box-links`,
  `pegtrans252ow37gij`, Outlook and Box were untouched. No
  `-ResetTestEstate` was used. Post-run script verification reported zero
  blobs remaining and zero wiped tables holding rows.

## Release 80 — 2 October 2026 (deployment live)

Release 80 deployed [PR 997](https://github.com/collisionengineers/pegasus/pull/997), the fix for the Audit regression the operator reported after Release 79:

- The Third Party Engineer contact link and its Original report cell ([PR 990](https://github.com/collisionengineers/pegasus/pull/990)) are removed. Its contact lookup ran in the Worker and joined `ContactRoles`, a table `pegasus_worker_runtime_role` may not read; the denied SELECT was swallowed as "the report could not be read", so every standalone Audit accepted since Release 79 (for example `a.QDOS26047`, Exclusive) had its Original report cells blank apart from the verdict.
- Custody files a standalone Audit's original report as the Case's **Audit report**; the other attachments stay instruction documents. Before, that file was labelled "Instruction" for good, because a Case that already holds its report never awaits recognition and hides Mark as original report.
- The OCR lookup by file hash matches on retained output, not on the operation's state, so a retry of the work that follows a completed reading still finds the text. The scanned John R Bell Audit `a.QDOS26049` had been given up as unread during such a retry.

The route was the normal App Service route with an unchanged migration identity, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. No wipe.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `3c0e5ec36e39d253b70a488fd851732c1404e87f` (the merge of PR 997 into `dev`; its tree equals the PR head `b00eb3145` that CI tested). Promoted atomically to both `dev` and `main` at 08:30:40Z; `main` was `c79470fe7`. Manifest schema 3 SHA-256 `CFADAA9BD9CEC9C2702A1EF1BA6E59A8F59961D28F7A78CDC1EF5E179D237426`. `web.zip` SHA-256 `6493581A6F5079B384842C4F9507308D3E7701BBDD146B5843141F5F71CA32F8`, 106,841,630 bytes. `worker.zip` SHA-256 `A160D080A8454F63FA57ECA516E0DA9F856137CC8D353464881CB87761A32B62`. Windows `efbundle.exe` SHA-256 `5C67F3BC7191B1C1D7DA9781D4438502B999FD9E9047499DF90C4AD89197C073`, built and not run. |
| Review and verification | CI run 36979637247 on PR 997: SQL shard 3 failed once on `CaseDataCompletenessPersistenceTests.MissingWrongHolderWrongTokenAndExpiredLeasesNeverOverwrite` with a SQL execution timeout on the runner (the test passes locally in 39 s and is unrelated to the change); the rerun of that shard passed, and every other job passed first time. CI run 36984436506 on the push to `main` was in progress when this was recorded. Local focused runs before the PR: 32 integration, 85 Core and 41 architecture tests passed. The Local, Artifact, PreDeploy and PreProvision gates passed. The operator granted merge and release authority in one order (2 October 2026). |
| Schema and grants | Unchanged. The manifest identity `20261001110000_StaffNotificationCancellationCause` equals the deployed head, so no migration or bootstrap ran. The Worker role's grant matrix is unchanged; the change removed the Worker's read of `ContactRoles` instead of granting it. A new Worker-role test (`WorkerRuntimeReadsAStandaloneAuditsOriginalReportAtAcceptance`) runs the acceptance-time report read through the real stores under `pegasus_worker_runtime_role`. |
| Web and Worker deployment | Provision took 13 seconds (08:38:53–08:39:06Z) and changed nothing: the six-name `Disabled` census stayed `false` and the four schedules unchanged. `az webapp deploy` (package `20261002083923.zip`, 08:39:11–08:43:30Z) restarted the site, and the exact SHA answered at 08:44:03Z. The Worker ZIP was deployed (deployment `b3b6a065-b8fd-485e-bb50-63cb684fc8b0`, 08:46:42Z). |
| Production smoke | Passed at 08:47:52Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261002083923.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-02T08:45:00Z`; the active Graph subscription expires `2026-10-06T16:05:00Z`. |
| Still owed | The live proofs of the change itself:<br>• An Exclusive Audit by e-mail: `CaseAssessmentFields` carries `original_report.assessor`, `report_date`, `roadworthiness` and `outcome` recorded by `original-report-extraction` at acceptance; the `Bodyshopreport*.pdf` occurrence has role `AuditReport` and the Files tab says "Audit report"; the Original report section has four cells and no contact box.<br>• The scanned John R Bell proof carried over from Release 79, including the case where the OCR work item retries after completion.<br>`a.QDOS26047` and `a.QDOS26049` are not repaired retroactively. The Release 79 items for PR 983 remain owed. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-80-3c0e5ec3`; the promotion, build and deploy drivers and their logs at `artifacts/releases/release-80-driver`. |

## Release 79 — 2 October 2026 (deployment live)

Release 79 deployed seven PRs merged into `dev` one after another and tested together on [PR 995](https://github.com/collisionengineers/pegasus/pull/995), the `dev` to `main` pull request:

- [PR 984](https://github.com/collisionengineers/pegasus/pull/984): a scanned document page produces no image asset. A full-page raster with no text is told apart by colour: mostly paper-white is a document page and goes to OCR; anything else is a photograph and is kept ([ADR-0061](adr/0061-ocr-every-scanned-document-page.md)).
- [PR 985](https://github.com/collisionengineers/pegasus/pull/985): the third-party report reader reads each file alone, never the e-mail body, and OCR text arrives in the reader's own page-labelled shape.
- [PR 986](https://github.com/collisionengineers/pegasus/pull/986): John R Bell's report form has field rules, and an instruction letter that names the firm is no longer a report.
- [PR 987](https://github.com/collisionengineers/pegasus/pull/987): every scanned document page from a Mailbox, Manual upload or Principal API receipt is OCR'd, one operation per file. The report reader always gets the text; the instruction reader only when the Principal is still unknown.
- [PR 988](https://github.com/collisionengineers/pegasus/pull/988): an Audit whose report arrived as a scan is filled from the OCR text once it completes, through the same recognition as a readable report. Filed documents are matched by SHA-256, so a report filed at Case creation can be recognised later.
- [PR 990](https://github.com/collisionengineers/pegasus/pull/990): the recognised report's assessor links the Audit to the one active Third Party Engineer contact of that name, shown and changeable in the Original report section.
- [PR 983](https://github.com/collisionengineers/pegasus/pull/983): the image tile's tools are one panel while editing, and a report blocker in Next action opens the tab that clears it.

The route was the normal App Service route with an unchanged migration identity, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. The intake test estate was wiped just before the release (below).

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `c79470fe73cf508ef5079d19efd47ea83da194c1`. It was promoted atomically to both `dev` and `main` at 00:17:16Z; `main` was `0a066dad2`. Manifest schema 3 SHA-256 `3F4B538975F86E894F060C8E0A60FE2A677EF051AD6539F1847CE3E6DEA7F1A3`. `web.zip` SHA-256 `17C73A6FB419F3F31EBEBD1A311DF7DAE6DDA4408AD2BC8316C1E04ED1F8C98E`, 106,857,074 bytes. `worker.zip` SHA-256 `0110067B6A707A82E6E054EFF36011D103E25A069CB4BEF14084AB417CB122A9`. Windows `efbundle.exe` SHA-256 `91FA11F2D60653CCD255B14094C26668656FD1F2BF6A78FD02D77F4ED46684FC`, built and not run. |
| Review and verification | Each PR passed CI at its own head. PR 990 failed once on `ReportRequirementOwnershipTests`: its two Original report contact cells widen the automation write surface from 57 to 59 paths, and the count was updated (`77761a091`). The seven PRs were merged bottom-up through the API, each stacked PR retargeted to `dev` first. CI run 36943114121 on PR 995 passed all 11 jobs at `c79470fe7`, the release SHA itself. CI run 36945225412 on the push to `main` passed all 11 jobs at the same SHA. The Local, Artifact, PreDeploy and PreProvision gates passed. The operator granted merge, wipe and deployment authority in one order (2 October 2026). |
| Intake wipe | Ordinary wipe (no test-estate reset), run at 23:53–23:54Z on 1 October with the Worker read back `Stopped` first; the Web App stayed Running. The fresh dry run found 85 blobs (114,559,931 bytes) in `pegcustody252ow37gij/transient-intake`. It found 128 tables, with the preserve list 38/38 found (39 effective), and 89 tables to wipe holding 713 rows (4 Cases, among them `a.QDOS26043`, the scanned John R Bell example). Blobs remaining: 0. One SQL transaction reported 714 affected rows, including the cutoff update. Wiped tables still holding rows: 0. Preserved rows after: 678. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were unchanged at 46/10/1. `ValuationPresets` and `EmailTemplates` were 0/0, and built-in image tags 5/5. Committed mail cutoff: `2026-10-01T23:54:05.5832189Z`; mailbox approval and activation times were unchanged. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. The Worker read back `Running` at 23:54:37Z. |
| Schema and grants | Unchanged. The manifest identity `20261001110000_StaffNotificationCancellationCause` equals the deployed head, read from `__EFMigrationsHistory`, so no migration or bootstrap ran. |
| azd environment | The first PreProvision gate refused: the workstation's `.azure/pegasus-prod` lacked `GLASS_VALUATION_USERNAME_SECRET_URI` and `GLASS_VALUATION_PASSWORD_SECRET_URI`, which Release 78 set in a release worktree since removed. No Azure write had happened. The two values were read back from the deployed Web App's Key Vault references and from the vault (`glass-valuation-username/b8bc1e3d…9228`, `glass-valuation-password/5733d146…2ca5`), set in both the release and the workstation environments, and the gate passed. Every parameter the template reads is present. |
| Web and Worker deployment | Provision took 2 minutes 18 seconds (00:22:36–00:25:02Z) and left the six-name `Disabled` census `false` and the four schedules unchanged. `az webapp deploy` (OneDeploy `dc37f422-be84-4c01-a4ce-24883615e1d6`, package `20261002002520.zip`, 00:26:05Z) restarted the site. It started first time in 225 s, and the exact SHA answered at 00:30:15Z. The Worker ZIP was deployed (deployment `854f21a4-70f9-4011-8e12-50c513d6e6dd`, 00:32:55Z). |
| Production smoke | Passed at 00:35:18Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261002002520.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-10-02T00:35:00Z`; the active Graph subscription expires `2026-10-06T16:05:00Z`. The wiped estate holds no Case yet. |
| First minutes (00:25–00:40Z, read-only KQL) | The warm-up held readiness for 34 s: 32 `/health/warm` probes answered 503 at 00:28:37–00:29:11Z, as designed, and two client-closed 499s at 00:30:01–00:30:03Z were the deploy poller. One SQL login was reset by the server at 00:36:04Z (`error: 35`, `Connection reset by peer`) while the Web read the Triage list; it did not recur. The Worker's timers ran throughout (113 invocations, 00:25:00–00:37:40Z). |
| Still owed | Live checks need a scanned report after the wipe, since `a.QDOS26043` is gone:<br>• A scanned John R Bell report by e-mail: one `Completed` OCR operation per scanned file, no `embedded_image` rows for its pages, `scan-page-document` issues on the receipt, a `third-party-report:{asset}:ocr:{op}` analysis row, and the Audit's Original report cells filled with the Extracted tag.<br>• A PDF of vehicle photographs: `scan-page-photograph` issues, the photos kept, no OCR row.<br>• The Third Party Engineer contact select read and edited at 1580 px and a smaller desktop width, in Scroll and Tabs, once the operator adds the "John R Bell" contact.<br>• PR 983's Images tab tile panel and a blocker jump.<br>Operator decisions: [#991](https://github.com/collisionengineers/pegasus/issues/991), [#992](https://github.com/collisionengineers/pegasus/issues/992), [#993](https://github.com/collisionengineers/pegasus/issues/993), [#994](https://github.com/collisionengineers/pegasus/issues/994). Follow-up [#989](https://github.com/collisionengineers/pegasus/issues/989) is open. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-79-c79470fe`; the build, promotion, wipe and deploy drivers and their logs at `artifacts/releases/release-79-driver`. |

## Release 78 — 1 October 2026 (deployment live)

Release 78 deployed [PR 981](https://github.com/collisionengineers/pegasus/pull/981), which merged six PRs into `dev` together, and [PR 971](https://github.com/collisionengineers/pegasus/pull/971), already on `dev`:

- [PR 971](https://github.com/collisionengineers/pegasus/pull/971): Import as repair spec is offered only on a file the Worker recognised as an estimate.
- [PR 973](https://github.com/collisionengineers/pegasus/pull/973): MCP tools return native file content, take one-command leases, and number 36 ([ADR-0059](adr/0059-native-mcp-file-content-and-consolidated-tool-inventory.md)). Its Codex security finding on the intake-source URL is open as [#980](https://github.com/collisionengineers/pegasus/issues/980), by operator decision.
- [PR 975](https://github.com/collisionengineers/pegasus/pull/975): Create audit no longer needs the Inspection report sent. The item is always listed, greyed with its blocker.
- [PR 976](https://github.com/collisionengineers/pegasus/pull/976): choosing a Repair Spec redraws it in place.
- [PR 977](https://github.com/collisionengineers/pegasus/pull/977): Correct classification acts on its Case, its bell and its next action. The two chasing subtypes merge into `chasing-for-update`, and the bell gains `CancellationReceived`.
- [PR 978](https://github.com/collisionengineers/pegasus/pull/978): the Inspection report is generated, prepared, sent and marked sent on its own work after Create audit, and never again once sent.
- [PR 979](https://github.com/collisionengineers/pegasus/pull/979): Get valuation on the Glass's card signs in with a Key Vault-held Glass's valuation account, fills Retail and Trade, saves the vehicle to the Glass's stock list, and files the "Values Only" PDF on the Case in the background ([ADR-0060](adr/0060-glass-valuation-account-and-valuation-report.md)).

The route was the existing App Service destructive migration route, run from the Windows workstation. The operator ordered it at once, so the outage fell at 18:39–18:51Z (19:39–19:51 BST). The intake test estate was wiped during containment, before the migration. Web and Worker are Running on the approved release, and full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `0a066dad2d58417741774983896796c67b5f8448`. It was promoted atomically to both `dev` and `main`; `main` was `f2b465e56`. Manifest schema 3 SHA-256 `9982171B2B412699FA12CDDA396C3E3D4E0E0C764DA60DE8A39ACFEBAE05A314`. `web.zip` SHA-256 `7C91211295CFAF9ED096B46970A3F710EA2CF0EA6E8C8F1969D83134226ED817`, 106,819,098 bytes. `worker.zip` SHA-256 `8F38BC06B80E7CEEABCD176D37A77F3351EF5D4562A9B6889DE34111BD4940CF`. Windows `efbundle.exe` SHA-256 `AFBA4D31E53BF9EE5AFC10B103CD0151A8906AF778AFFDB5213573EE8A5666F0`, built and run. |
| Review and verification | Each PR had a review. The review findings on 977, 978 and 979 were fixed before merge:<br>• 977: the Case page reads its linked cancellation in one statement instead of the whole correspondence list, and a cancellation on a Case in Query rings as one.<br>• 978: an Inspection-view report command is refused once that report is sent, and Mark report sent returns to the Inspection view.<br>• 979: the report-link test serves the page it asserts on, and the ADR is renumbered from 0059 to 0060.<br>The union built once locally with 0 warnings and 0 errors, and its documentation, change-classification and migration-grant invariants passed. CI passed every job at the fixed heads of 977 and 979, and on PR 981, whose tree is identical to the release SHA. PR 978 passed after one rerun of shard 4: a LocalDB execution timeout in `InspectionAddressSuggestionTests`, which 978 does not touch. The Local, Artifact, PreDeploy, PreMigration and both PreProvision gates passed. The operator granted merge and deployment authority and ordered the ordinary wipe (1 October 2026). |
| Glass's valuation account | The operator chose the login captured in the HAR. Secrets `glass-valuation-username` (version `b8bc1e3d…9228`) and `glass-valuation-password` (version `5733d146…2ca5`) were created in `pegasusprodkv252ow37g` and read back equal. The Web identity `pegasus-prod-web-id-252ow37gij` was granted Key Vault Secrets User at each secret's scope. `GLASS_VALUATION_USERNAME_SECRET_URI` and `GLASS_VALUATION_PASSWORD_SECRET_URI` were set in azd. After start, both `Glass__ValuationAccount__*` Key Vault references read `Resolved` at those versions. |
| Intake wipe | Ordinary wipe (no test-estate reset), run during containment at 18:40Z. The fresh dry run found 77 blobs (46,080,093 bytes) in `pegcustody252ow37gij/transient-intake`. It found 128 tables, with the preserve list 38/38 found (39 effective), and 89 tables to wipe holding 1,057 rows (5 Cases). Blobs remaining: 0. One SQL transaction reported 1,058 affected rows, including the cutoff update. Wiped tables still holding rows: 0. Preserved rows after: 672. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were unchanged at 42/10/1. `ValuationPresets` and `EmailTemplates` were 0/0, and built-in image tags 5/5. Committed mail cutoff: `2026-10-01T18:40:34.3325814Z`; mailbox approval and activation times were unchanged. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. |
| Containment | The old Web reported the approved old SHA `f2b465e560089c30f6c9938ed7a7b5b568aa0b4e`. The Worker's Flex update strategy read `Recreate`. The Worker's Disabled-setting census was set to `true` and smoked. The new Worker package was staged, and the disabled smoke passed again. The Worker read back `Stopped` at 18:39:32Z. Web then read back `Stopped` and unserved at 18:39:40Z. The fresh read-back immediately before SQL passed. |
| Schema and grants | This migration is destructive and forward-only. Three migrations were applied over `20260930090000_PrincipalSalvageMatrix`, at 18:41:56–18:42:17Z:<br>• `20261001090000_DocumentVersionEstimateRecognition` adds a nullable bit.<br>• `20261001100000_MergeChasingSubtypes` rewrites the two retired chasing subtypes, and the history snapshots that hold them, to `chasing-for-update`. After the wipe it touched 0 rows.<br>• `20261001110000_StaffNotificationCancellationCause` widens the bell cause check constraint.<br>Bootstrap verified 706 catalogued permission/denial rows and 494 effective runtime DML rows, unchanged from Release 76. The live head read back as the manifest identity, `20261001110000_StaffNotificationCancellationCause`. |
| Web and Worker deployment | The new `web.zip` was deployed to the stopped Web App (OneDeploy `fc904d5a-6e4b-45d0-b54e-f43f808abf66`, 18:43:39Z, package `20261001184323.zip`), and the site stayed `Stopped`. Provision with the Worker disabled took 1 minute 49 seconds and added the two Key Vault reference settings; its smoke passed. The activation provision took 1 minute 11 seconds. The Web App was started at 18:48:07Z and served the exact SHA at 18:51:15Z, first time. The Worker read back `Running` at 18:51:26Z. |
| Production smoke | The first run passed every check except intake liveness, because the wipe had cleared the poll cursor. The first poll completed at 18:55:08Z, and the full smoke then passed at 18:56:41Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20261001184323.zip` SHA-256 equals the approved `web.zip`. The active Graph subscription expires `2026-10-06T16:05:00Z`. |
| First minutes (18:48–19:00Z, read-only KQL) | The only failure lines were 27 health-check failures at 18:50:33–18:51:00Z, while the start-up warm-up held readiness. They stopped before the site read ready. No exceptions were recorded on Web or Worker. |
| Still owed | Live checks once a Case exists after the wipe:<br>• Get valuation on the Glass's card: the card fills, the Glass's stock list gains a row, and after a reload Documents shows `Glass's valuation {REG} {yyyy-MM}.pdf`. It also proves that Glass's prints without the vehicle's detail pages being opened first, and accepts concurrent sessions on the account and a past-month `valdate`.<br>• Correct classification's next actions and the `CancellationReceived` bell.<br>• Create audit greyed and enabled.<br>• The Inspection report after Create audit.<br>• Repair Spec in place.<br>• Native file content through Claude.<br>Issue [#980](https://github.com/collisionengineers/pegasus/issues/980) is open. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-78-0a066dad`; the driver scripts and logs at `artifacts/releases/release-78-driver`. |

## Release 77 — 30 September 2026 (deployment live)

Release 77 deployed [PR 970](https://github.com/collisionengineers/pegasus/pull/970), which merged the fourteen optimisation roadmap PRs into `dev` together, and [PR 950](https://github.com/collisionengineers/pegasus/pull/950) (save as you go carries the next operation key forward after a commit, and after a refusal):

- [PR 953](https://github.com/collisionengineers/pegasus/pull/953): the unused Microsoft.Graph package is gone, and the CI shard table is refreshed.
- [PR 954](https://github.com/collisionengineers/pegasus/pull/954): the Box token renews in the background, a first read asks for metadata and content together, and a read keeps one temporary copy.
- [PR 955](https://github.com/collisionengineers/pegasus/pull/955): the due-work sweep and the inbox fallback poll run inside the one-minute recovery timer, every fifth minute. Six Worker functions remain, each with a `Disabled` setting. The Worker's host log chatter is dropped at source, and its instance cap is 5.
- [PR 956](https://github.com/collisionengineers/pegasus/pull/956): pre-Case image tiles are renderings, not originals.
- [PR 957](https://github.com/collisionengineers/pegasus/pull/957): eight small Web fixes. The viewer scrolls into view, a retained e-mail is named by its subject, the Work Centre does not refresh in a hidden tab, and the Inter font is served immutable.
- [PR 958](https://github.com/collisionengineers/pegasus/pull/958): fewer round trips on Service health, Image intake details, upload status, Unidentified and staff names.
- [PR 959](https://github.com/collisionengineers/pegasus/pull/959): runtime counters, request stamps, the slow connection-open phase, a memory heartbeat and start-up marks. Probe, ping and static-file request rows are dropped unless they fail.
- [PR 960](https://github.com/collisionengineers/pegasus/pull/960): fewer full-size copies in EVA, vision, thumbnails and Graph mail, and the report logo decoded once. A message Graph declares over 750 MiB goes to quarantine unread.
- [PR 961](https://github.com/collisionengineers/pegasus/pull/961): fewer SQL round trips on the Inbox, Work Centre, Case page, Save and shell. The whole-Case read is deleted.
- [PR 962](https://github.com/collisionengineers/pegasus/pull/962): a failing custody item backs off and logs why, the ten-second sweep reads less, the Worker records its own spans, and a mail already retained is not downloaded again.
- [PR 963](https://github.com/collisionengineers/pegasus/pull/963): Box filing proves the Case folder once, drops the pre-upload lookup and files three at a time.
- [PR 964](https://github.com/collisionengineers/pegasus/pull/964): a save-as-you-go commit is answered with the parts the page swaps, not the whole page.
- [PR 965](https://github.com/collisionengineers/pegasus/pull/965): the hot reads and the report renderer are kept warm every three minutes, and the readiness probe remembers a current schema.
- [PR 966](https://github.com/collisionengineers/pegasus/pull/966): filing fills the document read cache, originals are kept 14 days, and Generate looks up its photos together.

The route was the normal App Service route with an unchanged migration identity, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. No wipe ran.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `f2b465e560089c30f6c9938ed7a7b5b568aa0b4e`. It was promoted atomically to both `dev` and `main`; `main` was `4caf885c1`. Manifest schema 3 SHA-256 `76A2FB0E40B6831EB0BC71FBD4BB69E54D0267B73B1291751D2E26B9FD7D23FA`. `web.zip` SHA-256 `60418B6815F12E3747315473D63386049274B3026F250357D17E5CE6449AE437`, 106,677,297 bytes (Release 76: about 131 MB). `worker.zip` SHA-256 `6AE6523C8B42A791ADA80ED7CC1B415A3473729E5DCA7A17601F46FEDB49BD03`. `efbundle.exe` (win-x64) SHA-256 `393AAFE6973833FFA4BA5C9DD98C5AB2A058989E85F17AC91A1FAACA5B9710D6`. |
| Review and verification | Each roadmap PR passed CI on its own head and had a Codex review. PR 970 merged them onto one branch; each merge commit records what it kept where lanes met (the Work Centre and warm-up with the deleted reads, the pending-custody sweep's two new parameters, the Graph source's skip, size check and subject name, and one shared SQL statement counter). The branch built with 0 warnings and 0 errors, and `Test-GlassBrowser.mjs` passed in headless Chrome. CI run 36766039539 passed all 11 jobs at `c16badfda`, whose tree is identical to the release SHA; shard 6 was re-run once because its cache save timed out after all 650 of its tests had passed. The Local, Artifact, PreDeploy and PreProvision gates passed. The operator granted merge authority and deploy authorization (30 September 2026). |
| Schema and grants | Unchanged. The manifest identity `20260930090000_PrincipalSalvageMatrix` equals the deployed head, so no migration or bootstrap ran. |
| Web and Worker deployment | Provision applied the Worker changes in 1 min 23 s: the six-name `Disabled` census (`AutomaticEvaReviewSubmissionFunction` gained one), `SentEvidencePollSchedule` at second 0, and `maximumInstanceCount` 5 (was 20). Provision itself removed `AzureWebJobs.InboxRecoveryFunction.Disabled`, `AzureWebJobs.DueWorkSweepFunction.Disabled`, `ApprovedInboxPollSchedule` and `DueWorkSweepSchedule`, so the one-time transition in PR 955 had nothing to delete. The B1 quota in `uksouth` read 3. `az webapp deploy` (OneDeploy `35acd1b9-d77e-4624-8e6d-56262dac4b7d`, package `20260930204830.zip`) restarted the site. It started first time in 223 s, and the exact SHA answered at 20:53:14Z. The Worker ZIP was deployed (deployment `2478a8d1-1de1-48c2-8cb3-c9c7946cfec9`). |
| Production smoke | Passed at 20:57:02Z. The Worker activation smoke passed as `approved-live-worker` with the exact six-name census. Active Web package `20260930204830.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-30T20:55:06Z`, which the recovery timer's fifth-minute poll now makes; the active Graph subscription expires `2026-10-06T16:05:00Z`. |
| First minutes (20:49–20:58Z, read-only KQL) | No exceptions on Web or Worker. The start-up ping answered 503 thirty times while the warm-up held readiness, as designed. The start-up warm-up ended at its 46 s bound: key ring 15.4 s, OAuth certificates 15.6 s, and the model step still running at 45.5 s, so its new build-and-connection split was not logged on this start; the renderer warm-up was left at its 16 s bound. Runtime counters and the memory heartbeat arrive. Each Worker timer ran: recovery, EVA review submission and sent-evidence poll once a minute, and staged-artifact reconciliation every 10 s. |
| Still owed | The seven-day measurement in the roadmap's `MEASURE.md` (on the operator's workstation) and the live walk: five single-field saves with the answer in place, View bringing the viewer into view, a Triage opened from an e-mail named by its subject, Triage tile sizes, Service health and Reports twice each, Generate and Preview, and a 13-photo filing. Operator decisions: [#967](https://github.com/collisionengineers/pegasus/issues/967), [#968](https://github.com/collisionengineers/pegasus/issues/968), [#969](https://github.com/collisionengineers/pegasus/issues/969). |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-77-f2b465e5`; the driver script, build log and deploy log at `artifacts/releases/release-77-driver`. |

## Read-only subscription observation — 30 September 2026

On 30 September 2026, Azure CLI read the subscription's properties. They carry
one promotion, category `freetier`, which ends on 17 July 2027 at 15:18 UTC.
The database is Standard S0 (10 DTU) with `useFreeLimit` null, so it is not the
serverless free-limit offer. This promotion is what makes the S0 database bill
0 GBP. That link rests on the standard 12-month free-services offer. It was not
read from the usage meter. After the end date the S0 database bills about 14 GBP
a month; [ADR-0002](adr/0002-dotnet-modular-monolith-on-azure.md) prices it at
13.94 GBP. The operator confirms the date in the portal (Cost Management)
before relying on it.

## Release 76 — 29 September 2026 (deployment live)

Release 76 deployed [PR 949](https://github.com/collisionengineers/pegasus/pull/949), which merged PRs 940–948 into `dev` together with the review fixes:

- [PR 940](https://github.com/collisionengineers/pegasus/pull/940): an ordinary wipe keeps each staff member's stored external credential (the Glass's login).
- [PR 941](https://github.com/collisionengineers/pegasus/pull/941): the source of `20260929120000_PrincipalVocabulary` no longer grants Web SELECT on the domain reference tables. This matches the grants revoked by hand in Release 75.
- [PR 942](https://github.com/collisionengineers/pegasus/pull/942): a Graph mail wake is queued even if Graph hangs up, and the mail-webhook read is warmed at start.
- [PR 943](https://github.com/collisionengineers/pegasus/pull/943): Glass's additional operations land as Specialist, and included operations charge nothing.
- [PR 944](https://github.com/collisionengineers/pegasus/pull/944): Upload lists a photograph only once custody can serve it.
- [PR 945](https://github.com/collisionengineers/pegasus/pull/945): a per-Principal salvage matrix, edited in Administration › Contacts, fills the Case salvage value.
- [PR 946](https://github.com/collisionengineers/pegasus/pull/946): Web and Worker may UPDATE `CaseDataSnapshots`, so filed images complete a Case's readiness.
- [PR 947](https://github.com/collisionengineers/pegasus/pull/947): the Case edit session saves as you go (Done, no Save or Cancel).
- [PR 948](https://github.com/collisionengineers/pegasus/pull/948): the Case Actions menu works without an edit session.
- PR 949's own fixes:
  - the review findings on 947 and 948, and 948's seven CI failures;
  - Take over is offered again on a Completed or Query Case (operator ruling);
  - the FRD-18 upload wording;
  - the wipe refuses to run when the built-in image tags are missing (the guard from the 27 September restore, below).

The route was the normal App Service route with an additive migration, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. No wipe ran.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `4caf885c19ec8ea51422c9d13dee1d485e1e9c12`. It was promoted atomically to both `dev` and `main`; `main` was `3b8706bd0`. Manifest schema 3 SHA-256 `07CA7FEA6CAF4ADE98EF300EBD660BA504F98C09E03913A0DDE1806ED18DDEEA`. `web.zip` SHA-256 `816CECD87B10144481DDBA643611DACB6FC6DDD1476ECE7183166ADEB107CC3E`. `worker.zip` SHA-256 `2C0C752426D89EF79AF04165485A4F44CEB075C568F4F4E9B0E240CA6077AA3C`. Windows `efbundle.exe` SHA-256 `9A2FEAAD3D12D03B886C7B4A1F0DFE29C50C08084E23C54DD353003FB0384F44`, built and run. |
| Review and verification | The nine PRs were merged onto one integration branch with `--no-ff`. The one conflict was #945 and #946 both adding their migration to the pinned lists in `CaseWorkflowMigrationTests` and `IntakePersistenceIntegrationTests`; both were kept, in id order. Every file changed by one PR matched that PR byte for byte, and every line added to a shared file survived. The review findings were checked against the code and fixed on the branch. The branch built locally with 0 warnings and 0 errors. `Test-GlassBrowser.mjs` passed 16/16 in headless Chrome and failed on the unfixed script. PR 949's CI run 36579042625 passed all 11 jobs at `ad9fa1334`, whose tree is identical to the release SHA. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. No signed-in browser walk ran. The operator granted merge authority and approved the build and deployment (29 September 2026). |
| Schema and grants | Additive. Two migrations were applied over `20260929120000_PrincipalVocabulary` at 14:34:28–14:34:34Z. `20260929150000_GrantCaseDataSnapshotUpdate` grants both runtime roles UPDATE on `CaseDataSnapshots`, and re-queued the one `merge_image_case_custody` item that had failed with `DbUpdateException`. `20260930090000_PrincipalSalvageMatrix` adds the nullable `Principals.SalvageMatrixJson`. Bootstrap verified 706 catalogued permission/denial rows and 494 effective runtime DML rows (Release 75: 704/492; the two new UPDATE grants account for the rise). The live head read back as the manifest identity, `20260930090000_PrincipalSalvageMatrix`. The re-queued merge completed at 14:35:03Z. |
| Web and Worker deployment | Provision found no changes (11 s); the B1 quota in `uksouth` read 3. `az webapp deploy` (OneDeploy `3f66f8b7-f9aa-46ce-83f7-d2c3c4a27d00`, package `20260929143610.zip`) restarted the site. The site started first time in 225 s, and the exact SHA answered at 14:41:02Z. The Worker ZIP was deployed (deployment `af9ca4f4-cbb7-4006-8253-7928d72282e8`). |
| Production smoke | Passed at 14:45:07Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20260929143610.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-29T14:45:03Z`; the active Graph subscription expires `2026-10-02T15:15:00Z`. No `DbUpdateException` work-item failure followed the release. |
| Still owed | The live walk is with the operator: save as you go (type, see the blocker clear, press Done); an Actions-menu item from read mode, and Take over on a Completed Case; the salvage matrix filling a Case's salvage value; a Glass's Specialist line in the report; Upload photographs appearing once custody confirms them; linked-upload images completing readiness. `Test-CaseRefreshBrowser.mjs` needs a live Case and has not run against this release. Re-measure the Web start (#922): this start took 225 s. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-76-4caf885c`; driver scripts and the deploy log at `artifacts/releases/release-76-driver`. |

## Release 75 — 29 September 2026 (deployment live)

Release 75 deployed the 28–29 September issue sweep: stacked PRs [924](https://github.com/collisionengineers/pegasus/pull/924)–[938](https://github.com/collisionengineers/pegasus/pull/938), promoted through release PR [939](https://github.com/collisionengineers/pegasus/pull/939). It resolves #829 #832 #835 #842 #843 #844 #845 #846 #848 #850 #857 #864 #865 #868 #870 #879 #902 #913 #916 #919 #922 #923, and the operator's request to tag images while editing a Case. It also carries the fix for #923: since Release 74's ReadyToRun build, every Box sign-in on the Web host failed with `Module checksum failed`, because ReadyToRun rewrote the Box SDK's FIPS BouncyCastle DLLs.

The route was the existing App Service destructive migration route, run from the Windows workstation. The operator ordered it at once, so the outage fell in working hours, about 08:30–08:50 BST. Web and Worker are Running on the approved release, and full production smoke passed. The intake test estate was wiped during containment, before the migration (below).

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `3b8706bd09c682fde3394d028de001961a764317`. It was promoted atomically to both `dev` and `main`; `main` was `62438281b`. Manifest schema 3 SHA-256 `B79E74464293E95F873B8513DC1EE1124DCF27B08291DEF0B570835742FA9824`. `web.zip` SHA-256 `4B885FB0A373CD395EE816147BB3BF5759817A062DA78C9E7B6C7B3D7CE48209`. `worker.zip` SHA-256 `F86A03134B31D0C8C0A5C4B7892A911CC380EC65451A1BF521DF882C353DD0FE`. Windows `efbundle.exe` SHA-256 `933F34A9541B7FE2F7604F030F4967CF4F619CE7BCCBCD5934FA1E74611C74EB`, built and run. The build's new guard confirmed the FIPS DLLs are not ReadyToRun. |
| Review and verification | Each of the 15 stacked PRs was read by two adversarial reviewers, then a fixer applied the confirmed findings. The stack tip was built once locally: 0 warnings, 0 errors. CI ran the full lanes on the bottom (#924) and the tip (#938), and both passed. The tip's first run failed only its shard coverage check, because a theory used exception objects as data; the fix went into #926 and the rerun passed. The Local, Artifact, PreDeploy, PreMigration and both PreProvision gates passed. No browser walk ran before release. The operator granted merge and deployment authority and ordered the wipe (29 September 2026). |
| Intake wipe | Ordinary wipe (no test-estate reset), run during containment with the deployed release's script (`62438281b`), because the table names change in this release. At the operator's instruction, `UserExternalCredentials` (the stored Glass's login) was added to the preserve list, and the credential survived (1 row). The same change is PR [940](https://github.com/collisionengineers/pegasus/pull/940). The fresh dry run found 15 blobs (35,754,283 bytes) in `pegcustody252ow37gij/transient-intake`. It found 130 tables, with the preserve list 38/38 found (39 effective) and 91 tables to wipe holding 199 rows (1 Case). Blobs remaining: 0. One SQL transaction reported 200 rows affected, including the cutoff update. Wiped tables still holding rows: 0. Preserved rows after: 625. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were unchanged at 30/10/1. `ValuationPresets` and `EmailTemplates` were 0/0. Committed mail cutoff: `2026-09-29T07:36:57.5115351Z`; mailbox approval and activation times were unchanged. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. |
| Follow-up intake wipe | At 14:58Z, with Worker `pegasus-prod-worker-252ow37gij` stopped, the approved ordinary wipe cleared 128 blobs (33,801,992 bytes) from `pegcustody252ow37gij/transient-intake` and 2,711 inventoried rows across 89 non-preserved tables in SQL `pegasus` on `pegasus-prod-sql-252ow37gij`; the batch reported 2,712 affected rows including the mail-boundary update. Post-run checks found zero blobs and zero wiped tables still holding rows. The committed mail cutoff is `2026-09-29T14:58:39.6798949+00:00`; 641 preserved rows remain. `CaseSequences` 37, `ImageIntakeSequences` 10 rows and `UnidentifiedSequences` 1 were unchanged; `ValuationPresets` and `EmailTemplates` remained 0/0, and built-in image tags remained 5/5. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No `-ResetTestEstate` was used. The Worker resumed and read back `Running`; the reloaded Web UI showed 0 outstanding cases and 0 messages with “No mail has been received.” |
| Containment | The old Web reported the approved old SHA `62438281b502ad355251b26b96668dbc9eb5087f`. The Worker's Flex update strategy read `Recreate`. The Worker's Disabled-setting census was set to `true` and smoked. The new Worker package was staged (deployment `434ea3ff-7470-4857-80a8-f6809ca43d1c`), and the disabled smoke passed again. The Worker read back `Stopped`. Web then read back `Stopped`, with `/health/live` answering HTTP 403. The fresh read-back immediately before SQL passed. |
| Schema and grants | This migration is destructive and forward-only. Four migrations were applied over `20260928160000_GrantWorkerDocumentOccurrenceUpdate`. `20260929090000_RetireUnusedTables` drops `VehicleConfirmations`, `AiWorkRequests` and four `SendToAiControl` connector columns. `20260929091000_GrantWebRetainedMailDismissal` grants `pegasus_web_runtime_role` UPDATE on `RetainedMailboxMessages`. `20260929093000_MarketResearchAttachedEvent` widens a Case-history index filter. `20260929120000_PrincipalVocabulary` renames the Provider tables and columns to Principal and rewrites the stored codes. The first bootstrap refused the permission census. The rename migration had also granted the Web role SELECT on `PrincipalDomainEvidence`, `PrincipalDomainPackages` and `PrincipalReferences`, which Web never held; only the Worker reads the domain reference catalog. Those three Web grants were revoked, restoring the pre-release permissions, and the migration source was corrected in PR [941](https://github.com/collisionengineers/pegasus/pull/941). The bootstrap then verified 704 catalogued permission/denial rows and 492 effective runtime DML rows (Release 74: 714/498; the dropped tables account for the fall). The live head read back as the manifest identity, `20260929120000_PrincipalVocabulary`. |
| Web and Worker deployment | The new `web.zip` was deployed to the stopped Web App (OneDeploy `7c728c56-0d74-45de-ac32-7af784b5e49c`, 07:42:20Z, package `20260929074146.zip`), and the site stayed `Stopped`. Provision with the Worker disabled (1 minute 41 seconds) and its smoke passed. It renames the app setting `Features__ProviderApi` to `Features__PrincipalApi`. The activation provision took 1 minute 17 seconds. The Web App was started at 07:46:36Z and served the exact SHA about 185 s later, first time, with no failed start. The Worker was started at 07:49:46Z and read back `Running`. |
| Production smoke | Passed. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20260929074146.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-29T07:50:08Z`; the active Graph subscription expires `2026-10-02T15:15:00Z`. The release ran no signed-in journey check, and the wiped estate holds no Case yet. |
| Public API change | The Principal API (formerly the Provider API) is now at `/api/principal/v1/submissions`, with the Principal auth scheme and realm; the response field is `principalReference`. Any Principal client must use the new path. Credentials are unchanged. |
| Still owed | Live checks once a Case exists: Glass's Save & Exit fills the repair spec (#923), and Fetch again for a failed export (#916); tag images while editing; one-press attach (#913); the Engineer's Value flow (#879); the Report sending template (#902); Inbox Dismiss and Restore (#829). Re-measure the Web start (#922): this start took about 185 s from start to ready. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-75-3b8706bd`; driver scripts and logs at `artifacts/releases/release-75-driver`. |

## Release 74 — 28 September 2026 (deployment live)

Release 74 deployed eleven PRs merged through [PR 918](https://github.com/collisionengineers/pegasus/pull/918), plus the release-build fix [PR 921](https://github.com/collisionengineers/pegasus/pull/921):
- #895 performance, in four PRs: [908](https://github.com/collisionengineers/pegasus/pull/908) Work Centre reads, [909](https://github.com/collisionengineers/pegasus/pull/909) documents and photos, [910](https://github.com/collisionengineers/pegasus/pull/910) Case page, Save, sign-in check, compression and warm-up, and [911](https://github.com/collisionengineers/pegasus/pull/911) Glass in the background;
- [900](https://github.com/collisionengineers/pegasus/pull/900): one Due, the scale preview and the repairer VAT route;
- [904](https://github.com/collisionengineers/pegasus/pull/904): report blockers in the aside;
- [917](https://github.com/collisionengineers/pegasus/pull/917): fee note from read mode;
- [915](https://github.com/collisionengineers/pegasus/pull/915): fileless Provider submissions;
- [903](https://github.com/collisionengineers/pegasus/pull/903): original-report recognition;
- [914](https://github.com/collisionengineers/pegasus/pull/914): later-Case image pairing;
- [906](https://github.com/collisionengineers/pegasus/pull/906): comic-burst damage marks.

The route was the normal App Service route with an additive migration, run from the Windows workstation. This was the first release that adds `WEBSITE_WARMUP_PATH`, so `web.zip` was deployed and read back before provisioning. Web and Worker are Running on the approved release, and full production smoke passed. There were two Web start gaps, described below. No wipe ran.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `62438281b502ad355251b26b96668dbc9eb5087f`. `92c7ca553` was promoted first. Its build then stopped: `Build-ReleaseArtifacts.ps1` ran the Linux ReadyToRun `Pegasus.Web.dll` to read its identity, and Windows cannot load it (`BadImageFormatException`). PR 921 reads the identity from metadata instead. `62438281b` was then promoted atomically to both `dev` and `main`. Manifest schema 3 SHA-256 `AD2ED3B0FD0AF3224CFFCB21ACAF99C5A5B26864E423922767B5FFDA4F0CF63C`. `web.zip` SHA-256 `D44F65D9CBD044EF1936BED3EF656A44A8D0620EFE62EA5EE6BCC608B8F4987A`. `worker.zip` SHA-256 `6E66C0B28A5AD45C4F0BCAB52497623BC2B3FD956CEFB3A49DD58E26F2F4DF4E`. Windows `efbundle.exe` SHA-256 `183C1DDD01C529DC7A1E2D489F2D6EFFF6A350E88423BA3F6410A89CE69D36DB`, built and run. |
| Review and verification | Review findings on PRs 900, 909, 910 and 911 were fixed on their branches, and each PR has a triage reply. The eleven heads were merged onto `task/integration-2809` and built once: 0 warnings, 0 errors. PR 918's first run found one wrong new test, which was fixed. Its second run passed all 12 jobs at `3ebc2c682`. PR 921's CI passed. The `main` push run 36474885515 passed at the release SHA. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. No browser walk ran before release. The operator granted merge and deployment authority for this release (28 September 2026). |
| Schema and grants | Migration **additive**. `20260928100000_WorkCentreQueryIndexes` adds three indexes. `20260928160000_GrantWorkerDocumentOccurrenceUpdate` grants `pegasus_worker_runtime_role` UPDATE on `DocumentOccurrences`. Both apply over `20260928090000_GrantWorkerGeneratedCaseArtifactUpdate`. Bootstrap verified 714 catalogued permission/denial rows and 498 effective runtime DML rows (Release 73: 713/497). Read-back: the live head equals the manifest identity. |
| Web and Worker deployment | `az webapp deploy` (`--restart true`) returned exit 1 after 711 s, but OneDeploy `5ed7f7d6-e6bd-4c6d-a645-b1401119f9c8` succeeded (status 4, active) onto package `20260928195856.zip`, and the exact read-back passed. Activation was set and `PreProvision` passed. `azd provision` took 106 s and set `WEBSITE_WARMUP_PATH=/health/warm` and `WEBSITE_WARMUP_STATUSES=200`. The exact read-back passed again. The Worker `config-zip` deployment ran with sync triggers and a health check. The Worker was not stopped. |
| Start gaps | Both restarts lost their first container attempt, and the platform then stopped the old site too. At 20:02:46Z the deploy's container exited with code 134 after 118 s; the retry served from 20:08:14Z. At 20:17:47Z provisioning's container had no listening port within 230 s; the retry served from 20:22:38Z, with its warm-up probe taking 133 s. So the site served nothing for about 20:03–20:08Z and 20:19–20:22Z. One user's Case page request hung 42 s at 20:15Z. Release 73's start also took 163 s, but it succeeded first time. Tracked in [#922](https://github.com/collisionengineers/pegasus/issues/922). |
| Production smoke | The first run, straight after the Worker deploy, timed out at 30 s on an HTTP probe during the second start gap. The rerun passed at about 20:24Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20260928195856.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-28T20:20:03Z`; the active Graph subscription expires `2026-10-02T15:15:00Z`. The release ran no signed-in journey check. |
| Still owed | Browser walks: the scale preview and blocker focus (#900), the Glass waiting window with `Test-GlassBrowser.mjs` (#911), a signed-in gallery walk (#909), and Case Edit → Save (#910). Re-measure #895 against the 28 September baseline with the same KQL. Check read-only that GJ13EVC-01 pairs with a.QDOS26028 (#905). |
| Evidence | Exact artifacts, the build log, the provision log and both smoke logs retained at ignored `artifacts/releases/release-74-62438281`. |

## Release 73 — 28 September 2026 (deployment live)

Release 73 deployed [PR 894](https://github.com/collisionengineers/pegasus/pull/894): Generate report records the true result, the report opens after it, and the printed report is rebuilt to the template.

- **Generate report.** When the Worker's sweep files the PDF before the Web request's own confirm, the request now reads the file's true state. A retry asks what was filed before drawing anything. A report whose file is filed after its request is recorded as stored by the Worker, from a new settle pass on the same timer as pending-custody reconciliation.
- **What staff see.** The generated report opens in the viewer. The Report card says Stored, Storing, Storage failed, Not confirmed or Not generated. A report still being filed is a warning. Timeouts and Box failures get their own sentence and a log line. The Next action waits until the report is stored.
- **The printed report.** Laid out to `reference/rendererref1` Design I: header and footer on every page, the template's margins, type, colours and tables, six images to a page, the Close-up and the Case page's vehicle plan on page 1. Rows the template lacks are removed. Every printed word comes from the template or the old manager's case page, from one owner in Core. A repairer VAT status of Unknown blocks the report. Report For prints the principal's name and address. The Repair Spec printout takes the same page style.

The route was the normal App Service route with one additive migration, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. No wipe ran.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `b1adbd8b11b1a6088e9e8e329e50dd301db0be05`. It was promoted atomically to both `dev` and `main` at 08:28:15Z, after the build; `main` was `eef0714ec`. Manifest schema 3 SHA-256 `DD771635F5F96EE61AC6E4B160F771CACA3F5266C9F0F052BB5A1F11ED8295F6`. `web.zip` SHA-256 `0323AD29A37D2D6947B7A6D4F3A0264FD3D879E4A5A1587BE106D943ABAAB4A3`. `worker.zip` SHA-256 `C6E44D5699A009F1E03E21E9C81B7DB18A93A5DA3E44E76B955BE409D5F0ED29`. Windows `efbundle.exe` SHA-256 `5C789DCFF30BEDD7B7E7C0DD709CC88ED67FC365D8C08709D99F8721BC2B8B03`, built and run. |
| Review and verification | The work was written on four branches (`task/report-true-result`, `task/report-after-generate`, `task/report-template`, `task/report-documents`) by separate sessions without building, merged onto `task/report-integration` and built once: 0 warnings, 0 errors. PR 894's CI run 36394318068 passed all 11 jobs at `42af0b1d8`, a tree identical to the release SHA; two earlier runs each found the same two tests failing on one Razor attribute, fixed twice (a tag helper, and then plain markup, both wrote the omitted attribute as an empty one; the mark is now written true or false). A new conformance test renders the four sample jobs and measures them against the template PDFs: 40 tests, worst text offset 0.14 mm. An architecture test proves the layout holds no printed text of its own. The `main` push run 36397557193 passed all 11 jobs at the release SHA at 08:49:58Z, after the release was live. The Local, Artifact, PreDeploy, PreMigration and PreProvision gates passed. No browser walk ran before release. The operator ordered the merge and granted merge and deployment authority in advance for this one release (28 September 2026). |
| Schema and grants | Migration **additive**. `20260928090000_GrantWorkerGeneratedCaseArtifactUpdate` grants `pegasus_worker_runtime_role` UPDATE on `GeneratedCaseArtifacts`, over `20260927004150_ImageInReport`. The bundle applied it 08:28:54–08:29:00Z. Bootstrap verified 713 catalogued permission/denial rows and 497 effective runtime DML rows (Release 71: 712/496). Read-back at 08:29:20Z: 172 applied migrations at the manifest identity, and the Worker's UPDATE grant present. |
| Web and Worker deployment | Activation was set at 08:29:38Z. `PreProvision` passed (B1 in uksouth limit 3). `azd provision` ran 08:29:55–08:30:08Z and reported no changes to provision. `az webapp deploy` started at 08:30:09Z. OneDeploy `04770361-be7c-4afc-b8a5-56a170a1450e` succeeded at 08:31:14Z onto package `20260928083050.zip`, and the site started after 239 seconds. A public probe every three seconds read `/health/live` as 200 throughout. It read `eef0714e` until it first read `b1adbd8b` at 08:35:11Z. No outage was observed at that cadence. Worker `config-zip` deployment `1c4b11d4-1d9d-4d03-b1d4-95e56312ef7f` ran 08:35:52–08:38:37Z. The Worker was not stopped. |
| Production smoke | Passed at 08:39:41Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20260928083050.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-28T08:35:03Z`; the active Graph subscription expires `2026-10-02T15:15:00Z`. The release ran no signed-in journey check. |
| First live effect | The report generation `8d236f7f-ec26-41d9-96f3-87ef59b4c9bc` on `a.QDOS26023`, stuck Pending since 21:47Z on 27 September with its file already in Box, was recorded as stored by the Worker's settle pass at 08:37:32Z, unprompted. It is a Release 72 report on the old layout; a saved change to the Case starts a fresh one. |
| Still owed | The operator's signed-in check on `a.QDOS26023`: the report reads Not ready until the repairer's VAT status is recorded on the Repair Spec; record it, save, press Generate report; the report opens in the viewer; the pages match the template. A browser walk of the Report card at 1580 px and a smaller width. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-73-b1adbd8b`; driver scripts, phase logs, the build log and the public probe log at `artifacts/releases/release-73-driver`. |

## Release 72 — 27 September 2026 (deployment live)

Release 72 deployed [PR 893](https://github.com/collisionengineers/pegasus/pull/893): a report image's hash is compared by value, not letter case.

- No report could be previewed or generated from images that arrived by mail. The mail fold stored each file's hash in capitals. The report's image check compared a small-letter hash exactly.
- The check now ignores letter case, and the fold stores the hash in small letters.
- The refusal names the file in staff's words. Generate report shows a render refusal's reason and names the document otherwise. Preview and Generate log the failure.

The route was the normal App Service route with an unchanged migration identity, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. No wipe ran.

| Observation | Value |
| --- | --- |
| Finding | Read-only SQL at about 21:00Z: `a.QDOS26023` held 15 files, 13 with the hash in capitals (the mail, its three PDFs and all nine images) and 2 in small letters (the Glass's XML and PDF). Image 5 was tagged Close-up and image 13 Overview. One report generation, `8d236f7f-ec26-41d9-96f3-87ef59b4c9bc`, was frozen at 20:10:15Z and read `Pending`. Both halves of the fault date from August; earlier faults had stopped the report before this check. |
| Source and packages | Version `0.1.0-alpha.1`, application source `eef0714ecad30c31d2729231c087bb9676f81140`. It was promoted atomically to both `dev` and `main` at 21:27:25Z, after the build; `main` was `1e3b02117`. Manifest schema 3 SHA-256 `7C422A10B1EE190930AF6535262DB37C53AC2C987985B4148BF764D4C11DCC55`. `web.zip` SHA-256 `DFEAE0C06F471AC7D0EC2A4CE9979A9FFF0154497092BC3C3FE34993674637D8`. `worker.zip` SHA-256 `244E3D59E7AD3DEDB0C04DC0964D8FA120B73F9BCE1A06448C43C2E80A4F2AF7`. Windows `efbundle.exe` SHA-256 `9C06C5AFF3B1B3DF6FCC22A20CB693ED12706BBC983B16B01D50F98E6C6D67ED`, built and not run. |
| Review and verification | PR 893 had no independent review. The operator ordered the merge knowing that. Its CI run 36348717806 passed 10 jobs at `8103fe31b`, with `infrastructure` skipped as not routed. The merged tree is identical to that head's tree. A focused local run passed first: build 0 warnings and 0 errors, Core 33 and Integration 74 tests. The `main` push run 36351796417 passed 10 jobs at the release SHA at 21:50:04Z, after the release was live, with `infrastructure` skipped. The Local, Artifact, PreDeploy and PreProvision gates passed. No browser walk ran before release. The operator granted merge authority for `eef0714ec` and approved the exact manifest, targets and operations, deploying without waiting for the `main` run (27 September 2026). |
| Schema and grants | Migration **unchanged**. The deployed head read back as the manifest identity, `20260927004150_ImageInReport`, at 21:28:43Z. No migration and no bootstrap ran. |
| Web and Worker deployment | Activation was set at 21:28:43Z. `PreProvision` passed (B1 in uksouth limit 3). `azd provision` ran 21:29:03–21:29:14Z and reported no changes to provision. `az webapp deploy` started at 21:29:26Z. OneDeploy `c153991b-5142-44a5-95bb-73dafaecd49a` succeeded at 21:30:08Z onto package `20260927212937.zip`, and the site started after 129 seconds. A public probe every three seconds read `/health/live` as 200 throughout. It read `1e3b0211` until it first read `eef0714e` at 21:32:23Z. No outage was observed at that cadence. Worker `config-zip` deployment `2bd28388-b850-46e0-b6f5-34d8a8ceef3f` ran 21:32:56–21:35:41Z. The Worker was not stopped. |
| Production smoke | Passed at 21:37:08Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20260927212937.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-27T21:35:03Z`; the active Graph subscription expires `2026-10-02T15:15:00Z`. The release ran no signed-in journey check. |
| Still owed | The live check is with the operator: on `a.QDOS26023`, press Preview, then Generate report. Generation `8d236f7f…` still read `Pending` after the release, and a retry keeps its frozen report date of 27 September. `QDOS26024`, created at 20:22Z with 6 files in capitals, is a second Case for the same check. The steps after the image check (render, file the PDF in Box, confirm) have no production record. The fold's new write is unproven until an instruction arrives after 21:35:41Z: its `DocumentVersions.Sha256` should read in small letters. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-72-eef0714e`; driver scripts, phase logs, the build log and the public probe log at `artifacts/releases/release-72-driver`. |

## Built-in image tags restored — 27 September 2026

- **Finding.** `ImageTags` in `pegasus` on `pegasus-prod-sql-252ow37gij` held
  0 rows (read-only SQL, about 18:45Z). The Case page's tag picker offered no
  tag. Since Release 71 the report reads its Close-up and Overview by the
  built-in tag identifiers, so no report could be generated.
- **When.** The rows were lost between 14 September 11:12Z, when the Market
  research row was seeded, and 24 September 16:18Z, since when the table has
  had no write. What deleted them is not recorded. SQL auditing is off, no
  migration deletes them, the wipe has preserved `ImageTags` since
  10 September, and both runtime roles are denied DELETE.
- **Restore.** The operator approved one insert of the missing rows. At
  18:54:08Z it added five rows with the values of
  `20260910120000_CaseImageTags` and `20260913090000_MarketResearchImageTag`:
  Overview, Close-up, Third party, Reflection and Market research,
  identifiers `…17a1` to `…17a5`. Read-back: five rows, all built-in. There
  was no outage, and Web and Worker were not stopped.
- **Guard.** Since 29 September the intake wipe prints the built-in tag count, refuses
  `-Execute` when it is 0, and fails its verification when the count changes.
  A dry run after the restore printed 5.
- **Still owed.** The signed-in check is with the operator: tag one image
  Close-up and one Overview on a Case, then generate the report.

## Release 71 — 27 September 2026 (deployment live)

Release 71 deployed [PR 892](https://github.com/collisionengineers/pegasus/pull/892), which merged PRs 881–891 into `dev` together:

- [PR 881](https://github.com/collisionengineers/pegasus/pull/881): the Upload picker is centred, its narration is gone, and limits read in megabytes.
- [PR 882](https://github.com/collisionengineers/pegasus/pull/882), [PR 884](https://github.com/collisionengineers/pegasus/pull/884), [PR 888](https://github.com/collisionengineers/pegasus/pull/888) and [PR 889](https://github.com/collisionengineers/pegasus/pull/889) (report plan 03):
  - The report prints the recorded salvage category.
  - Generate report works without pressing Edit.
  - Retail, Trade and Engineer's value are typed boxes.
  - The image tag decides how an image prints, and "in report" is a plain on/off.
- [PR 883](https://github.com/collisionengineers/pegasus/pull/883): Case page Refresh returns to idle after every press.
- [PR 885](https://github.com/collisionengineers/pegasus/pull/885): in the Inbox, the subject opens the message, and the preview reads the message, counts attachments and names the Case.
- [PR 886](https://github.com/collisionengineers/pegasus/pull/886): Triage has no Edit step, one Assign, Complete on a click, and Reply with outcome.
- [PR 887](https://github.com/collisionengineers/pegasus/pull/887): Administration has editable e-mail templates, starting with the Triage outcome reply.
- [PR 890](https://github.com/collisionengineers/pegasus/pull/890): Vehicle Images photographs become Case images on merge.
- [PR 891](https://github.com/collisionengineers/pegasus/pull/891): the report preview and EVA can read Case images from Box.

The route was the existing App Service destructive migration route, run from the Windows workstation. Web and Worker are Running on the approved release, and full production smoke passed. The intake test estate was wiped just before the release (below).

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `1e3b02117e6190e1ff47655211899cd258bea370`. It was promoted atomically to both `dev` and `main` before the build; `main` was `6e7235a0d`. Manifest schema 3 SHA-256 `3095B077EF4E3432D803EF29928926D7CBFE005E0DF4DA5D47E6B7C6F0818FEE`. `web.zip` SHA-256 `45CAA854EBDFF043C6F1517A9BC29C5139769D465E5C900D41456531C4F59985`. `worker.zip` SHA-256 `299AE6239FC421545D270B235AADC394900D8FE5939403D3145994B74F6E9532`. Windows `efbundle.exe` SHA-256 `9CD3105AC1642CCE8226E555B2697DC31E52409813355EBFB8ED0F17044CB605`, built and run. |
| Review and verification | The 11 PRs were merged onto one integration branch with `--no-ff`, so every commit was kept. The two stacks were 882 → 884 → 888 → 889 → 890 and 886 → 887. Two conflicts were resolved by hand. In `OperatorLabels.cs`, #881's removal of the nested MiB `FileSize` was kept together with #885's `Kind` comment. The two pinned migration lists (`CaseWorkflowMigrationTests`, `IntakePersistenceIntegrationTests`) gained both new migrations, in id order. The EF model snapshot merged without conflict. PR 892's CI run 36329102584 passed all 11 jobs at `3f34aafae`, a tree identical to the release SHA. The `main` push run 36332663580 at the release SHA first failed one test in `sql-integration (5)`: `CaseWorkflowPersistenceTests.ReportApprovalIsServerStampedAndSentEvidenceFollowsPreparationAndApproval` hit a SQL execution timeout after 41 s. PR 892 had passed that test on the identical tree. The release was already live when the failure was seen. A rerun of the failed shard passed, and the run is now green. The Local, Artifact, PreDeploy, PreMigration and both PreProvision gates passed. No browser walk ran before release. The operator approved the ordinary wipe, granted merge authority, and approved the exact manifest, targets and an immediate outage window (27 September 2026). |
| Intake wipe | Ordinary wipe (no test-estate reset), after the PRs merged and before promotion. The fresh dry run found 31 blobs (6,311,732 bytes) in `pegcustody252ow37gij/transient-intake`. It found 129 tables, with the preserve list 36/36 found (37 effective with `ApprovedMailbox*`) and 92 tables to wipe holding 177 rows (1 Case, 1 intake receipt, 1 retained message). Worker `pegasus-prod-worker-252ow37gij` was stopped and read back `Stopped` before execution. Blobs remaining: 0. One SQL transaction reported 178 rows affected, including the cutoff update. Wiped tables still holding rows: 0. Preserved rows after: 607. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were unchanged at 22/9/1. `ValuationPresets` and `EmailTemplates` were 0/0. Committed mail cutoff: `2026-09-27T16:16:08.7972132Z`; mailbox approval and activation times were unchanged. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. The Worker was restarted and read back `Running`. |
| Containment | The old Web reported the approved old SHA `6e7235a0db42b4dc9a2b9a0e741c6b737a9f82c5`. The Worker's Flex update strategy read `Recreate`. The Worker's Disabled-setting census was set to `true` and smoked. The new Worker package was staged (deployment `b1308cd6-c036-4c5f-a02d-51c5768691b1`), and the disabled smoke passed again. The Worker read back `Stopped`. Web stop was issued at 16:29:53Z. Web then read back `Stopped` with `/health/live` unserved. The fresh read-back immediately before SQL passed, with `/health/live` answering HTTP 403. |
| Schema and grants | This migration is destructive and forward-only. Two migrations were applied over `20260926150000_DeclaredUploadDestination`. `20260927002303_EmailTemplates` creates `EmailTemplates` and grants `pegasus_web_runtime_role` SELECT, INSERT and UPDATE on it. `20260927004150_ImageInReport` adds `DocumentOccurrences.InReport` (bit, default on) and drops `DocumentOccurrences.PreparationRole`; that drop was the recovery boundary. Bootstrap verified 712 catalogued permission/denial rows and 496 effective runtime DML rows (Release 70: 707/493). The live migration head read back as the manifest identity, `20260927004150_ImageInReport`. |
| Web and Worker deployment | The new `web.zip` was deployed to the stopped Web App (OneDeploy `4d24af9b-67da-428f-a1d1-fd7d4dc4f1e5`, 16:32:41Z, package `20260927163220.zip`), and the site stayed `Stopped`. Provision with the Worker disabled (deployment `pegasus-prod-1790526799`, 1 minute 39 seconds) and its smoke passed. The activation provision (`pegasus-prod-1790526928`) took 1 minute 37 seconds. The Web App was started at 16:37:00Z and served the exact SHA at 16:39:19Z. The Worker read back `Running`. Web outage: 16:29:53–16:39:19Z (9 minutes 26 seconds). |
| Production smoke | Passed at 16:40:23Z. The Worker activation smoke passed as `approved-live-worker`. Active Web package `20260927163220.zip` SHA-256 equals the approved `web.zip`. Intake liveness passed: last completed poll `2026-09-27T16:40:00Z`; the active Graph subscription expires `2026-10-02T15:15:00Z`. The release ran no signed-in journey check, and the wiped estate holds no Case yet. The live walk is with the operator once a Case exists: Generate report without Edit, typed values, image tags and the in-report on/off; Triage Complete and Reply with outcome; an edited e-mail template in Administration; the Inbox preview; and Upload's centred picker. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-71-1e3b0211`; driver scripts and the activation log at `artifacts/releases/release-71-driver`. |

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

## Intake data wipe — 28 September 2026

- Approved ordinary intake wipe: Worker `pegasus-prod-worker-252ow37gij`
  stopped for the maintenance window, then resumed and read back `Running`.
  The fresh dry run found 24 blobs (37,848,379 bytes) in
  `pegcustody252ow37gij/transient-intake` and 270 rows across 92 non-preserved
  tables in SQL `pegasus` on `pegasus-prod-sql-252ow37gij`; the batch reported
  271 affected rows including the mail-boundary update. The committed mail
  cutoff is `2026-09-28T13:58:05.5131004+00:00`; 620 preserved rows remain.
  Every reference-sequence value was unchanged (`CaseSequences` maximum 29,
  `ImageIntakeSequences` 10 rows and `UnidentifiedSequences` 1 row);
  `ValuationPresets` and `EmailTemplates` remained 0/0. `authentication-ring`,
  `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No
  `-ResetTestEstate` was used. Post-run script verification reported zero
  blobs remaining and zero wiped tables holding rows. Reloading the previous
  Case URL returned “We could not find that page”.

- Approved ordinary intake wipe: Worker `pegasus-prod-worker-252ow37gij`
  stopped for the maintenance window, then resumed and read back `Running`.
  The wipe cleared 43 blobs (9,832,299 bytes) from
  `pegcustody252ow37gij/transient-intake` and deleted 384 inventoried rows
  from 93 non-preserved tables in `pegasus` (385 affected rows including the
  mail-boundary update). The committed mail cutoff is
  `2026-09-28T08:47:33.1197394+00:00`; 617 preserved rows remain. Every
  value in `CaseSequences` (24 rows), `ImageIntakeSequences` (9 rows), and
  `UnidentifiedSequences` (1 row) was unchanged; `ValuationPresets` remained
  0/0. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook,
  and Box were untouched. No `-ResetTestEstate` was used. Post-run script
  verification reported zero blobs remaining and zero wiped tables holding
  rows. The Web UI reload was skipped at the operator's request.

- Approved ordinary intake wipe: Worker `pegasus-prod-worker-252ow37gij`
  stopped for the maintenance window, then resumed and read back `Running`.
  The fresh dry run found 66 blobs (19,366,816 bytes) in
  `pegcustody252ow37gij/transient-intake` and 543 rows across 92 non-preserved
  tables in SQL `pegasus` on `pegasus-prod-sql-252ow37gij`; the batch reported
  544 affected rows including the mail-boundary update. The committed mail
  cutoff is `2026-09-28T13:07:24.1678471+00:00`; 620 preserved rows remain.
  Every reference-sequence value was unchanged (Case sequence maximum 28,
  Image Intake 10 rows and Unidentified 1 row); `ValuationPresets` and
  `EmailTemplates` remained 0/0. `authentication-ring`, `box-links`,
  `pegtrans252ow37gij`, Outlook and Box were untouched. No
  `-ResetTestEstate` was used. Post-run script verification reported zero
  blobs remaining and zero wiped tables holding rows. The Web UI reload was
  skipped at the operator's request.

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
boundary, confined to an approved disposable test subtree. Folder
`425169015650` is the local-test custody root a hosted `DevelopmentOffline`
run writes under when `Features:LiveBoxCustody` is on (approved 2026-10-07);
its holding folder is created by the operator beneath it. None of these
folders is standing write authority. The exact-target approval and invocation checks are
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
