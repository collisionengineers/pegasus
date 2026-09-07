# Archived execution log — part 2 of 4

Original SHA-256: `e6b834160b41638637d9ffd1115c7ebc1cc502987578eb36c6711e474485bdc7`
Character range: 25000–49999 of 95046.

## Payload

gents died at ~13:50Z. Slice heads: c01 `ea4848acd`, c07 `7000842ed`, c08 `c64d9cf83`, c05 WIP `815385cda` (three draft Core files committed as wip), c06 `f2b99b5ce` (no edits). Wave 3 had only started its build; the C01/C07 reviews produced no attestation.
- G12 `c4d09b6e8` merged → C head `ab9f3fcd8`, pushed. C07 and C01 resumed to merge G12 and resolve their typed-actor conflicts in-stream; then a combined runner wave (C07 lanes incl. the concurrency proof twice, C01 lanes), then the two reviews, then C05/C06 resume. Running at low priority; one runner and at most two editors.
- Baseline (e5c9b1f43) whole-solution: 33 integration failures mapped to owners on PLAT-075 notes; the C-owned ones are addressed by C07 (Triage), C01 (negative tests) and C06 (organization administration).

## Wave 4 — C01 at `9b4dd1ef2` (G12 merged, no conflicts)

build PASS; Core 108/108; Architecture 100/100; integration 95/96 — the single failure is `ProviderApiSubmissionTests.ADeclaredTriageOpensATriageAndAllocatesNoCase`, which needs C07's Triage allocator (Sequence = 0 on this slice) and is expected to clear at integration. C01 is integration-ready pending its independent review (running) and the corpus rebuild on A.

## C07 at `b46a07452` — G12 merged and resolved in-stream

Conflicts resolved in `EfTriageStore.cs` (usings) and `Pages/Triage/Details.cshtml.cs` (explicit engineer choice with the typed actor); `AddTriageNoteRequest.Actor` typed and hashed with kind+subject; `ValidateNote` through G12's `ValidateActorAndOperation`; A-owned G12 files untouched. Two fixes found in the merge review: Triage note bound now 500 (one constant, matches `TriageHistory.Reason`), and `ICaseEngineerChoices` (registered by G10) is a required dependency of the assignment picker. Still A handoffs: `IAddTriageNote`, `IListTriagePage` registrations. Wave 5 dispatched (incl. the concurrency proof twice and the G12-touched suites). Deviation 3 (typed actors) closed.

## C01 review (9b4dd1ef2): needs-changes

Independent reviewer: one major `C01-R-1` (Unidentified keyset continuation filters media kind after a window and can drop rows under a sparse filter), five minors (source-asset rule triplicated; stranded XML doc; `SingleOrDefaultAsync` on non-unique key; C03 deferral unrecorded; no simplification pass), four nits; PR 639/646 rows, analysis command, selector and A05 items verified faithful; no A-owned file touched. Correction round 2 dispatched to the C01 implementer; a targeted re-review and wave follow. C07 review running.

## Corpus lane finding from A

`MultiFormatGenuineCorpusWebTests.GenuineMsgIsRetainedInNeedsSortingWithoutReference` fails on A's immutable corpus (pinned MSG `7e4c50d5…`): expected `NeedsSorting`, actual `CaseCreated`. The sample is not in the reference pack and this machine has no `corpus/`, so C requested the receipt's route/classification/extraction evidence from A's lane before deciding between re-pointing the assertion to what a routed staff-uploaded instruction should prove (with a non-routed `.msg` pinned for the NeedsSorting case) and fixing a C route/classification defect. Evidence limit recorded: the genuine-format cohort and QDOS cohorts skip on this workstation (INCONCLUSIVE, never PASS); C relies on A's corpus lane for those.

## C07 review (b46a07452): needs-changes

