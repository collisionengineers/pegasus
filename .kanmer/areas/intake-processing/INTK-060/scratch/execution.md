## Stream C startup 2026-09-06T05:56Z (claude-fable-c, CEMATTYPC)

- Clean worktree `../pegasus-worktrees/v1-intake` on `task/pegasus-v1-intake` created at D `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2` (origin/dev verified equal to D; origin/main = `32f8679d`). Shared checkout preserved: its dirty Kanmer-skill/AGENTS/config paths were set aside and restored around a fast-forward of local `main` to origin/main; no src/tests/docs paths were touched; `pegasus_pack/` untracked input preserved.
- Tooling: dotnet SDK 10.0.302 (global.json), pwsh 7.6.5, gh 2.88 authenticated, node 24. `dotnet restore --locked-mode` exit 0 and `dotnet build Release --no-restore` exit 0 at D in the C worktree.
- GitHub census: no `task/pegasus-v1-platform` / `task/pegasus-v1-casework` branch published yet; open PRs 639, 646, 670, 671 unchanged. PLAT-075 scratch reports F01 contracts compiling in A's checkout, F SHA not yet published. Stream C therefore stays in Wave 0 (read-only evidence/PR inventory); no domain commit before `git merge --ff-only <F>`.
- Wave 0 dispatched (Opus 5, read-only): corpus hash manifest 81/29/14 under ignored `artifacts/evaluation/v1-intake/`; PR 639 line-by-line preservation table (with PR-069 correction); PR 646 hunk disposition; PR 671 hunk/behavior disposition against `743311a0`. Results are merged into this ticket's scratch documents when complete.
- Local corpus: `corpus/` is absent on this workstation; the immutable evidence is the reference pack `pegasus_pack/` beside the plan (MANIFEST.sha256, 729 rows). Corpus-category tests will need the documented `PEGASUS_CORPUS_ROOT`/pack path convention; recorded as an open point for the manifest test design.

## 06:27Z checkpoint

- `origin/task/pegasus-v1-platform` = `5713d9b58` (two commits over D: `d819034ca` foundation, `5713d9b58` SQL permission correction). `origin/task/pegasus-v1-casework` and `origin/task/pegasus-v1-intake` were pushed by A at D. A's foundation.md still says adoption is pending final validation; no F SHA announced. C worktree remains at D with no commits.
- Reviewed the F candidate's S02 contracts; four findings and two questions posted on PLAT-075 `scratch/c-stream-notes` (SourceFieldCandidate needs nullable document/asset identity; SaveClaimSourceRequest lacks Reason; OrganizationDirectoryEntryEntity narrower than its record; Limit parameter on the location query). None blocks C's read-only preparation.
- Anchors research (read-only, at D) complete for intake core (C01–C03), pre-case/upload (C04/C07, pending) and directory/shell (C06/C08); persisted under the session scratchpad `wave0/` and summarized below for the implementers.
- Notable anchor facts: `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` (A-owned) pins exactly one `IInstructionExtractionPolicy` implementation; `IntakeContentFragment(Source, SourceLabel, Text)` has no locator today (page number only inside the label string); DOCX reader never visits tables; EML quoted history is not a separate fragment; `ProcessIntake` takes one extraction policy and throws on a principal mismatch; the Ctrl+K command palette and the four shell dialogs already exist in `_ShellDialogs.cshtml`/`site.js` (notifications dialog is a hard-coded empty stub); `Mail/Message` has no reply/forward/compose controls; `GetOperationsSnapshot` issues ~10 sequential queries; `provider-domains.v1.json` carries 11 provider codes, not 15; no Claim Source/OrganizationDirectory code exists at D.

## 06:45Z checkpoint — Wave 0 read-only research complete

