# Archived execution log — part 3 of 4

Original SHA-256: `e6b834160b41638637d9ffd1115c7ebc1cc502987578eb36c6711e474485bdc7`
Character range: 50000–74999 of 95046.

## Payload

2 at `f55a5adac`

R-12 (uncertainty by Core semantics; `System.Net.Http` gone from the built Core assembly), R-13 (cancellation → unknown, proved), R-14 (double enforces A's staff-only status read; exposes the contract gap — public Pending cannot self-reconcile; handoff with two shapes posted to A, plus A04 refusal-type instruction). Re-review dispatched; wave 25 starts when wave 24 (C08) releases LocalDB. C05 round 3 dispatched (Opus) for R-16 and R-6. Editors: C06 r4, C05 r3.

## Checkpoint — GitHub relays and C06 round 4

Board sync paused for A: C07 status-read contract gap + two options posted as PR 673 comment 5560698342. B handoff (comment 5560632798) answered in comment 5560702xxx: `_Layout.cshtml` `case-workspace.css` link goes into C08's next shell commit; Case-vehicle labels stay B-owned; documents/chase `RequestUploadPolicy` Recipient/Reason + labels to be answered from the CASE-047 B01 table with the C07 slice. C06 round 4 at `f1519a2f9` (fixture fixes only; R-23 disclosed; R-24/R-25 deferred); re-review dispatched; wave 26 queued behind wave 24 (C08) and wave 25 (C07 caller) on LocalDB. Editors: C05 r3 only; C08 round 2 (B's stylesheet link + wave-24 findings) next.

## B01 C handoff (PR 670) queued as C07 follow-up

F carries `RequestUploadLinkEntity.Recipient/Reason`; C owes `CreateRequestUploadLinkCommand`/`RequestUploadLink` Recipient (required ≤500) / Reason (optional ≤1000), `NormalizeCreate`, store replay-snapshot compare + projections, and `OperatorLabels.CaseWorkspace.{Recipient,Reason,Content,RecordChase}` (C08 labels batch). Shapes posted to B on PR 673; the policy/store half lands as the next C07 commit set after the retention-caller slice integrates (same files). `IntakePersistenceIntegrationTests` migration-list item is moot under the single v1 migration.

## Checkpoint — A option 1 in progress; C07 caller published for inspection

Helper `task/pegasus-v1-c-retention-caller` = `f55a5adac` pushed; PR 673 comment posted with exact caller files. Per A: identityless-Unknown + fresh-key duplicate risk reopened as an open finding (C will re-issue the same operation key while an arrived/unknown/pending occurrence exists for link+content; A owns an identity-less status lookup); refusal timing — A's adapter rechecks authority after reading bytes, so C's pre-read exception list needs A's explicit refusal contract. Reviewer told to keep both open. Wave 25 (C07 caller) running; C08 round 2 dispatched (Compose test posts its key; preview fake must delegate reads; B's `case-workspace.css` link; `CaseWorkspace` labels). Editors: C05 r3, C08 r2.

## Wave 25 (C07 caller `f55a5adac`): C lanes all PASS

Build 0/0, core 19/19, integration 59/60 (+1 skip), architecture 100/100 (Core no longer references System.Net.Http); a-owned lane 1/5 = A's seeded-QDOS duplicate only. Re-review binds with the two A-reopened findings. Wave 26 (C06 `f1519a2f9`) launched.

## G15 + C07 caller re-review

G15 `9297cee60` (`ICaseArtifactCustodyStatus.FindByOperationKeyAsync`, null = no committed intent observed, never authorizes a fresh key) merged `--no-ff` → C head `714f19009` (unpushed: C's Core recording double must implement the member explicitly — integration fix running, then push). C07 caller re-review at `f55a5adac`: R-12/R-13/R-14 fixed; open R-3a (fresh key after identityless unknown re-offers bytes; dedupe is session+key only), R-22 (A rechecks authority after reading, so `StaffAuthorizationException` may arrive with bytes staged; A's rule: StaffAuthorization before the Pending-intent commit = definite refusal, staging alone is not intent; post-commit exceptions uncertain, reconcile by original key; adapter `ArgumentException` not assumed refusal), minor R-18 (page does not handle StaffAuthorizationException). Round 3 brief: merge C head (G15), reuse the original occurrence/key on the sender's next GET while an arrived/unknown/pending occurrence exists, reconcile identityless unknown via `FindByOperationKeyAsync` copying recovered identities+state, refusal → occurrence refused (fresh deliberate submission allowed only after a definite refusal), double implements the new lookup under the link rule. Queued behind C05 r3 / C08 r2.

