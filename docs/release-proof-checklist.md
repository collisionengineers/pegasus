# Release proof checklist

The owed live-proof items of Releases 81 to 128, settled once (operator, 29 October
2026, R525 and R526; the page `proof-debt-2026-10-28.html` in the operator-held
workspace is its source). Every release record from Release 81 ended "Release N to
M owed items otherwise stand" and no release discharged the chain. This file is the
chain: the items still provable are one checklist, run on the next release and
recorded once in [operations](operations.md); the rest are struck here with their
reasons. When the next release record names the run, this file is deleted and the
per-release sentences are not repeated.

Each item is the record's wording, trimmed at 300 characters; "not verified" in a
reason means the classifier kept the item rather than guess.

| Class | Items |
| --- | --- |
| decision | 16 |
| discharged | 20 |
| issue | 16 |
| provable | 131 |
| superseded | 18 |
| wiped | 7 |
| total | 208 |

## Checklist: provable on the current release (131)

Run on the next release with a signed-in staff session or the named observation; record the run once in the release record.

### Release 126

- [ ] None beyond the smoke's liveness line: the changed journeys (Inbox ledger, cog menu, Work Centre views, query assignment) need a signed-in staff session, which the release terminal does not hold. The first office use is the live proof; the Query Store "after" window below is to be read then.
  Reason: Inbox ledger, cog menu, Work Centre views and query assignment are at the tip (PRs 1700-1703; ledger row redrawn by PR 1708); a signed-in walk proves them; the Query Store part is #1713.

### Release 123

- [ ] No snapshot was taken for this release; none is retained under `artifacts/performance`. Combine B changes data access across the Web, Worker and Infrastructure, so the before and after measurements are owed.
  Reason: No after snapshot is recorded; the change is in the current release and a Query Store read over retained intervals measures it (Releases 105 and 126 read windows after the fact); not verified.
- [ ] The Administrator removes the seven per-scope connectors and adds the one Automation MCP connector at `<origin>/mcp`, consenting once and ticking the scopes its grant holds, including the claude.ai connector. There is no alias for `/mcp/<scope word>`.
  Reason: ADR-0087 (PR 1789) replaces the seven servers of ADR-0080 (PR 1658) with one at `/mcp`; the seven connectors were never re-added (R509); the proof is the one connector added at the release that ships ADR-0087, and no later record says it was done.
- [ ] One classifier tick that connects on `/mcp`.
  Reason: The classifier's `PEGASUS_MCP_URL` moves from `/mcp/mail` to `/mcp` at the `azd provision` of the release that ships ADR-0087; one tick trace in App Insights proves the connect; not recorded.

### Release 122

- [ ] PR 1518's Cases quick-detail read (PERF-WEB-03) and PR 1534's Management Reports scoping (PERF-WEB-08) change data access, so their measurement is owed.
  Reason: No after snapshot is recorded; the change is in the current release and a Query Store read over retained intervals measures it (Releases 105 and 126 read windows after the fact); not verified.
- [ ] The Stage 2 side-by-side conformance shots on the live release (R284).
  Reason: The v41 Stage 2 pages (PRs 1515-1518, 1529, 1534) are live and R284 puts the shots on the live release; v42 (Release 126, PRs 1702/1703) has since redrawn Inbox and Work Centre, so those two compare against v42.
- [ ] Query Store before and after for PERF-WEB-03 and PERF-WEB-08, as above.
  Reason: No after snapshot is recorded; the change is in the current release and a Query Store read over retained intervals measures it (Releases 105 and 126 read windows after the fact); not verified.
- [ ] Carried from Release 120: the sc-02 live Companion press.
  Reason: Cazana Companion (src/Pegasus.Infrastructure/Cazana/CazanaCompanion*.cs) is still at the tip beside Brego; no record shows the Companion press.

### Release 121

- [ ] The sc-02 live Companion press from Release 120.
  Reason: Cazana Companion is still at the tip beside Brego; no record shows the Companion press.

### Release 120

- [ ] One live Get valuation press on a real Case through the Companion account: sign-in, lookup, and the Companion Report filed as `cazana-companion:{uuid}` evidence.
  Reason: CazanaCompanionAccount and CazanaCompanionReport are at the tip and Cazana__CompanionAccount__* are provisioned; the Release 124 live proof was Brego, not Companion, so the press is still owed.
- [ ] The Release 96 Cazana proof's changed meaning (sc-02).
  Reason: Its meaning is the Companion press (Release 120 L432); the same press proves it.

### Release 119