Pre-case/upload anchors (C04/C07) at D, with what the F candidate already supplies:
- Triage has no reference at D (`TriageRecord` TriageContracts.cs:34–41, `TriageEntity` PegasusDbContext.cs:1261–1281); F adds `Sequence`/`Reference`/`PrincipalId` + `TriageSequenceEntity`. Name collision to resolve in C07: `TriageSummary.Reference` (TriageContracts.cs:271–280) today projects the originating `InstructionDraft.ClaimNumber` (EfTriageStore.cs:477–503, asserted by `TriageQueuesWebTests.TriageRowRendersReferenceRegistrationProviderAndAssignee:207`); the T reference needs its own member and the claim number its own name.
- `ICaseArtifactCustody` absent at D (only `ICaseCustody`, CustodyContracts.cs:66–239); F adds `ICaseArtifactCustody.RetainAsync` and `IntakeAssetEntity.CustodyStatus/BoxFileId/BoxVersionId`.
- No `PrincipalId` on Image Intake at D; F adds it (record + entity + FK/index).
- Limits trap: `IntakeEnvelopeLimits.MaximumContentLength = 10 MiB` and `MaximumBatchContentLength` is derived as `20 × per-file + 64 KiB` (IntakeContracts.cs:13, :68–69) — raising per-file to 100 MiB would derive 2 GiB; C07 pins the budget to the literal `(200*1024*1024)+64*1024`. `FormOptions.MultipartBodyLengthLimit` (Program.cs:634–639) is the only body-size setting; no Kestrel `MaxRequestBodySize` override exists (F owns host limits — handoff).
- No public submission session at D: `RequestUploadLink` (RequestUploadPolicy.cs:237–248) is Case-scoped, expiry fixed at creation (`CalculateExpiry` :353–361), caller-supplied operation key, no Finalized status, no `LimitsVersionMismatch` (mismatch throws :372–376 / 404s EfDocumentRequestStore.cs:410–418; Request.cshtml.cs:127–128 `default: return NotFound()`); store writes straight to `CaseDocumentEntity`. F adds `PublicUploadSessionEntity`/`PublicUploadOccurrenceEntity`.
- No eligible-engineer list query exists (`ICaseEngineerEligibility` is a single-id probe, Identity/CaseEngineerEligibility.cs:8–13; `ListSignOffEngineersAsync` StaffAccountAdministration.cs:159–160 is sign-off-specific and requires a stored signature). C07 assignment needs an A-owned account query or reuse of `ICaseEngineerEligibility` per candidate — handoff question for A (Identity is A-owned).
- Plan path corrections: `QdosBoundaryContractTests.cs` is `tests/Pegasus.Core.Tests/Qdos/`; `DefinitiveIntakeCaseTypeTests.cs` is `tests/Pegasus.Core.Tests/Intake/`; `UploadOutcomeKind` has nine members, not six.
- C04 "collapse byte-identical/format-equivalent renderings" has no hash-bearing input in `IntakeSourceReadResult.Content`; `EvaluateStandaloneAuditReport` (QdosMailClassificationPolicy.cs:203–238) grouping by `AssetSourceLabel` is the nearest pattern; asset hashes are on `AssetCandidates`.
- `EfTriageStore.CreateAsync:72–80` re-verifies the exact accepted-match evidence detail string; bumping `QdosMailClassificationPolicy.Version` changes that string and can break Triage creation for receipts already in flight — C04 must handle the version transition.

Labelling pass dispatched (Sonnet, read-only): independent expected-candidate sets for all 15 profiles (81 samples) under ignored `artifacts/evaluation/v1-intake/expected/`, to be used as test expectations by C03 rather than labels derived from the implementation.

## 06:36Z — F adopted

- A validated F = `dc3cfd908348b38d69b5d5357c6d5899bfe5f812` (PLAT-075 `scratch/shared-corrections.md`, 06:28Z). C worktree `../pegasus-worktrees/v1-intake` fast-forwarded with `git merge --ff-only dc3cfd908…`; `git merge-base --is-ancestor` confirms F ∈ HEAD; HEAD == F, no C commits yet. Same commit objects as A's branch (d819034ca, 5713d9b58, dc3cfd908).
- A accepted the C contract review into common G1 (`task/pegasus-v1-foundation-g1`, to be merged `--no-ff` as the same object): nullable document/version OR IntakeAsset identity + int occurrence on `SourceFieldCandidate`; source queries support pre-case; `SaveClaimSourceRequest.Reason`; directory entity matches the record incl. prefix indexes; inspection suggestion limit fixed at 20 internally, caller limit removed. `EvaAutomaticSubmission` column removal deferred to a later G; C keeps it out of new UI/contracts. C continues domain work against F without altering shared signatures.
- Slice topology: implementers work in slice worktrees branched at F (`../pegasus-worktrees/v1-intake-c01` = `c01-retained-analysis`; `../pegasus-worktrees/v1-intake-c07` = `c07-precase`), disjoint file sets, one author per file; each reviewed slice is rebased onto `task/pegasus-v1-intake` and fast-forwarded in so the C branch remains linear over F. Wave 1: C01 (Opus) ∥ C07 first slice (Opus, excluding the `IntakeEnvelopeLimits` edit in `IntakeContracts.cs`, which C01 owns for now); then C02, then C03 batches, C04, C05, C06/C08 after G1.