## G15 pushed — C head `5405c88f7`

`714f19009` (G15 merge) + `5405c88f7` (Core recording double implements `FindByOperationKeyAsync`); build 0/0; pushed; A told (PR 673 comment 5560755365). C07 caller round 3 (original-key reconciliation, refusal mapping, R-18) waits for an editor slot (C05 r3, C08 r2 active); c07b receives the G15 head now.

## A blocker on C07 caller (PR 673 comment 5560753915) — accepted

`arrived` is not a durable hand-over claim: `FindAsync` returns null for it and simultaneous same-key callers both reach custody; a Confirmed return followed by a `RecordAsync` failure leaves `arrived` and re-offers. Disposition posted (comment): atomic CAS claim `arrived → unknown` before `RetainAsync` (one winner; losers reconcile the original key via G15), claim persisted before the call, monotonic confirmation (no downgrade of Confirmed), `FindAsync` returns arrived/unknown honestly, refusal → `failed`, four regression tests. Folded into the C07 caller round 3 brief with the G15 double implementations (c07b at `4e3d3c803` currently fails to build on the two doubles). Round 3 starts at the next editor slot.

## Checkpoint — C08 round 2 at `6690a33cc` (merged G15 → `df03ccd4e`)

Compose test posts the rendered OperationKey; preview 404 root cause was the test's own `queue=all` (invalid queue value → Index 404 before Preview), fixed with a real queue key; B's `case-workspace.css` link in `_Layout.cshtml`; `OperatorLabels.CaseWorkspace.{Recipient,Reason,Content,RecordChase}`. Wave 27 queued behind wave 26 (C06). C07 caller round 3 dispatched (Opus): G15 doubles, atomic arrived→unknown claim, monotonic confirmation, original-key reconciliation, refusal mapping, R-18, six regression tests. Editors: C05 r3, C07b r3.

## A: G15 adapter published (`58cf07ecb` on task/pegasus-v1-platform; combined host supplies it, no C import)

Exact-link `GetAsync` and `FindByOperationKeyAsync` include live-link authority and accepted-creator provenance in one query; other active links on the same Case see nothing; lookup does no provider/content access; strengthened provider-write-failure test proves the original-key lookup returns the committed Pending intent with exact identities after an IOException. A confirms C's original-key claim/recovery/monotonic-recording changes remain necessary — all in the running C07 caller round 3.

## Wave 26 (C06 `f1519a2f9`): all six lanes PASS

Build 0/0, core 61, integration 38, host 118, browser 2, architecture 100. C06 re-review binds on it. Wave 27 (C08 `df03ccd4e`) launched.

## C06 integrated — C head `aa3202746`

Review `pass` at `f1519a2f9` (`scratch/review-c06`, four rounds; open minors R-24/R-25 accepted-risk with reasons). Merged `--no-ff`, build 0/0, pushed; DI list + routes posted to A/B on PR 673. Residual for C06 closure: A's real registrations (then bridges revert to required), catalogue routes, R-24 (needs an A migration), B's picker consumption.

## Checkpoint — C05 round 3 at `eb46b7a7d` (merged `7467190b1`)

R-16 fixed (document-level locator = retained file name via `ThirdPartyReportSourceContext`); R-6 root-caused to an A-owned defect — queued re-evaluation deletes the staged copy at first-pass completion, so the re-claimed pass fails `staged_artifact_integrity_failure` before the reader (handed to A: PR 673 comment 5560823100); test renamed to assert the reading stands and the work item's real failure; replay guard proved against SQL separately (ASSUMPTION 8). R-17/R-18 fixed. Re-review dispatched; wave 28 queued behind wave 27 (C08). C02 slice worktree `v1-intake-c02` (`c02-provenance`) created off `aa3202746`; implementer dispatch next (editors: C07b r3 + C02).

## A's C06 DI patch applied — C head `306db9502`

One A-owned file (`DependencyInjection.cs`: Claim Source store/command/query, `IOrganizationDirectoryQueries`, `IUpdatePrincipalDefaultInspectionLocation`, inspection choices/location ports → one scoped concrete); hash verified; build 0/0; pushed; A told. Queued C06 cleanup (next editor slot): make the directory and default-location dependencies required again, delete `C06AdapterRegistrations`/`WithC06Adapters`, then C06 HTTP/persistence lanes through production registrations. Editors: C07b r3, C02.

