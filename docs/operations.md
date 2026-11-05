# Operations

This is the last recorded deployed-state and support summary. It is not a fresh
cloud observation. Exact source structure belongs in [architecture](current-architecture.md);
procedures are reached through [the runbook](runbook.md). Records before
Release 124 are one line each in [operations history](operations-history.md).

## Intake data wipe - 30 October 2026

- Approved ordinary intake wipe: Worker `pegasus-prod-worker-252ow37gij`
  stopped and read back `Stopped` before execution; the Web App stayed
  Running. The dry run immediately before execution found 187 blobs
  (67,104,218 bytes) in `pegcustody252ow37gij/transient-intake` and 88
  non-preserved tables / 2,559 rows in SQL `pegasus` on
  `pegasus-prod-sql-252ow37gij`; the preserve list was 34/34 found (35
  effective). The batch reported 2,567 affected rows including the
  mail-boundary update. Blobs remaining: 0. Wiped tables still holding rows:
  0. Preserved rows after: 7,144. The committed mail cutoff is
  `2026-10-30T11:03:22.1801223+00:00`; mailbox approval and activation times
  were unchanged. `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences`
  were unchanged at 127/15/1. `ValuationPresets` 6/6, `EmailTemplates` 3/3
  and built-in image tags 4/4 before/after. `authentication-ring`,
  `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No
  `-ResetTestEstate` was used. The Worker was resumed and read back `Running`.

## Release proof checklist run - 30 October 2026

The release proof checklist (R525, R526) was run once on Release 130 (`0c2acf91`)
from 09:47Z to 10:21Z. A signed-in `alex` session walked the pages in Chrome, and
read-only App Insights, Log Analytics and SQL queries covered the telemetry items.
Of the 138 items, 46 passed, 34 passed in part, 51 were not run and 7 cannot be
proved until their event happens; none failed. Sends, paid or shared lookups, MCP
and connector work, Principal API submissions, mailbox admission and wipes were out
of bounds (operator, 30 October 2026). Writes were limited to a.QDOS26124, which
was left With Engineer, assigned to alex and Repairable, with its report regenerated
at 10:21, six synthetic images and one export. The owed rows, with their wording,
are #1801. The walk found two defects, #1799 (the viewer draws under the shell after
keyboard input) and #1800 ("report wording is unavailable" on a Total loss that
saved). The classifier called the model once, on 29 October at 12:45Z. This record
names the run, so the checklist file is deleted (R525).

## Brego credential rotation - 29 October 2026

- Brego reset the `digital@collisionengineers.co.uk` valuation account. A new
  device-approval code was verified for the existing `deviceId`, a fresh 1-year
  `brego-session` was minted, and
  `pegcustody252ow37gij/authentication-ring/brego-session.json` was overwritten
  at 09:12Z (105 bytes). Cookie value not recorded.
- Key Vault `pegasusprodkv252ow37g` gained `brego-valuation-password` version
  `fce005f2ba044316935c9501dd3b2bbd`; the old version `8310aab6…` is disabled.
  `BREGO_VALUATION_PASSWORD_SECRET_URI` in `pegasus-prod` and the Web App's
  `Brego__ValuationAccount__Password` reference name the new version (set with
  `az webapp config appsettings set`, not a provision). The reference reads
  `Resolved`, the Web App is Running and `/health/ready` is 200 on `3dec03615`.
- Proof: the local helper's lookup for `SH65HTL` with the new session answered
  Retail high £5,010 and Trade high £3,004. Get valuation in the app was not
  pressed. Secret values are not recorded.

## Intake data wipe - 26 October 2026

- Approved ordinary intake wipe: Worker `pegasus-prod-worker-252ow37gij`
  stopped and read back `Stopped` at 08:46:42Z before execution; the Web
  App stayed Running. The dry run after that read-back and the execute
  inventory both found 304 blobs (223,769,642 bytes) in
  `pegcustody252ow37gij/transient-intake` and 88 non-preserved tables /
  3,028 rows in SQL `pegasus` on `pegasus-prod-sql-252ow37gij` (15 Cases,
  81 documents, 49 retained mails, 34 intake receipts); the preserve list
  was 34/34 found (35 effective with `ApprovedMailbox*`). The batch
  reported 3,036 affected rows including the mail-boundary update. Blobs
  remaining: 0. Wiped tables still holding rows: 0. Preserved rows after:
  6,660. The committed mail cutoff is
  `2026-10-26T08:47:10.4659095+00:00`; mailbox approval and activation
  times were unchanged.
  `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were
  unchanged at 116/14/1. `ValuationPresets` 4/4, `EmailTemplates` 3/3 and
  built-in image tags 4/4 before/after. `authentication-ring`,
  `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No
  `-ResetTestEstate` was used. Post-run script verification reported zero
  blobs remaining and zero wiped tables holding rows. The Worker was
  resumed and read back `Running` at 08:48:08Z after verification. The
  Web App read back `Running` at 08:48:11Z.
- Approved ordinary intake wipe, the second on 26 October 2026: Worker
  `pegasus-prod-worker-252ow37gij` stopped and read back `Stopped` at
  09:25:21Z before execution; the Web App stayed Running. The dry run after
  that read-back and the execute inventory both found 26 blobs
  (6,797,499 bytes) in `pegcustody252ow37gij/transient-intake` and 88
  non-preserved tables / 320 rows in SQL `pegasus` on
  `pegasus-prod-sql-252ow37gij` (1 Case, 11 documents, 1 retained mail,
  1 intake receipt); the preserve list was 34/34 found (35 effective
  with `ApprovedMailbox*`). The batch reported 328 affected rows
  including the mail-boundary update. Blobs remaining: 0. Wiped tables
  still holding rows: 0. Preserved rows after: 6,663. The committed mail
  cutoff is `2026-10-26T09:25:44.3222415+00:00`; mailbox approval and
  activation times were unchanged.
  `CaseSequences`/`ImageIntakeSequences`/`UnidentifiedSequences` were
  unchanged at 117/14/1. `ValuationPresets` 4/4, `EmailTemplates` 3/3 and
  built-in image tags 4/4 before/after. `authentication-ring`,
  `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched. No
  `-ResetTestEstate` was used. Post-run script verification reported zero
  blobs remaining and zero wiped tables holding rows. The Worker was
  resumed and read back `Running` at 09:26:12Z after verification. The
  Web App read back `Running` at 09:26:15Z.

## Page p95 baseline - 28 October 2026 (read-only observation)