## Transitions

- 2026-09-06T06:45:31.506Z lease-phase implementing → running-command (lease 63073298-18b0-430d-a135-bd3a610d0f30 rev 9; expires 2026-09-06T08:45:31.487Z)

## 06:50Z — labelling pass complete; implementers relaunched under the guard roles

- Independent expected-candidate sets for all 15 profiles (81 samples) are under ignored `artifacts/evaluation/v1-intake/expected/<CODE>.json|.md` with batch summaries in the session scratchpad. Contradictions with the method files to carry into C03: FW derived texts hold only the 4-line transport header (no body) — the FW profile must be developed and tested against the MSG originals through the repo reader, not the pack's derived text; SBL has one combined "Vehicle Make:" field (no separate model label); BLACK's circumstances anchor carries no accident facts and its address anchor conflates claimant address with inspection location; BC has no "Our Ref" (unlabelled RTA token, ambiguous in 5/5) and its "Address:" is the claimant's home; ALS "Mileage" has no evidence in any sample and identical labels repeat for the third-party column; YML method bounds were too loose; MP PDF/Weird samples have no embedded text on page 1 (OCR-only) and "Inspection:" lines are requested dates paired with a "Report:" deadline; QDOS method's image-based-assessment override claim has no literal sample evidence and samples 03–05 show a row shift in the client/vehicle table; PCH "Performance"/"Lawshield" fingerprints co-occur in 4/5; DFD method.md hashes carry a stray trailing hex digit; AX "Report Due on" confirmed as deadline in 5/5.
- The workstation guard hooks (`~/.claude/hooks/pegasus-*.ps1`) restrict worktree edits to `pegasus-implementer` agents and `dotnet test`/scripts to `pegasus-test-runner`; the first C01/C07 launches (general-purpose) were stopped and relaunched as `pegasus-implementer` with controller overrides (no packet, slice worktrees, build-only, READY_FOR_TESTS). Tests run through the controller wave loop with `pegasus-test-runner`; reviews with `pegasus-reviewer` before each slice is fast-forwarded into `task/pegasus-v1-intake`.

## 10:45Z — A coordination received; G1–G7 merged; draft PR open

- User/A instruction: merge `task/pegasus-v1-foundation-g1` G1–G7 (latest `fec546170`) as identical commits, not the A branch; A05 needs C typed `ActionActor` mutations, stable protected keyset cursors for intake/Triage/Unidentified, exact source metadata and streaming authorization; implement the global `T-00001` allocator (current F allocator persists sequence zero and fails `CK_Triage_Sequence`); admin nav needs Action logs, AI jobs, Reports, Health and B's Valuation presets; A07 needs the unused Case/Mail activity projections removed from `OperationsSnapshot`/Index; publish the draft PR and concrete DI requests; A board sync paused; B PR #672 at `ca6a97c72`; no merge to dev.
- Done: `git merge --no-ff fec546170` → `efbf3c8f6` on `task/pegasus-v1-intake`, pushed; draft PR #673 opened to dev; slice worktrees c01/c07 (WIP committed) and c08/c05 merged the C branch; DI requests and questions posted on PLAT-075 `scratch/c-stream-notes`.
- Both Wave 1 implementers were killed by the session rate limit at ~06:50Z (reset 10:30Z); relaunched now with the A05 additions folded into C01 (Unidentified/intake keyset continuations, typed actor, exact source metadata + authorized streaming) and C07 (T allocator first, typed Triage actor, Triage keyset continuation); C08 slice 1 (shell/admin nav/notifications/OperationsSnapshot trim/Inbox read-only proof/compose handlers) launched in parallel on disjoint files.