## Wave 27 (C08 `df03ccd4e`): 1 failure left

Build/core 59/browser 82/architecture PASS; integration 146/147 — `OpenPreviewFilterUnreadAndSortNeverWriteThroughTheRetainedMailPorts` now 404s on the list GET itself: the test's query uses `sort=asc`, but `Index.cshtml.cs` `TryParseSort` accepts only `oldest` (absent = newest); `unread=true` must also be checked against `TryParseUnread`. C08 round 3 (test query values + assert the parsed filters round-trip) queued with the C06 bridge/test-composition cleanup behind the two active editors (C07 caller r3, C02). Wave 28 (C05) running.

## Wave 28 (C05 `7467190b1`): all lanes PASS

Build 0/0, core 30, corpus 13/13 against the pack, web 11 + 5 known absent-pinned-sample skips, architecture 100. C05 re-review binds on it; on pass, C05 integrates.

## C05 integrated — C head `2c1a9d8a1`

Review `pass` at `eb46b7a7d` (3 open minors R-21/R-22/R-23, residual). Merged `--no-ff`, build 0/0, pushed, published on PR 673. Residual for C05 closure: A's re-evaluation staging fix (tripwire test re-pointed after), `ThirdPartyReportValuation.Adjustments` contract request, Finding chip via C04/C08, minors R-21–R-23.

## Checkpoint — rate limit hit (resets 21:30 London); continuing at low priority

C07 caller round 3 at `4a92a06e4` (atomic claim `TryClaimHandOverAsync`, monotonic `IncomingArtifactCustodyProgress`, original-key GET/recovery, refusal → failed, R-18, G15 doubles, tests a–f); merged C head → `37a923067`; wave 29 + re-review dispatched. C02 implementer cut at `796778e8b` (locator + reader structure committed; `IntakeOcr.cs`, `AzureDocumentIntelligenceOcr.cs`, `EfIntakeOcrOperationStore.cs` untracked drafts) — resumed with "commit drafts first" and the C-F02 persistence caveat. C06 cleanup agent cut before any change — resumed. C08 round 3 (query values) still queued.

## Wave 29 (C07 caller `0a0e88975`): C lanes PASS

Build 0/0, core 23, integration 66/67 (+1 skip), architecture 100; a-owned lane = A's seeded-QDOS duplicate only. Helper `task/pegasus-v1-c-retention-caller` moved to `0a0e88975`; A told what changed. Re-review pending; on pass, C07 caller integrates (then the B01 Recipient/Reason follow-up in the same files).

## Checkpoint — C06 cleanup at `fea0c0e78`

Directory and default-location dependencies required again; `C06AdapterRegistrations`/`WithC06Adapters` and the three bridge-proof tests deleted; C06 HTTP/persistence tests run on production DI (A's `306db9502`). Wave 30 + targeted review running. C08 round 3 (test query `sort=oldest`, page's unread token, round-trip assertion) dispatched. Editors: C02 (resumed), C08 r3. C07 caller re-review pending on `0a0e88975`.

## A replies (PR 673 5560993657 / 5561005209)

Re-evaluation defect accepted by A (INTK-027: re-read the logical confirmed Box version after staging expiry); C keeps and later retargets the tripwire test. `Adjustments` contract request withdrawn — source-row representation stands. A's combined run flagged the obsolete C06 no-registration test; already deleted in the C06 cleanup `fea0c0e78` (wave 30 running). Replied on PR 673.

## C07 caller re-review at `0a0e88975`: needs-changes — 3 majors, 2 minors

A's three items (atomic claim, original key, refusal mapping) verified fixed. New: R-24 `RecordAsync` forward-only rule is a non-atomic read-modify-write on an entity with no concurrency token (a loser's Pending can overwrite the winner's Confirmed) → one conditional `ExecuteUpdateAsync` naming allowed source states; R-25 the web double's `GetAsync` still staff-only while `FindByOperationKeyAsync` has A's link fence — must be the same fence for both; R-26 re-presenting the unresolved key per LINK blocks a second, different file (`OperationConflict`) contrary to plan item 6 — round 4 direction: reuse the unresolved key only when the posted bytes' SHA-256 equals the unresolved occurrence's, otherwise a new deliberate submission gets its own occurrence/key. Minors R-27 (unclaimed `existing is null` branch), R-28 (stale comment). Round 4 queued (editors: C02, C08 r3).

## Checkpoint — C08 round 3 at `86e8659f5` (merged `2c1a9d8a1` → `e1f8850ee`)

