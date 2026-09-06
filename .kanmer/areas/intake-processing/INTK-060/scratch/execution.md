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