## 10:55Z — narrow ownership handoff from A acknowledged

- C branch `efbf3c8f6` (F+G1–G7) restore/build exit 0.
- A05 authors only the grant-id consent hunk in `Pages/Connect/Authorize.cshtml.cs`; A02/A01 test-support hunks in `PollApprovedInboxTests`, `RetainedMailTests`, `LocalIntakeAccessTests`, `QdosAllocationRecoveryTests`, `AdministrationSearchAccountWebTests` are preserved (C implementers told not to edit them); Box identity assignment in `EfDocumentRequestStore` travels with C07's custody caller; A07 removal inventory posted on PLAT-075; G8 `LondonCalendar` adoption + removal of the UTC fallback in `OperatorLabels` queued for the C08 branch once G8 is published.
- Running: C01 (Opus, `v1-intake-c01`), C07 (Opus, `v1-intake-c07`), C08 slice 1 (Sonnet, `v1-intake-c08`) as `pegasus-implementer` roles; next: test-runner waves, independent reviews, integration into `task/pegasus-v1-intake`, then C02 → C03 batches → C04 → C05 → C06 (after G contracts) → C08 slice 2 → C09.

## 11:05Z — G8 merged; C08 paused

- User/A: G8 `b260098a7` published (LondonCalendar `LocalAt`/`TimeAt`, UTC fallback removed, chase scheduling on the shared helper; 18 calendar/chase tests pass on A); at most two implementation editors at a time. Merged G8 `--no-ff` after G7 → C head `4e8be0690`, pushed; merged into idle slices c08/c05; c01/c07 receive it when their slices integrate. C08 implementer stopped before it edited anything (still in the read phase); it resumes read-write when C01 or C07 reports, with the `OperatorLabels` → `LondonCalendar.LocalAt` change added to its brief.

## 12:00Z — C07 and C01 READY_FOR_TESTS; user/A requests

- C07 `447e1c271` (`c07-precase`): T allocator, Triage keyset over G9, PR 671 re-applied (C1–C21, T1/T2/T4–T7), public session policy + typed refusal, `RetainIncomingArtifact` + `EfPublicUploadRetentionStore`, notes/assignment, Provider API assertion. Not delivered: typed-actor Triage mutations (needs A `TriageHistoryEntity.ActorKind` + six out-of-slice files → dedicated slice after C01 merge); read-count test asserts equal reads over 3 vs 6 rows instead of a pinned number. Test wave running (`wave1/c07-tests/`).
- C01 `1be524f6b` (`c01-retained-analysis`): PR 639/PR-069 port, PR 646 residual + API-level Ambiguous, `AnalyzeRetainedInstruction` + selector + store + Received page panel, A05 metadata/keyset over G9, `PrincipalSourceManifestTests`. Known: corpus drift test will fail (A rebuild); page dependencies optional until A's DI patch; `DownloadIntakeSource.cs` deferred to C02. Test wave queued after C07's.
- User/A: fix `IntakeWebNegativeTests.AssertNoBusinessPersistenceAsync` baseline (F seeds 15 principals) — dispatched on the C01 slice; A07 minimal C hunks for an atomic common G — dispatched on helper branch `c-a07-dashboard-hunks` (worktree `v1-intake-a07`), C08 told to drop its trim item; A adding `DrainStagedToTerminalAsync` + interceptor hook.
- Editors active: C08 slice 1 (Sonnet) and the small-fixes implementer (Sonnet) — two, per the rule.

## 12:55Z — typed-actor Triage correction queued

- A reviewed `TriageHistory.ActorKind`: a column-only correction cannot fill the kind truthfully because Triage requests carry plain-string actors. Agreed: C authors the minimal typed-actor contract/writer/caller hunks (no Staff default, no inferred backfill; replay hashes include kind+subject; history exposes kind; email evidence keeps SystemWorker) as one commit on `task/pegasus-v1-c-typed-actor-hunks` rooted at the G9 boundary `2ec79df3f` (worktree `v1-intake-typed-actor` created); A adds schema + `TriageMcpTools.cs` and publishes the atomic G; C merges and resolves against C07's Triage files. A also publishes the engineer-choice contract + `EfStaffAccountQueries` in the next G so the C07 picker resolves standalone; A pins the historical migration fixture to its exact pre-foundation target with assertions preserved.
- Dispatch of the typed-actor implementer waits for a free editor slot (C07 correction, C08, small-fixes active).