Test query `sort=oldest` (page vocabulary), unread token verified, round-trip assertion added; no production change. Wave 31 + review after wave 30 releases LocalDB. C07 caller round 4 dispatched (Opus): R-24 conditional-update transitions, R-25 same fence on both status reads, R-26 same-bytes reuse vs new-file new key, R-27, R-28. Editors: C02 (resumed), C07b r4.

## A INTK-027 coordination (PR 673 5561022593 / C ack 5561054246)

A's re-evaluation fix makes `IReadLogicalDocumentVersion` a required `ProcessQueuedIntake` dependency; A adapts the two direct constructions in `QdosAllocationRecoveryTests.cs` (near 603 and 749) on its branch, assertions preserved; C takes the same two-argument change when the fix arrives via composition/G and edits nothing there meanwhile. C05 tripwire retarget remains C's after publication.

## Wave 30 (C06 cleanup `fea0c0e78`): all six lanes PASS on production DI

Build 0/0, core 61, integration 35, host 131, browser 2, architecture 100. Cleanup review binds; on pass, integrate and publish. Wave 31 (C08 `86e8659f5`) launched.

## Checkpoint — C02 READY at `494767d30` (merged `ca3ec9abb`)

All seven C02 items; OCR persistence on F's `IntakeOcrOperations` (no new table), `LocatorJson` v2 envelope, `EfIntakeOcrOperationStore.cs` disclosed deviation; `intake_ocr` enqueue deliberately not built (kind must come from A's `ExternalWorkKinds`) — exact A hunks in the report; INCONCLUSIVE: provider correctness (no genuine DI response), AcroForm locators. Review dispatched; wave 32 queued behind wave 31 (C08). C03 worktree `v1-intake-c03` (`c03-profiles`) created on the C02 merged head so profiles build on the structured extraction; batch 1 (QDOS extension + PCH) dispatches next. Editors: C07b r4 + C03 b1.

## C06 cleanup integrated — C head `92f98b749`

Review `pass` at `fea0c0e78` (zero open findings; R-24/R-25 accepted-risk). Merged `--no-ff`, build 0/0, pushed, reported on PR 673. C06 now proves its bindings on the real host. Remaining C06 residual: R-24 needs an A migration; B's picker consumption; catalogue routes (A).

## Wave 31 (C08 `86e8659f5`): 159/160

Build/core 59/browser 82/architecture PASS; the one failure is round 3's own round-trip assertion: with `sort=oldest` active the sort toggle omits `sort` (`Index.cshtml:120`) and row links exist only when `search=vehicle` matches a subject/sender (the seed text is body/excerpt), so `unread=true&amp;sort=oldest` is never rendered. C08 round 4 (assert tokens where the page actually emits them) queued behind the two active editors (C07b r4, C03 b1). Wave 32 (C02 `ca3ec9abb`) launched.

## A INTK-027 fix published on A's branch (`9028aa12b`, PR 673 5561151076)

Touches `DurableIntake.cs` (C02 map, INTK-027 = A), `CustodyOutboxIntegrationTests.cs` (C07 file), `QdosAllocationRecoveryTests.cs` (+2 authorized args), A infra. Asked A for the transport (G object preferred vs bounded patch). A's revised retry rule: after the fix, an identityless Unknown with G15-null may be retried with the SAME original scoped key and identical validated bytes (never a fresh key); serializable RequestLink path converges on one intent / one provider initiation; double's `GetAsync` admits the exact active RequestLink artifact, `CreatedBy = RequestLink:<D-guid>`. To be folded into the running C07 caller round 4.

## Wave 32 (C02 `ca3ec9abb`): 3 failures in new C02 tests

Build 0/0, core 287/287, web 57 + 7 known corpus skips, architecture 100. Reader lane 27/30: `AzureDocumentIntelligenceOcrTests.ConfidenceIsCarriedThroughAndIsNeverWhatAcceptsAValue` (no confidence item produced), `StructuredIntakeSourceReaderTests.TheOuterSenderTheCurrentBodyAndTheQuotedHistoryStayThreeSeparateThings` (quoted-history fragment starts with a newline), `OcrIntakeRecoveryTests.ASubmittedOperationCompletesOnceAndReanalysesOnce` (response hash read back space-padded — fixed-width column vs short fake hash; store must trim or tests use real-length hashes). C02 review will bind; correction round 1 queued behind the active editors (C07b r4, C03 b1), then C08 r4.