- [ ] Live evidence for PR 1481: a Discard on a running Case's repair specification that completes and leaves its history row.
  Reason: The PR 1481 grant and Discard are at the tip (Release 123 #1644 made Discard set-based); one Discard on a running Case spec proves it.

### Release 118

- [ ] Not snapshotted: the release changes no data access or telemetry. Release 117's owed after snapshot stands.
  Reason: No Release 117 after snapshot is named anywhere; a Query Store read over the current release still measures the PR 1465 promotion path; not verified.
- [ ] Live evidence for PR 1476: the Tasks dialog on a running Case record with an open task, its controls on one line at the dialog width and the table scrolling when narrow (the PR verified the rendered markup and CSS in Edge only).
  Reason: The Tasks dialog is at the tip (Release 123 #1609 adjusted its gap); a signed-in walk proves it.

### Release 117

- [ ] This release's after snapshot (PR 1465's promotion path under the workflow lock) is owed after a day of use.
  Reason: No Release 117 after snapshot is named anywhere; a Query Store read over the current release still measures the PR 1465 promotion path; not verified.
- [ ] PR 1465: five images attached to one Case from the left-rail Upload all reaching the Case (the #1462 shape), one with the Case editor held and released; a read-only count of intake work items in `Failed` with code `invalid_intake_data` or `unexpected_intake_processing_failure` once a non-transient...
  Reason: PR 1465 serialised promotions and the deferred-filing sweep are at the tip; the five-image Upload and a read-only FailureCode count prove it.
- [ ] PR 1466: one batch download through the automation connector with a PDF whose text carries multi-byte characters, its inline total at or under `maxInlineBytes`, and the Action Logs row resolving to the Case.
  Reason: pegasus_document_download_batch is at the tip (PR 1530 reworked it in Release 121); one batch with a multi-byte PDF proves it.
- [ ] PR 1467: a Case page whose Repair Specification offers only Registered and Not registered, and a `pegasus_estimate_save` with an explicit Unknown refused.
  Reason: RepairerVatStatus offers Registered and Not registered and a save refuses Unknown (Estimates.cs:15-25, R386); a Case page and one MCP save prove it.
- [ ] PR 1468: a Glass's import carrying `0.91` paint hours saving from the editor without the step refusal.
  Reason: Two-decimal hours are at the tip (Release 123 #1616 also reads hours to two decimals); one Glass import proves it.
- [ ] PR 1464: the Files link from the top of a Case with unmounted sections below landing on Files and the row following a scrollbar drag afterwards.
  Reason: The PR 1464 settle logic is in case-workspace.js; a browser walk proves it.
- [ ] the classifier's tick with eligible mail is still owed.
  Reason: The classifier and its Classifier agent: reasons are at the tip; it needs eligible mail (Release 126 changed its prompt for the three categories).

### Release 116

- [ ] A tick with eligible mail: `eligible`, `classify` or `link` and `result` lines, each write with a history reason beginning `Classifier agent: `, `costUsd` at most 2; only real Unclassified or Case-less mail decided by the pipeline produces one, and ...
  Reason: The classifier and its Classifier agent: reasons are at the tip; it needs eligible mail (Release 126 changed its prompt and tools for the three categories).

### Release 115

- [ ] PR 1417: a read-only count of intake work items by `FailureCode` once the estate has seen a pool exhaustion or a Box outage.
  Reason: PR 1417 FailureCode classification is at the tip; a read-only count after a pool exhaustion or Box outage proves it; not verified.
- [ ] PR 1458: one PCH instruction through an intermediary reading `Accepted` / `Intermediary` in its receipt; the overlap `NeedsSorting` reason sentence is operator copy.
  Reason: PR 1458 intermediary recognition is at the tip; one PCH instruction through an intermediary proves it; the NeedsSorting sentence is operator copy for a rulings round.
- [ ] PR 1456: a real `PL on` instruction end to end and a plate-only subject leaving the description empty.
  Reason: PR 1456 PL on and plate-shaped Vehicle: reads are at the tip; one real instruction proves it.
- [ ] PR 1447: a large managed version downloaded through a staff document download completing with no second copy held.
  Reason: The PR 1447 verified Box download is at the tip; one large staff download proves it.
- [ ] PR 1416: the first authorised wipe records its dry run and the Sent boundary read-back (merging the script authorised no wipe).
  Reason: The wipe advances the Sent poll boundary (wipe skill lines 79-81); the 19, 21 and 26 October wipe records give dry runs, the mail-boundary update and the cutoff but no explicit Sent poll-row read-back, so the next wipe can record it.
- [ ] PR 1425: a repeat page load inside the window sending no release-note SQL dependency, and a deleted account's name turning to Former staff on the next load.
  Reason: PR 1425 shell caches and the Former staff label are at the tip; an App Insights read and one deleted account prove it.
- [ ] PR 1455: Azure Blob under four simultaneous uploads from one item (the PR owes nothing to this record; noted for the proof round).
  Reason: PR 1455 four-at-a-time retention is at the tip; one four-upload item observed proves it.

### Release 114

- [ ] Worker: two simultaneous Outlook moves in one mailbox without a 429 and a real tidy of two due instructions (PR 1423)
  Reason: PR 1423 Worker paths are at the tip; a telemetry read and a real tidy prove it; not verified.
- [ ] two simultaneous MIME reads beside webhook wake reads, and the backlog poll inside the 60 s folded-job budget (PR 1422)
  Reason: PR 1422 concurrent MIME reads are at the tip (Release 123 #1624 reads Sent MIME two at a time); a telemetry read proves it; not verified.
- [ ] no poll overlap across the three approved mailboxes (PR 1430)
  Reason: PR 1430 concurrent mailbox polling is at the tip; a telemetry read proves it; not verified.
- [ ] the receipt read's SQL dependency durations (PR 1431)
  Reason: The PR 1431 receipt read is at the tip; an App Insights dependency read proves it.
- [ ] Box: the create and commit answers carrying parent and file version, session endpoints on `upload.box.com`, the error code for a held name and the upload-session abort (PR 1421)
  Reason: Box upload sessions are at the tip (Release 123 #1627 sends parts two at a time); a logged Box answer proves it; not verified.
- [ ] the read count of the next production image-case fold (PR 1442)
  Reason: PR 1442 fold reads are at the tip; the next image-case fold read count proves it.
- [ ] the upload answer carrying `sha1` with no content read after a holding upload (PR 1445)
  Reason: The PR 1445 sha1 check is at the tip; one holding upload proves it.
- [ ] a live Export of a Case with several photographs (PR 1427)
  Reason: Export case is at the tip (case_exported; Release 123 #1630 reads ahead); one live export proves it.
- [ ] Web: the sprite under the production CSP (rail, `/Cases`, the sign-in frame) with its cache header and encoding (PR 1449)
  Reason: The icon sprite is at the tip (IconSprite); a browser read under the production CSP proves it.
- [ ] `Content-Encoding` on a JSON route (PR 1437)
  Reason: PR 1437 response compression is at the tip; one JSON route header read proves it; not verified.
- [ ] the upload-group polling watched in a browser (PR 1443)
  Reason: UploadGroupStatus JSON polling is at the tip (Release 123 #1632, #1636); a browser watch proves it.
- [ ] a non-selected Search row showing its circumstances on selection and the `/Search` keyboard walk (PR 1446)
  Reason: PR 1446 Search circumstances on selection are at the tip; a keyboard walk proves it.
- [ ] no `?handler=Refresh` under 2 minutes and one after (PR 1448)
  Reason: OnGetRefreshAsync and the 2-minute FOCUS_GAP in work-centre.js are at the tip; a telemetry read proves it.
- [ ] the bulk-bar markup and its words, which wait for v41 Stage 2 (PR 1419)
  Reason: The Inbox bulk bar exists (Mail/Index.cshtml inbox-bulk); v41 Stage 2 Inbox (PR 1516) and v42 (PR 1702) have landed, so its markup and words can be walked (R140).
- [ ] Extraction: a real MP scan read through Document Intelligence (PR 1413)
  Reason: AzureDocumentIntelligenceOcr is at the tip; one real MP scan proves it.
- [ ] detector box area picking the subject vehicle's plate on real multi-vehicle photographs (PR 1444)
  Reason: PR 1444 detector selection is at the tip; real multi-vehicle photographs prove it.

### Release 113

- [ ] live evidence of a real instruction mail from each new route's domain landing on a Case
  Reason: Principal recognition stays in code at the tip; one real instruction mail from each new route domain proves it; not verified.
- [ ] the operator-run regeneration of `reference/workproviders-and-repairers/principal-identification-corpus.v1.json`, which PR 1403 leaves as is so the generator's `-Verify` reports drift until then
  Reason: The corpus JSON was re-committed after Release 113 (3791c9cea 18 October, 10260c97f 27 October); the generator -Verify run shows whether drift remains; not verified.

### Release 112

- [ ] Live proof signed in: a PCH Case's Report sending form reads the v8 rules (Rate card tick, attach override, the audit removal) and FRZ appears inactive in Administration.
  Reason: Principal report sending rules (Rate card, attach override) are at the tip; a signed-in read of a PCH Case and Administration proves it.

### Release 111

- [ ] Live proof signed in: a Principal's Report sending form shows Reference prefix, Previous job, Rate card and the attach override and refuses a non-letter prefix naming its rule; the Contacts list pages a type at 50 rows; a contact save sets an existing Principal's inspection mode.
  Reason: Reference prefix, Previous job, Rate card and the attach override are at the tip (OperatorLabels); a signed-in walk proves them.
- [ ] After the authorised reference-data load (REF-04, after REF-06 B): a Case offering a Principal's reviewed locations at Inspect at, and the Repairers list over five pages.
  Reason: REF-04 was applied (L821); Inspect at and the Repairers list are at the tip; a signed-in read proves them.

### Release 110

- [ ] Live proof signed in: a 1205 or lost-race retry under `StoreConcurrencyRetry` logged as an attempt, not a failed request; the isolation programme's week of deadlock telemetry now runs against PR 3.
  Reason: StoreConcurrencyRetry (PR 1390) is at the tip; it needs a 1205 or lost race in telemetry.

### Release 109

- [ ] Live proof signed in: a mailbox wake that finds the lease held is kept and polled afterwards; the MOT mileage estimate fills a Case and reads `Estimated` as its source; Take over of an Automation lease; the Unidentified and Awaiting lists page; Market Research jobs end Completed.
  Reason: Mailbox lease wake, MOT Estimated mileage (R176), Automation Take over, Unidentified and Awaiting lists and Market Research Completed (R28) are at the tip; a signed-in walk proves them.

### Release 108

- [ ] Live proof signed in: the Actions menu on a Report sent Case offers no Place on Hold and no Close; Close, Correct principal and Unlink on a Case holding an open task proceed and the replacement cancels the original's open tasks; every closed Case reads Closed and the Close dialog requires a reason; the ...
  Reason: The R5/R62 guards, the Close reason and task cancellation on replacement are at the tip; a signed-in walk proves them.

### Release 107

- [ ] Live proof signed in: an open mail Query offers Draft reply with AI and shows its job state; a send completes the Draft ready job and the refused-confirm notice reads as ruled; Administration no longer lists Mail categories.
  Reason: Draft reply with AI on a Case query and no Mail categories in Administration are at the tip (PR 1700 made queries assignable); a signed-in walk proves it.

### Release 106

- [ ] Live proof signed in that the utility bar shows one New case button on every page and that Unassigned Images has no separate Create Case link.
  Reason: The utility-bar New case button and Unassigned Images are at the tip; a signed-in look proves it.

### Release 105

- [ ] The after snapshot is owed after a day of use.
  Reason: No after snapshot is recorded; the change is in the current release and a Query Store read over retained intervals measures it (Releases 105 and 126 read windows after the fact); not verified.
- [ ] Live proofs of the changes on a real Case and the Contacts pages signed in: the one-type Contacts list and contact page at v40, a route-less Principal's "No accepted e-mail route…" box, the SOP-seeded Principals' Notes on every Case, Unassigned Images and No Case Found, Tasks in Actions, the two labour-...
  Reason: One-type Contacts, Unassigned Images, No Case Found, Tasks in Actions and the two built-in labour-rate cards are at the tip; a signed-in walk proves them.

### Release 103

- [ ] The after snapshot is owed after a day of use.
  Reason: No after snapshot is recorded; the change is in the current release and a Query Store read over retained intervals measures it (Releases 105 and 126 read windows after the fact); not verified.
- [ ] The mailboxes are not yet admitted in Pegasus Mail settings (Incoming + Outgoing); that is the operator's next step, and each addition starts its own intake boundary.
  Reason: Whether engineers@, info@ and desk@ are admitted Incoming + Outgoing is a read of Mail settings; no later record states it; not verified.
- [ ] Live proofs of the changes themselves on a real Case: a report send by the rules (recipients, holds, a Stop override, the "{REG} Report" subject of a new message), the after-send tasks and the open-task step, the sent-instruction tidy against Exchange, a combined send of both reports as a reply, a ...
  Reason: Report sending rules, after-send tasks, the sent-instruction tidy and the combined send (PRs 1015, 1300/1303) are at the tip; one real send proves them.
- [ ] Operations before a live report send from engineers@, info@ or desk@: those mailboxes admitted in Mail settings as Incoming + Outgoing (the Exchange grant above is done); the seeded SOP addresses are real third-party addresses, so a test send reaches them unless staff edit To/Cc.
  Reason: A precondition read of Mail settings plus a caution on the real SOP addresses; not verified.
- [ ] The Query Store after snapshot.
  Reason: No after snapshot is recorded; the change is in the current release and a Query Store read over retained intervals measures it (Releases 105 and 126 read windows after the fact); not verified.

### Release 102

- [ ] The after snapshot is owed after a day of use.
  Reason: No after snapshot is recorded; the change is in the current release and a Query Store read over retained intervals measures it (Releases 105 and 126 read windows after the fact); not verified.
- [ ] Live proofs of the changes themselves: PR 1271's Report sent chip, Query chip, Log query, Tag as query, Resolve query and a reply's Also resolve query tick on a real Case; PR 1284's matched mailbox and upload images filing on the Case with no record or folder; PR 1274's staff link; the Query Store a...
  Reason: PR 1271 Report sent and query controls (Log query, Tag as query, Resolve query, Also resolve query) and PR 1284/1274 filing are at the tip; PR 1700 (Release 126) made queries assignable.

### Release 101

- [ ] PR 1254: Discard on a real spec removes its tab, its Compare entry and its Changes vs option, and Case history keeps the reason.
  Reason: Discard (PR 1254, made to work in production by PR 1481) is at the tip; one Discard on a real spec proves it.
- [ ] PR 1255: the App Insights per-request account read count falls, and a disable or force logout ends the session on the next request.
  Reason: The PR 1255 account read cache and session end are at the tip; an App Insights read and one disable prove it.
- [ ] PR 1257: the UI guardrail browser check on the Case page, Image Intake and Triage.
  Reason: The Case page, Image Intake and Triage are at the tip; the guardrail browser check runs on them.

### Release 100

- [ ] Live proof of each sweep PR's done-when: the App Insights dependency counts and durations each PR body names (Case page GET and Save, Work Centre, Inbox, Admin pages, intake, Box reads) read after a day of use; the first Case GET showing the shell reads overlapping the handler (fixed here, no test).
  Reason: The sweep data access is in the current release; App Insights reads after a day of use prove it; not verified.

### Release 98

- [ ] PR 1139: the refusal sentence on a chosen card whose calculation changed.
  Reason: The PR 1139 refusal wording (Use this value removed; #1105 closed) is at the tip; one changed card proves it.
- [ ] PR 1143: a Save & Exit relayed more than eight hours after launch; a reopen after a mileage correction; a placeholder reopen after the plate is corrected (the three walks its body lists).
  Reason: The PR 1143 no-lifetime and registration-only rules are at the tip; the three walks prove them.
- [ ] PR 1145: the Case page in read and edit without the switch.
  Reason: The Scroll/Tabs switch is gone (PR 1145); a read and edit look proves it.
- [ ] PR 1146: a correction offers no Dispute; MI-01 has no Disputes column.
  Reason: PR 1146 removed Dispute; a correction and an MI-01 read prove the absence.
- [ ] PR 1147: Pop out from the Files head and from the viewer, one window per Case.
  Reason: Pop out (Images.cshtml, CaseViewerView) is at the tip.
- [ ] PR 1148: a Next action jump unfolds, centres and outlines the field, in a browser.
  Reason: The PR 1148 jump outline is at the tip; a browser walk proves it.
- [ ] PR 1149: Decisions and Damage in read and edit at 1580 px and a smaller width.
  Reason: The Decisions and Damage sections are at the tip; a width walk proves it.
- [ ] PR 1150: the Management Reports page against the v37 Design A shots, each CSV and the workbook, and an invalid period.
  Reason: Management Reports, its CSVs and workbook are at the tip; the v37 Design A shot comparison is superseded by v41 Stage 2 (PR 1534), so compare against the current round.

### Release 97

- [ ] PR 1137: conformance screenshots of the routed Case page against the v36 shots (read and edit at 1580, 1440 and 760 px), and the two parts left open in the round's notes (item T's catch-up double redraw and first-save defaults; item W's locked-section sentence, which needs approved copy).
  Reason: The v36 shot comparison is superseded by the v41 Stage 2 Case page (PR 1529; R284 conformance in the Release 122 row); items T (double redraw, first-save defaults) and W (locked-section sentence, needs approved copy) still stand.
- [ ] PR 1134: with the Overview image as the only blocker in edit mode, tagging an image Overview shows Generate report without a reload.
  Reason: The PR 1134 report head redraw is at the tip; one Overview tag proves it.
- [ ] PR 1136: Edit, then Actions → Assign Engineer, comes back still editing and a later field edit saves.
  Reason: The PR 1136 lease reclaim is at the tip; one Assign Engineer while editing proves it.

### Release 96

- [ ] PR 1131: a vehicle with no MOT test shows "No MOT history" in the mileage box.
  Reason: The No MOT history mileage text is at the tip; one vehicle with no MOT proves it.

### Release 95

- [ ] PR 1118: the Case ribbon's Registration cell and its chip row on a real Case above 1100 px, including a total-loss Case in edit mode.
  Reason: The ribbon Registration cell is at the tip; a real Case above 1100 px proves it.

### Release 94

- [ ] PR 1110: the Valuation section read with an empty Engineer's Value and guide month.
  Reason: The PR 1110 greyed Engineer Value box is at the tip; one read proves it.
- [ ] PR 1112: the slider starts at the spec's share on a real Repair Spec, and Apply there leaves the spec unchanged.
  Reason: The Target % slider is in _CaseEstimate.cshtml; one real spec proves it.
- [ ] PR 1114: a Needs attention reference opens its record.
  Reason: Needs attention reference links (RecordRoute) are at the tip; one click proves it.
- [ ] PR 1116: the UI guardrail browser check and a Case list export with a saved preset.
  Reason: The guardrail check and the Case list column presets (PR 1116) are at the tip.

### Release 93

- [ ] PR 1103: a generated report files as `{REF}_report.pdf`, and its image pages print portraits and landscapes at full column width, two to a row; no rendered PDF has been inspected.
  Reason: FileNameOf in CaseReportGeneration.cs:1091 names {REF}_report.pdf; one generated report proves the name and the image layout.

### Release 92

- [ ] PR 1095: a long Linked cases finding wraps in the aside.
  Reason: The PR 1095 aside wrapping is at the tip; one long finding proves it.
- [ ] PR 1096: New cases lists new Cases and no Automation workflow events.
  Reason: The Work Centre New cases tab is at the tip; a read proves it.
- [ ] PR 1097 and PR 1099: a signed-in walk of the Valuation section in both views, in Scroll and Tabs, with a real save: cards, a card click, Selected, Retail and Trade following the card, the two-column increases with Add 20 % VAT, the previous total loss switch, and Get valuation on Glass's hiding its noti...
  Reason: The Inspection and Audit views (CaseWorkspaceLabels) and the guide cards are at the tip; the Scroll and Tabs clause is superseded by PR 1145 (switch removed), per R174(a).
- [ ] PR 1098: a live MCP walk from Claude Desktop: `edit_begin`, several writes under one token, `edit_end`; an impacts write in the published format; `pegasus_valuation_get` on a test Case with the figures matching the card and the Values Only PDF filed.
  Reason: edit_begin/edit_end and pegasus_valuation_get are at the tip on the one server at `/mcp` (ADR-0087), so the connector is re-added first.

### Release 91

- [ ] PR 1084: a Principal API submission with `fileName` + `contentBase64`, an Audit's top-level `originalReport`, and a replayed idempotency key answering `replayed: true`.
  Reason: Principal API fileName/contentBase64 and originalReport are at the tip; one submission proves it.
- [ ] PR 1085: set the Engineer's Value, then open Settlement without a reload; strip and salvage slider present; typing during a redraw; Repair Spec full screen.
  Reason: Sections following a landed save, the salvage slider (_CaseSettlement.cshtml) and Repair Spec full screen are at the tip.
- [ ] PR 1087: a new QDOS Triage → instruction pair: claimant on the Triage ribbon, Linked cases card on the Case, findings filled with the Triage tag, and a finding recorded after the link.
  Reason: Triage claimant, Linked cases card and finding fill are at the tip; the finding-after-the-link clause changed with PR 1689 (Release 128: a link now closes the Triage).
- [ ] PRs 1089–1092: a live MCP walk: findings, a note, a details edit that keeps unnamed facts, an estimate without a job, a priced To-be-confirmed line, a lease take-over, the vocabulary read, a Case action, a report generation and approval, a document tag, an intake accept with a supplied address, a re...
  Reason: The parity tools are at the tip on the one MCP server at `/mcp` (ADR-0087); a live MCP walk after the connector re-add proves them.
- [ ] PR 1091: Export case on a standard Case in each state downloads `{reference}.zip` with the JSON and `Images/`; one `case_exported` history row; Work Centre "Sent to Engineer" counts once.
  Reason: Export case (case_exported) and the Sent to Engineer figure are at the tip.

### Release 90

- [ ] PR 1079: a Get valuation on a real plate and a Glass's estimate return each fill an empty VIN, and neither replaces an existing one.
  Reason: Empty-VIN fill from a valuation and a Glass return is at the tip; one real plate proves it.
- [ ] PR 1080: a report generated with no Report date prints the generation day and the Report section read cell shows it; a typed date prints and stales the report.
  Reason: The one Report date box is at the tip; one generated report proves it.

### Release 89

- [ ] PR 1062: the Report Attach tickboxes in the delivery form.
  Reason: The Attach tickboxes (AttachReport, AttachFeeNote, AttachRepairSpec in _CaseReport.cshtml) are at the tip.
- [ ] PR 1063: the Repair Spec stays full screen after Glass's returns.
  Reason: Repair Spec full screen across the Glass return is at the tip.
- [ ] PR 1064: a generated report with the Overview on page 1, the Close-up leading the image pages, and every image printed whole; tile order numbers; the thumbnail opening the viewer.
  Reason: Report image placement is at the tip; one generated report proves it.
- [ ] PR 1072: a report send reaching Sent and listed in the Case's Correspondence.
  Reason: Report sends reaching Sent are at the tip; one live send proves it.
- [ ] PR 1074: a completed Triage leaving the queue; Reply with finding gone once sent; a reply to a staff forward going to the original sender.
  Reason: Completed Triages leaving the queue, Reply with finding and staff-forward replies are at the tip (TriageLifecycle).
- [ ] PR 1075: Generate report making the report and its separate fee note; an existing confirmed report without a separate fee note offering Generate report again, which adds only the fee note.
  Reason: Generate report with its separate fee note is at the tip.

### Release 88

- [ ] PR 1052: a second Estimate save on an open page; a Glass's return after a hand-off and re-claim; Edit within seconds of a Case's creation.
  Reason: PR 1052 estimate line ids, the held-estimate landing and the claim behaviour are at the tip; a walk proves them.
- [ ] PR 1053: a Report section walk in edit mode, and one live send on a test Case, sent twice.
  Reason: One-step report send (PR 1053, then the PR 1015 rules) is at the tip; one send made twice proves it.
- [ ] PR 1054: the Upload selection and review pages showing image previews in production.
  Reason: The _UploadReview partials still draw previews (PR 1054); #1684 (OPEN) separately asks for previews during processing.
- [ ] PR 1056: Register images on a multi-file upload group's Unidentified item.
  Reason: Register images on an Unidentified item is at the tip (UnidentifiedItemContext, StaffIntakeActions).
- [ ] PR 1057: a generated report ending with its fee note, and a separate fee note generated beside it.
  Reason: The PR 1057 fee-note ending and the PR 1075 separate fee note are both at the tip.
- [ ] PR 1058: the six-step walk in its PR body.
  Reason: The Glass reopen of the same spec is at the tip; PR 1143 changed the mileage step to continue rather than refuse, so walk it against FRD-25.

### Release 87

- [ ] PR 1036: Resume before and after a save, and Fetch again, on a Case whose estimate exists.
  Reason: Resume and Fetch again are at the tip; one Case with an estimate proves it.
- [ ] PR 1039: one estimator session longer than 30 minutes returning its export.
  Reason: FRD-25 step 9 keeps this walk; PR 1143 removed the session lifetime.
- [ ] PR 1041: a Case edit continuing through a custody confirmation without the "case action was not applied" notice.
  Reason: System writes keep the edit session (PR 1041) at the tip; one custody confirmation proves it.
- [ ] PR 1043: the Review Next action opening the Assign Engineer dialog.
  Reason: The Review Next action opens Assign Engineer at the tip.
- [ ] PR 1045: a signed-in walk of the Valuation section in both views with a real save.
  Reason: The Valuation section in the Inspection and Audit views is at the tip.

### Release 86

- [ ] PR 1024: the Work Centre Owner column on an assigned Review row shows the Engineer's name; the open row and dialogs carry no Assign to me.
  Reason: The Owner column is at the tip; the no-Assign-to-me clause is contradicted by Take on the row (#1213, PRs 1517/1529, Release 122), so that clause is superseded.
- [ ] PR 1033: Contacts › Report generation shows £180.00 for each Principal, and the next new Case starts with that agreed fee, tagged Principal.
  Reason: DefaultFee on Contacts and the Principal-tagged agreed fee are at the tip; a signed-in read and one new Case prove it.

### Release 85

- [ ] PR 1018: a walk of the Triage Case page (Record finding with Complete Triage, Reply with finding, Correspondence reload).
  Reason: Record finding, Complete Triage and Reply with finding are at the tip; a Triage walk proves them.
- [ ] PR 1019: Dismiss on each Work Centre tab, and the Activity figures against known counts.
  Reason: Work Centre Dismiss and the Activity figures are at the tip (v42 Design A).
- [ ] PR 1020: one Send chaser from a Not ready Case reaching Sent and recording the chase as the Worker.
  Reason: Send chaser opens /Cases/{id}/Chaser (PR 1304) and records the chase at Sent; one chaser proves it.

### Release 84

- [ ] The operator's live walk of option D: Generate report, a Send and a Reports download, watching the glyph turn, the Still words on a slow action and the tick on success; and a DevTools look at why the Release 83 ring looked still on the operator's machine, since its CSS carried the same keyframe.
  Reason: The busy glyph (data-busy-label) is at the tip; a signed-in walk proves it.

### Release 83

- [ ] PR 1010: a live walk of busy buttons on the Case page, Triage, the composers and Glass's paths, and one EVA ZIP export from the Case page.
  Reason: Busy buttons are at the tip; the EVA ZIP export clause is superseded by PR 1091 (EVA removed, Release 91).

### Release 82

- [ ] PR 1005/1007: a browser walk of the Inspection view on an Inspection + Audit Case (Edit, Save, Generate report again, the report opening on arrival), and whether the operator's "Generate report does not open the report" reproduces.
  Reason: The Inspection view and Next action per view are at the tip; a browser walk proves them.
- [ ] PR 998: the first live return on an unknown plate (read the "returned a placeholder estimate identified as type number" log line).
  Reason: The placeholder-estimate log line is at the tip; the first unknown-plate return proves it.
- [ ] PR 1004: the aside's blocker order on a Case with blockers across sections.
  Reason: _CaseAside.cshtml lists blockers in page order; one Case with blockers proves it.

### Release 81

- [ ] Get valuation on a vehicle older than Glass's values: the blue sentence with no Report a problem link, and `glass.valuation.vehicle_age` in the log. If the live `errormsg` lacks "due to the age", the red unavailable sentence shows instead and the wording needs capturing.
  Reason: glass.valuation.vehicle_age is at the tip; one old vehicle proves it.
- [ ] Add evidence with a JPEG: the file has role `Image` and appears as an Images tile.
  Reason: Add evidence is at the tip; one JPEG proves the Image role.
- [ ] Dismiss on a Needs attention, New cases and AI jobs row: the row leaves every tab and returns when the record next qualifies.
  Reason: The Needs attention, New cases and AI actions tabs and their Dismiss are at the tip (v42 Design A).
- [ ] Settlement edit mode at 1580 and 860 px: the reason bank wraps under the reason box.
  Reason: The unroadworthy reason bank is in _CaseSettlement.cshtml; a width walk proves it.
- [ ] PR 998 awaits its stricter review; its first live return on an unknown plate is the export evidence it still lacks.
  Reason: PR 998 merged 2 October; its first live unknown-plate return is still unrecorded.

### Operations note `Classifier-status-21Oct`

- [ ] The Function App has never called Anthropic. No mail has been eligible since Release 116, so the app's own egress and the app's own copy of the key are untested, and Release 116's owed tick with eligible mail still stands
  Reason: The 22 October addendum (L65) shows the never-called statement leans on wiped ActionHistory rows; the classifier and its Classifier agent: prefix (tools.ts:27) are at the tip, so a tick with eligible mail proves egress.

### Operations note `REF-04-17Oct`

- [ ] Live proof signed in, owed by the Release 111 record and not shown by these counts: a Case offering a Principal's reviewed locations at Inspect at, and the Repairers list over five pages (222 repairers, 50 a panel).
  Reason: REF-04 was applied (L821); Inspect at (_CaseInspectionAddress.cshtml) and the Repairers list are at the tip; a signed-in read proves them.

## Checklist: re-written for a new Case (wiped rows) (7)

The proof as written named a Case or row the 19, 21 or 26 October 2026 intake wipes deleted; the behaviour is provable on a new Case and is run as such.

### Release 99

- [ ] Live proof of the change: alex presses Glass's on a.QDOS26101 with "Glass's 1" on screen. Session `18109a40` then reads `Cancelled`, with the history reason "The Glass's estimate was reopened by the staff member holding the Case.", and a new session of alex's opens the estimate. At 11:31Z the sessio...
  Reason: a.QDOS26101 and session 18109a40 were deleted by the wipes (every Case before 26 October 08:47Z); the Glass reopen that cancels the session is provable on a new Case.

### Release 98

- [ ] PR 1144: a Glass's reopen whose export files once, with no `name_temporarily_reserved` (a.QDOS26101's recovery).
  Reason: a.QDOS26101 was deleted by the wipes; a Glass reopen whose export files once is provable on a new Case.

### Release 94

- [ ] PR 1115: Generate report after a change files the fee note. a.QDOS26082 was removed by the wipe.
  Reason: a.QDOS26082 was removed by the wipe, as the item says; Generate report after a change filing the fee note is provable on a new Case.

### Release 93

- [ ] PR 1107: run Get valuation and Save again on QDOS26086 and on any other Case valued since Release 92; their Retail likely holds Glass's trade figure.
  Reason: QDOS26086 and every Case valued since Release 92 were deleted by the wipes; the Retail/Trade fix is provable on a new Case.

### Release 91

- [ ] PR 1088: discard QDOS26078's "Audatex 1" spec and re-import `AI026078_BD65OAZ.pdf`; hours 0.7/0.3/0.1/0.9/0.1, paint hours 1.2/0.9/1.0/0.7/0.3/0.5, £961.75 materials on the first paint line.
  Reason: QDOS26078 and its Audatex 1 spec were deleted by the wipes; re-importing AI026078_BD65OAZ.pdf on a new Case proves the time basis.

### Release 86

- [ ] PR 1028: the QDOS26062 Laird report must be filed again, or marked by staff, before its Original report cells fill.
  Reason: QDOS26062 was deleted by the wipes; Laird recognition is provable on a new Case.

### Operations note `5Oct-send-fails`

- [ ] The two 5 October operations stay `Submitted`: the poll cursor has passed their items and nothing re-reads them.
  Reason: The two 5 October StaffMailSendOperations rows were deleted by the wipes.

## Struck: discharged (20)

The records themselves show the item was done; the reason cites the line.

### Release 122

- #1490: the driver change so containment stops the classifier itself (done by the driver here under P18).
  Reason: #1490 CLOSED 24 October by PR 1602; Release 123 (L282) records containment stopping the classifier first, as the skill now requires.

### Release 120

- The reusable migration drivers under `artifacts/releases` still set `Cazana__ApiKey` for the migration host; they switch to the two Companion placeholders at the next release with a migration.
  Reason: The retained drivers for Releases 122, 123, 126 and 128 (e.g. artifacts/releases/release-122-driver/stage-b2-migrate.ps1) set only Cazana__CompanionAccount__Email/Password; no record line says so.

### Release 119

- The per-Discard read-and-delete cost the review noted (register row A1-26) awaits the operator's efficiency ruling.
  Reason: R268 ruled A1-26 (build in eff-06: read IsCurrent, then ExecuteDeleteAsync) and Release 123 shipped set statements for Discard and stale-marking (#1644).

### Release 115

- This release's after snapshot (PR 1440's registration lookup, PR 1454's Open tasks statement) is owed after a day of use.
  Reason: Release 117 L579: the Release 115 after snapshot was taken 09:41Z (query-store-release-115-after-20261021T0941Z.csv).
- PR 1440: the Query Store after snapshot compared with the release-102, -103 and -105 baselines of the registration lookup; with one Case it will not move and is to be recorded as not exercised.
  Reason: Release 117 L579 recorded the registration lookup as not exercised, as the item asks; the addendum L65 reads its after-snapshot figures (2,812 executions, 144.52 microseconds).
- PR 1454: the pre-migration zero count is recorded above; owed are the Open tasks statement text without `ActionHistory` and its average against the 485-execution, 31.1 ms baseline (an empty table shows no gain), and the Open tasks rows before and after (none on either side).
  Reason: The addendum L65 reads the Open tasks statement without ActionHistory from the after snapshot (1,003 executions, 289.87 microseconds) beside the 485-execution baseline and the Release 114 after figure.
- PRs 1451 and 1452: the classifier activation release (the two per-secret grants, `PEGASUS_CLASSIFIER_ACTIVATION=approved`, `CLASSIFIER_DRY_RUN` read back `true`, one observed timer run, the switch) is its own release and record.
  Reason: Release 116 (L645-670) is that activation release: both grants, CLASSIFIER_DRY_RUN true, one observed dry tick and the switch.

### Release 114

- The after snapshot that shows the four new indexes (PR 1415) is owed after a day of use.
  Reason: Release 115 L722: the Release 114 after snapshot was taken 07:39Z (query-store-release-114-after-20261019T0739Z.csv).
- Data: the Query Store after snapshot for the four indexes (PR 1415).
  Reason: Release 115 L722: the Release 114 after snapshot was taken 07:39Z.

### Release 112

- The after snapshots for Releases 107 to 112 are owed after a day of use.
  Reason: Release 113 L816: the after snapshot owed by Releases 107 to 112 was taken (query-store-release-112-after-20261018T0229Z.csv) and those owed items are discharged.
- The production reference-data load (REF-04: 15 inactive Principals, 35 claim sources, 222 repairers, 50 storage and recovery, 18 engineers, 1328 inspection locations, 269 links, five inspection modes, SWADE deactivated) follows this release under the same authorisation and is recorded by its own PR; ...
  Reason: The REF-04 production load is recorded at L821 (1696 rows applied 17 October).

### Release 111

- The after snapshots for Releases 107 to 111 are owed after a day of use.
  Reason: Release 113 L816: the after snapshot owed by Releases 107 to 112 was taken (query-store-release-112-after-20261018T0229Z.csv) and those owed items are discharged.

### Release 110

- The after snapshots for Releases 107, 108, 109 and 110 are owed after a day of use.
  Reason: Release 113 L816: the after snapshot owed by Releases 107 to 112 was taken (query-store-release-112-after-20261018T0229Z.csv) and those owed items are discharged.

### Release 109

- The after snapshots for Releases 107, 108 and 109 are owed after a day of use.
  Reason: Release 113 L816: the after snapshot owed by Releases 107 to 112 was taken (query-store-release-112-after-20261018T0229Z.csv) and those owed items are discharged.
- The Graph subscription expiring 2026-10-18 17:10:00Z is inside the 48 h window the due-rows predicate reads, so its renewal is the first live proof of PR 1369.
  Reason: The renewal ran: the Release 115 smoke (L721) reads the subscription expiring 2026-10-23 08:20:00Z, after the 18 October expiry every earlier smoke read.

### Release 108

- The after snapshot is owed after a day of use, with Release 107's.
  Reason: Release 113 L816: the after snapshot owed by Releases 107 to 112 was taken (query-store-release-112-after-20261018T0229Z.csv) and those owed items are discharged.

### Release 107

- The after snapshot is owed after a day of use.
  Reason: Release 113 L816: the after snapshot owed by Releases 107 to 112 was taken (query-store-release-112-after-20261018T0229Z.csv) and those owed items are discharged.

### Release 105

- The classifier identity's exact-secret grants on `anthropic-api-key` and the Automation client secret, and `PEGASUS_CLASSIFIER_ACTIVATION=approved`, are a separate approved release.
  Reason: Release 116 L670: the Release 105 classifier clause is discharged by this record.

### Release 93

- PR 1104: a first export into `corpus/holding/` under the operator's sign-in.
  Reason: corpus/holding/desk@collisionengineers.co.uk/index-20261008T203513Z.csv (121 files) shows the first export ran on 8 October; operations.md does not record it.

### Release 91

- PR 1086: none in production (local instance).
  Reason: The record itself owes nothing in production: PR 1086 was the hosted local test instance.

## Struck: superseded (18)

A later PR or ruling changed or removed the behaviour, so the proof no longer applies as written.

### Release 109

- The isolation programme owes a week of deadlock (1205) telemetry before its PR 3.
  Reason: Release 110 (L985) shipped the isolation programme PR 3 (PR 1390) and says the deadlock telemetry week now runs against PR 3, so before its PR 3 no longer applies; the telemetry stays owed in the Release 110 row.

### Release 102

- PR 1126 rebases onto `CaseQueryLedger` and the migration chain; PR 1015 onto the #1280, #1281 and #1293 edits.
  Reason: PR 1126 and PR 1015 both merged 12 October 13:54Z through combine PR 1307 (Release 103), so no rebase is owed.

### Release 101

- PR 1253: a Completed Case's aside and quick detail read None.
  Reason: R48 (DOC-27): Release 102 PR 1271 replaced Completed with Report sent, so there is no Completed Case to run on (plan sc-02).

### Release 98

- PR 1142: the Activity panel's Sent to Engineer and Completed figures against known Cases.
  Reason: R48 (DOC-27): PR 1271 removed the Completed figure; Activity now shows New cases, Sent to Engineer, Reports sent and E-mails received (_WorkCentreBody.cshtml); Sent to Engineer stays owed in the PR 1091 row.

### Release 96

- PR 1130: Get valuation from Cazana fills a test Case, and a registration Cazana holds no data for shows the information sentence.
  Reason: PR 1483 (Release 120; ADR-0079 supersedes ADR-0066) replaced the API-key Cazana call with the Companion account; Release 120 L432 carries this proof as sc-02.

### Release 95

- PR 1120: the Work Centre Activity panel shows all ten figures.
  Reason: R48 (DOC-27): PR 1271 removed the Completed figure; the panel shows four figures today and this week, not ten.

### Release 94

- PR 1113: conformance screenshots of the routed Case page aside.
  Reason: The aside shots were against v35 design 8; the v41 Stage 2 Case page (PR 1529, Release 122) and #1603 (Release 123) replaced it, and the R284 live conformance walk (Release 122 row) is the current proof.

### Release 91

- Re-add the MCP connector's consent for the new `automation.send` scope where the connector needs it.
  Reason: Release 123's one server per scope (ADR-0080, PR 1658) required every connector re-added with per-server consent (Release 123 L300), replacing the single automation.send consent; ADR-0087 (PR 1789) replaces the seven with one connector at `/mcp`, consented per scope, so the re-add is the Release 123 row above.

### Release 90

- PR 1076: Search shows one With Engineer option and finds Cases in both of its states.
  Reason: PR 1271 (Release 102) removed PostReport, so With Engineer is ReportPreparation alone (CaseLifecycleState); there are no longer two states to find.

### Release 89

- PR 1073: a Market research job's findings file carrying the Market research role in Documents.
  Reason: R220 and PR 1533 (Release 122) folded Market research into Valuation Evidence; DocumentSemanticRole has no MarketResearch value.

### Release 88

- Also owed: operator approval of its new refusal sentence ("The Case registration or mileage has changed since this Glass's estimate was started. Restore the original vehicle details to reopen it.")
  Reason: PR 1143 (9 October rulings) made the rule registration-only; the sentence now reads The Case registration has changed since this Glass estimate was started (GlassRepairEstimates.cs:148).

### Release 85

- PR 1016: one Compose send on `a.QDOS26059` with the Case / PO typed in full, reaching `Submitted` and then `Sent` within a minute, listed under Sent Items and on the Case's Correspondence and Notes. The two earlier 5 October operations stay `Submitted`.
  Reason: PR 1304 (12 October rulings, Release 103) removed Compose; a.QDOS26059 was also wiped.

### Release 83

- PR 1009: one Compose send to `digital@` reaching Sent now that the Exchange `Mail.ReadWrite`/`Mail.Send` grant exists, and Compose dialog screenshots.
  Reason: Compose and its dialog were removed by PR 1304 (12 October rulings).

### Release 82

- PR 1006: one Compose send and one report send observed as Sent by the Worker poll, once the Exchange `Mail.ReadWrite`/`Mail.Send` grant exists; until then every send shows as failed.
  Reason: The Compose half was removed by PR 1304; the report send observed as Sent stays owed in the PR 1072 row (Release 89).

### Operations note `2Oct-staff-send`

- Release 82 is live with the grant in place, so the PR 1006 proof can run now: one Compose send to `digital@collisionengineers.co.uk` reaching `Submitted` and then `Sent` with the Sent item linked to its Case.
  Reason: Compose was removed by PR 1304 (12 October rulings); send-to-Sent stays owed through the report-send row (PR 1072).
- After this PR's release: the dialog screenshots at 1580×1000 and a smaller desktop height.
  Reason: The Compose dialog was removed by PR 1304.

### Operations note `5Oct-send-fails`

- After release: one Compose send to `digital@collisionengineers.co.uk` on `a.QDOS26059` using the one field, typed in full, reaching `Submitted` and then `Sent` within a minute, listed under Sent Items and on the Case's Correspondence and Notes.
  Reason: Compose was removed by PR 1304 (12 October rulings); a.QDOS26059 was also wiped.

### Operations note `Cazana-key-9Oct`

- After the release: `Cazana__ApiKey` reads `Resolved`, and Get valuation on a test Case's Cazana card fills Retail and Trade.
  Reason: Cazana__ApiKey was removed at Release 120 (L430, PR 1483); its Resolved read-back was recorded at Release 96 (L1386) and the Get valuation half is now the Companion press.

## Tracked as issues (16)

Already a GitHub issue; tracked there, not here.

### Release 128

- a.QDOS26120 re-upload and Triage image merge on first reconcile: not run, issue 1711
  Reason: Issue #1711 OPEN; a.QDOS26120 was created after the 26 October wipes (Release 124 valued it at 16:00Z), so the Case still stands.
- issues 1711, 1712 (Release 127's 1580/1100 px Inbox walk), 1713 (Release 126's Query Store after-window)
  Reason: Issues #1711, #1712 and #1713 are all OPEN.

### Release 127

- None beyond the smoke: the ledger needs a signed-in staff session. The operator's view of `/Inbox` after this release is the proof; the browser walk at 1580 and 1100 widths named in PR 1708 is still owed.
  Reason: The 1580/1100 px Inbox walk is issue #1712 (OPEN); the ledger row is PR 1708, live since Release 127.
- Release 126's Query Store "after" window is still to be re-read.
  Reason: Issue #1713 (OPEN) holds the Release 126 after-window re-read.

### Release 126

- `release-126-after.csv` (from 22:40Z; 0 statements at 22:58Z because the runtime-stats interval had not flushed — to be re-read after the first day of use)
  Reason: Issue #1713 OPEN.

### Release 123

- Carried from Releases 120 to 122: the sc-02 live Companion press, the #1350 Action logs rows after a send, #989 libpdfium on the linux-x64 Web and Worker with the three photo PDFs, #1484 criterion 4, the eff-09 telemetry window and its measurements, and the Stage 2 side-by-side conformance walk (R284).
  Reason: #1350 CLOSED, #989 CLOSED, #1484 OPEN, eff-09 is #1491 CLOSED; the sc-02 Companion press and the R284 conformance walk are provable and owed in the Release 120 and 122 rows.
- #1355 alert rules, after seven days of telemetry from Release 121.
  Reason: #1355 OPEN; seven days of telemetry from Release 121 have now elapsed.

### Release 122

- #1488 stays open: PR 1533 closes it in its body and PR 1536 only refers to it, and plan PRs 3 and 4 are owed.
  Reason: #1488 OPEN; plan PRs 3 and 4 have not landed.
- #1488 plan PRs 3 and 4 (the Web control to change a document's type, and the type at upload), after the mockup.
  Reason: #1488 OPEN; the Web type control and the type at upload are its plan PRs 3 and 4.
- Carried from Release 121: the #1350 Action logs rows after a send, #989 libpdfium on the linux-x64 Web and Worker with the three photo PDFs, #1484 criterion 4, and the eff-09 telemetry window.
  Reason: #1350 CLOSED, #989 CLOSED, #1484 OPEN, #1491 (eff-09) CLOSED.

### Release 121

- #1350: the Action logs rows after the next report or fee-note send.
  Reason: #1350 CLOSED 22 October (PR 1524; R445 confirmed as built); the live Action logs read after a send is not recorded.
- #989: libpdfium present on the linux-x64 Web and Worker, and the three photo PDFs.
  Reason: #989 CLOSED 22 October (PR 1527); the live libpdfium and photo-PDF check is not recorded.
- #1491: the eff-09 telemetry window starts now that the demotion is live; 24 hours, then the measurements.
  Reason: #1491 CLOSED 22 October (PR 1522); the eff-09 window measurements are not in the release records (R412 cites an eff-09 report; not verified).
- #1484: criterion 4, the live `AppExceptions` read (the lead's).
  Reason: #1484 OPEN.

### Release 98

- Review findings reported on PR 1151, filed 10 October 2026 as issues #1153 (Last 12 months can exceed the 366-day limit across a leap day with a BST-to-GMT span, PR 1150), #1154 (the images window lacks the no-store response cache every other Cases page carries, PR 1147), #1155 (a session with no re...
  Reason: #1153 to #1159 are all CLOSED (11 October 2026).

### Release 92

- Follow-up [issue 1105](https://github.com/collisionengineers/pegasus/issues/1105): the valuation refusal still says "Press Use this value again" after PR 1099 removed the button; its wording awaits the operator.
  Reason: #1105 CLOSED 10 October (PR 1139 dropped the wording).

## Decisions awaiting a round (16)

Not a proof but a decision or wording of the operator's; they come to the operator in rounds of four after the held wording below (R525).

### Release 126

- Operator questions left open at review: whether an empty VRM signal set should veto linking under v2 (it no longer does, D16).
  Reason: Operator question left open at review (D16), not a proof; no later row in docs/rulings.md answers it.

### Release 120

- After that press, deleting `cazana-api-key` and `CAZANA_API_KEY_SECRET_URI` under their own approval.
  Reason: An approval-bound Key Vault and azd deletion that waits on the Companion press, not a proof.
- `cazana-api-key` stays in Key Vault and `CAZANA_API_KEY_SECRET_URI` stays in the azd environment, for rollback to Release 119 until the live Companion press is proven.
  Reason: A retained-secret state awaiting an approved deletion after the Companion press, not a proof.

### Release 113

- The operator's acceptance of each Principal's development and holdout outcomes (the PR bodies quote them)
  Reason: Operator acceptance of holdout outcomes, not a proof.
- a ruling on KMR's empty holdout
  Reason: A ruling the operator owes on the KMR holdout, not a proof.
- ACSP's route decision and more instruction-stage samples under `corpus/holding/`.
  Reason: The ACSP route is the operator decision (L801-802); sample collection follows it.
- ACSP has no route: its one real instruction mail carries no inspection tell, so it waits for the operator.
  Reason: States that ACSP waits for the operator; no later ruling gives ACSP a route.

### Release 111

- For the operator: the FRD-06 and FRD-16 words "the Principal's reviewed locations" and the architecture row (PR 1392, R121), the FRD "disabled" against the code's "absent" (R127 (d)), and the runbook "Reference-data load" section and architecture sentence (PR 1397, R121).
  Reason: FRD, architecture and runbook wording put to the operator, not a proof.

### Release 103

- Operator decisions left open in PR 1307's body: #1015's v39 sign-off list A–Z and its eight resolution choices (Send from only preselects the From picker; the MCP send is refused where the rules need staff; the Archive-only gate cannot show, item X), #1302's three design points, #1305's two wording ...
  Reason: Operator decisions left open in the PR 1307 body, for a rulings round.

### Release 102

- Operator decisions left open in the PR bodies: #1284's 2-hour "left without a decision" window, the arrival notification on a mailbox single match and the PrincipalApi channel; #1293's SafeFileName unification (CORE-REP-07 waits for PR 1015); #1280's WEB-CASES2-13 focused estimate fragment; #1273's...
  Reason: Operator decisions left open in the PR bodies; R314 has since kept the two SafeFileName copies, the rest are unanswered.

### Release 101

- Review notes for a later decision: a supplementary spec whose base is discarded loses its Changes vs option, so its next Save clears its supplementary reason and statement (PR 1254); replaying an old operation key whose spec has since been discarded fails with "Sequence contains no elements" rather...
  Reason: Review notes held for a later decision, not a proof.

### Release 100

- The Opus review's unverified notes, for a later decision: `ReleaseAllAsync` (#1224) updates Automation-leased workflow rows without the per-Case `UPDLOCK` the other mutations take; a Prepared Glass's session with a NatCode or placeholder flag but no vehicle (#1219) follows a changed registration w...
  Reason: Unverified review notes held for a later decision, not a proof.
- Glass's: the #1172 trial left one stock vehicle (plate YS09LXJ) in the valuation account.
  Reason: A clean-up of the external Glass valuation account under its own approval, not a proof.

### Release 91

- Outside the repository, each needing its own approval: delete the EVA secrets from Key Vault `pegasusprodkv252ow37g` (`EVA_CLIENT_ID_SECRET_URI`, `EVA_CLIENT_SECRET_SECRET_URI`) and clear the azd `EVA_*` values in `pegasus-prod`; the new Bicep no longer reads them.
  Reason: An approval-bound Key Vault and azd clean-up outside the repository, not a proof.

### Release 88

- proof that a second Glass's login can open another login's stock record.
  Reason: FRD-25 step 7: both stored credentials are one Glass login, so the proof needs a second login the estate lacks; R174(a) leaves the strike to the walk.

### Release 85

- An empty Sent Items list shows only its count: there is no approved sentence for it, and the operator decides whether it needs one.
  Reason: The operator decides whether the empty list needs a sentence; not a proof.

## Held wording of Release 123, approved as built (R526)

Fourteen constituent PRs of combines 1678 and 1679 landed with items under "Waiting
for the operator" (R218 says no PR lands with a held word). On 29 October 2026 the
operator approved them as they stand (R526): the live defaults stay as built and the
drafted FRD, ADR and tool-description sentences already on `dev` stand. Approving
changed nothing; a later ruling that overturns one names it here.

### Live behaviour built on a default (32)

- [PR 1603](https://github.com/collisionengineers/pegasus/pull/1603): None. One reading to confirm at review, already built: a terminal or Report sent Case that still has an open task draws the card holding the task list alone, because a task remains. If the card should go there too, it is a one-line change to the boolean.
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): The capture PDF (second investigation, decision 1, unanswered). ... This PR applies the investigation's default: a source holding a PDF the capture designation selects ... keeps its image route. Does the capture PDF stay on the image route?
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): A PDF that is the upload itself (decision 2, unanswered). ... This PR applies the default, the literal reading of R296 and R297: an uploaded PDF with a readable text layer is a document, and an uploaded image-only PDF stays on the image route ... Should every uploaded PDF be a document instead?
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): Letterhead when the read stopped early (decision 3, unanswered). Applied default: the images are kept (fail open). A read that stops before some page leaves that page with no placements, so "every page" cannot hold.
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): A PDF attachment that will not open (corrupt or encrypted) still counts as a PDF the e-mail carries. This applies R297's "every PDF attachment is a document" as written. Should it?
- [PR 1608](https://github.com/collisionengineers/pegasus/pull/1608): The label is the existing "Principal notes" (`CaseWorkspaceLabels.Frame`); no heading or other word was written. Say if the Triage page wants another.
- [PR 1608](https://github.com/collisionengineers/pegasus/pull/1608): The two CSS rules above are the one exception to "no CSS change" (the investigation's decision 1); say if you would rather accept collapsed lines.
- [PR 1608](https://github.com/collisionengineers/pegasus/pull/1608): `TriageDetail` is also the MCP result of `pegasus_triage_get` and every Triage mutation, so the Automation Actor sees the note as it sees `PrincipalCode` (the investigation's decision 2); carried, nothing excluded.
- [PR 1611](https://github.com/collisionengineers/pegasus/pull/1611): R378's "out to two hours". The replay is spaced exactly like the live path: 30 s, 2 min, 10 min and 30 min between the five attempts. ... A persistent fault therefore exhausts about 42 minutes after the first failure. Confirm, or rule a different reading.
- [PR 1611](https://github.com/collisionengineers/pegasus/pull/1611): A custody `Failed` outcome. Nothing in production writes it today. If the filer returns it, it is recorded as `replay_exhausted` at once, with reason `case_custody_failed` (my reading of "a non-transient fault still stops at once"). Confirm.
- [PR 1611](https://github.com/collisionengineers/pegasus/pull/1611): Work items already moved to `failed` by `RecordTerminalReplayFailureAsync` since Release 117 stay `failed`; nothing here repairs them. The investigation's default is to leave them and owe a read-only count. Confirm.
- [PR 1611](https://github.com/collisionengineers/pegasus/pull/1611): Which clock the R189 cutoff reads (investigation Decision 1). ... That default is not assumed (R285). This PR builds plan criterion 3 as written: Core computes the cutoff from its `TimeProvider`, less two hours ... Rule which.
- [PR 1611](https://github.com/collisionengineers/pegasus/pull/1611): With Engineer and #1498's Done-when. ... Criterion 2 says the rule's results are unchanged, so this PR lists and files such a pair ... Rule whether With Engineer stays eligible ... `Closes #1498` stands on that ruling.
- [PR 1614](https://github.com/collisionengineers/pegasus/pull/1614): What the report prints for the Trade figure under a market-research basis. R311 says the report no longer needs Trade for that basis but does not say what it prints. This PR leaves the Trade Value row out of the Vehicle Data table ... The alternative is a printed figure.
- [PR 1614](https://github.com/collisionengineers/pegasus/pull/1614): A Case that chose an AI card before this release has no marker, so its Trade stays a blocker until the next save that carries the choice. No compatibility is built (test estate; R114).
- [PR 1646](https://github.com/collisionengineers/pegasus/pull/1646): Words not given by a ruling (OQ-177, asked with OQ-138): the new ADR's title ("The deployment plan check does not read the App Service quota"), its Decision paragraph, and its replaced-scope sentence ... are the implementer's wording of recorded facts.
- [PR 1658](https://github.com/collisionengineers/pegasus/pull/1658): The seven `ServerInstructions` (agent-facing copy; drafted ... R218 shape; approve or replace before landing), P: "Lists page by cursor, 50 by default and 100 at most."
- [PR 1658](https://github.com/collisionengineers/pegasus/pull/1658): L: "A write takes expectedVersion and operationKey and accepts an editLeaseToken from pegasus_edit_begin; without one it holds the record's edit lease for that one command."
- [PR 1658](https://github.com/collisionengineers/pegasus/pull/1658): E: "pegasus_edit_begin, pegasus_edit_renew and pegasus_edit_end hold a record for multi-step work."
- [PR 1658](https://github.com/collisionengineers/pegasus/pull/1658): N: "A download returns the file as content: an image as an image, a PDF as its page text, a small text file as text, and anything else as metadata and an authenticated URL for the original bytes."
- [PR 1658](https://github.com/collisionengineers/pegasus/pull/1658): Open with it: whether the documents, assessment and send servers should say that their Case ids and vocabularies come from `pegasus-cases`, and whether `pegasus-cases` should say that `pegasus_case_get` and `pegasus_vocabulary_get` are on it only. That placement is the held OQ-201 default ... no sentence is drafted for either.
- [PR 1658](https://github.com/collisionengineers/pegasus/pull/1658): `resource_name` in every server's metadata stays "Pegasus Automation MCP", as today. A connector may show it; a name per server needs words.
- [PR 1658](https://github.com/collisionengineers/pegasus/pull/1658): To confirm, not new questions: the refusal for a token without a server's scope is the transport 403 naming no scope (ADR-0080 decision 4); the classifier answers 404 or a redirect on `/mcp` between this Web deploy and its setting change, so C3 ships in the same release ...
- [PR 1662](https://github.com/collisionengineers/pegasus/pull/1662): R198 names contradictions as "the existing FRD-23 field-provenance conflicts". After R286 FRD-23 defines none, so the clause says none is drawn and `Closes #1351`. If the operator names a real conflict ... none is invented here.
- [PR 1662](https://github.com/collisionengineers/pegasus/pull/1662): The INT-20 row's replacement of "validation" by "section-blocker" is R76's reading restated; the operator may prefer to leave the row as it was.
- [PR 1667](https://github.com/collisionengineers/pegasus/pull/1667): The replay cutoff's clock (investigation decision 1, unanswered). ... still taken from Core's `TimeProvider` ... Should the SQL clock also decide that cutoff ...? ... This PR does not build it.
- [PR 1669](https://github.com/collisionengineers/pegasus/pull/1669): Tool descriptions: `pegasus_upload_begin`: "Begins staging one document of at most 10 MiB in parts for pegasus_document_add or pegasus_ai_job_complete_market_research. Declare its size and SHA-256; ... " (Every agent-facing sentence drafted, none approved; placeholders only where code needs a sentence.)
- [PR 1669](https://github.com/collisionengineers/pegasus/pull/1669): Tool descriptions: `pegasus_upload_part`: "Sends one part of a staged upload from pegasus_upload_begin: every part is partBytes long but the last. The part's own SHA-256 is checked before it is stored ... "
- [PR 1669](https://github.com/collisionengineers/pegasus/pull/1669): Tool descriptions: `pegasus_document_add`: "Content is base64 and is limited to 10 MiB before decoding." becomes "Retains one document of at most 10 MiB ... exactly one of the two." plus contentBase64 / uploadId parameter text.
- [PR 1669](https://github.com/collisionengineers/pegasus/pull/1669): Tool descriptions: `pegasus_ai_job_complete_market_research` keeps "Requires automation.jobs only" and adds the staged-upload sentence. Correction 3 of the investigation: the staging side needs `automation.documents` on the documents server, so the operator may want "only" reworded.
- [PR 1669](https://github.com/collisionengineers/pegasus/pull/1669): Refusal sentences (`DocumentUploadRefusals` in Core, one place): "The upload was not found or has expired."; "The total size must be between 1 and 10485760 bytes."; "A SHA-256 of 64 hexadecimal digits is required."; ... "Exactly one of contentBase64 and uploadId is required." ...
- [PR 1669](https://github.com/collisionengineers/pegasus/pull/1669): Choices the investigation left as held defaults (decisions 1 and 2 of the 23 October comment), built as defaulted: the upload belongs to the client, not the grant ...; the two tools are on the documents server only (A1), so a market-research client connects both `/mcp/documents` and `/mcp/jobs`.

### Drafted sentences on dev and main (26)

- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): R299 / FRD-02 'What becomes Unidentified' new paragraph: "A PDF the third-party report profiles recognise as an engineer's report is correspondence or evidence, never photographs; ... The PDF's images become Case images after filing (operator, 22 October 2026)."
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): R299 / FRD-19 'PDF and mailbox photograph entry', after 'takes precedence.': "A PDF with a readable text layer is a document ... An e-mail carrying a PDF is a document carrier: ... with no Case the e-mail waits in No Case Found (operator, 22 October 2026)."
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): R299 / FRD-19 'Photograph selection', after 'not a logo classifier.': "An image object that appears on every page of a multi-page PDF is letterhead art, never a photograph, never an Image Intake candidate, never in the gallery; the 40 KB floor and the 3:1 side ratio stay as they are ..."
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): R299 / ADR-0072 decision 1, after 'refused by Dismiss.': "An e-mail carrying a PDF is a document carrier: its loose photograph attachments open no Image Intake or Unidentified item and are filed with it once the Case is matched; ..."
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): R299 / Not edited: the parenthetical "(photographs with no instruction fields)" at FRD-02:122, FRD-08:14 and ADR-0072:59-60, and CONTEXT.md:77. Should they also say "and no PDF"? The words would be yours.
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): R300 / FRD-08 'Automatic Case association of retained mail' (#1470), new sentence: "Mail association never re-decides a Case the message's case match eliminated, or an `Ambiguous` match (...)."
- [PR 1604](https://github.com/collisionengineers/pegasus/pull/1604): R300 / #1469's FRD-19 and FRD-05 lines landed with PR 1537 and are on `dev` now ... They are listed here for the same approval, and this PR does not edit them. #1484 carries no FRD line.
- [PR 1608](https://github.com/collisionengineers/pegasus/pull/1608): Placement at the three widths (R309, R310); this PR is not green-reviewed until it is approved.
- [PR 1611](https://github.com/collisionengineers/pegasus/pull/1611): FRD-22, R190 sentence: "Five attempts, then record and keep completed: a replay's attempts are bound at the existing transient-failure class count (five), spaced like the live filing path, from 30 seconds out to two hours ... " Approve or replace (R383).
- [PR 1611](https://github.com/collisionengineers/pegasus/pull/1611): FRD-22, R189 sentence: "Retroactive filing within the replay window only: the sweep files to a Case that becomes eligible inside the bounded replay window it already has, the existing two hours (FRD-18) ... " Approve or replace.
- [PR 1611](https://github.com/collisionengineers/pegasus/pull/1611): FRD-05, R190 sentence: "A Case-filing replay has five attempts, then record and keep completed ... " Approve or replace. The `replay_exhausted` row appears on no screen; R190 asks for the row only. Is the existing "Exhausted failures stay visible" sentence meant to cover it?
- [PR 1614](https://github.com/collisionengineers/pegasus/pull/1614): The FRD-24 sentence for R311 (OQ-357, R218 shape). Proposed in R311's own words and not written into any document: "Trade value is not a report blocker when the valuation basis is market research." ... They are left as they are here so no held word lands.
- [PR 1630](https://github.com/collisionengineers/pegasus/pull/1630): Open question (R218 shape): approve or replace one FRD-10 sentence in R363's own words ... Draft, replacing the sentence at :119-121: "A metadata-only answer fetches zero content bytes, except ... " The PR lands only after the answer; on approval the line changes in this PR.
- [PR 1643](https://github.com/collisionengineers/pegasus/pull/1643): Held back: A3-13 (R327). `EfCaseQueryStore.ListHistoryAsync` is unchanged by this PR. ... The operator rules whether a changed tie order is accepted; until a ruling is quoted here and in the `eff-07` plan, the re-sort stays and A3-13 is not built.
- [PR 1648](https://github.com/collisionengineers/pegasus/pull/1648): Open question OQ-377 (A3-24, R337): should each Case images section draw one create-tag form that every editable tile's name field, colour select and Create button point at with `form=` ... No copy changes either way. The PR lands without it unless the operator rules it in.
- [PR 1667](https://github.com/collisionengineers/pegasus/pull/1667): FRD-05 wording (R383; approve or replace before landing). The added lines, in the rulings' own words: "Skip Box holding upload while a group member is `GroupPending` ... For the intake escape window, Core owns the window with one clock ... (operator, 22 October 2026; R191, R379)."
- [PR 1669](https://github.com/collisionengineers/pegasus/pull/1669): FRD-10 lines (drafted, restating R203 and R448): the "Large documents" paragraph after the Documents paragraph; "67 tools"; the inventory row "Stage one document in parts ... "; the edge case "An incomplete, mismatched, foreign or expired staged upload is refused by the call that uses it, and nothing is written."
- [PR 1669](https://github.com/collisionengineers/pegasus/pull/1669): ADR-0083 as a whole (Status quotes R203; Decision restates criteria 1-4 and 9; ADR-0059 stays as filed).
- [PR 1669](https://github.com/collisionengineers/pegasus/pull/1669): Technical figures this PR had to pick, none operator-facing: the open reads four parts at a time (`DocumentUploads.ReadsAtOnce`); the hourly sweep lists one page of up to 5,000 objects (`UploadSweepPageSize` ...).
- [PR 1671](https://github.com/collisionengineers/pegasus/pull/1671): FRD-22 recheck sentence (R383). ... Please approve or replace it: was: "rechecks the Case version before each custody attempt and confirmation. ..." now: "rechecks the association, the archive, the Case's state and a live edit lease before each custody attempt and confirmation. A later retry finds the lease released, so a finished staff edit never strands retained files."
- [PR 1674](https://github.com/collisionengineers/pegasus/pull/1674): FRD-03 "Automatic creation from intake": "A request in reply form opens the Triage unless the thread already holds a Case or Triage (operator, 22 October 2026; R195)."
- [PR 1674](https://github.com/collisionengineers/pegasus/pull/1674): FRD-03 "How a Triage starts", third way, appended: "the Automation Actor does so with `pegasus_intake_action` `open_triage`, and a correction to Triage request opens nothing by itself (operator, 22 October 2026; R223)".
- [PR 1674](https://github.com/collisionengineers/pegasus/pull/1674): FRD-10 queue and intake tools: "With `open_triage` it opens the Triage Case from an item classified, or corrected to, Triage request, as **Open the Triage** on its message does". The scope table row adds "or `open_triage` on a Triage request".
- [PR 1674](https://github.com/collisionengineers/pegasus/pull/1674): FRD-02: "and opens the Triage as **Open the Triage** does (operator, 22 October 2026; R223)".
- [PR 1674](https://github.com/collisionengineers/pegasus/pull/1674): "The thread" is the exact mailbox conversation: confirmed by the operator (R454, OQ-375, 23 October 2026, round 106).
- [PR 1674](https://github.com/collisionengineers/pegasus/pull/1674): Scope: R195 and criterion 9's `CurrentCaseId` clause apply to requests in reply form only (OQ-158's subject): confirmed by the operator (R453, OQ-374, 23 October 2026, round 106). A request not in reply form still opens as it does today.