## 11:50Z — wave 2 and typed-actor helper

- C07 correction round 1 done → `7850a7bd7` (exception-type assertion, allocator concurrency proof re-shaped onto `ITriageStore.CreateAsync`, keyset query re-ordered before projection). C07 attempt-1 whole-solution lane: Core 1305/1307 (ValuationTests.SourceVocabularyIsClosed — B/A valuation contract at G9, not C; AddTriageNote fixed), Architecture 100/100, Integration 1321/1345 with 21 failures: 6 `IntakeWebNegativeTests` (fixed on c01 Task B), 4 `DocumentCustodyDurabilityTests` (duplicate QDOS seed, fixed on c01 Task C), `RetainedMailPersistenceTests` (A-owned), `QdosAllocationRecoveryTests` (A-owned), `CaseWorkflowMigrationTests` (A pinning the fixture), 2 `CaseDataCompletenessPersistenceTests`, `IntakeAllocationConsumerTests`, `ConcurrencyTokenPersistenceTests` (owner to confirm from the baseline), 2 `OrganizationAdministrationPersistenceTests` (C-owned, C06 area — likely the 15-principal seed; queued), and the two C07 tests fixed in round 1.
- Small fixes: A07 hunk branch `task/pegasus-v1-c-a07-hunks` = `c40b9d6e8` + `2855d4a97` pushed; c01 gained `e80862f37` (negative-test baseline from the seeded estate) and `fea61abb8` (QDOS seed dedupe in `LayoutIntegrityTests`, `DocumentCustodyDurabilityTests`, `MailWorkspaceWebTests`).
- Wave 2 runner dispatched (sequential): C07 attempt 2 focused lanes; A07 branch dashboard/corpus lanes; C01 focused lanes (+ negative/custody/mail suites); baseline whole-solution on the unmodified C head `0840c33a5` to separate foundation-inherited failures from slice regressions.
- Typed-actor helper implementer (Opus) dispatched on `c-typed-actor-hunks` at the G9 boundary. Editors: C08 + typed-actor = 2.

## 12:05Z — C08 slice 1 READY_FOR_TESTS (`32f9f3ee1`)

Items 1,2,3,5,7,8 delivered; item 4 dropped (A07 hunk branch instead); item 6 partial: Compose/New done, Reply/ReplyAll/Forward blocked on A-owned `RetainedMailDetail` lacking the immutable Graph identity and on a mailbox generation/send-enabled query (both requested from A for the next G). Correction requested: `RailCountsPageFilter` must resolve `IGetAttentionRows` optionally until A's DI patch, otherwise every authenticated page test fails on the standalone branch. C08 wave queued after wave 2. Snapshot/catalogue changes handed to A (`/Inbox/Compose` entry).

## 12:25Z — G10 merged; typed-actor hunks published; C05 started

- G10 `9c5ddf454` merged `--no-ff` after G9 → C head `e5c9b1f43`, pushed; wave 2 baseline lane repointed at it. Typed-actor helper commit `fda3a35bb` (12 C-owned files at the G9 boundary) published as `task/pegasus-v1-c-typed-actor-hunks`; the four A-owned sites and the entity property are on PLAT-075 `scratch/c-stream-notes`.
- C08 correction (optional `IGetAttentionRows` resolution) in progress; C05 Core slice (profiles/extraction/validation + Core and corpus tests) dispatched on `v1-intake-c05` (Opus). Editors: C08 + C05 = 2. Wave 2 runner on group C (C01) then the baseline.

## 13:00Z — wave 2 results