## A: INTK-027 bounded patch (PR 673 5561171653) + same-key replay correction (5561181368)

Patch: 8,024 bytes, SHA-256 `14691a9f…`, against `92f98b749`, only `Core/Intake/DurableIntake.cs` + the two authorized `QdosAllocationRecoveryTests.cs` arguments; A infra/readers stay in the combined host; C owns further direct-constructor adaptations and the C05 tripwire retarget. A adapter `0c72818da`: same-key Pending replay returns the persisted status with one provider write; Failed replay returns its disposition. Applying the patch now.

## INTK-027 patch applied — C branch `31e9857b8` (unpushed, build red)

Patch hash verified, applied, committed with A attribution. Build fails on C's own direct `ProcessQueuedIntake` construction in `CustodyOutboxIntegrationTests.cs:2467` (new required `IReadLogicalDocumentVersion`). Integration implementer dispatched: adapt every C-owned direct construction with a C-owned reader double (standalone C has no A04 adapter — no production fallback), and retarget the C05 tripwire to `recorded_reading_stands` with the double serving the exact retained bytes. Then build, push, report. Editors: C07b r4, C03 b1, + this integration fix.

## C08 review at `86e8659f5`: needs-changes — 1 blocker, 5 majors, 5 minors

R-1 (blocker) round-3 round-trip assertion: `search=vehicle` matches only `BodySearchText`, which the seed never creates, so no row link renders; R-2 `Between(html, "<a class=\"btn\" asp-page"…)` hunts a stripped tag-helper attribute; R-3 `OperatorLabels` `_ => "Submitted"` mislabels Prepared/DraftCreating/DraftReady/Sending; R-4 Compose's redirect discards `Operation` so the Send-status panel/Reconcile form never render and Unknown-without-resend/same-key replay untested; R-5 `Take(MaximumAttentionRows)` unproved (no 10/over-10 test); R-6 palette `open()` lacks an already-open guard (second Ctrl+K leaks `inert`). Residuals honest: `IGetAttentionRows` still unregistered (bridge stays), no `IStaffMailSend` impl, `AllowStaffSend` unmapped, `Generation` never populated by A's store (Compose sends 0 — flag to A), Reply/Forward blocked, catalogue routes, `/Administration/AiJobs` owner. Round 4 queued behind active editors.

## C02 review at `494767d30`: needs-changes — 1 blocker, 4 majors, 9 minors

R-1 (blocker) `AzureDocumentIntelligenceOcr.Pages` builds lines with empty `Words` so no confidence/coordinates survive; R-2 provider operation id persisted only after `AnalyzeAsync` returns (cancelled/crashed attempt → row Pending without id → next delivery resends the pages); R-3 whole-body fragment stamped `CurrentBody` though it contains the quoted history, and `DistinctBy` drops the QuotedHistory duplicate (quoted-only values become the sender's); R-4 `EfIntakeOcrOperationStore` never writes back to A's `ExternalWorkItems` row (no redelivery on RetryScheduled, completed never closes); R-5 DOC/MSG partial not extended (no locator/quoted fragment for `.msg`), undisclosed. Ownership clean; no QDOS snapshot change. Round 1 queued behind the three active editors (C07b r4, C03 b1, INTK-027 integration fix), with C08 round 4.

## A on C08 residuals (PR 673 5561214716; C ack posted)

Generation and StaffSend ARE mapped by A's store (combined) — C08 round 4 must remove the `StaffSend`-or-`SentEvidence` fallback and require real `StaffSend` + positive `Generation`; `IGetAttentionRows` mapping: A authors after C publishes the reviewed C08 head (bridge stays until then, then reverts); `StaffMailSend` exists in A (DI:815), offline adapter A's; `/Administration/AiJobs` is A06 — keep the link; `/Inbox/Compose` catalogue entry A's. Residuals must cite combined availability. Folded into the C08 round 4 brief (queued).

## INTK-027 integration on the C branch — `78cb51c2c` (unpushed, build 0/0)

`465d099f8` adapts C's direct `ProcessQueuedIntake` constructions (`CustodyOutboxIntegrationTests:2467` refusing double; `IntakeWebDriver.CreateProcessor` prefers a host-composed reader, else the refusing double — test support only); new C-owned `Support/RecordingLogicalDocumentVersionReader.cs`. `78cb51c2c` retargets the C05 tripwire to work item Complete + `recorded` → `recorded_reading_stands` with the armed double serving the exact retained bytes (ASSUMPTION 8 closed). Wave 33 running before push. C02 round 1 dispatched (Opus). Editors: C07b r4, C03 b1, C02 r1. C08 r4 next.

## A DevelopmentOffline mail composition patch (PR 673 5561281887)

Two A-owned files (`Infrastructure/Email/UnavailableStaffMailSend.cs` new; `Web/Program.cs` offline-only registration), 2,721 bytes, SHA-256 `6eb2a5b5…` verified, dry-run applies on the C head `78cb51c2c`. Applied after wave 33 completes (so the wave binds to a stable head), then push + report. `IGetAttentionRows` binding follows after C08 publishes.

## C head `0efc1d0df` pushed (INTK-027 + C adaptations + C05 retarget + A offline mail)

Wave 33 at `78cb51c2c`: build 0/0, Core 801/801; integration 95/100 and architecture 98/100 fail ONLY on A-owned lanes — `IReadLogicalDocumentVersion` unregistered in standalone C's Web/Worker composition (2 `WorkerCompositionTests`, 3 `QdosAllocationRecoveryTests`) plus the seeded-QDOS precondition fixture A fixed on its branch. Reported; asked A for a bounded standalone-composition patch (real reader registrations for both profiles) + the fixture hunk against `0efc1d0df`. No C stub added.

## Checkpoint — C07 caller round 4 at `6dfb0b8c8` (merged `2c427c643`)

R-24 conditional-write transitions (two-context race proof), R-25 `GetAsync` under A's link fence, R-26 second file gets its own key (DECISION with A's caveat), R-27 `UnclaimedHandOverException`, R-28; A addendum: same-key re-offer for identityless G15-null claim with identical bytes, `HandOverContentMismatchException`, tests g/h/i. Wave 34 + re-review dispatched. C08 round 4 dispatched (Sonnet). Editors: C03 b1, C02 r1, C08 r4.