Two majors in `EfPublicUploadRetentionStore` (Box identities cleared on non-Confirmed records; UPDATE grant on `PublicUploadOccurrences` missing in A's migration — handoff), six minors/nits; allocator order, keyset, PR 671 corrections, session policy, custody invariants, G12 typed actors verified in code; no A-owned file touched. Correction round 3 dispatched; grant request posted to A. Both slices then get a targeted re-review and integrate.

## C01 integrated — C head `ca9caae70`

Independent review `pass` at `741f1a70d` (attestation `scratch/review-c01`); wave 6 build/Core 108/Architecture 100 PASS, integration 96/97 (C07-dependent test). Merged `--no-ff` into `task/pegasus-v1-intake`, Release build exit 0, pushed (PR #673). Residual for C01 closure: A's DI/host patch (+A04 reader), corpus rebuild, preservation statement 15 real-SQL proof at integration, fifteen-profile reachability via C03. C07 `2ba5e4e21` in wave 7 + re-review; G13 allocator hunk extraction running.

## Checkpoint 13:50Z

- C07: review at `2ba5e4e21` = needs-changes with one new major (C07-R-9, custody fixture resolved the scoped receipt store from the root provider — the same defect wave 7 surfaced). Fixed at `28148f54f` (test-only, 7/3 lines, solution build 0/0); wave 8 integration lane running.
- G13 helper published for A: `task/pegasus-v1-c-triage-allocator-hunks` = `65002169f` (rooted at G12; contracts+store+allocation tests only). Posted on PLAT-075 `c-stream-notes`.
- C08: C branch (`ca9caae70`) merged into `c08-shell` → `88028df51`; sole conflict `OperationsSnapshot.cs` resolved to G11's seven-field snapshot with C08's shared `FetchAttentionInputsAsync` and `IGetAttentionRows` retained; `UnidentifiedCount` from the open queue. Build 0/0. Test wave queued behind wave 8 (LocalDB is serial).
- C05 resumed at `36b0d84f1` (C branch merged over wip `815385cda`), Opus implementer; C06 started at `930440465`, Sonnet implementer. Two editors active.
- Stale wave 3 runner stopped.

## C07 integrated — C head `1561b886f`

Superseding review `pass` at `28148f54f` (`scratch/review-c07`); wave 8 build 0/0, integration 72/72. Merged `--no-ff` into `task/pegasus-v1-intake` → `1561b886f`, Release build exit 0, pushed (PR #673). A notified with C07 DI list, the PublicUploadOccurrences UPDATE grant and snapshot regeneration on PLAT-075 `c-stream-notes`. Residual for C07 closure: A's host patch + A04 custody adapter, grant, G13 same-object merge, snapshots.

## G13 merge in progress

A published G13 `99f48a459` (counter-first Triage allocator from C's helper, migration line, A/B fixture corrections). `git merge --no-ff` into the C branch at `1561b886f` conflicts in `EfTriageStore.cs` (4 blocks) and `TriageReferenceAllocationTests.cs` (add/add) against C07's superset; an Opus resolver is completing the merge commit with second parent `99f48a459`, build-only. Slices c08 (`88028df51`, wave 9 running), c05 and c06 (implementers active) receive G13 via a C-branch merge once their current runs finish.

## G13 integrated — C head `b1773601e`

`99f48a459` merged `--no-ff` as the identical object (parents `1561b886f`, `99f48a459`); conflicts in `EfTriageStore.cs` and `TriageReferenceAllocationTests.cs` resolved to C07's superset plus G13's own-row reference invariant; A changed nothing beyond C's helper `65002169f`. Build 0/0, pushed. Wave 10 (Triage + A/B fixture lanes) running on the C branch. Pinned MSG disposition posted to A: obsolete NeedsSorting assertion, correction queued (`MultiFormatGenuineCorpusWebTests`, C-owned). C08 wave 9 at `88028df51`: build/core/architecture PASS, integration 93/95, browser 75/82 — nine C08-own defects (admin rail clipping at 760px with eleven links, palette Escape focus return, preview 404 under the recording classification store, Compose not redirecting); correction round queued behind the two-editor limit (C05, C06 active).

## A composition patch applied — C head `2b6b5ed37`

A's C01/C07 DI+MCP patch (PR 673 comment 5559772047, sha256 verified) applied `--3way` clean on `b1773601e`, committed with A attribution, build 0/0, pushed. Wave 10 (G13 head): build/core 75/integration 60+1 skip PASS; its runner stalled before the architecture lane, which is folded into wave 11 (architecture, MCP 9, C01/C07 activated ports) on `2b6b5ed37`. C06 implementer stalled once, resumed from `30a5196c5` (one commit). Queued C-owned edits (two-editor limit, C05+C06 active): pinned-MSG correction + stale Triage note comment/required `IAddTriageNote`; C07 public-upload retention caller (`IUploadToRequest` → session → Pending occurrence → `RetainIncomingArtifact`, fail-closed without custody; A asked to extend `EfCaseArtifactCustody.RetainAsync` authorization to RequestLink/SystemWorker); C08 correction round (nine wave-9 defects).

## Wave 11 on `2b6b5ed37` — all PASS

Build 0/0, architecture 100/100, MCP ingress 9/9, C01/C07 activated-port integration 37/37 (`wave1/wave11-tests/`). Head stands as pushed. Slice c08 receives the patched head next; C05/C06 implementers still active.

## Checkpoint 15:45Z

- C05 READY_FOR_TESTS at `11a306580` (4 commits; drafts' padding-dependent rules rewritten; `ProcessIntake` optional caller; DI = C01's four registrations, now present via A's patch; one frozen-contract change requested to A: `ThirdPartyReportValuation.Adjustments`). C branch `2b6b5ed37` merged into the slice → `d0daa2340`; wave 12 (build/core/corpus/web/architecture with pack root) running; independent review dispatched against `11a306580`, consuming wave 12.
- Small C edit batch running on the C branch: pinned-MSG correction (exact CaseCreated + route/classification/extraction predicate evidence) and Triage note gate removal.
- A's retention auth rule received: RequestLink actor subject = `RequestUploadLinkEntity.Id`; C07 caller brief bound to it; dispatches when the small batch frees its slot. C06 resumed (active).

## Checkpoint 16:05Z

- Wave 12 (C05 at `d0daa2340`): build, Core 24/24, corpus 8/8 (pack present), web 7/7 + 5 genuine-corpus skips, architecture 100/100 — all PASS. C05 review in progress against `11a306580`.
- C06 READY_FOR_TESTS at `0f3bec931` (10 commits, 5 assumptions on `c06-notes`, one pre-existing test updated for the changed page shape); C head merged → `7c7f724dd`; wave 13 running. Its web tests resolve C06 adapters through DI that A has not registered — expected DI failures; correction round will compose the C-owned adapters test-side (as C08/StaffCorrespondence did) so the slice proves itself before A's patch. DI list posted to A.
- C07 retention caller dispatched (Opus) in `v1-intake-c07b` (`c07-retention-caller` off `2b6b5ed37`) with A's exact RequestLink rule. Small C batch (MSG correction + Triage note gate) still running on the C branch. Editors: 2.

## Checkpoint 16:20Z — C branch small batch

`d9c6e6ed2` pinned-MSG correction (exact CaseCreated; route/classification/extraction predicates asserted on `receipt.MailRouteDecision`/`MailClassificationDecision`/`ExtractionPolicyKey|Version`, not on `Evidence`, which does not carry them; `established-principal` evidence item asserted), `f81932aa0` Triage note gate removed (`IAddTriageNote` required). Operations `GetServiceHealth` removal (A comment 5559965571) in progress on the same branch. Dispositions to A: IntakeWebNegativeTests already fixed at C01 `e80862f37`; OrganizationAdministrationPersistenceTests duplicate QDOS → C06 correction round. Not yet pushed; wave 14 then push.

## Checkpoint — corrections in flight

- C branch at `15518699c` (unpushed): MSG correction, Triage note gate, Operations health removal (partial-data notice links `/Administration/Health`; the page itself lands with C08's admin surface / A's health page). Wave 14 running (architecture, Triage/Operations/MultiFormat/QDOS recovery lanes); push after.
- Wave 13 (C06 at `7c7f724dd`): build/core 54/architecture PASS; integration 6/30 — all 24 failures one root cause: `InspectionAddressChoicesQueries` (A-registered) gained a required `IOrganizationDirectoryQueries` dependency → host scope validation fails for every page. C06 correction round 1 dispatched: optional bridge + test-side composition of the C06 adapters + seeded-QDOS reuse in the replacement test.
- C05 review (`scratch/review-c05`): needs-changes, 1 major (`ProcessIntake` persists candidates but discards `Findings`), 8 minors. Correction round queued.
- C07 retention caller (Opus) running in `v1-intake-c07b`. C08 correction queued. Editors: 2 (C06 correction, C07 caller).

## C head `15518699c` pushed

Wave 14: build/architecture PASS; integration 62/70 — 5 corpus skips (local pinned samples absent), 3 A-owned failures (two obsolete OperationsWebTests health tests A is removing; QdosAllocationRecoveryTests seeded-QDOS precondition fixed by A at `8e6f3b21d`). Pushed; A notified with the MSG correction detail for the combined genuine rerun.

## Checkpoint — C06 correction round 1 at `556a26b1a`

Optional directory bridge on `InspectionAddressChoicesQueries` (ASSUMPTION 6), test-side composition helper `C06AdapterRegistrations.WithC06Adapters`, bridge-proof test, seeded-QDOS reuse in two `OrganizationAdministrationPersistenceTests` (ASSUMPTION 7). C head `15518699c` merged → `c94e3dddc`; wave 15 (build/core/integration/host/architecture) running; independent review dispatched against `556a26b1a`. C05 correction round 1 dispatched (Opus) at `975bf107b` for review majors/minors. C07 caller still running. C08 correction waits for a slot (its worktree holds the in-progress merge with the one `OperatorLabels.Admin.Health` conflict).

## Checkpoint — A comments 5560061438/5560095062/5560149495

- A04 request-link custody published on A (`7a6157d88`); C07 caller slice `c07-retention-caller` at `6bb5453ba` (impl `87eebffe1`; controller reverted the A-owned `DocumentCustodyDurabilityTests.cs` edit at `b5b5338a4`, hunk posted as PR 673 comment 5560181686). Wave 16 + review running.
- A's regenerated principal-corpus source snapshots applied on the C branch → `d2b50f46e` (package hash verified); wave 17 Core hash lane running; push after.
- `StaffAccounts.ReviewDue/Review` labels: still rendered by A01's Accounts page on the C branch; removal deferred to the step that carries A01's page here (posted to A).
- C06 round 2 running (EvaSubmission optional bridge, UpdateLocation 302 defect); C06 review running on `556a26b1a`; C05 correction running. C08 correction still queued on the two-editor limit.

## Checkpoint — C07 caller slice at `6c8b945bd`

Wave 16 build failed: the reverted A-owned `DocumentCustodyDurabilityTests.cs` still passed the removed `IDocumentContentStore` ctor argument. Controller applied a one-line compile-only accommodation (`6c8b945bd`), solution build 0/0; wave 18 (build/core/integration/a-owned/architecture) running; reviewer re-bound to `6c8b945bd`. The A-owned lane is expected to fail for A's reasons (seeded-QDOS duplicate in `SeedCaseAsync`, legacy content-write premise) — hunk posted to A (PR 673 comment 5560181686). C branch `d2b50f46e` pushed (A snapshot update, hash lane 7/7).

## C06 review at `556a26b1a`: needs-changes (2 blockers, 3 majors, 10 minors)

Blockers = the two wave-15 failures (EvaSubmission required dependency; two non-nullable Reason/OperationKey pairs invalidating ModelState). Majors: default-location history written with `before: null`; item 6 untested (QDOS→IBA unasserted); unordered `.Take(500)` before in-memory prefix filter. Forwarded to the running round-2 implementer to fold in; wave + targeted re-review follow. Wave 18 (C07 caller `6c8b945bd`): build/core 11/architecture PASS, integration 58/60 with the only failures A-owned durability seeds; C07b review pending.

## C07 caller review at `6c8b945bd`: needs-changes (4 majors, 4 minors)

R-1 replay can double-count link counters when no receipt is written; R-2 Pending hand-over writes a receipt so later submissions are refused as Replay and nothing sweeps Pending; R-3 a thrown hand-over leaves the arrival `pending` (must be `unknown`) so bytes may be re-offered; R-4 Pending renders upload success. Controller accommodation `6c8b945bd` judged correct and minimal; A-owned lane failures confirmed as the seeded-QDOS collision only. Correction round queued (next free editor slot, ahead of C08).

## Checkpoint — C05 correction round 1 at `7b632169b`

R-1…R-9 fixed (findings persisted as `finding.<code>` source rows; printed labels kept; catches named; dead members removed; one classification-table owner; negatives extended). "Finding" chip deferred to C04 (`Intake/Details.cshtml`) + C08 (`OperatorLabels`) — recorded. Wave 19 + targeted re-review running. C07 caller correction round 1 dispatched (Opus) for the four majors. Editors: C06 round 2, C07b correction. C08 correction still queued.

## Wave 19 (C05 at `7b632169b`): 2 failures in the new correction code

Build/core 27/architecture PASS; corpus 10/11 — `EveryRecordedFindingIsPersistedAsItsOwnSourceRow` fails at line 325 (a finding row has an empty `RawValue` or `SourceLabel`); web 9/15 — `ReprocessingTheSameRetainedBytesDoesNotWriteASecondSetOfCandidates` observed outcomes `[no_report_signature, recorded]`, expected `recorded_reading_stands` on the re-evaluation (the reevaluate path is not recognising the report signature it recorded the first time — behaviour, not assertion). C05 round 2 queued behind the two-editor limit (C06 round 2, C07 caller correction active); re-review will bind to the next head.

## Checkpoint — C06 round 2 at `8384e28bb` (merged `0be584782`)

Both blockers, three majors and 7/10 minors fixed (R-6/R-11/R-15 dispositioned on `c06-notes`); root cause of the 302 defect: non-nullable Reason properties across two independent forms invalidating ModelState. Wave 20 + targeted re-review running. C05 round 2 dispatched (Opus) for the two wave-19 defects. Editors: C05 r2, C07 caller correction. C08 correction next in queue.

## Wave 20 (C06 at `0be584782`): host DI fixed, two form POSTs still 200

Build/core 61/host 32/browser 2/architecture PASS; integration 34/36 — the EvaSubmission page's `UpdateEva` (shared A/B test, previously passing) and `UpdateLocation` POSTs both return `Page()` instead of 302 after round 2's ModelState change. Reviewer asked to name the branch from source; round 3 queued (editor slots: C05 r2, C07 caller correction) and will make both tests print the validation summary on a non-redirect.

## G14 merged — C head `fa564c0a7`

`2a20adbed` (approved-mailbox StaffSend enum, Generation, VerifiedEncodedMessageSizeLimit) merged `--no-ff` as the identical object; build 0/0; pushed. Propagated to c06 (idle). c05/c07b receive it after their active correction rounds; c08 with its correction round.

## C06 re-review at `8384e28bb`: needs-changes — 1 blocker, 4 minors

C06-R-16 root cause of both 200s: `EvaOperationKey`/`LocationOperationKey` are non-nullable `string` bind properties each posted by one form only; MVC validates the binding result (null), not the initializer, so every POST fails implicit-Required on the other form's key. Minors R-17 (SQL prefix predicate narrower than `NormalizeNamePrefix` for irregular whitespace), R-18 (client-bound `NewClaimSourceId` can overwrite a `Version = 0` claim source), R-19 (Storage-source coverage absent), R-20 (both tests assert only the status code). 12/15 prior findings closed; R-6/R-11/R-15 deferrals accepted. Round 3 queued for the next editor slot (ahead of C08). G14 propagated to c06 → `ab7108c0c`.

## Checkpoint — C07 caller correction round 1 at `6490623c3` (merged `3c0e1931c`)

Four majors + four minors fixed via a pre-custody `arrived` occurrence state, derived accepted totals under a link-row lock, receipt only on Confirmed, Pending reconciled like Unknown, thrown hand-over → `unknown`, `AcceptedPending` decision with its own wording (label constant handed to C08). New residual C07B-R-3a recorded. Wave 21 + targeted re-review running. C06 round 3 dispatched (Sonnet) for blocker R-16 (non-nullable operation keys) and minors R-17–R-20. Editors: C05 r2, C06 r3. C08 correction next.

## Checkpoint — C05 round 2 at `868e7a5ea` (merged `b506c3b8d`)

R-10 (finding filed against an issuer row with an empty label for scan-only sources), R-6 (re-evaluation only queued Worker work; test now drives the pass; outcome tag keyed by receipt id), R-11 (scan-only page rows/OCR findings recorded), R-12 (finding ordinal in the derived id) fixed. Wave 22 + re-review start when wave 21 releases LocalDB. C08 correction round 1 dispatched (Sonnet): finish the in-progress merge, nine wave-9 defects, G14 mailbox fields in Compose, labels batch (C07 AcceptedPending wording, C05 Finding chip). Editors: C06 r3, C08 r1.

## Wave 21 (C07 caller `3c0e1931c`): C lanes green, architecture red

Build 0/0, core 16/16, integration 58/59 (+1 skip), a-owned 1/5 (A's seeded-QDOS duplicate only). Architecture 99/100: `CoreHasNoInfrastructureOrHostDependencies` — the correction's `exception is HttpRequestException` in `RetainIncomingArtifact.cs` (`05d9a0e49`) makes Core reference `System.Net.Http`. C07 caller round 2 queued (drop the transport-type test from Core). Wave 22 (C05 `b506c3b8d`) + re-review running. Editors: C06 r3, C08 r1.

## Wave 22 (C05 `b506c3b8d`): 2 failures remain

Build/core 29/architecture PASS; corpus 12/13 — the new `AScanOnlyOriginalIsRecordedRatherThanDiscardedAtTheGate` finds a JohnRBell `identity.issuer` row (Disposition Missing) with no source label; web 9/15 — `ReprocessingTheSameRetainedBytes…` sees 1 outcome where 2 are expected (the driven re-evaluation pass still emits none). Re-review in progress will bind the causes; C05 round 3 queued with C07 caller round 2 behind the two active editors (C06 r3, C08 r1).

## C07 caller re-review at `6490623c3`: needs-changes — 1 major, 3 minors

All eight round-0 findings fixed; R-3a accepted as recorded residual. Open: R-12 `IsUncertainHandOver` names `HttpRequestException` (Core → System.Net.Http; the fix is to classify via `IntakeDependencyUnavailableException`/`IntakeExceptionPolicy.IsTransientFailure` and let A04's adapter translate transport faults), R-13 a bare `TaskCanceledException` leaves the occurrence `arrived` and re-offers bytes, R-14 the double's status port never checks authority. Round 2 queued for the next editor slot (C06 r3, C08 r1 active).

## Checkpoint — C06 round 3 at `dc24438e2`

R-16 (nullable operation keys, per-handler validation), R-17 (SQL predicate whitespace), R-18 (create id from the operation key), R-19 (Storage covered), R-20 (validation causes surfaced) closed by the implementer; wave 23 + targeted re-review running. C07 caller round 2 dispatched (Opus): R-12 Core transport-type reference, R-13 cancellation → unknown, R-14 status-read authority (possible A contract gap). Editors: C07b r2, C08 r1. C05 round 3 queued pending its re-review's diagnosis.

## C05 re-review at `868e7a5ea`: needs-changes — 2 majors, 2 minors

R-10/R-11/R-12 fixed and proved. Open: C05-R-16 `ThirdPartyReportProfiles.Verdict` builds the scan-only `identity.issuer` row with an empty source label and R-11 now lets it reach storage (caught by the implementer's own new test at `ThirdPartyReportCorpusTests.cs:453`); C05-R-6 the driven re-evaluation pass still tags only one third-party outcome for the receipt, so "re-evaluation re-reads and leaves the reading standing" is unproven (the standing branch itself is honest). Round 3 queued for the next editor slot (C07 caller r2 and C08 r1 active).

## Wave 23 (C06 `dc24438e2`): blocker closed, 2 new failures in round-3 tests

Build/core 61/host 41/browser 2/architecture PASS; the two settings-page POSTs now redirect. Integration 36/38: `InspectionAddressSuggestionTests.SearchMatchesAPriorLocationWhoseStoredWhitespaceIsIrregular` (line 108 — the prior location is returned with a collapsed label, the assertion's filter does not match it) and `SearchUnionsCaseClaimantPriorPrincipalLocationAndDirectory` (lines 69/81 — the newly seeded Storage case is absent from the union). Re-review in progress will bind; C06 round 4 queued with C05 round 3 behind the active editors (C07 caller r2, C08 r1).

## C06 re-review at `dc24438e2`: needs-changes — 2 blockers (test fixtures), 3 minors, no production defect

R-16/R-18/R-20 closed; R-17's SQL predicate judged correct and needed. New: C06-R-21 the whitespace regression test seeds through `ISaveCase`, which collapses whitespace on write, so the asserted value cannot exist (seed the irregular value at row level instead); C06-R-22 `SaveStorageLocationAsync` posts a partial `CaseEditableData` and `EfCaseDataStore.SetConfirmed` deletes a confirmed field whose incoming value is null, wiping the claimant address (seed with the complete payload). C06-R-23 `AdministrationPageModel.cs` edit (out of the C06 map, behaviour-neutral, prescribed) must be disclosed as a deviation. Round 4 queued behind C07 caller r2 / C08 r1.

## Checkpoint — C08 round 1 at `8c5351296`

Merge conflict resolved (`Admin.Health = "Service health"`), G14 taken; admin rail wraps at ≤980px; palette focus-return threads the real opener; Compose uses `Generation` + `StaffSend`/`SentEvidence`; labels batch (C07 AcceptedPending, C05 Finding). Preview-404 and Compose-redirect causes not confirmed statically — diagnostics added so wave 24 names them. Wave 24 running. C06 round 4 dispatched (fixture fixes R-21/R-22, deviation R-23). Editors: C07 caller r2, C06 r4. C05 round 3 next.

## Checkpoint — C07 caller round