- C07 attempt 2 (`7850a7bd7`): build PASS, Core 207/207, Architecture 100/100, integration 65/66 — concurrency proof deadlocks reproducibly on the allocator (counter taken last) → correction round 2 (counter first, replay probe before the counter) dispatched.
- A07 hunk branch (`2855d4a97`): build PASS, integration 38/38 (+5 corpus skips), Core 7/9 — the two day/week boundary tests lost their source; handed to A for G11.
- C01 (`abfc219aa`): build PASS, Core 108/108, Architecture 100/100, integration 168/173: 2 × `UnidentifiedReconciliationTests` fail in A-owned `EfCaseWorkflowStore.Map` (`'not_ready'` enum parse — baseline pending); `ProviderApiSubmissionTests.ADeclaredTriageOpensATriageAndAllocatesNoCase` (Triage `Sequence = 0` on this slice; clears with C07); `AnAmbiguousExistingCaseMatchIsRejectedOnTheSamePath` (expected Failed, got Complete — C01 defect to investigate); `RetainedInstructionAnalysisTests.ARetainedQdosLetterIsAnalysedFromTheDocumentAndAllocatesNothing` (assertion mismatch on the receipt — C01 to investigate). C01 correction round queued behind C07 round 2 (two-editor rule; C05 running, C06 paused before any edit).
- Baseline whole-solution lane on `e5c9b1f43` running.

## 13:10Z — G11 merged (`b06a71b96`)