## A CI (isolated A) fixture failures — confirmed covered on C (PR 673 5561316238 / reply posted)

Six `IntakeWebNegativeTests` (one-principal assumption) fixed at C01 `e80862f37`; two `OrganizationAdministrationPersistenceTests` fixed at C06 `556a26b1a`; both on the C head `0efc1d0df` and passing in C waves. No C rewrite. A's MIME boundary test is A's.

## Wave 34 (C07 caller `2c427c643`): C lanes PASS

Build 0/0, core 25, integration 68/69 (+1 skip); a-owned 1/5 (A seed); architecture 98/100 = the C-head A-owned standalone gap (`IReadLogicalDocumentVersion` unregistered in `WorkerDependencyInjection`), not the slice. Reviewer told to bind on the slice's lanes.

## A rulings (PR 673 5561349163 / 5561352547 / 5561355946)

G16 `e028ddf39` (filtered unique `CaseReportGenerations(CaseId,SnapshotHash) WHERE State <> Stale`, migration + snapshot; B owns the SQL regression proof) — merging `--no-ff`. Standalone reader: a host-only patch cannot compose the production reader (needs A04 Box fenced reads + custody writer identities); A retains adapters in its PR and proves the combined host; C adds NO stub — the `WorkerCompositionTests`/`QdosAllocationRecoveryTests` DI failures on the C branch are a qualified cross-stream dependency, stated as such in C's residuals. C's `0efc` test-reader adaptations passed A's source review. QDOS seeded-fixture hunk (3,714 bytes, sha `118128c3…`, against `0efc1d0df`, C-owned `QdosAllocationRecoveryTests.cs`, A-authored) — applying with A authorship.

## Wave 35 (C03 batch 1 `0f1355108`): 2 failures

Build 0/0, architecture 100; core 272/273 — `QdosInstructionExtractionPolicyTests.ThePartyBlocksAreReadAsTheirOwnRolesAndCutFreeOfTheirNeighbours` (line 1097: repairer block now cut to "Gordon Marshall Coachworks", expected the address to follow — C03 QDOS extension regressed the party-block cut or the test's expectation must move with the row's "scope … location" rule; reviewer to bind); corpus 2 + 2 known skips; web 15/21 — `IntakeAllocationConsumerTests.QualifyingTriageRemainsOne…` (Expected 0, Actual 1) is the A-owned seeded-QDOS precondition: A's fixture hunk is on the C branch (`27004c0ea`) but c03 sits on `ca3ec9abb` — merge the C head into c03 before the next wave. `TrackedPegasusSourceHashesHaveNotDrifted` did not fail in these lanes (check filter coverage).