The first recorded page baseline for the budgets in
[engineering](engineering.md#page-budgets) (operator, 28 October 2026, R507).
Read at 21:35Z from `pegasus-prod-appi-252ow37gij` through its workspace
`pegasus-prod-logs-252ow37gij`, over the seven days to the reading: the
operator's test use only, with Releases 124 to 128 and their restarts inside
the window, so every figure includes cold starts (the Work Centre maximum is
a 74 s request after a restart). Server time is `AppRequests.DurationMs`;
the request timing filter's phases are `AppEvents` named
`Pegasus.Document.Read` with `Properties.phase` and `Measurements.durationMs`.

```kusto
AppRequests | where TimeGenerated > ago(7d)
| extend Page = tostring(split(Name, '?')[0])
| summarize Count = count(), P95 = round(percentile(DurationMs, 95)) by Page
```

| Budget line | Page | Requests | p50 ms | p95 ms | Budget |
| --- | --- | --- | --- | --- | --- |
| Work Centre | `GET /Index` | 239 | 741 | 3,431 | 500 ms |
| Inbox list | `GET /Mail/Index` | 376 | 129 | 1,391 | 500 ms |
| Cases list | `GET /Cases/Index` | 174 | 258 | 1,965 | 500 ms |
| Case page open | `GET /Cases/Details` | 530 | 291 | 2,043 | 500 ms |
| Save and handler fetches | `POST /Cases/Details` | 493 | 70 | 2,238 | 300 ms |
| Dialog loads | `GET /Mail/Message` | 51 | 339 | 4,140 | 300 ms |
| Everything else | `GET /Administration/*` (6 to 28 requests each) | | 52 to 330 | 676 to 3,468 | 1 s |
| Report PDF render | phase `report.pdf.generate` | 6 | 59 | 1,416 | 3 s |

Timing-filter phases over the same window (p95 ms): `web.case.resource`
3,026 (921 requests), `web.case.main` 2,165, `web.case.section.resource`
1,169 (427), `web.workcentre.resource` 3,013 (211), `web.workcentre.main`
2,534, `web.workcentre.refresh.resource` 6,330 (62), `web.auth.validation`
54 (2,886); `report.renderer.initialize` 22,092 over 12 cold initialisations.

Every hot page is over its budget on this reading. The reading is a test-use
week with restarts in it, not the week of real use the budget rule names; it
is recorded so the next reading has a comparison, and no efficiency change is
built from it alone.

## Proof debt settled - 29 October 2026

The owed live-proof chain that every record from Release 81 to Release 128
carried ("Release N to M owed items otherwise stand") is settled once
(operator, 29 October 2026, R525 and R526). Its 208 items are classified in
the release proof checklist (run and deleted 30 October 2026): 131 still provable
and 7 re-written for a new Case form one checklist, run on the next release and
recorded once in that release's record; 20 are discharged by the records
themselves, 18 are superseded by later PRs or rulings, 16 are GitHub issues and
16 are operator decisions that come in rounds of four. The 58 "Waiting for the
operator" items that landed in Release 123 are approved as built (R526). No
record is edited: this note supersedes their owed sentences, and the release
record that names the run deletes the checklist.
## Operations record corrections - 22 October 2026 (addendum)

This section adds dated observations to three statements below that lean on
rows the intake data wipes deleted. No dated record is edited: each statement
is quoted as written and its record is linked. The sources are this file, the
wipe [skill](../.agents/skills/pegasus-wipe-intake-data/SKILL.md) and
[script](../scripts/Invoke-IntakeDataWipe.ps1), the retained Query Store files
named below, and one read-only count of 22 October 2026 posted on
[issue 1510](https://github.com/collisionengineers/pegasus/issues/1510#issuecomment-6334098599).

| Record | Observation |
| --- | --- |
| [Classifier agent status](https://github.com/collisionengineers/pegasus/blob/5a7c2b75c5d52c8d73a70e684073bf87369beec7/docs/operations.md#classifier-agent-status---21-october-2026-read-only-observation) | Four statements: "it has never called the model"; "no `result`, `classify`, `link` or `closed as failed` line has ever been written"; "`ActionHistory` holds no row whose reason begins `Classifier agent: `"; and, in the last row, "The Function App has never called Anthropic. No mail has been eligible since Release 116". The wipe skill lists `ActionHistory` among the tables a wipe clears and the wipe script's preserve list omits it. The agent had run in write mode since the switch read back at 08:18:28Z on 19 October ([Release 116](https://github.com/collisionengineers/pegasus/blob/5a7c2b75c5d52c8d73a70e684073bf87369beec7/docs/operations.md#release-116---19-october-2026-classifier-activation-live)). Four wipes are recorded after it; their committed mail cutoffs are `2026-10-19T08:31:22.0176414+00:00`, `2026-10-19T13:29:39.9462093+00:00` and `2026-10-19T13:36:30.1217128+00:00` ([19 October](https://github.com/collisionengineers/pegasus/blob/5a7c2b75c5d52c8d73a70e684073bf87369beec7/docs/operations.md#intake-data-wipe---19-october-2026)) and `2026-10-21T10:24:58.9788633+00:00` ([21 October](https://github.com/collisionengineers/pegasus/blob/5a7c2b75c5d52c8d73a70e684073bf87369beec7/docs/operations.md#intake-data-wipe---21-october-2026)); a cutoff is a mail time and no record gives the time the SQL commit landed. Those wipes found 4 retained mails (13:29Z), 3 (13:36Z) and 4 (21 October); the 08:31Z record gives none. The ticks the records list are the dry tick at 08:15:00Z and the first write tick at 08:30:00Z on 19 October, and the six from 11:30:00Z to 12:45:00Z on 21 October; no tick between them is recorded. Release 116 read the `Classifier agent: ` rows at 0 at 08:15:50Z and 08:30:39Z, before the first cutoff. The zero in the status record was read in the pass of 12:00Z to 13:15Z on 21 October, after that day's wipe. The log statement rests on Application Insights, which "answered for about one hour of the classifier's traces on 21 October", and the `reviewed` table was unreadable (403). Whether the Function App called the model before 11:30Z on 21 October is not shown by these records. |
| [Release 117](https://github.com/collisionengineers/pegasus/blob/5a7c2b75c5d52c8d73a70e684073bf87369beec7/docs/operations.md#release-117---21-october-2026-deployment-live), Query Store row | Statement: "the one Case on the estate means the registration lookup (PR 1440) and the Open tasks statement (PR 1454) were not exercised, as Release 115's record anticipated". The wipe records give the Case count at four times: 1 Case at the 19 October wipe with mail cutoff 08:31:22Z (`CaseSequences` 104), 4 Cases with 54 documents at 13:29Z (108), 1 Case with 12 documents at 13:36Z (109) and, on 21 October, 2 Cases with 28 documents at 10:24Z (111). Release 117's containment read found 2 `CaseRepairSpecifications` rows at 09:23:34Z. The counts between those times are not recorded. The retained after snapshot (`artifacts/performance/query-store-release-115-after-20261021T0941Z.csv`, 48 h, 357 statements) reads: the Open tasks statement without `ActionHistory` (PR 1454) 1,003 executions, average 289.87 microseconds, total 290,742 microseconds; the candidate-page read that compares `VehicleLookupRequests.Registration` with `NormalizedRegistration` (PR 1440) 2,812 executions, average 144.52 microseconds, total 406,403 microseconds. The file holds no `INSERT` into `VehicleLookupRequests`. Executions count runs and do not show how many rows the tables held. For the earlier Open tasks text, [Release 115](https://github.com/collisionengineers/pegasus/blob/5a7c2b75c5d52c8d73a70e684073bf87369beec7/docs/operations.md#release-115---19-october-2026-deployment-live) names a baseline of 485 executions at 31.1 ms; the retained Release 114 after snapshot (`query-store-release-114-after-20261019T0739Z.csv`, 24 h to 07:39Z on 19 October) reads 459 executions, average 112.17 microseconds, total 51,487 microseconds. |
| [Reference-data load (REF-04)](https://github.com/collisionengineers/pegasus/blob/5a7c2b75c5d52c8d73a70e684073bf87369beec7/docs/operations.md#reference-data-load-ref-04---17-october-2026-production-applied), read-back row | Statement: "`ActionHistory` rows of batch `ref04:prod-20261017`: 329 `contact_created`, 39 `contact_saved` and 1328 `inspection_location_created`, 1696 in all", with "`OrganizationAdministrationOperations` holds 1696 `ref04` rows", read at 20:07:31Z on 17 October. A read-only count at 06:11:57Z on 22 October returned 0 `ActionHistory` rows where `CorrelationId` starts `ref04:prod-20261017:` and 1,696 `OrganizationAdministrationOperations` rows where `OperationKey` starts `ref04:prod-20261017:`. The 17 October `ActionHistory` count no longer reproduces; the operations table still holds 1,696 rows for the batch. The wipe skill lists `ActionHistory` among the tables a wipe clears and the wipe script's preserve list names `OrganizationAdministrationOperations`. The wipes this file records after that read are the three of 19 October and the one of 21 October. |

## Release 142 - 4 November 2026 (deployment live, additive migration)

Release 142 deployed combine PR 1904 (#1868–#1903): a regenerated document files as the next Box version of its earlier file (#1894, R664); the v48 Send report composer dialog and the Original report card (#1903, R665–R672); the classifier agent reads each e-mail on its own and marks reviewed only on a verdict (#1887, R583/R584/R592); a vehicle-capture document never registers an Image Intake (#1889, R585); the Inbox copy of a mailbox's own reply is not intake (#1888, R582/R587); a QDOS Triage subject registration read after a comma (#1885); an Audatex line value with a joined `*` (#1901, R659); the Overview printed again first on the image pages (#1902, R660); the #1801 proof-checklist rows struck (#1884, R661); and the Release 135–141 records. Normal route after the additive migration; no provision; no outage, under the operator's grant of 4 November 2026 ("approved, release", to the read-only run's request); the combine PR's CI covered the merge tree, which equals `dev`'s. The first read-only run failed on uncaptured `git fetch` output in the preflight (PR 1922); it was repeated with that fix applied locally.

| Observation | Value |
| --- | --- |
| Source | `c11bd784cbef41901c00a8d3079eb5110d3ee799`, promoted from `8da7957cead9dc3c50ff30f3845b0d9ecceeb322` with SHA, manifest and route as the read-only run reported them; `main` and `dev` read it back |
| Packages | manifest `84130BF77AA6C96F1DDF706273453C53177D649BC8B90CE8334F28CA31DBD759`; `web.zip` `4ABE7E7978CEA39B001AECD71705C191C270F7F8995D9C76E3C4A5C3CDD7CCFA`; `worker.zip` `C54457E030234368C8CA399CE5CB4A430DBD34E0F1C4BCFB09AE70D5C7210B0F`; `classifier.zip` `7B2DF7BD703B1F2AAA8DEBDC13A66465948144794CA352E8C58BE39144A6374E`; `efbundle.exe` `349F0E137020DF37E728DF3FF455243D7B7576A6162102503F56CD730089F156` |
| Migration | `20261104170938_BoxDocumentSeries` (nullable `CaseDocuments.SeriesIdentity` and its index) applied with Release 141's bytes live; head read back `20261104170938_BoxDocumentSeries` |
| Deployment | provision not run (no infra change, azd environment equal to Release 141's); Web OneDeploy `51a61d9e-3f37-4ff2-bd68-6044b109657b` succeeded 22:27:25Z, the site read back Running, ready and at the SHA on attempt 1; Worker `config-zip` `4b8e2674-a05f-43f0-b98d-5c903ba201b8`; classifier `cb2a806d-f968-4c22-b2ed-489010b816a6`, `Running` |
| Smoke | passed at 22:36:10Z; package SHA equals `web.zip`; intake liveness last poll 22:35:01Z, subscription expires 2026-11-05 14:00Z |
| Owed | PR 1908 (a mailbox capture with no matching Case becomes Unidentified under #1889, against R663; the fix awaits merge); PR 1922 (the preflight fetch fix); #1923 (the live checks for #1894, #1901 and #1903); the #1905 review's #1907 and #1909–#1921 |

## Release 141 - 4 November 2026 (deployment live)

Release 141 deployed PR 1899 (#1897): the Sent-evidence poll moves its cursor only at a page boundary and replays an item Graph presents again by identity rather than position, so a busy mailbox's Sent poll no longer stalls with `invalid_sent_source_item`; a poll with a failed mailbox is logged at Warning. The range also carried PR 1880 (CI test lanes on Linux, R657/R658), which deploys no code. Normal route; no migration; no provision; no outage, under the operator's grant of 4 November 2026 ("approved", to the read-only run's request); the PR's CI covered the merge tree, which equals the PR head's.

| Observation | Value |
| --- | --- |
| Source | `8da7957cead9dc3c50ff30f3845b0d9ecceeb322`, promoted from `ff91df4b899d5af3452bbf4057d2a9e7106b47e0` with SHA, manifest and route as the read-only run reported them; `main` and `dev` read it back |
| Packages | manifest `6B42EB4C781F40FBA6C1B4578C909C83829DC80172D090BD56F2E4A378720A6D`; `web.zip` `5B88B6A73C5A9114AE4DC3A5906910E0F667597D20CEA9D787227190FDF056C5`; `worker.zip` `84E1546EF3863EB8F698D692DCC68EE186A805232E359BB9B5164DD50F646986`; `classifier.zip` `7B044DC65D86A9CE7949E1E261416A58D1C7576EEE1E335C81BF27E9784D799A`; `efbundle.exe` `F38AFDE0018397474EA3F3309E3F27A7DF7E0AD29F9BF567332E2541DF021FB4` |
| Migration | none; head `20261104120000_CaseReferenceSuffix` unchanged |
| Deployment | provision not run (no infra change, azd environment equal to Release 140's); Web OneDeploy `64965eb4-1cc3-4549-87f2-d0cd68fb8dc0` succeeded 12:48:12Z, the site read back Running, ready and at the SHA on attempt 1; Worker `config-zip` `c5a430f1-dd20-47b3-b50b-fe81fe795b68`; classifier `bbea7d87-a2a7-4251-95e5-946c8970de1f`, `Running` |
| Smoke | passed at 12:56:26Z; package SHA equals `web.zip`; intake liveness last poll 12:55:00Z, subscription expires 2026-11-05 14:00Z |
| Sent poll | desk@ `LastCompletedAtUtc` 12:57:00Z with no failure code, the first completion since 3 November 20:14Z; the first run under the release recorded six items at 12:54:06Z (one Unmatched, five quarantined re-presentations) and replayed the rest at their new positions. a.QDOS26157-p's 11:11:04Z send had been observed at 12:40:03Z, before the deployment, once Graph's live page shifted. QDOS26159-p's 12:07:49Z send (`51ac6fca…`) stayed Submitted because the stored position cursor had skipped its item: with the operator's approval, desk@'s `Cursor` was set NULL at 13:02:57Z (one row, between runs), the poll re-read from the activation boundary, observed the send at 13:03:06Z and completed at 13:03:20Z. Both Cases read Report sent |
| Owed | none |

## Release 140 - 4 November 2026 (deployment live, destructive migration)

Release 140 deployed PR 1893 alone: the temporary **Add -p to new Case/PO** switch, shipped on, which ends every newly minted Case/PO in `-p`; every existing Case/PO and Audit reference was rewritten once and their Box folders renamed (R656). Destructive route with one destructive migration and a short outage, under the operator's grant of 4 November 2026 ("approved, now"); the PR's CI covered the merge tree, which equals the PR head's.

| Observation | Value |
| --- | --- |
| Source | `ff91df4b899d5af3452bbf4057d2a9e7106b47e0`, promoted from `75152f80c6969eb42ceed628be15c7d91af77e0d`, with SHA, manifest, route and old Web SHA as the read-only run reported them; `main` and `dev` read it back |
| Packages | manifest `BA1E53C94637A7E66499A8861904A26AB7659AD7A914E126AF3B57C1E1F8C3DD`; `web.zip` `A1855C361D8C8DF57091AB2694E8B41227F701D0E728FDAEF2CBDCB76533C3B1`; `worker.zip` `A421F945759BCE8C7870576FFD379180631413D83A8E20E09BDDD51C1661F825`; `classifier.zip` `5D1C73B6C9C2199E51291A0FD3732874589B6FA09EE86B25D58ABAC0E1AB6716`; `efbundle.exe` `C8EECD608484B9431B8E22121D9BC83103723AB5D6FD90A9B215EA47C0F611A9` |
| Migration | `20261104120000_CaseReferenceSuffix`, applied with the classifier, Worker and Web read back Stopped and unserved: it appends `-p` to `Cases.Reference` and `AuditReference` and to the matching `StaffNotifications` reference and reply link; forward only. Bootstrap census 698/490; head read back at the new id. 3 Cases on the estate, all rewritten |
| Deployment | Worker staged `470db9a7-45b4-4f69-8a52-a9397a764a27` before SQL; Web OneDeploy `0b011175-e2e5-4418-9fe0-7a8128dcc7a5` onto the stopped site at 10:37:09Z, read back Running at the SHA on attempt 1; classifier `f313f6ec-7b8e-41b3-a089-a91f6b094ca6`, again `9a8da9af-284b-45cb-875b-5b0a1a3dbaa9` on the resumed run |
| Provision | skipped as unchanged with the Worker Disabled settings left `true` (#1857), so the first smoke failed; `azd provision --no-state` set them `false` and the same `-Execute` line resumed |
| Smoke | passed at 10:48:31Z; package SHA equals `web.zip`; intake liveness last poll 10:45:00Z, subscription expires 2026-11-05 14:00Z |
| Box rename | the staged Worker ran the one-off pass before SQL and recorded 0 renamed (#1895); with the operator's approval that `ActionHistory` row was deleted and the Worker stopped and started 11:03:37–11:03:55Z (`az functionapp restart` did not recycle it); 11:04:10Z "3 Box folders renamed for 3 Cases", confirmed in Box by the operator |
| Owed | #1857 (provision skip), #1895 (rename pass ordering) |

## Release 139 - 4 November 2026 (deployment live)

Release 139 deployed PR 1883 alone: From is a default on every send, changeable to any send-ready Outgoing mailbox, preselected on the mailbox the message arrived in (for an instructions@ staff forward, the forwarding mailbox), else the Principal's Send from; Forward keeps the holding mailbox (R655). Normal route; no migration; no outage, under the operator's grant of 4 November 2026 ("PR now green, proceed on to merge to dev and release"); the PR's CI covered the merge tree, which equals the PR head's, so dev→main CI was skipped as the operator directed.

| Observation | Value |
| --- | --- |
| Source | `75152f80c6969eb42ceed628be15c7d91af77e0d`, promoted from `ac836fef5d80ce1b772428171b693162edcc8103`, with SHA, manifest and route as the read-only run reported them; `main` and `dev` read it back |
| Packages | manifest `CD5365B2648EBA127952DA402991759503B6B2213E31C4A5F104C4DD3202699C`; `web.zip` `A301714CB5C4419D2E94AC7C23EE30E6934EE3FF6E200DEA765A2CE928DC6368`; `worker.zip` `49106136022E9A98205B8BCB2DC3109AFF85DDBD85831E023B0D8DA063B5F7BE`; `classifier.zip` `07E7CFCF6FFA9581BE95635AF907D3ED4B104CF35F9E1BB7C64034E61D2A4D05` |
| Migration | none (head `20261103120000_ImpactsEnteredByHand`); provision not run (no infra change, azd environment unchanged) |
| Deployment | Web OneDeploy `a8a1d9d4-1ef3-44f0-a621-9dd4253f9d2d` at 09:10:39Z, read back Running at the SHA on attempt 1; Worker and classifier `config-zip` `5ed42343-e314-42bb-81cf-ae921f2cc6ad` and `6d7083cd-60c8-4799-9b46-59c4bf97dc6f` |
| Smoke | passed at 09:19:25Z; package SHA equals `web.zip`; intake liveness last poll 09:10:00Z, subscription expires 2026-11-05 14:00Z; no send was made |
| Evidence | `artifacts/releases/release-139-75152f80`, logs `artifacts/release-139/` |
| Owed | none; #1881 is superseded by R655 |

## Release 138 - 3 November 2026 (deployment live)

Release 138 deployed PR 1878 alone: a Collision Engineers staff forward held in instructions@ is answered from the mailbox that forwarded it, or from the mailbox staff choose with the Principal's Send from first (R651–R654), with the Fable review's forwarded-recipient parse fix. Normal route; no migration; no outage, under the operator's grant of 3 November 2026 ("authority to deploy this granted and approved within this prompt now"); the PR's CI covered the merge tree, which equals the PR head's.

| Observation | Value |
| --- | --- |
| Source | `ac836fef5d80ce1b772428171b693162edcc8103`, promoted from `7a6a43b92f6db1c22a759d0b58932753c4649183`, with SHA, manifest and route as the read-only run reported them; `main` and `dev` read it back |
| Packages | manifest `046EB12EAFFA3302D65DCC76C3CE4B19A2501964DDD3380BFB9F2FD2CB53BBA7`; `web.zip` `8D787D87FF7D7B1041A6B800728486DE7693EC2953F71DEA8772A68D056F1F23`; `worker.zip` `2D8877A053FB88559AAB3553C49C1C2ACCB2356BE359BA2362F26389C356F03A`; `classifier.zip` `CEF9EB0C0274C8F441285352CAED59CA56580A32295FA1B2329026B90ADFD009` |
| Migration | none (head `20261103120000_ImpactsEnteredByHand`); provision not run (no infra change, azd environment unchanged) |
| Deployment | Web OneDeploy `dfca3d40-1eaf-4bb7-9307-77861b36d350` at 18:58:10Z, read back Running at the SHA on attempt 1; Worker and classifier `config-zip` `9a4cab54-62c9-4f0c-a623-c7c633fbed12` and `df77d675-6d89-4a94-8163-6705b6c7975a` |
| Smoke | passed at 19:04:46Z; package SHA equals `web.zip`; intake liveness last poll 18:55:00Z, subscription expires 2026-11-05 14:00Z; no send was made (operator: desk@ is send-ready, "dont send anything") |
| Evidence | `artifacts/releases/release-138-ac836fef`, logs `artifacts/release-138/` |
| Owed | #1881 (the store and engine accept a cross-mailbox Reply for any message, not only an instructions@ staff forward) |

## Release 137 - 3 November 2026 (deployment live)

Release 137 deployed PR 1874 alone: v47, where the report needs at least one damage marker (R650) and an impact or a deliberate Skip impacts (R647–R649), and the Case page's Incident narrative spaces its sentences. Normal route; no migration; no outage, under the operator's grant of 3 November 2026 ("Deploy auth granted now"); the PR's CI covered the merge tree, which equals the PR head's, so dev→main CI was skipped as the operator directed.

| Observation | Value |
| --- | --- |
| Source | `7a6a43b92f6db1c22a759d0b58932753c4649183`, promoted from `7231a4b375c46d2a00aa700bdf1dc2f80c3aa453`, with SHA, manifest and route as the read-only run reported them; `main` and `dev` read it back |
| Packages | manifest `6F58C82E8FA5CBD78B8A6A5424D552360477F2BACB43FAABCE1BEBD2F4CEBBC0`; `web.zip` `452C96FFAE7B02153DC1633A92A503CC0CE3794D43E7889D5AC12511863FA7C8`; `worker.zip` `955EA262A52A1EEA73FCA949C507EC0B205E18C76F2587FEE58330DD9D576F67`; `classifier.zip` `93832031AA201FF284471C937CFEEC08526E689AC6D2D45C53EDED665C53A3CE` |
| Migration | none (head `20261103120000_ImpactsEnteredByHand`); provision not run (no infra change, azd environment unchanged) |
| Deployment | Web OneDeploy `c41250e2-a0a8-4213-8226-45f2ee05a701` at 14:40:29Z, read back Running at the SHA on attempt 1; Worker and classifier `config-zip` `3433fc72-7379-486d-b57c-4a7b58cf6f83` and `390c1c9a-61ba-41f3-b4a6-46e7bd561e65` |
| Smoke | passed at 14:47:44Z; package SHA equals `web.zip`; intake liveness last poll 14:45:03Z, subscription expires 2026-11-05 14:00Z; `/diagnostics/version` and the served Damage script and styles carry the release |
| Evidence | `artifacts/releases/release-137-7a6a43b9`, logs `artifacts/release-137/` |
| Owed | #1876 (live walk of the v47 Damage journey) |

## Release 136 - 3 November 2026 (deployment live, destructive migration)

Release 136 deployed PR 1869 alone: the v46 impact entry, where staff enter each impact by hand (severity, location, note) and the damage markers are the report's picture only, superseding R639 (R642–R646). Destructive route with one destructive migration and a short outage, under the operator's grant of 3 November 2026 ("approved. proceed immediately"); the PR's CI covered the merge tree, which equals the PR head's.

| Observation | Value |
| --- | --- |
| Source | `7231a4b375c46d2a00aa700bdf1dc2f80c3aa453`, promoted from `fb4c5e741436ebfdb90da74bea4aef76056e29b2`, with SHA, manifest, route and old Web SHA as the read-only run reported them; `main` and `dev` read it back |
| Packages | manifest `08BAB3B06767E4017B1119B03921D59682CA691E1E0F7D19B2D572B7B53D6DD2`; `web.zip` `BCDB7A4DCCF6C9C2666FE33DE0C33A362863DCC26005CBF847C97D82076A669C`; `worker.zip` `77745822B66640C0CB9B4BC18EFDBBF314CACC54FD0F55ACDDE89D5D0A9282A9`; `classifier.zip` `A2728734CCA4A669192651AFE95E408154655FBFF130631A3420111683C9513B`; `efbundle.exe` `7ACFCDE6DBCC3BB9185AA06C8AD686AB3AB52B791A98CDF323EE10534F7FA0D4` |
| Migration | `20261103120000_ImpactsEnteredByHand`, applied with the classifier, Worker and Web read back Stopped and unserved: it rewrites each `damage.impacts` value to marker discs and deletes the `assessment.impact_location` and `assessment.impact_severity` rows; forward only. Bootstrap census 698/490; head read back at the new id |
| Deployment | Worker staged `05b053cf-6987-40a4-9aeb-93c99eac2776` before SQL; Web OneDeploy `deba14e9-5a2c-4bcb-b7b5-c46bd7bf99e4` onto the stopped site at 11:50:32Z, Web read back Running at the SHA on attempt 1; classifier `config-zip` `ad56fdee-8436-468b-906f-360b4f19a842`, again `c2f4e571-89ae-4291-bb36-1a0c9aa96138` on the resumed run |
| Provision | the route's `azd provision` again skipped as unchanged and left the five Worker Disabled settings `true` (#1857), so the first full smoke failed; `azd provision --no-state` at 11:56–11:58Z set them `false`, and the same `-Execute` line resumed at the activated stage |
| Smoke | passed at 12:01:41Z; package SHA equals `web.zip`; intake liveness last poll 12:00:00Z, subscription expires 2026-11-05 14:00Z |
| Owed | #1857 (provision skip), #1870 (live check of the impact entry) |

## Release 135 - 2 November 2026 (deployment live)

Release 135 deployed combine PR 1866 (PRs 1859, 1853, 1855, 1860, 1862, 1861): the Release 134 record; a manual Case's instruction clearing when its last required item lands (R630, R631); the v44 Inbox message settings with the Case / PO picker in the link dialogs (R632–R637); Get valuation choosing its card (R638); touching damage bursts as one impact location (R639); and the v45 Report wording holding every report content choice (R640, R641). Normal route, no migration, no provision; no outage.

| Observation | Value |
| --- | --- |
| Source | `fb4c5e741436ebfdb90da74bea4aef76056e29b2`, promoted from `f6de68d1ad0578c4b3f4d80ff96ef65a575e68f7` under the operator's grant of 2 November 2026 ("the deploy is approved and auth'd"), with SHA, manifest and route as the read-only run reported them; `main` and `dev` read it back |
| Packages | manifest `4E73CD38ED6AEC155BCF96B3C2424DF54542257C8ECCE811D5FAF3047E11D685`; `web.zip` `BA080ADAB7EB3C56A3806240175167711D4AD7A51E59E0EBEA20B2469AAC4735`; `worker.zip` `4F6CF643C2679A85790B804407AC75A3CFDE8CFCC4E6654CEA5207217F0D6568`; `classifier.zip` `7CC105C352617E86EDB356A10BCFE408C731714EA8686287A7A7662F62EB925C`; `efbundle.exe` `B1641252EAA70AFF894F112DA0D4BB63E06535C4E01F916BFB01427BEF23FD0A` |
| Migration | none; head `20261102154620_RemoveCaseContactAndClientNotes` |
| Deployment | Web OneDeploy `e236c78d-69f6-436a-a1c3-c40e0cf4b37b` at 22:35:31Z, read back Running at the SHA on attempt 1; Worker `config-zip` `31b8154d-cfa1-4b61-9201-31abcd171288`; classifier `config-zip` `cd0873ce-dd9d-4a21-8d9c-c0c9c7abb639` |
| Smoke | passed at 22:41:56Z; package SHA equals `web.zip`; intake liveness last poll 22:40:03Z, subscription expires 2026-11-05 14:00Z |
| Owed | #1867 (live checks of the changed journeys) |

## Release 134 - 2 November 2026 (deployment live, destructive migration)

Release 134 deployed combine PR 1856 (PRs 1843, 1854, 1840): the Release 133 record; the a.QDOS26157 report findings (R607–R616); and the v43 Case details, which bring the Claim source card and the Principal notes panel and remove the Case contact and Notes from client (R617–R622). Destructive route with one destructive migration and a short outage, under the operator's grant of 2 November 2026 ("deploy authorized and approved"); no restore point was recorded (operator: "not needed this time").

| Observation | Value |
| --- | --- |
| Source | `f6de68d1ad0578c4b3f4d80ff96ef65a575e68f7`, promoted from `b78a4f63c922b0a3bbd97079e57111e43e8a4db3`, with SHA, manifest, route and old Web SHA as the read-only run reported them; `main` and `dev` read it back |
| Packages | manifest `9511EDE4C55F4902CBD40898338BE2C5B7A1215F150DAD4961F245C42508AD1E`; `web.zip` `F2BF209C0AC38947E7890309A3AC0E0BF6391F66ED4F993E1C0E2C556A4F5176`; `worker.zip` `D6E53CF92CE2649A6B170B5A5A422AA767DBB48C40383BAC2315E7C6D072AD4C`; `classifier.zip` `843A9FD8DEA587EEBD9DB1D20210BF9EA2FF044C16D44FB4E09027863CC28B01`; `efbundle.exe` `9A378B86B4089887EC0D2571E1193BC8CD87DFFF30807F34C4B74F11D4272CD3` |
| Migration | `20261102154620_RemoveCaseContactAndClientNotes`, applied with the classifier, Worker and Web read back Stopped and unserved: it deletes the `contact_*` and `client_notes` CaseDataFields rows and drops the three `InstructionDrafts.FileHandler*` columns; forward only. Bootstrap census 698/490; head read back at the new id |
| Deployment | Worker staged `8fe0e991-a6b3-4356-b66a-87695a59adb6` before SQL; Web OneDeploy `d54b7cfc-d566-4df2-9ad4-8d46cfba3b25` onto the stopped site at 16:51:11Z, Web read back Running at the SHA on attempt 1; classifier `config-zip` `205a5b78-e3d8-49d1-a880-1884f12b9072`, again `82f43cd9-5d31-4b70-b099-7abec800a582` on the resumed run |
| Provision | the route's `azd provision` skipped as unchanged and left the five Worker Disabled settings `true`, so the first full smoke failed; `azd provision --no-state` at 16:57–16:59Z set them `false` (#1857), and the same `-Execute` line resumed at the activated stage |
| Smoke | passed at 17:02:42Z; package SHA equals `web.zip`; intake liveness last poll 17:00:00Z, subscription expires 2026-11-05 14:00Z |
| Owed | #1857 (provision skip), #1858 (live checks of the changed journeys) |

## Release 133 - 2 November 2026 (deployment live, additive migration)

Release 133 deployed combine PR 1841 (PRs 1815, 1817, 1818, 1823, 1824, 1825, 1832, 1838, 1839): one-off Cases for private customers under the PRIVATE record, which may also be an Audit `a.REG` on a shared repeat (R574–R581, R600, R601); Create case takes files, offers Audit and suggests Principal codes without PRIVATE (R594–R597, R602); every Case starts at a Good pre-incident condition (R598); the Unidentified record tidy (R599); no sender CSS or signature placeholders in message bodies; the MCP staff roster (R593); the combine-prs renumber script; and rulings R582–R592. Normal route after two additive migrations, no provision; no outage; the ordinary intake wipe followed.

| Observation | Value |
| --- | --- |
| Source | `b78a4f63c922b0a3bbd97079e57111e43e8a4db3`, promoted from `826c2aed70555ac68fd4d2ce0bde032901db206d` under the operator's grant of 2 November 2026 (SHA, manifest and route as the read-only run reported them); `main` and `dev` read it back |
| Packages | manifest `4F1F982BCCBB63F8AC3DAF63F3910A3C0C07662AC9C687A6D4C8EB0ABAD39A3D`; `web.zip` `5B4F16D8ABED2D46FE8803F7E78B226A5B6A92713C21DC6EA5511A2DA4C4D7C9`; `worker.zip` `036C031F73BB96988BEBF69D93C93BBF0530D316D9E60DC1D4AAF80242670CFB`; `classifier.zip` `904E7703CCBF0CFA879B94A02CE9B8A3D6DD32E737A9A863CFC2D90B787BC711`; `efbundle.exe` `829147FCEBA98A9482564378C13A79647A384248207A37D5FCD7FD9F000FEC4F` |
| Migration | the bundle applied `20261101120000_PrivateCustomerPrincipal` (column with default, check, PRIVATE seed) and `20261102120000_PreIncidentConditionDefaultsGood` (insert-only backfill) with the Release 132 bytes live; bootstrap census 698 permission rows and 490 effective runtime DML rows verified; head read back `20261102120000_PreIncidentConditionDefaultsGood` |
| Deployment | provision not run (no infra change, azd environment equal to Release 132's); Web deployment `204d5937-2932-419d-ab25-48f6b129f3a5`, the site read back Running, ready and at the SHA on attempt 1; Worker `config-zip` deployment `ae452745-f6ae-406c-bcd5-4118cc914e53` and classifier `92bb7730-4a0e-4f96-8742-084b98b664be`; classifier `Running` |
| Smoke | passed at 12:25:38Z; package SHA equals `web.zip`; intake liveness last poll 12:25:00Z, subscription expires 2026-11-05 14:00Z |
| Intake wipe | approved ordinary wipe right after the smoke. The Worker read back `Stopped` before execution and `Running` after it. The dry run just before execution found 1,048 blobs (1,038,989,001 bytes) in `pegcustody252ow37gij/transient-intake` and 84 tables / 6,568 rows in SQL `pegasus`, with the preserve list 39/39 found. The batch reported 6,576 affected rows including the mail-boundary update. Blobs remaining: 0; wiped tables still holding rows: 0; preserved rows after: 7,516. The mail cutoff is `2026-11-02T12:28:29.2816818+00:00`. Sequences were unchanged at 156/18/1, and presets, templates and image tags at 6/6, 3/3 and 4/4. `authentication-ring`, `box-links`, `pegtrans252ow37gij`, Outlook and Box were untouched; no `-ResetTestEstate` |
| Owed | #1842 (live checks of the changed journeys) |

## Release 132 - 30 October 2026 (deployment live)

Release 132 deployed PRs 1811, 1812 and 1813, merged into `dev` one after another: a new repair spec's first save keeps editing that spec (1811); the Inbox All tab lists every message, and unlinked mail with no category reads No Case Found (1812, R570, R571); and a Case-page commit carries the report wording only when it changed, so a quick second edit after Total loss is no longer refused (1813, #1800, R572, R573). Normal route, no migration, no provision; no outage.

| Observation | Value |
| --- | --- |
| Source | `826c2aed70555ac68fd4d2ce0bde032901db206d`, promoted from `e70e57bf334641c9bdcac24284c4b86aa430ae27` under the operator's grant of 30 October 2026 for the normal route with no migration and no provision (SHA and manifest as the read-only run reported them); `main` and `dev` read it back |
| Packages | manifest `5490A9C94C131505FF7D4B332865383328F2A766C4E8BC70EA51116441909A10`; `web.zip` `C8D0A002EAA0DF05CD420D306999F92F21753873D5A00D450D4F8102F2D6C168`; `worker.zip` `EF396D3B3CB712B37D579C9AD9DCEB5C83C8108A7E93F915FFD3A85B7D84BEDB`; `classifier.zip` `F78F360974FFDC2A51217BB8A9C4215B59392AD1FACEEA4E988866E016247D74`; `efbundle.exe` `27478843F64653723B336225478AA2F4EBD52E5AE6FEE5EB709B3BC65DA3AB63` |
| Migration | none; head `20261030120000_DeclaredUploadDocumentType` |
| Deployment | provision not run (no infra change, azd environment equal to Release 131's); Web deployment `05819026-a235-4347-89f4-59e1be6fefb4`, the site read back Running, ready and at the SHA on attempt 1; Worker `config-zip` deployment `22ac5fc8-da99-44d9-9ba4-83549e04f149` and classifier `7bf38b31-b53f-41c3-a46c-31c3af2df37c`; classifier `Running` |
| Smoke | passed at 20:14:13Z; package SHA equals `web.zip`; intake liveness last poll 20:10:00Z, subscription expires 2026-11-04 08:25Z |
| Owed | #1816 (the live checks of the three changed journeys); #1814 (the refusal message's wording, R573) |

## Release 131 - 30 October 2026 (deployment live, additive migration)

Release 131 deployed combine PR 1808 (PRs 1793 to 1796 and 1802 to 1806 over the merged lease PR 1798): requests to edit a Case with Accept, Deny and Take over anyway and the Automation Actor asking through `pegasus_edit_begin` (R550 to R558), the Inbox Type-column chips and one message view (R559, R560), the Pre-Pegasus case category with the two read-only old-system lookups and `Box__OldSystemRootFolderId` (R561), the Documents-tab Change type and the type chosen at upload (R562 to R565), the proof checklist run recorded and its file deleted, and the release skill rewritten around `scripts/Invoke-Release.ps1` (R566 to R569). First release run by that script: normal route after two additive migrations, provision once; no outage.

| Observation | Value |
| --- | --- |
| Source | `e70e57bf334641c9bdcac24284c4b86aa430ae27`, promoted from `0c2acf91bf57e9f11916b536a41478e684dc48e0` under the operator's standing grant of 30 October 2026 (SHA and manifest as the read-only run reported them); `main` and `dev` read it back |
| Packages | manifest `8381614EAA639AAD3CD13897D333C4C93D2B56D534F673D0FF6ADA64FB1786B8`; `web.zip` `62C9F6D977539ED5F02289BDF2BF20954279AD3C4C19B5C6BED7DF6C049FA77D`; `worker.zip` `9D9046608C13E03402BCB14AF4931B2DBDB22ECA1D34F9359D8C963B2FDAE7EE`; `classifier.zip` `9EFF6649514BDA8DDF89D2DDCFED436C2BA7C80E84935CBE3FC968495E614FBF`; `efbundle.exe` `CE4CA3CCE7DE3F2E0A35815D4E15F8C4B0F6541292F9BEB46162599982109F68` |
| Migration | head read `20261029200000_EditScopeRetainsItsClaim`; the bundle applied `20261030101326_CaseEditRequests` and `20261030120000_DeclaredUploadDocumentType` with the Release 130 bytes live; bootstrap census 698 permission rows and 490 effective runtime DML rows verified; head read back `20261030120000_DeclaredUploadDocumentType` |
| Deployment | `azd provision` in 127 s (adds `Box__OldSystemRootFolderId`); Web deployment `7362b61c-9a35-4ff2-8b25-fa1b854913a9`, the site read back Running, ready and at the SHA on attempt 12; Worker and classifier `config-zip` deployments (ids not reported by the CLI); classifier `Running` |
| Smoke | passed at 13:31:19Z; package SHA equals `web.zip`; intake liveness last poll 13:30:00Z, subscription expires 2026-11-04 08:25Z |
| Owed | #1809 (the live checks that need a signed-in session or a second client, the classifier dry run against folder `3220448509`, Exchange RBAC for the three unadmitted mailboxes, the Release 130 connector re-add); #1712 now walks this release's ledger |
| Evidence | `artifacts/releases/release-131-e70e57bf` (packages, `azd-env.sha256`, the read-only and approved run logs) |

## Release 130 - 30 October 2026 (deployment live, additive migration)

Release 130 deployed the edit-authority collapse to three concepts (PRs 1753, 1757, 1780, 1787, 1788, 1791, 1792; R514 to R524), the Administration walk fixes by page (PRs 1758 to 1760, 1762, 1765, 1767, 1769 to 1772, 1774, 1779, 1783; R527 to R543), the intake and Case fixes and features (PRs 1754, 1761, 1763, 1766, 1778, 1781), the automatic Glass's valuation at Case creation with its Administrator switch (PR 1786; R545, R546), one Automation MCP server at `/mcp` (PR 1789; R547 to R549, ADR-0087), the wipe fix (PR 1768), the rulings and records (PRs 1764, 1773, 1777, 1782, 1784, 1785) and a focus-ring style (PR 1790). Normal route after an additive migration identity; no outage.

| Observation | Value |
| --- | --- |
| Source | `0c2acf91bf57e9f11916b536a41478e684dc48e0`, promoted from `da599ad33b37adfc86b0363f91123fdf839ea794` after `MERGE AUTH GRANTED`; `main` and `dev` read it back; CI on the `main` push green |
| Packages | manifest `F670ADDBDE425FDA926BF24490F81F92DF4840B549FA37584927A601E0DC3EFE`; `web.zip` `370CFE550218F9232888C5B8CFB1614ADADAEE623B4F5D4C50027263528FA022`; `worker.zip` `7991664C01646349B44DC49E617B11489D55D30DEDFC28D4DA89052D1B4274E2`; `classifier.zip` `83C23BAC6D074832CE231DF97485CA629D02B657B4D0B3580F360FFF01D6D070`; `efbundle.exe` `8D008A0E5F71A2DCC34E83AE347937235F4196846897103532F740E1BB4BDDFF` |
| Migration | history read 245 rows at `20261028171919_Baseline`; the bundle applied `20261029120000_RemovedValuationPresetFreesItsLabel`, `20261029160000_WorkerReadsAutomaticValuationSwitch` and `20261029200000_EditScopeRetainsItsClaim` (248 rows); bootstrap census 698 permission rows and 490 effective runtime DML rows verified (the Worker's `SELECT` on `SendToAiControl`) |
| Deployment | `azd provision` in 1 min 38 s (deployment `pegasus-prod-1793349199`); the classifier's `PEGASUS_MCP_URL` read back `https://pegasus-prod-web-252ow37gij.azurewebsites.net/mcp`; Web OneDeploy `8571ee76-4a4d-4f51-9103-1d71a71cd538` (site started in 127 s, `/diagnostics/version` read back the SHA on attempt 1); Worker `4b1c9a9c-e50f-4e10-9f1f-593b98ad3028`; classifier `2d013809-bd83-408a-ab17-c39d992ad788` |
| Smoke | passed at 08:43Z; package SHA equals `web.zip`; intake liveness last poll 08:40:01Z, subscription expires 2026-11-04 08:25Z |
| Web start | `model build 18554 ms, first connection 1420 ms`; warm-up finished in 33,977 ms (key-ring 17,452 ms and oauth-certificates 19,432 ms alongside); figure added to #1775 |
| Owed | an Administrator removes the seven old connectors and adds one at `<origin>/mcp` with one consent (ticked scopes); the classifier read-back on `/mcp` with one connecting tick; `tools/list` sizes (classifier 7, full connector 69); the proof checklist not run (no signed-in session): #1776 |
| Evidence | `artifacts/releases/release-130-0c2acf91` (packages, build and route logs) |

## Release 129 - 29 October 2026 (deployment live, Baseline adopted)

Release 129 deployed the 28 October simplicity run: the one `Baseline` migration (PRs 1725, 1726) with production adopting it by history rows, the compiled model removed (PR 1727, R480), the index key-size rule (PR 1729), 38 Core use-case interfaces removed (PRs 1741 to 1745), one in-process cache mechanism (PRs 1748, 1750), the telemetry consolidation (PR 1749), the Triage-thread reply link (PR 1751), the three alert rules (PR 1752, #1355) and the documentation shrink (PRs 1734 to 1740, 1747, 1755, 1756). Normal route after an additive migration identity; no outage.

| Observation | Value |
| --- | --- |
| Source | `da599ad33b37adfc86b0363f91123fdf839ea794`, promoted from `3dec03615836fbe7cff29414b3b33286ab6a9ff4` after `MERGE AUTH GRANTED`; `main` and `dev` read it back |
| Packages | manifest `924570054EDE127457D579063016309E60DB94EAA011047C97D8573DC876F709`; `web.zip` `A25067DD33FC3E91790BEFF1F5DA56DEB26CADB3936DE64F00CEE5E139DB0A17`; `worker.zip` `83AB82270D4FF5A233E3256BE038BB97E5F1B2A4CCD1E3C58CB39FABE4093EDA`; `classifier.zip` `86AD7E834E235E12751F290D60AF2866FC2E4F9AAFD356B514CEAA87CEBB3B17`; `efbundle.exe` `B47148B5AD826E543F657D08A1474C1F3ECAB11DD717F9F845755E15EF599C47` |
| Migration | history read 243 rows at `20261028090000_ExternalWorkSourceCase` (not the 244 the recipe expected: the squashed denials migration never ran there and its four denials were already held, PR 1755); the adoption inserted `20261028160000_DenyApprovedMailboxSubscriptionsWrites` and `20261028171919_Baseline` (245 rows); the bundle printed "No migrations were applied"; bootstrap census 697 permission rows and 489 effective runtime DML rows verified |
| Deployment | `azd provision` in 2 min 1 s created `pegasus-prod-matching-failures` (sev 2, PT15M/PT15M), `pegasus-prod-chasing-failures` (sev 2, PT15M/PT1H) and `pegasus-prod-web-readiness` (sev 1, PT15M/PT15M), all enabled beside the two existing rules; Web OneDeploy `97dfde12-9f65-48d7-bdf6-6dcbd708ad3f` (the CLI reported Kudu 502 during the container recycle while Kudu recorded success; `/diagnostics/version` read back the SHA at 11:32:14Z); Worker `e77c2e93-3ecb-45a7-a86f-2549dd0592b0`; classifier `3350fc65-b523-488e-abce-71ace71f1c3f` |
| Smoke | passed at 11:37Z; package SHA equals `web.zip`; intake liveness last poll 11:35:03Z, subscription expires 2026-11-04 08:25Z |
| Web start | first start on the new package: `model build 17156 ms, first connection 1003 ms` (the release workstation built it in about 450 ms), warm-up finished in 46,818 ms, `case-page` left running at 16,020 ms; issue #1775 |
| Owed | the release proof checklist was not run (terminal release, no signed-in session): issue #1776; no alert has fired yet, so the three rules are provisioned, not proved |
| Evidence | `artifacts/releases/release-129-da599ad3` (packages, build and route logs) |
## Release 128 - 28 October 2026 (deployment live, additive migration)

Release 128 deployed the one Case combination model (PR 1689) and the one photograph rule (PR 1697), with the wipe and skill-script fixes (PRs 1705, 1704) and the simplicity rules and rulings register (PR 1710). Normal route after the additive migration `20261028090000_ExternalWorkSourceCase`; no outage.

| Observation | Value |
| --- | --- |
| Source | `3dec03615836fbe7cff29414b3b33286ab6a9ff4`, promoted from `5746b248864f331092f82b663c35e62f229e2df4` after `MERGE AUTH GRANTED`; `main` and `dev` read it back |
| Packages | manifest `512C98D2B557CD6E00BD70ABA970B42530551DF0DBEDC28332930EFA8E0010B4`; `web.zip` `92816878C1D9591253C8701AA229B2B76EBE5B4CE754A6B783F9DC4B4C95C5C7`; `worker.zip` `6F4855B81CF4993B2725D11DFC736C9E49072E8835905367227B745F49EC2338`; `classifier.zip` `64B24F7D5E9FBB8DE02A5F316E3E544184AF9F9367DFAE044335598EC8024B96`; `efbundle.exe` `320F43FE6589F5D39A7DCAB547A9E51596EF48D6B776F1721D05FF029BE43385` |
| Migration | head `20261027200000_RetainedMailReplyIdentityIndex` (242) to `20261028090000_ExternalWorkSourceCase` (243); bootstrap census 697 permission rows and 489 effective runtime DML rows verified |
| Deployment | `azd provision` found no changes (no deployment id); Web OneDeploy `665dc936-8062-4972-b73f-01bab7f377f0`, `/diagnostics/version` read back on attempt 1; Worker `cc0eccba-e0aa-418c-a4f2-77878544dac6`; classifier `99e9ecc9-d757-4948-86e3-c679c4814e13` |
| Smoke | passed at about 09:36Z; package SHA equals `web.zip`; intake liveness last poll 09:35:00Z |
| Live checks | a.QDOS26120 re-upload and Triage image merge on first reconcile: not run, issue 1711 |
| Owed | issues 1711, 1712 (Release 127's 1580/1100 px Inbox walk), 1713 (Release 126's Query Store after-window) |
| Evidence | `artifacts/releases/release-128-3dec0361`; driver `artifacts/releases/release-128-driver` |

## Release 127 - 28 October 2026 (deployment live)

Release 127 deployed [PR 1708](https://github.com/collisionengineers/pegasus/pull/1708): the Inbox
ledger row drawn as the v42 mockup draws it. After Release 126 the operator reported the ledger's
tick, Case and Type cells stacked on each other: `inbox.css` still carried v41's absolutely
positioned row tick, so the tick left the grid and every cell sat one column to the left. The row
had also been flattened against the mockup's row builder; it now carries the unread dot, the mono
Case link with the vehicle beneath, the category as its status chip with the work type beneath (or
the purple Query chip with its assignee, or plain Correspondence), the subject with its attachment
pill and one-line excerpt, the sender with the Principal, the received date with its clock, and
Reply beside Dismiss where the mailbox can send. The range from Release 126's `main` `5bc665966` is
2 pull requests and 7 files: the Release 126 record
([PR 1707](https://github.com/collisionengineers/pegasus/pull/1707)) and PR 1708 (`ccb5d7bd8`,
merged into `dev` as `5746b2488`).

The route was the normal one (release skill section 8). The migration identity was unchanged at
`20261027200000_RetainedMailReplyIdentityIndex` (242 applied) and no migration ran. Web, Worker and
the classifier are Running on the approved release and the full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `5746b248864f331092f82b663c35e62f229e2df4`, the merge of PR 1708 into `dev`; `main` and `dev` both read it back. Manifest SHA-256 `61A32A373F63A966077DC4E213BAA186C534B75FDB58ED31EB7E720D3A6657D8` (schema 3, clean, Windows x64: `efbundle.exe`, rid `win-x64`, created 01:30:50Z); `web.zip` `8DABC048A1651769A06934E1F1BD3C5C4D1D8ACFC6C323B09712033EF98A9ED3` (113,784,077 bytes); `worker.zip` `72CDE85B6D6433311B1546139F0CD946B3BCE7398D4D7829318A7AAB17FA6ABC` (67,325,858 bytes); `classifier.zip` `A394CFCB619A04443EA678C743ECD94E4730F8E8B15CD5BF1DFCAE4BCECD67CA` (137,787,872 bytes); `efbundle.exe` `0A5ADEABBDC2C0653927D582ECD83735C58F68949BF01F3F794298909B92B6A2` (265,060,303 bytes). Built in a detached worktree at `../pegasus-worktrees/release-5746b248`; `Local`, `Artifact` and `PreDeploy` plan validations passed. Tools: .NET SDK `10.0.302`, Node `v24.15.0`, npm `11.14.1`, Azure CLI `2.88.0`, azd `1.34.1`. |
| Review and promotion | PR 1708 passed all 10 active CI checks (`changes`, `invariants`, `unit`, six `sql-integration` shards and `sql-integration-coverage`); its 63 `MailWorkspaceWebTests` also ran locally. One atomic leased promotion from `main` `5bc665966e89a5e884fe7cc46f1136bc348259e1` to `5746b248864f331092f82b663c35e62f229e2df4` after fresh `MERGE AUTH GRANTED` on that SHA; `main` and `dev` both read `5746b248864f331092f82b663c35e62f229e2df4`. |
| Migration | None ran. Identity unchanged at `20261027200000_RetainedMailReplyIdentityIndex` (242 applied). |
| Deployment | Azure CLI account `digital@collisionengineers.co.uk`. Serving Web before: `5bc665966e89a5e884fe7cc46f1136bc348259e1`, `0.1.0-alpha.1`; Web Running on `DOTNETCORE\|10.0`, Worker Running, classifier Running. The operator approved the manifest and the three targets. `PreProvision` passed and `azd provision` succeeded with no infrastructure change. `web.zip` deployed with `--clean true --restart true` (OneDeploy record `267f7416-d61c-4b94-a536-6c6f23162863` succeeded 01:34:58Z, package `20261028013435.zip`; the site started in 175 s under the CLI's tracker) and `Wait-PegasusExpectedWebSite` read back `Running`, `/health/ready` 200 and `5746b248864f331092f82b663c35e62f229e2df4` / `0.1.0-alpha.1` on attempt 1. `worker.zip` deployed (deployment `1c5be6f2-b797-46f1-893f-9234d5ed3ff1`); `classifier.zip` deployed (deployment `8aff845f-7cb1-44d7-bb8f-bb6c03e89f02`). |
| Smoke | Full smoke passed at about 01:40Z: Worker activation `approved-live-worker` (census all `false`); Web Running on `DOTNETCORE\|10.0`; active package `20261028013435.zip` SHA-256 equals the approved `web.zip`; intake liveness passed (last poll 01:35:00Z; Graph subscription expires 2026-10-31 08:25:00Z). |
| Behavioural check | None beyond the smoke: the ledger needs a signed-in staff session. The operator's view of `/Inbox` after this release is the proof; the browser walk at 1580 and 1100 widths named in PR 1708 is still owed. |
| Still owed | Release 108 to 126 owed items otherwise stand; Release 126's Query Store "after" window is still to be re-read. |
| PR states | PR 1707 and PR 1708 read Merged with their heads in `main` and `dev`. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-127-5746b248`; the driver scripts at `artifacts/releases/release-127-driver` (`release-preflight.ps1`, `release-promote-127.ps1`, `release-build-127.ps1`, `release-common.ps1`, `release-normal-127.ps1`). |

## Release 126 - 27 October 2026 (deployment live, destructive migration)

Release 126 deployed the signed-off v42 design round (`pegasus-design-archive` `v42_planning` at
`342e78b`; operator rulings of 27 October 2026) as six pull requests: assignable Case queries
([PR 1700](https://github.com/collisionengineers/pegasus/pull/1700),
[ADR-0085](adr/0085-mail-is-instructions-or-correspondence-and-queries-are-assignable.md)),
mail linking v2 with reply-header and claim-reference signals
([PR 1701](https://github.com/collisionengineers/pegasus/pull/1701)), the three mail categories,
the Inbox ledger and the message record's cog menu
([PR 1702](https://github.com/collisionengineers/pegasus/pull/1702)), the Work Centre's Design A
views computed on read ([PR 1703](https://github.com/collisionengineers/pegasus/pull/1703),
[ADR-0086](adr/0086-work-centre-views-are-computed-on-read.md)), the Upload record sheet
([PR 1699](https://github.com/collisionengineers/pegasus/pull/1699)) and the lead's review
follow-ups ([PR 1706](https://github.com/collisionengineers/pegasus/pull/1706)). The classifier
agent's prompt and tools changed with the categories. The range from Release 125's `main`
`e9b69340c` is 7 pull requests, 205 files, 8,791 insertions and 5,749 deletions, including the
Release 125 record ([PR 1695](https://github.com/collisionengineers/pegasus/pull/1695)).

The route was the destructive one (release skill sections 6, 7, 9 and 10) in a window the operator
opened at about 22:20Z. Four migrations were applied over `20261023160000_AppliedValuationCalculationRows`
(238 applied): `20261027120000_RetainedMailReplyIdentities` and
`20261027200000_RetainedMailReplyIdentityIndex` (additive), `20261027140000_AssignableCaseQueries`
(additive) and `20261027160000_ThreeMailCategories` (destructive, forward-only: every
`IntakeMailClassificationDecisions` row and `IntakeMailClassificationHistory` snapshot in the
categories `pre-instruction`, `images-received`, `case-update`, `query`, `billing`, `general`,
`not-client-related` and `internal-cc` became Unclassified with no category; `OpensQuery` was added
with default `false`, so no pre-existing decision carries it; `CaseQueries` rows were left alone).
The classifier, the Worker and the Web App were stopped and read back before SQL; the old package
was never started against the new schema. Web, Worker and the classifier are Running on the
approved release and the full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `5bc665966e89a5e884fe7cc46f1136bc348259e1`, the merge of PR 1695 into `dev` on top of the six v42 merges; `main` and `dev` both read it back. Manifest SHA-256 `183E96AA8A4A9EA23F88C4F8DAE60988380AF20FB358996FF08B89F071F451D5` (schema 3, clean, Windows x64: `efbundle.exe`, rid `win-x64`, created 21:18:10Z); `web.zip` `2AB0CFA2547014786E5DEE3686A63A987B8AF494F4226497E59CEB5A47511CC6` (113,778,892 bytes); `worker.zip` `05EA867A93ED73F823E1A231313A3E3AC9CC02F8EBFF30059D8757FEFD954F14` (67,325,684 bytes); `classifier.zip` `ECFB7A44435BA6A085AEC37F1342F87B6F7FF6A4C5D3D755571E5447AAB803B5` (137,787,872 bytes); `efbundle.exe` `CAA11426D8FD076814F0AD3B110DFAD150C6E18E02960614A2B4184019987C5D` (265,055,183 bytes). Built in a detached worktree at `../pegasus-worktrees/release-5bc66596`; `Local`, `Artifact` and `PreDeploy` plan validations passed. Tools: .NET SDK `10.0.302`, Node `v24.15.0`, npm `11.14.1`, Azure CLI `2.88.0`, azd `1.34.1`. |
| Review and promotion | Each of the six v42 PRs passed review (reviewer-beam, and the lead's own line-by-line read, which found and fixed before merge: an untranslatable New cases read in PR 1703, an images-paired read of a payload the store never writes in PR 1703, the Inbox ledger rows leaving the arrow-key roving selector in PR 1702, and test fixtures whose removed categories had been renamed to `Cancellation` in PR 1702) and all active CI checks; one shard of PR 1703's final run was re-run after a runner LocalDB pre-login timeout. PR 1695 (docs only) was merged into `dev` first at the operator's agreement so this record stacks on Release 125's. One atomic leased promotion from `main` `e9b69340c8901daf04708dfdda2777e3bc9165cf` to `5bc665966e89a5e884fe7cc46f1136bc348259e1` after fresh `MERGE AUTH GRANTED` on that SHA; `main` and `dev` both read `5bc665966e89a5e884fe7cc46f1136bc348259e1`. |
| Approval | The operator approved the destructive route, this manifest, the targets (`pegasus-prod-web-252ow37gij`, `pegasus-prod-worker-252ow37gij`, `pegasus-prod-classifier-252ow37gij`, SQL `pegasus-prod-sql-252ow37gij`/`pegasus`), the old serving Web source SHA `e9b69340c8901daf04708dfdda2777e3bc9165cf` and a window starting at once ("now", about 22:20Z). |
| Containment | Azure CLI account `digital@collisionengineers.co.uk`. Section 6 precondition: Web Running on `DOTNETCORE\|10.0` at `pegasus-prod-web-252ow37gij.azurewebsites.net`, `/diagnostics/version` reporting `e9b69340c8901daf04708dfdda2777e3bc9165cf`, equal to the approved old SHA. The five-member Worker Disabled census (`PendingWorkRecoveryFunction`, `UnifiedWorkFunction`, `UnifiedWorkPoisonFunction`, `StagedArtifactReconciliationFunction`, `SentEvidencePollFunction`) was set `true` and the Worker-only smoke passed `disabled`; the approved `worker.zip` was staged (deployment `76415222-57a2-43d0-885b-cdd9e47e1327`) and the smoke passed `disabled` again. Classifier stopped and read back `Stopped`; Worker stopped and read back `Stopped`; Web stopped and read back `Stopped` with `/health/live` answering 403 on the first poll. The fresh pre-SQL read-back repeated all three (Worker `Stopped`, smoke `disabled` with classifier `Stopped`, Web `Stopped`, `/health/live` 403). |
| Migration | `PreMigration` gate passed. `efbundle.exe` (SHA-256 equal to the manifest's) ran from `src/Pegasus.Web` against `pegasus-prod-sql-252ow37gij.database.windows.net`/`pegasus` under the migration host environment and applied `20261027120000_RetainedMailReplyIdentities`, `20261027140000_AssignableCaseQueries`, `20261027160000_ThreeMailCategories` and `20261027200000_RetainedMailReplyIdentityIndex` (exit 0). `Invoke-AzureDatabaseBootstrap.ps1` verified 697 catalogued permission/denial rows and 489 effective runtime DML rows. Live head read back `20261027200000_RetainedMailReplyIdentityIndex`, 242 applied, equal to the manifest's `migrationIdentity`. |
| Staging and provision | azd environment guard: `AZURE_RESOURCE_GROUP` `rg-pegasus-prod`, all 21 secret URIs in `pegasusprodkv252ow37g`, `PEGASUS_WEB_LOCATION` unset. With the site read back `Stopped`, `web.zip` (hash re-verified) was deployed with `--clean true --restart false --track-status false` and the site read back `Stopped` again. `PEGASUS_WEB_ACTIVATION=approved`, `PEGASUS_WORKER_ACTIVATION=disabled`; `PreProvision` passed against the disabled Worker; `azd provision` succeeded in 1 min 26 s (deployment `pegasus-prod-1793140850`, 22:42:04Z); the Worker-only smoke passed `disabled`; the site was still `Stopped`. |
| Activation | `PEGASUS_WORKER_ACTIVATION=approved-live-worker`; `PreProvision` passed against the observed disabled Worker; `azd provision` succeeded in 2 min 6 s (deployment `pegasus-prod-1793140987`, 22:45:00Z). `az webapp start`; `Wait-PegasusExpectedWebSite` read back `Running`, `/health/ready` 200 and `5bc665966e89a5e884fe7cc46f1136bc348259e1` / `0.1.0-alpha.1` on attempt 6. Worker read back `Running` without a separate start. Classifier started and read back `Running`; `classifier.zip` deployed (deployment `5ba1a1b3-285e-4f2a-931e-b93e803e36c7`). OneDeploy record for the Web package: `5e1b2e6f-385d-470b-9db0-68a96e2c6602`, 22:40:04Z to 22:40:06Z, package `20261027223948.zip`. |
| Smoke | Full smoke passed at about 22:51Z: Worker activation `approved-live-worker` (census all `false`); classifier `Running`; Web Running on `DOTNETCORE\|10.0`; active package `20261027223948.zip` SHA-256 equals the approved `web.zip`; intake liveness passed (last poll 22:50:00Z; Graph subscription expires 2026-10-31 08:25:00Z). Worker `Graph__ChangeNotificationUrl` and classifier `PEGASUS_MCP_URL` read back the Web App origin (`/hooks/microsoft-graph/mail`, `/mcp/mail`); no MCP server path moved. |
| Behavioural check | None beyond the smoke's liveness line: the changed journeys (Inbox ledger, cog menu, Work Centre views, query assignment) need a signed-in staff session, which the release terminal does not hold. The first office use is the live proof; the Query Store "after" window below is to be read then. |
| Query Store | `READ_WRITE`. Read-only windows saved under ignored `artifacts/performance/`: `release-126-before.csv` (24 h to 22:20Z, 274 statements) and `release-126-after.csv` (from 22:40Z; 0 statements at 22:58Z because the runtime-stats interval had not flushed — to be re-read after the first day of use). The before window was read after the release, from Query Store's retained intervals, not snapshotted beforehand. |
| Consequences | Mail in the eight removed categories is now Unclassified; linked it is Case correspondence, unlinked it waits in No Case Found. Existing PAV-dispute decisions do not carry `OpensQuery`. Mail delivered during the window (about 22:20Z to 22:40Z) was carried by the five-minute recovery poll once the Worker resumed. |
| Still owed | Release 108 to 125 owed items otherwise stand. Operator questions left open at review: whether an empty VRM signal set should veto linking under v2 (it no longer does, D16). |
| PR states | PRs 1695, 1699, 1700, 1701, 1702, 1703 and 1706 read Merged with their heads in `main` and `dev`. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-126-5bc66596`; the driver scripts at `artifacts/releases/release-126-driver` (`release-preflight.ps1`, `release-preflight2.ps1`, `release-readback.ps1`, `release-promote.ps1`, `release-build.ps1`, `release-common.ps1`, `release-contain.ps1`, `release-migrate.ps1`, `release-stage-web.ps1`, `release-activate.ps1`, `release-querystore.ps1`). |

## Release 125 - 27 October 2026 (deployment live)

Release 125 deployed [PR 1694](https://github.com/collisionengineers/pegasus/pull/1694) (persists
and reuses the 1-year `brego-session` cookie in `authentication-ring/brego-session.json` across Web
host restarts and 24-hour AWS Cognito `USER_SRP_AUTH` token refreshes so Brego's browser
device-approval challenge is not re-triggered on container recycles or subsequent days;
[ADR-0084](adr/0084-brego-valuation-account-and-valuation-report.md)). The range from Release 124's
`main` `373dcc81a` is 5 commits and 8 files: the Release 124 record
([PR 1692](https://github.com/collisionengineers/pegasus/pull/1692), `f80c4c150`) and PR 1694
(`97fb9ffc3`, merged into `dev` as `e9b69340c`).

The route was the normal one (release skill section 8). Before deployment, the active 1-year
`brego-session` cookie (`sessionId: 5e51b371-4c5f-44f9-808c-b7911bbc2573`, expires
`2027-10-27T08:55:59.412Z`, minted after verifying OTP `355228`) was seeded to
`pegcustody252ow37gij/authentication-ring/brego-session.json` (`105 bytes`, `09:51:57Z`). The
migration identity was unchanged at `20261023160000_AppliedValuationCalculationRows` (238 applied),
and no migration ran. Web, Worker and the classifier are Running on the approved release and the
full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `e9b69340c8901daf04708dfdda2777e3bc9165cf`, the merge of PR 1694 into `dev`; `main` and `dev` both read it back. Manifest SHA-256 `45576CDF261CC007265D586BAE231555849E5C85F32A797783908BCB4AD5AB64` (schema 3, clean, Windows x64: `efbundle.exe`, rid `win-x64`, created 09:39:34Z); `web.zip` `B114B14AA39FD0E8616099F755E4B6A6E53321EF6C4E5D4EA63F861532673689` (113,519,876 bytes); `worker.zip` `EC06A87201220DACC546F37BB0489D1EB7DAB2BDE2BA887DE5A565E606A66F23` (67,275,465 bytes); `classifier.zip` `3EC6084CEEE4CBFB71B5B911AA8671D30BB353F529FB759AEE0B9D6A2CBAF1FC` (137,787,687 bytes); `efbundle.exe` `3875158C50910D16475EF07EE289A422CD0612C0B639581145912C4A1A6964B8` (264,835,023 bytes). Built in a detached worktree at `../pegasus-worktrees/release-e9b69340`; `Local`, `Artifact` and `PreDeploy` plan validations passed. Tools: .NET SDK `10.0.302`, Node `v24.15.0`, npm `11.14.1`, Azure CLI `2.88.0`, azd `1.34.1`. |
| Review and promotion | PR 1694 passed independent code review (`correct`, 0 blocking findings) and all 10 active CI checks (`changes`, `invariants`, `unit`, six `sql-integration` shards and `sql-integration-coverage`), and merged into `dev` as `e9b69340c` at 09:29Z on 27 October. One atomic leased promotion from `main` `373dcc81aedd7ff1415090b4fb50486652ebc595` to `e9b69340c8901daf04708dfdda2777e3bc9165cf` at 09:30Z; `main` and `dev` both read `e9b69340c8901daf04708dfdda2777e3bc9165cf`. |
| Session seed | `pegcustody252ow37gij/authentication-ring/brego-session.json` seeded at 09:51:57Z (`105 bytes`) with the active 1-year `brego-session` cookie (`sessionId: 5e51b371-4c5f-44f9-808c-b7911bbc2573`, valid until `2027-10-27T08:55:59.412Z`) for `digital@collisionengineers.co.uk`. Cookie value is not recorded. |
| Migration | None ran. Identity unchanged at `20261023160000_AppliedValuationCalculationRows` (238 applied). |
| Deployment | Azure CLI account `digital@collisionengineers.co.uk`. Serving Web before: `373dcc81aedd7ff1415090b4fb50486652ebc595`, `0.1.0-alpha.1`; Web Running on `DOTNETCORE\|10.0`, Worker Running, classifier Running. `PreProvision` passed 09:52:51Z; `azd provision` succeeded 09:53:04Z (no infrastructure changes). `web.zip` deployed with `--clean true --restart true` (OneDeploy record `2a22a5a3-359b-40b7-9984-34d464ead415` succeeded 09:53:35Z, package `20261027095313.zip`), site started and `Wait-PegasusExpectedWebSite` read back `Running`, `/health/ready` 200 and `e9b69340c8901daf04708dfdda2777e3bc9165cf` / `0.1.0-alpha.1` on attempt 1 at 09:57:16Z. `worker.zip` deployed 09:59:18Z (deployment `4dc4b33f-1454-465a-9cd4-8d1fbb0b9d07`); `classifier.zip` deployed 10:01:24Z (deployment `8efb3208-bafa-42e6-801b-ec5ae49c11d5`). |
| Smoke | Full smoke passed 10:02:13Z: Worker activation `approved-live-worker`; Web Running on `DOTNETCORE\|10.0`; active package `20261027095313.zip` SHA-256 equals the approved `web.zip`; intake liveness passed (last poll 10:00:03Z; Graph subscription expires 2026-10-27 13:35:00Z); Worker disabled census all `false`. |
| Still owed | Release 108 to 123 owed items otherwise stand. |
| PR states | PR 1692 and PR 1694 read Merged with their heads in `main` and `dev`. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-125-e9b69340`; the driver scripts at `artifacts/releases/release-125-driver` (`build-and-validate.ps1`, `seed-brego-session-blob.ps1`, `stage-normal.ps1`). |

## Release 124 - 26 October 2026 (deployment live)

Release 124 deployed [PR 1688](https://github.com/collisionengineers/pegasus/pull/1688) (Brego's
Get valuation signs in to the Brego platform through the dedicated Brego valuation account, answers
`retail.high` and `trade.high`, fills an empty Case VIN, and files the **Brego Valuation Report**
PDF on the Case in the background with studio stock-image repair for broken 14×16 listing image
placeholders), which [ADR-0084](adr/0084-brego-valuation-account-and-valuation-report.md) records.
The range from Release 123's `main` `0ad8c6855` is 5 commits and 32 files: the Release 123 record
([PR 1680](https://github.com/collisionengineers/pegasus/pull/1680)), the two 26 October intake
wipe records on `docs/operations.md` (`109f71729`), and PR 1688 (`ed4516c45`, merged into `dev`
as `373dcc81a`).

The route was the normal one (release skill section 8). Provision added
`Brego__ValuationAccount__Email` and `Brego__ValuationAccount__Password` as versioned Key Vault
references without removing any setting required by the serving Release 123 package, followed by
`web.zip`, `worker.zip`, `classifier.zip`, and the full production smoke. The migration identity
was unchanged at `20261023160000_AppliedValuationCalculationRows` (SQL head read before deployment:
238 applied), and no migration ran. Web, Worker and the classifier are Running on the approved
release and the full production smoke passed.

| Observation | Value |
| --- | --- |
| Source and packages | Version `0.1.0-alpha.1`, application source `373dcc81aedd7ff1415090b4fb50486652ebc595`, the merge of PR 1688 into `dev`; `main` and `dev` both read it back. Manifest SHA-256 `F93155C925F3AA1B034B03D4B6B62142F90ED6C74B9734CD4BAC5A1B38907A59` (schema 3, clean, Windows x64: `efbundle.exe`, rid `win-x64`, created 15:26:24Z); `web.zip` `88220A8E9EFC6D233478CD2C5EB6D0D46A4314A00B0F05B2F20CA6CC29E25E9E` (113,511,423 bytes); `worker.zip` `BF4D3337A8CF37B0E2BEA7A6E4E707D1A315189802C1A3B394616D8C2F68EC1D` (67,271,611 bytes); `classifier.zip` `0C910A005AF40DF5156C28C679DB7AEAC6847117585CACC729A64417DF3EAED2` (137,787,687 bytes); `efbundle.exe` `EF10F9D7B7E5662B429030E52DA070D5AA9ED0B6E79351A2197164766825D3BA` (264,826,319 bytes). Built in a detached worktree at `../pegasus-worktrees/release-373dcc81`; `Local`, `Artifact` and `PreDeploy` plan validations passed. Tools: .NET SDK `10.0.302`, Node `v24.15.0`, npm `11.14.1`, Azure CLI `2.88.0`, azd `1.34.1`. |
| Review and promotion | PR 1688 passed an 8-reviewer `/review` pass with 0 findings and all 11 active CI checks (`changes`, `invariants`, `infrastructure`, `unit`, six `sql-integration` shards and `sql-integration-coverage`), and merged into `dev` as `373dcc81a` at 15:15Z on 26 October. One atomic leased promotion from `main` `0ad8c6855b62922c25f210f6b2e90b684cc34ee9` to `373dcc81aedd7ff1415090b4fb50486652ebc595` at 15:18Z; `main` and `dev` both read `373dcc81aedd7ff1415090b4fb50486652ebc595`. |
| Secrets and grants | Key Vault `pegasusprodkv252ow37g` gained `brego-valuation-email` (version `d4d6bd1ed65d4f1f8142544bf2688d2c`) and `brego-valuation-password` (version `8310aab6586744d8979905e2b655edcd`) at 15:31:04Z. The Web user-assigned identity `pegasus-prod-web-id-252ow37gij` (`f3b032cc-7591-4ea8-bd68-d165578c576f`) was granted `Key Vault Secrets User` at each secret's scope at 15:31:14Z. The `pegasus-prod` azd environment carries `BREGO_VALUATION_EMAIL_SECRET_URI` and `BREGO_VALUATION_PASSWORD_SECRET_URI` with those versioned URIs (15:31:16Z). Secret values are not recorded. |
| Migration | None ran. Identity unchanged at `20261023160000_AppliedValuationCalculationRows`; SQL head read before deployment: 238 applied. |
| Deployment | Azure CLI account `digital@collisionengineers.co.uk`. Serving Web before: `0ad8c6855b62922c25f210f6b2e90b684cc34ee9`, `0.1.0-alpha.1`; Web Running on `DOTNETCORE\|10.0`, Worker Running, classifier Running. `PreProvision` passed 15:32:07Z; `azd provision` succeeded 15:35:02Z (2 min 53 s, deployment `pegasus-prod-1793028736`), adding `Brego__ValuationAccount__Email` and `Brego__ValuationAccount__Password` as Key Vault references. `web.zip` deployed with `--clean true --restart true` (OneDeploy record `cefd5671-db6c-4a06-8aa8-ef54eee5ccb9` succeeded 15:36:21Z, package `20261026153534.zip`; the CLI's default 600 s startup tracker timed out on the client side while the site restarted, and `Wait-PegasusExpectedWebSite` read back `Running`, `/health/ready` 200 and `373dcc81aedd7ff1415090b4fb50486652ebc595` / `0.1.0-alpha.1` on attempt 1 at 15:47:50Z). `worker.zip` deployed 15:49:30Z (deployment `9374e43b-8418-4906-809c-f775b502ae0b`); `classifier.zip` deployed 15:51:40Z (deployment `46d15e58-597d-4fb8-a054-daffa08b302d`). |
| Smoke | Full smoke passed 15:52:36Z: Worker activation `approved-live-worker`; Web Running on `DOTNETCORE\|10.0`; active package `20261026153534.zip` SHA-256 equals the approved `web.zip`; intake liveness passed (last poll 15:50:02Z; Graph subscription expires 2026-10-27 13:35:00Z); Worker disabled census all `false`; Web `Brego__*` settings afterwards: `Brego__ValuationAccount__Email` and `Brego__ValuationAccount__Password`. |
| Live proof | After verifying the initial device-approval OTP (`067939`, [issue 1691](https://github.com/collisionengineers/pegasus/issues/1691)) for `deviceId` `c1e1ff9f-5081-4eb6-a94f-2567cfd88e8b` on `digital@collisionengineers.co.uk`, pressing Get valuation on Case `a.QDOS26120` (`a49c2f3b-a373-4985-9632-64eab119c303`, registration `SH65HTL`) recorded `CaseValuations` row `69c2c474-6eac-4f97-b88c-7e1fa05fe58d` (`Source=Brego`, `RetailValue=5005.00`, `TradeValue=2999.00`, `GuideMonth=2026-10-01` at 16:00:18Z) and filed `Brego valuation SH65HTL 2026-10.pdf` (3,079,413 bytes, SHA-256 `5f2bbc58477abe595ac2f933529e1d015f3e0e3bf493c0fefb38eeb9729c7861`, occurrence `guide-valuation-report:Brego:brego-valuation:5b270c21-4b51-42f9-b673-099f3a4d3d91`, Box file `2550663443078`) in Box custody at 16:00:33Z. |
| Still owed | Release 108 to 123 owed items otherwise stand. |
| PR states | PR 1680 and PR 1688 read Merged with their heads in `main` and `dev`. |
| Evidence | Exact artifacts retained at ignored `artifacts/releases/release-124-373dcc81`; the driver scripts at `artifacts/releases/release-124-driver` (`setup-keyvault-and-azd.ps1`, `stage-normal.ps1`, `finish-stage-normal.ps1`). |

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
configured root and has no move, copy, or share operation, and no delete
except the abort of an upload session that was not committed (operator ruling
of 16 October 2026). Failed attempts remain visible for authorised staff
retry; there is no automatic business retry.

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

Three Azure Monitor rules page the action group (`infra/modules/platform.bicep`):
`pegasus-prod-web-http5xx` (Sev1) fires when the Web App returns more than
one HTTP 5xx in a five-minute window, and `pegasus-prod-application-exceptions`
(Sev2 since 15 September 2026) fires on a correlated Web or Worker exception
signature in a fifteen-minute window. The first two auto-resolve. Before the tuning
each fired on a single event and paged several times a day on transient
faults.

The third rule, `pegasus-prod-telemetry-daily-cap` (Sev3, added 11 October
2026), fires when billable ingestion since 00:00Z reaches 80 percent of the
daily cap, which `telemetryDailyCapGb` sets for the component and the
workspace. It watches the component cap that resets at 00:00Z, not the workspace
cap that resets at 03:00Z, and does not auto-resolve. It fires daily until the
`AppTraces` volume is cut. The portal 90 percent warning email stays as it is.

Read-back on 11 October 2026 (18:52Z), read-only,
`az monitor app-insights component billing show`: cap 0.5 GB, `resetTime` 0,
`warningThreshold` 90, `stopSendNotificationWhenHitCap` false.

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