G11 retires the dashboard activity projections (carrying C's A07 hunks and the corpus-locator correction) and adds `UnidentifiedCount` from the open-Unidentified query. C08 (`c64d9cf83`) must merge the C branch and reconcile `OperationsSnapshot.cs`/`Index.cshtml(.cs)` before its wave; the A07 helper branch is superseded by G11 and stays as evidence. Editors: C07 round 2 + C05.

## 13:40Z checkpoint

- C07 round 2 `7000842ed` (replay probe → counter first under UPDLOCK/HOLDLOCK → writes): wave 3 (build/core/integration ×2/architecture) queued behind the baseline lane; independent review (pegasus-reviewer) running in parallel.
- C01 round 1 `ea4848acd` (G10/G11 merged; ambiguous Provider API fixture seeds a genuine duplicate index row; whole-receipt assertion narrowed; `not_ready` was C's fixture spelling into `CaseWorkflows.State`): wave appended after C07's; independent review running.
- C06 relaunched (merges the C branch first); C05 running. Editors: C05 + C06.
- G12 (typed actor, atomic) pending from A; C07 resolves the Triage conflicts in-stream after the merge.

## 13:50Z — session rate limit hit again (resets 15:30Z)

The C01 review agent was killed by the API session limit; other running agents (C07 review, wave 3 runner, C05, C06) may follow. On reset: check each slice worktree's commits and each agent's last state, relaunch the reviews and the runner wave from the recorded heads (C07 `7000842ed`, C01 `ea4848acd`), and resume C05/C06 from their uncommitted work.

## 15:35Z — resumed after the rate limit; G12 merged

- All agents died at ~13:50Z. Slice heads: c01 `ea4848acd`, c07 `7000842ed`, c08 `c64d9cf83`, c05 WIP `815385cda` (three draft Core files committed as wip), c06 `f2b99b5ce` (no edits). Wave 3 had only started its build; the C01/C07 reviews produced no attestation.
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

## Checkpoint — C07 caller round 2 at `f55a5adac`

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

Build 0/0, architecture 100; core 272/273 — `QdosInstructionExtractionPolicyTests.ThePartyBlocksAreReadAsTheirOwnRolesAndCutFreeOfTheirNeighbours` (line 1097: repairer block now cut to "Gordon Marshall Coachworks", expected the address to follow — C03 QDOS extension regressed the party-block cut or the test's expectation must move with the row's "scope … location" rule; reviewer to bind); corpus 2 + 2 known skips; web 15/21 — `IntakeAllocationConsumerTests.QualifyingTriageRemainsOne…` (Expected 0, Actual 1) is the A-owned seeded-QDOS precondition: A's fixture hunk is on the C branch (`27004c0ea`) but c03 sits on `ca3ec9abb` — merge the C head into c03 before the next wave. `TrackedPegasusSourceHashesHaveNotDrifted` did not fail in these lanes (check filter coverage). C03 round 1 = the party-block cut + merge C head.

- 2026-09-06T19:28:31.145Z claim-transfer claude-fable-c → codex-stream-c-replacement (live; operator: user explicitly appointed this fresh Codex session as the replacement Stream C controller for usage-exhausted claude-fable-c; preserve the recorded task/pegasus-v1-intake branch, existing v1-intake worktree, helper worktrees, and all dirty work; lease 63073298-18b0-430d-a135-bd3a610d0f30 → 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 33; branch task/pegasus-v1-intake; worktree ../pegasus-worktrees/v1-intake; expires 2026-09-06T19:58:31.136Z; evidence: workspace clean (matches-claim), pr absent, commits 0, proof absent)

## 2026-09-06 replacement-controller takeover

User-authorized transfer from claude-fable-c completed without force. Preserved branch task/pegasus-v1-intake and worktree ../pegasus-worktrees/v1-intake; exact clean owner head 27004c0ea974057aa6363d4a61c6e2907a29c897 matches origin and open draft PR 673 against dev. New lease 40f03659-6845-4f20-b1cc-cfd09686ec00 revision 34, controller codex-stream-c-replacement, extended through 21:30Z. Native dispatch is disabled with zero dispatches. The remaining local claude.exe process and its Kanmer MCP child were stopped after transfer so the exhausted controller cannot auto-resume.

Preserved helper state: C07b clean 2c427c643667596e397b6e51672bc32595075646; C02 6482cca59a628019ffe781a9056cba7d85a04bc5 plus dirty src/Pegasus.Core/Intake/IntakeOcr.cs; C03 clean 0f1355108e2cb25c022a5e54454a97857b484794; C08 ef9d71655f76c69f88f04c0f8d52e133582a7d5c plus six dirty C-owned files. No reset, stash, rebase, clean, duplicate worktree or restart.

PR 673 takeover/status posted as comment 5561608677. C07b has no round-4 attestation: retained review file ends at round 3 needs-changes against 0a0e88975, while helper has advanced. Do not integrate until a fresh independent exact-head review exists. Next bounded executable lane is to inspect/complete the preserved C02 correction without replacing its work.

- 2026-09-06T19:51:21.907Z lease-phase running-command → implementing (lease 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 36; expires 2026-09-06T20:21:21.900Z)

Replacement controller integrated independently reviewed C07b custody/public-upload caller slice: helper 348cb07d9d7bc3e14a01f75f96e34882e2c10f71 (review PASS; focused Integration 36/36 PASS) merged with --no-ff into Stream C owner head 9e1565a30dd4492e35a00c8900b01a03668c14ac, build 0/0, pushed to PR #673. Architecture remains 98/100 due A-owned missing IReadLogicalDocumentVersion Worker composition; handed to PR #674 comment 5561776928. No merge/deploy/live writes.

- 2026-09-06T20:48:15.018Z lease-phase implementing → running-command (lease 40f03659-6845-4f20-b1cc-cfd09686ec00 rev 42; expires 2026-09-06T21:33:15.012Z)

## Replacement-controller progress 2026-09-06 20:50Z

- C07/C-B01 reviewed helper published at bbda5b38acc52d4649b87853313408e998657b43. Boundary semantics match A; Recipient required <=500, Reason optional <=1000. A durability blob b1f463047 retained. Standalone/combined run awaits A-owned EfCaseArtifactCustody and A-owned durability conflict resolution requested on PR 674.
- C08 reviewed/tested helper published through 1c50286d3a71e872f95e1788f505945e49855a1e: palette idempotence 2 browser PASS, notification cap 2 Core PASS, mail round-trip 1 PASS after correcting the test anchor extraction, capability truth table 3 PASS. Reply modes still await A retained-message identity projection.
- C03 AX/FW reviewed/tested at d0937813e9621e4d62a012d9304cecee74e8cd95: Core 11 PASS including AX02; FW genuine five-source matrix 1 PASS/no skip. QCL is next bounded implementation.
- C06 zero-state delta remains reviewed and Release-build clean at refreshed head 49fabcb9da9667fdd9f4db8e8c425e7430dbc7fa. Required snapshot capture failed before generation (122/129) solely because the same A custody adapter is absent; no generated changes.
- PRs 672/673/674 remain open/unmerged. No deployment, mailbox/provider write, or mail send occurred.
