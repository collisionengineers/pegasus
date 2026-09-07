---
verdict: needs-changes
independent: true
head: 9b4dd1ef2295c17f5b2d5ddc633f5ca9044ea40a
reviewed_at: 2026-09-06T13:52Z
slice: C01 (INTK-060, Stream C)
branch: c01-retained-analysis
base_for_c_owned_diff: ab9f3fcd821b604a162e9448d5dd44e0ad9fcb27
a_owned_files_touched: none
attestation_file: C:/Users/PGUSER/AppData/Local/Temp/claude/C--Users-PGUSER-documents-github-pegasus/e752479c-0f90-4a5e-bc40-b525ea3bf932/scratchpad/wave1/c01-review.md
tests_at_head: build PASS; Core 108/108; Architecture 100/100; integration 95/96 (one pre-existing non-C01 failure)
findings:
  - id: C01-R-1
    severity: major
    file: src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs:442-467
    statement: >
      ListQueueByCursorAsync applies the media-kind filter AFTER a bounded fetch
      window (limit*4+1 rows) and derives `next` only from the last MATCHING row,
      so `next` is null whenever fewer than `limit` rows in the window match and
      always null when none match. A sparse filter ends the continuation early and
      silently drops the rest of the queue. The comment at :442-445 claims the
      opposite ("the continuation is exact either way") and its premise ("cannot
      be filtered in SQL") is untrue - UnidentifiedMediaKindPolicy reads only
      IntakeReceipts.SourceChannel and MediaType. No test covers the filtered path
      (the keyset test pages with mediaKind null only) and MediaKind is a
      published A05 connector parameter.
    disposition: >
      REQUIRED. Advance the cursor over scanned-but-filtered rows: when the fetch
      window was exhausted and fewer than `limit` matched, mint `next` from the
      last SCANNED row; keep minting from page[^1] when page.Length == limit.
      Acceptable alternative: refuse a media-kind filter on the cursor path until
      the kind is a filterable persisted column. Either way correct the two false
      sentences and add a test whose first window holds no matching row.
  - id: C01-R-2
    severity: minor
    file: src/Pegasus.Core/Intake/AnalyzeRetainedInstruction.cs:527-543
    statement: >
      Three copies of one rule ("the receipt's single Kind=Source/Disposition=Source
      asset, else none"): SelectAsset here, IntakeFileIdentity.SourceAsset
      (IntakeQueryUseCases.cs:116-125, which claims to be the one owner), and
      Details.cshtml.cs:556-559. All three written in this slice.
    disposition: Delegate SelectAsset's null branch and the page check to IntakeFileIdentity.SourceAsset.
  - id: C01-R-3
    severity: minor
    file: src/Pegasus.Core/Intake/IntakeContracts.cs:728-760
    statement: >
      The new ListByCursorAsync default member was inserted between ListAsync's doc
      comment and ListAsync, so the 25-row-cap remarks now document the wrong member,
      that member carries two <summary> tags, and ListAsync (:756) is undocumented.
    disposition: Move the member below ListAsync, or move the doc back onto it.
  - id: C01-R-4
    severity: minor
    file: src/Pegasus.Infrastructure/Persistence/EfRetainedInstructionAnalysisStore.cs:28,66
    statement: >
      Both probes use SingleOrDefaultAsync on OperationKey, but the unique index is
      (IntakeReceiptId, IntakeAssetId, OperationKey) - the key alone is not unique.
      Concurrent cross-receipt reuse of one key leaves two rows and every later read
      throws InvalidOperationException instead of the documented conflict exception.
      Remote in practice (GUID keys) but the probe assumes an unenforced invariant.
    disposition: Scope RecordAsync's probe to (receipt, asset, key) and order-and-First in the finder, or document the caller obligation.
  - id: C01-R-5
    severity: minor
    file: src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs:48-52
    statement: >
      The signature is a faithful transcription of collision-profile-qdos (including
      the corpus's U+2019), but that fingerprint records operatorAccepted=false and
      runtimeActive=false while C01 makes the document profile live at runtime, and
      PrincipalIdentificationCorpusTests asserts !(active && !accepted) corpus-wide.
      reference/** is A-owned so C01 cannot reconcile it.
    disposition: Handoff to A/root alongside the qdos-extraction-policy-v7 rebuild - update the criterionState or gate the activation. No C01 code change.
  - id: C01-R-6
    severity: minor
    file: pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md:611
    statement: >
      The plan's "each of all 15 genuine profile samples reaches extraction" bullet
      and S13's "all fifteen profiles are reachable" cannot be met by C01, which
      adds no second IInstructionExtractionPolicy; the report records the design
      property but not the deferral, so the bullet can be lost at integration.
    disposition: Record the deferral to C03 explicitly in the report/handoff. No code change.
  - id: C01-R-7
    severity: minor
    file: scratchpad/wave1/c01-report.md
    statement: No simplification pass is recorded (no reuse/simplification/efficiency/altitude section, no dispositions).
    disposition: Satisfied by this review's lens pass (C01-R-2 reuse, C01-R-1 efficiency, C01-R-10 altitude); record those dispositions.
  - id: C01-R-8
    severity: nit
    file: src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs:1
    statement: Six previously BOM-free files gained a UTF-8 BOM (patch-script churn). None is a tracked corpus snapshot, so nothing fails.
    disposition: Optional cleanup.
  - id: C01-R-9
    severity: nit
    file: scratchpad/wave1/c01-report.md (Correction round 1, item 2)
    statement: >
      The stated cause is wrong - Assert.Equal(receipt, after) could never pass because
      IntakeReceipt's record equality compares its IReadOnlyList members by reference,
      not because it pinned the mail route's reason (which the replacement still compares).
      The replacement assertions are correct and not weaker; only the explanation is.
    disposition: Correct the explanation.
  - id: C01-R-10
    severity: nit
    file: src/Pegasus.Web/Pages/Intake/Details.cshtml:606-617
    statement: The analysis panel renders on every receipt, so an allocated CaseCreated receipt shows "This retained instruction has not been analysed."
    disposition: Optional (altitude).
---

# C01 independent review - summary

Independent: this reviewer wrote nothing on `c01-retained-analysis`; the implementer
is a separate agent. Read-only throughout; head unchanged at `9b4dd1ef2` and the
worktree clean.

**Ownership.** 32 C-owned files (17 src, 15 tests). No A-owned path in the dispatch
list appears in `git diff --name-only ab9f3fcd8...9b4dd1ef2`; the two A-owned test
files briefly touched were reverted by their own commits (`abfc219aa`, `315268059`)
and the corpus JSON was not edited. No blocker.

**Q1 - brief vs plan step.** Plan items 1-7 all discharged. The manifest check ran
for real in wave 4 (81/81, 29/29, 14/14, E01-E28 `unavailable`, two differently
hashed `providers-worked-on.xlsx` copies). One gap: the plan's 15-sample extraction
bullet and S13's "all fifteen profiles reachable" are C03's and the deferral is not
recorded (C01-R-6). Preservation statements 19/20 can only be pinned in the A-owned
architecture suite; statement 20 holds by inspection (no second `"intake-processing"`
literal in `Pegasus.Infrastructure`).

**Q2 - implementation vs brief.** PR 639 statements 1-14, 16-18, 20, 22, 23 verified
in code and in named tests; the operation key is item-keyed at
`ReconcileUnidentifiedDestinations.cs:350`; `ReopenAsync` uses PR-069's replay
reconstruction, clears the watermark, and conflicts on version-or-state;
`ListResolutionsToRecheckAsync` joins a 1:1 association key and cannot starve
(`EfIntakeReceiptStore.cs:689` projects the association version regardless of
`IsActive`, so every selectable row is markable); `MarkResolutionRecheckedAsync` is
token-free by design. Statement 15 has no real-SQL test - accepted with the reason
as stated, since the load-bearing property is asserted at Core and the same store
path is covered end-to-end; keep it on the integration list rather than closing it
as covered. PR 646 H3-H11/H13 all present with H9's relocation and H10's placement
correction; H12 correctly left to A. The API-level Ambiguous fixture is a faithful
ambiguity - a second real Case whose `CaseMatchIndex` row copies every key, workflow
state written as the enum name and deliberately `NotReady`, so both candidates hit
and neither is contradicted; direct SQL is the only route to that state and
`ASubmissionContradictingTheExistingCaseCreatesItsOwnCase` pins the other half.
`AnalyzeRetainedInstruction` has the five outcomes, Core authorization on a typed
actor, A04's reader for bytes, replay without duplicates, page/locator persistence,
every conflicting candidate kept and no port that could allocate. The selector has
Selected/NotApplicable/Ambiguous only, no scores or first-match, provable order
independence, the corpus-derived QDOS signature, and no second policy. A05: metadata
without storage keys, identical authorization on metadata and bytes, keyset pages on
the shared `ICursorProtector`/`CreateScope` contract with cross-scope rejection, and
no C-owned token codec. The Web advisory catch excludes
`UnidentifiedOperationConflictException`; the page mints via
`StaffPageModel.NewOperationKey()`; nothing is swallowed; no weakened assertions
(the negative-persistence test moves from absolute literals to a captured baseline,
which preserves and slightly widens the property); no fabricated domain data.
The one defect is the filtered keyset page, C01-R-1.

**Q3 - simplification.** No pass recorded (C01-R-7); lenses applied in review -
one real duplication (C01-R-2), one over-complex-and-wrong construct (C01-R-1), one
altitude nit (C01-R-10), nothing else.

**Unwired, all stated by the report:** the ten DI registrations (A-owned file, none
present); A04's `IReadLogicalDocumentVersion`, which has no implementation anywhere
in `src/` - so A must register the reader and the command together, or the Details
page 500s instead of showing "not available"; and the `qdos-extraction-policy-v7`
corpus rebuild, which will fail `TrackedPegasusSourceHashesHaveNotDrifted` until A
regenerates.

**Tests at head:** build PASS (0/0); Core 108/108; Architecture 100/100; integration
95/96. The single failure, `ProviderApiSubmissionTests.ADeclaredTriageOpensATriageAndAllocatesNoCase`,
is pre-existing (TICK-058), untouched by this diff, and caused by the foundation's
`CK_Triage_Sequence [Sequence] > 0` against a Triage insert still writing 0 until
C07 integrates. The report attributes it exactly that way.

**Verdict: needs-changes on C01-R-1 alone.** Fix (or refuse) the filtered
continuation with a test over a window holding no matching row; C01-R-2..R-4 are
worth the same pass; C01-R-5/R-6 belong in the handoff.

---
verdict: pass
independent: true
head: 741f1a70d598a8a287689154624448a4e1fbbcee
reviewed_at: 2026-09-06T14:31Z
supersedes: review at head 9b4dd1ef2295c17f5b2d5ddc633f5ca9044ea40a (needs-changes)
slice: C01 (INTK-060, Stream C)
branch: c01-retained-analysis
correction_commits: aa518b798 (C01-R-1), 741f1a70d (R-2, R-3, R-4, R-9, R-10)
a_owned_files_touched: none
attestation_file: C:/Users/PGUSER/AppData/Local/Temp/claude/C--Users-PGUSER-documents-github-pegasus/e752479c-0f90-4a5e-bc40-b525ea3bf932/scratchpad/wave1/c01-review.md
tests_at_head: wave 6 - build PASS (0/0); Core 108/108; integration 96 passed / 1 failed of 97; Architecture 100/100
new_findings: none
findings:
  - id: C01-R-1
    severity: major
    status: closed
    file: src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs:441-553
    verification: >
      Fixed at the root, not worked around. MediaKindPredicate (:522-552) returns
      a typed Expression over the origin receipt's own two columns, and the
      filter, the keyset bound, the order and Take(limit + 1) now execute in ONE
      statement (:441-470): every row returned matched, so page[^1] is always a
      valid next position, hasMore is the honest limit+1 probe, and the window
      arithmetic is gone - the fallback the review offered is genuinely
      unnecessary. The predicate mirrors UnidentifiedMediaKindPolicy branch for
      branch including the no-receipt fallback to Image; the channel code comes
      from EfIntakeReceiptStore.ToCode (already internal for this store);
      SourceChannel and MediaType are both IsRequired (PegasusDbContext.cs:311,313)
      so no three-valued-logic hole exists; LIKE '[Ii][Mm][Aa][Gg][Ee]/%' makes
      the image test collation-independent, which is the honest counterpart of
      the policy's OrdinalIgnoreCase. Both false sentences in the old comment are
      gone.
    drift_lock: >
      Adequate and bidirectional. AFilteredQueuePagesPastNonMatchingRowsWithoutDroppingAny
      seeds eight rows OLDEST-first as four documents, two e-mails, two images, so
      the old code would have returned an empty first page and a null cursor at
      page size one. It asserts each returned row's MediaKind - which comes from
      MapQueueRow and therefore from the POLICY, not the predicate - equals the
      filter (catches a predicate admitting a wrong row); each filtered sequence
      equals in order the unfiltered queue's own rows of that kind (catches a
      predicate omitting a row); the three filters partition the queue exactly
      (4/2/2, union equals the whole queue); and a page large enough for every
      match ends with a null cursor rather than an empty page. It covers the two
      likeliest drifts: a mailbox receipt carrying image/jpeg (Email, never Image)
      and an upper-case IMAGE/PNG. The predicate cannot change without failing it.
      Residual, not a finding: the Receipt == null branch is not seeded (no
      producer of a receipt-less origin exists today) and is covered by inspection.
  - id: C01-R-2
    severity: minor
    status: closed
    verification: >
      One owner. AnalyzeRetainedInstruction.SelectAsset's default branch is now
      IntakeFileIdentity.SourceAsset, and the page's third derivation is gone -
      Details.cshtml.cs:567-571 and the new ShowRetainedAnalysisPanel (:64-73)
      both call the same owner, so the page cannot offer an analysis the command
      would refuse.
  - id: C01-R-3
    severity: minor
    status: closed
    verification: >
      ListAsync now directly follows its own summary/remarks and ListByCursorAsync
      sits below it with one summary of its own, sharpened to say why both shapes
      exist (IntakeContracts.cs:728-766).
  - id: C01-R-4
    severity: minor
    status: closed
    verification: >
      RecordAsync probes the (receipt, asset, key) triple the unique index covers
      and then raises the documented conflict for a key already spent on another
      receipt or asset. Because that second read runs inside the serializable
      transaction, the race that could leave two rows under one key is CLOSED, not
      merely diagnosed - further than the finding required. FindByOperationKeyAsync
      no longer asserts uniqueness the schema does not enforce.
  - id: C01-R-5
    severity: minor
    status: handed off
    verification: >
      Recorded on the same A/root item as the qdos-extraction-policy-v7 rebuild,
      with the implementer's reading stated (the fingerprint's criterionState is
      route-activation state; the analysis command is a staff-invoked read that
      allocates nothing and changes no route). Defensible; the decision stays with
      the owner of reference/**.
  - id: C01-R-6
    severity: minor
    status: closed
    verification: >
      The deferral is now explicit: the remaining fourteen profiles and the
      fifteen-sample corpus run are a C03 obligation, with the plan and S13
      references named so it cannot be lost at integration.
  - id: C01-R-7
    severity: minor
    status: closed
    verification: >
      "## Simplification pass" added with all four lenses and a disposition each,
      including two items this review did not raise (a redundant
      RequireStaffOrAutomation wrapper removed; the per-recheck receipt re-read
      deliberately kept because batching it would break preservation statement 11)
      and two looked-at-but-unchanged efficiency notes.
  - id: C01-R-8
    severity: nit
    status: not applied (reason accepted)
    verification: Declined with a recorded reason; no tracked corpus snapshot is affected and the repository is already mixed.
  - id: C01-R-9
    severity: nit
    status: closed
    verification: >
      The round-1 explanation is corrected to the real cause - record equality
      over IReadOnlyList members is by reference, so the whole-record assertion
      could never have passed - and no longer blames the mail-route reason string
      the replacement still compares.
  - id: C01-R-10
    severity: nit
    status: closed
    verification: ShowRetainedAnalysisPanel hides the panel where there is neither an analysis on record nor a retained source, still without keying on the decision.
---

# C01 re-review at head 741f1a70d - superseding attestation (pass)

Independent: this reviewer wrote nothing on `c01-retained-analysis` and made no
edit in the worktree; `git status --porcelain` is empty at
`741f1a70d598a8a287689154624448a4e1fbbcee`.

**Scope.** `git diff 9b4dd1ef2..741f1a70d` is 7 files, +295/-58 - the four
production files the findings named, the view, the page model and one test file.
No A-owned path in the dispatch list appears. The diff is entirely
finding-driven; no earlier verified behaviour was disturbed (the reconciler, the
selector, the case-match port, the manifest test and every A05 contract are
untouched by these two commits).

**The major is closed the better way.** The implementer took the SQL option
rather than the fallback, which removes the failure mode instead of compensating
for it, and the method is now shorter than the one it replaced. I checked the
three things that could have gone wrong with that approach - predicate-vs-policy
branch parity including the no-receipt fallback, the null/collation traps, and
drift - and all three hold; the drift lock is asserted in both directions by the
new test rather than asserted about. See the frontmatter for the detail.

**Minors and nits.** R-2, R-3, R-4, R-9, R-10 applied as specified, R-4 beyond
what was asked; R-8 declined with a reason I accept; R-5 and R-6 recorded as
handoffs (R-6 verbatim, R-5 with reasoning added). The report's new
simplification pass answers review question 3 in its own right and names two lens
items I had not raised plus two deliberate non-changes with reasons - the
honest-disposition shape the repository asks for.

**Evidence.** Wave 6, all four jobs complete at this head: build PASS (0/0),
Core 108/108, integration 96 passed / 1 failed of 97, Architecture 100/100. The
integration total rose 96 -> 97: the new
`AFilteredQueuePagesPastNonMatchingRowsWithoutDroppingAny` ran and passed, so the
`MediaKindPredicate` expression - the first `EF.Functions.Like` anywhere in this
repository - translates and the filtered continuation partitions the queue
exactly on real SQL at page size one. The one failure is
`ProviderApiSubmissionTests.ADeclaredTriageOpensATriageAndAllocatesNoCase`
(`COUNT(*) FROM Triage` is 0 at :507): pre-existing TICK-058, untouched by any
C01 commit, caused by the foundation's `CK_Triage_Sequence [Sequence] > 0`
against a Triage insert that still writes 0 until C07's allocator integrates. It
failed identically at the previous head and the report attributes it that way. No
C01-owned test fails.

**Verdict: pass.** No new finding. Merge gating stays the controller's - this
slice opens no PR - and these handoffs must travel with it to integration: the
ten DI registrations; A04's `IReadLogicalDocumentVersion` registered *together*
with the command (registering the command alone turns the page's optional
dependency bridge into a 500); the corpus rebuild plus the fingerprint activation
question; and C03's fourteen remaining profiles with the fifteen-sample run.

---
kind: review-attestation
pr: "none — source review of a local branch commit (no PR opened for this slice)"
head_sha: "d505d60788da096cb11d480a64bd936dba93ca7e"
branch: "c01-retained-analysis"
parent_sha: "aa5e669d76ad2f7cc24783f8076644c439509feb"
worktree: "C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c01"
verdict: needs-changes
reviewer: "pegasus-reviewer (INTK-060 source review, review-c01)"
independent: true
plan_hash: "sha256:4d3c0b73e770923251c8549197309cdb284701c94f85d81e745d9f70e56d3558 (pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md)"
ticket_updated: "2026-09-07T01:28:05.469Z"
ticket_revision: "rev1:04b328704d2627c5"
board_sha: "not read (controller override: reads scoped to this slice; no board push in scope)"
expected_reviewers: []
threads_snapshot: []
reviewed_blobs:
  - "tests/Pegasus.IntegrationTests/RetainedInstructionAnalysisTests.cs sha256:bb4a3593e2997c1511edffcc1cd0437f5e69988c30162a3dd0d5ed84f05a1a6e"
  - "tests/Pegasus.IntegrationTests/Top15InstructionCorpusTests.cs sha256:648d377f0ae74c279eceb18d53153b9f07b9797e3d7b2fb5e52d1f143ac54898"
skill_sha256:
  - "kanmer-review/SKILL.md eefcbf902c9d6113ce13d37f767d74dd9d09a21921ec64afc0127006b70e6404"
execution_evidence: "scratchpad/takeover/wave38-tests (combined A+B+C tree v1-intake-combined-verify @ 1d11ac1ebbd58312573c88d9f3f3660c65c5d0f7)"
findings:
  - id: F-001
    severity: blocker
    disposition: open
    summary: "AnalyzeRetainedInstruction has no IsIncomplete guard, so every extraction policy's ArgumentException escapes the command; the corpus run aborted on PCH."
  - id: F-002
    severity: blocker
    disposition: open
    summary: "The 81-original loop cannot survive a throw and writes its matrix only after the loop, so one bad original destroys all evidence; the run stopped at original 6 of 81 and produced no artifact."
  - id: F-003
    severity: major
    disposition: open
    summary: "No IReadLogicalDocumentVersion implementation exists anywhere under src/, so IAnalyzeRetainedInstruction cannot be resolved by any real host; the report must say the proof is host-dependent."
  - id: F-004
    severity: minor
    disposition: open
    summary: "The author's report states the drain does not allocate; ProcessQueuedIntake does call allocateIntake.AttemptAutomaticAsync. The tests hold for a different reason."
  - id: F-005
    severity: minor
    disposition: open
    summary: "The QDOS exclusion from the 14-sample test is justified by a mail-route behaviour that the manual-upload channel cannot reach."
  - id: F-006
    severity: minor
    disposition: open
    summary: "The principal-disposition assertion accepts Ambiguous, which production cannot emit on this path (forceReviewOnly has no call site)."
  - id: F-007
    severity: note
    disposition: open
    summary: "Upload/drain run in the base factory's container while the analysis runs in the WithAnalysis host's: two hosts over one LocalDB, and the drain gets the refusing reader double."
  - id: F-008
    severity: note
    disposition: open
    summary: "All SHA-256 comparisons are OrdinalIgnoreCase and the stand-in reader returns uppercase hex, so 'exact source SHA-256' is proved only up to case."
  - id: F-009
    severity: note
    disposition: open
    summary: "The plan's 'multiple profiles return Ambiguous' bullet is already satisfied by a Core test; cite it instead of leaving it apparently unmet."
  - id: F-010
    severity: note
    disposition: open
    summary: "The proof exercises the Web-host composition, which has no VehicleRegistrationCandidateLookup (Worker-only), so INTK-049 candidate expansion is out of its reach."
---

# C01 all-15 / 81-original retained-analysis proof — independent source review

**Verdict: NEEDS CHANGES.** Bound to commit `d505d60788da096cb11d480a64bd936dba93ca7e`
on `c01-retained-analysis` (parent `aa5e669d7`), two files changed, +539/-18 in
`RetainedInstructionAnalysisTests.cs` and 8 visibility lines in
`Top15InstructionCorpusTests.cs`, nothing under `src/`.

This is **not** a source-only verdict. The controller's runner results were present
when this review completed (`scratchpad/takeover/wave38-tests`, combined tree
`v1-intake-combined-verify` @ `1d11ac1eb`, which contains this commit — the stack-trace
line numbers 321/545/646 match this commit's file exactly). They are decisive:

| Lane | Result |
| --- | --- |
| 1 build integration | exit 0, PASS, 0 warnings 0 errors |
| 2 `FullyQualifiedName~RetainedInstructionAnalysisTests` (pack root set) | exit 1, **FAIL** — Failed 1, Passed 5, Total 6, 53 s |
| 3 Top15 + manifest suites | exit 0, PASS, 9 tests |
| 4 `artifacts/evaluation/v1-intake/retained-analysis-corpus.md` | **absent** — nothing written |

`NoGenuineNonQdosOriginalIsAllocatedAutomaticallyThroughNormalIntake` **passed**, and so
did the four pre-existing tests under the rewritten `WithAnalysis`.
`EveryGenuineOriginalReachesRetainedAnalysisWithoutAllocating` **failed with an unhandled
exception** after ~6 s:

```
System.ArgumentException : The PCH extraction policy accepts only fully readable,
complete reader results. (Parameter 'readResult')
  at PchInstructionExtractionPolicy.Extract(...)  PchInstructionExtractionPolicy.cs:314
  at AnalyzeRetainedInstruction.ExecuteAsync(...) AnalyzeRetainedInstruction.cs:417
  at ...AnalyseRetainedOriginalAsync(...)         RetainedInstructionAnalysisTests.cs:545
  at ...EveryGenuineOriginalReachesRetainedAnalysisWithoutAllocating() ...cs:321
```

So the headline claim — *every one of the 81 genuine originals reaches extraction through
the production command, persists review-only candidates with exact source hash and
occurrence, replays without duplicates, and creates zero Cases/links* — is **unproven**.
The array runs QDOS 01-05 then PCH 01; the run died at approximately the sixth original,
and because the matrix is written after the loop, **zero** per-sample evidence exists.
The second half of the claim (14 non-QDOS originals through the real upload + Worker
drain are not `case_created`, allocate nothing, and are held Open in Unidentified) **is**
proven by a green run.

## Answers to the nine questions

**(1) Does the command really come from `AddPegasusInfrastructure`? YES.**
`WithAnalysis` (`RetainedInstructionAnalysisTests.cs:800-803`) now adds exactly one
registration — `services.AddScoped<IReadLogicalDocumentVersion, RetainedIntakeAssetReader>()`.
The diff removed the whole hand-composed graph the previous version registered (selector,
`EfRetainedInstructionAnalysisStore`, `IRetainedInstructionAnalysisStore`,
`ISourceCandidateQueries`, `IGetLatestRetainedInstructionAnalysis`,
`AnalyzeRetainedInstruction`). `src/Pegasus.Infrastructure/DependencyInjection.cs:162-188`
now supplies all of them plus the fifteen `IInstructionExtractionPolicy` registrations and
`IAnalyzeRetainedInstruction`. The base `IntakeWebApplicationFactory` is constructed
through its parameterless ctor, so its optional `extractionPolicy`, `artifactStore`,
`recognitionEngine`, `mailClassificationPolicy` and `approvedMailboxIdentityResolver`
overrides are all null and shadow nothing (`IntakeWebTestSupport.cs:178-199`). It does
replace `TimeProvider` with a frozen clock and the two `ICommitted*WorkPublisher`
transports with doubles (`:165-172`) — pre-existing, queue transport only, unrelated to
analysis. That the four pre-existing tests in the class still pass on the real run is the
strongest available evidence that production composition really does build this command.
**Caveat: see F-003 — the graph does not close in a real host.**

**(2) Is the staging path the real one? YES.**
`IntakeWebDriver.UploadAndProcessAsync` (`IntakeWebTestSupport.cs:355-372`) posts the real
upload endpoint with real antiforgery tokens and then `ProcessQueuedAsync` →
`DrainStagedAsync` (`:736-…`), which dispatches through `DispatchPendingIntakeWork` and the
Worker's own `ProcessQueuedIntake`. No synthetic insert anywhere; no `IIntakeReceiptStore`
write by the test; nothing constructs a receipt by hand.

**(3) Are the assertions member-level and un-loosened? YES, with one dead tolerance.**
Per original: outcome `Analyzed` or a failure line (`:545-560`); profile equality via
`principal.RawValue == expectation.Profile` — and I verified every one of the fifteen
`SupportedPrincipalCode` constants equals its label, including `YML` for the HDUK-named
files (`Yml/YmlInstructionExtractionPolicy.cs:7`); `PartyRole == "principal"`;
`analysis.SourceSha256 == asset.ContentHash`; per-row `SourceSha256`; the sorted
`Field#Occurrence` multiset of persisted rows equals the analysis's (`:596-609`); replay
`IsReplay` + same `Analysis.Id` + unchanged row count (`:611-619`); receipt version and
`AcceptedCaseId`/`ManualLinkedCaseId`/`AllocationState` unmoved (`:623-631`); per-sample
Cases/CaseIntakeLinks; whole-run `Cases`, `CaseIntakeLinks`, `IntakeManualAssociations`
all zero. Expectations counted from the shared list: **81 rows across 15 profiles**
(5 each except MP's 11).
There is **no** per-profile exception list, **no** `Skip` beyond the shared pack gate,
**no** `if (profile == …)` other than the documented QDOS filter in test 2 (`:391`), **no**
`try`/`catch` anywhere in the file, and the only "measured not asserted" material is the
per-profile disposition matrix, which nothing reads back. The one soft spot is F-006.

**(4) Does the 14-sample test use the real drain and assert what it claims? YES.**
`IntakeWebDriver.CreateClient(factory)` + `UploadAndProcessAsync` (real drain), then
`receipt.Decision != CaseCreated`, all three allocation fields null, retained hash intact
via `IntakeFileIdentity.SourceAsset`, and `IUnidentifiedStore.GetByOriginAsync(
UnidentifiedOrigin.Receipt(receiptId))` returning an item in state `Open` (`:428-467`).
`Assert.Equal(14, samples.Length)` pins the count. QDOS is cited, not re-proved. See F-005
for why the citation's reasoning does not fit the channel.

**(5) Were the shared `Expectations` copied? NO — rule 8 is kept, and the visibility
change is the minimum.** The new test reads `Top15InstructionCorpusTests.Expectations`
directly; not one of the 81 rows was copied, moved or edited (the Top15 diff is
`private` → `internal` on eight members and nothing else). All eight are load-bearing:
`Expectations`, `SampleExpectation`, `PackRoot()`, `PackRootVariable`, `MediaType()` and
`Cell()` are used by the new test, and `ExpectedIdentity`/`NeighbouringValue` must be at
least as accessible as `SampleExpectation`'s primary constructor or the file will not
compile. `internal`, not `public`, is the right widening.

**(6) Does the matrix gate the assertions? NO — but it is destroyed by an abort.**
`measured` feeds only `AppendMeasuredDispositions`; every consequential check appends to
`failures`, and `Assert.True(failures.Count == 0, …)` is the gate. The report is honest.
However `WriteCorpusReport` runs *after* the loop and *before* the asserts, so the observed
abort produced no artifact at all (lane 4). See F-002.

**(7a) Does the absent production `IReadLogicalDocumentVersion` make this host-dependent?
YES — and the report must say so in those words.** `grep` over `src/` finds the interface
(`Core/Documents/DocumentContracts.cs:20`) and four consumers
(`AnalyzeRetainedInstruction.cs:196`, `DurableIntake.cs:544`, `InstructionEvidenceImages.cs:186`,
`IntakeOcr.cs:449`) and **no implementation**. So `AddPegasusInfrastructure` registers
`IAnalyzeRetainedInstruction`, but the graph does not close: a real Web or Worker host
throws on resolve today. The accurate claim is "every registration in this proof is
production except the A04 reader port, which no host composes at all" — not "these tests
resolve what the Web host resolves". The C-owned `RetainedIntakeAssetReader` is a decent
stand-in (it re-reads by storage key and verifies hash *and* length before returning
bytes, `:820-855`), which is exactly why it can mask the absence. Recorded as F-003 with a
concrete way to make the gap enforce itself.

**(7b) "Multiple profiles return `Ambiguous`" versus treating Ambiguous as a failure —
the author's reading is right, and the plan bullet is already satisfied elsewhere.**
`RetainedInstructionAnalysisOutcome.Ambiguous` is defined as "More than one profile
matched" (`AnalyzeRetainedInstruction.cs:18-19`), and the command returns it from the
selector branch (`:383-386`). It is a property of *documents that match two signatures*,
never a licence for a labelled original to come back unresolved. For the 81 operator-
labelled originals, Ambiguous or NoProfile is a failure, and this test is right to treat it
as one. The plan bullet is discharged by
`tests/Pegasus.Core.Tests/Intake/AnalyzeRetainedInstructionTests.cs:148
TwoMatchingProfilesAreAmbiguousAndBothAreNamed` — cite it (F-009) so C01 does not read as
though the bullet is unmet. No plan change is needed.

**(8) Runtime — 81 uploads + 162 analyses on LocalDB.** No per-sample host rebuild: both
hosts are built once per test method, outside the loop. Per sample the test does one HTTP
upload, one drain, two analyses, one scope, one `DbContext` and two count queries. The
observed pace was roughly a second per original (six originals in ~6 s of a 53 s run
including two host builds and two database restores), so a complete pass is on the order
of 90-120 s — acceptable for a standing corpus lane. The one real waste is structural, not
per-sample: **two web hosts are built over one LocalDB** because the upload/drain go
through `factory` while the analysis goes through `host` (F-007). Fixing that removes a
host build, a `DevelopmentOfflineInitialization` pass and the reader-double asymmetry.
Before this becomes a standing lane, also note the trait shape: the class carries
`Category=SqlServer` and the two new methods add `Category=Corpus`, so both apply — any
lane selecting `Category=SqlServer` without excluding `Corpus` now pulls in 81 uploads.

**(9) Nothing weakened elsewhere, nothing out of scope.** Two files, both under `tests/`.
The Top15 diff is visibility only — no expectation, threshold, negative assertion or
`inconclusive` rule was touched, and lane 3 confirms that suite still passes green. No
`src/` file, no `AGENTS.md`, no fixture shared with another stream.

## Findings

### F-001 — BLOCKER — `AnalyzeRetainedInstruction` lets every policy's `ArgumentException` escape

The command guards only the read *status*:

`src/Pegasus.Core/Intake/AnalyzeRetainedInstruction.cs:365-373`
```csharp
if (readResult.Status != IntakeSourceReadStatus.Readable)
{
    return new(RetainedInstructionAnalysisOutcome.SourceUnavailable, null,
        readResult.FailureReason ?? "The retained source is not readable.", [], false);
}
```

It never inspects `readResult.IsIncomplete`, and then calls `policy.Extract(...)` at
`:417`, outside the `try`/`catch` that wraps opening and reading (`:351-363`). **All
fifteen** extraction policies refuse an incomplete result by throwing — `Pch:312-317`,
`Qdos:289`, `Ax:65`, `Black:75`, `Dfd:46`, `Fw:96`, `Kbs:43`, `Mp:38`, `Oak:74`,
`Qcl:85`, `Rjs:46`, `Sbl:79`, `Yml:27`, `Als:11`, `Bc:8`. The reader sets
`IsIncomplete` in a dozen places, several of them on the legacy `.doc`/`.msg` path
(`MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs:83,135,143`), which is exactly what a
PCH `.DOC` original hit on the run.

Both of the other callers of this material already keep the guard the command dropped:
`src/Pegasus.Core/Intake/ProcessIntake.cs:655` returns `NeedsSorting` for an incomplete
read before any policy is invoked, and `Top15InstructionCorpusTests.cs:641` records the
sample INCONCLUSIVE and continues. `AnalyzeRetainedInstruction` is the odd one out.

This is a production defect, not a test artefact: the `/Received/{id}` analysis action
named in plan C01's "Production callers" will throw `ArgumentException` out to the page for
any retained `.DOC` or partially-read PDF.

**Exact correction** — in `src/Pegasus.Core/Intake/AnalyzeRetainedInstruction.cs`, replace
the guard at `:365` with

```csharp
if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
{
    return new(
        RetainedInstructionAnalysisOutcome.SourceUnavailable,
        null,
        readResult.FailureReason
            ?? "The retained source could not be read completely.",
        [],
        false);
}
```

and add a Core test beside the existing ones in
`tests/Pegasus.Core.Tests/Intake/AnalyzeRetainedInstructionTests.cs` — an incomplete but
readable result returns `SourceUnavailable`, records no row and no candidate, and leaves a
later attempt under the same key free to analyse (the same contract
`ASourceThatCannotBeOpenedIsReportedRatherThanRecorded:162` already states).
`SourceUnavailable` is the right member: its own summary is "The immutable logical source
could not be opened or read", and nothing is recorded, so re-analysis after a better read
is still possible. Do **not** fix this by catching the exception in the test.

### F-002 — BLOCKER — one bad original destroys the whole proof and its artifact

`EveryGenuineOriginalReachesRetainedAnalysisWithoutAllocating` is built to accumulate
failures ("A run that stopped at the first bad original would say nothing about the other
eighty", `:531-533`), but nothing in the loop survives an exception from
`UploadAndProcessAsync` or `analyze.ExecuteAsync`. The run proves the point: it died on
original ~6 of 81, so 75+ originals were never exercised, and because
`WriteCorpusReport(...)` is called at `:608` — after the loop — no matrix was written at
all (lane 4: the file does not exist). The evidence yield of the failed run is one stack
trace. The author anticipated this as open question 3(b) and left it; it has now happened.

**Exact correction**, in `RetainedInstructionAnalysisTests.cs`:

1. Write the report in a `finally` around the `foreach`, so an abort still leaves the
   matrix and the failure lines gathered so far on disk.
2. Give the per-sample body a *typed* catch so one original cannot end the run —
   `catch (Exception exception) when (exception is ArgumentException
   or InvalidOperationException or IntakeArtifactIntegrityException)` appending
   `$"{name} ({expectation.Profile}): analysis threw {exception.GetType().Name} - {exception.Message}"`
   to `failures`. Typed clauses keep CA1031 satisfied; a blanket `catch (Exception)`
   would not, and must not be used.
3. Carry over the discipline the direct-drive suite keeps and this one lost: a separate
   **INCONCLUSIVE** bucket for an original the reader could not deliver (mirroring
   `Top15InstructionCorpusTests.cs:641-661`, including its `MinimumRecoveredCharacters`
   floor), reported in its own section and — per that suite's own words — never counted as
   a pass. With F-001 fixed, an original that comes back `SourceUnavailable` is a reader
   gap, and reporting it as an extraction failure blames the wrong component; reporting it
   as a pass would be worse.

Until this and F-001 land, the C01 claim cannot be re-asserted: rerun and attach a matrix
covering all 81 rows.

### F-003 — MAJOR — the production graph does not close; say so, and make it enforce itself

Detail under (7a). The C01 report's "production composition" language should be corrected
to name the exception explicitly and state the consequence: **`IAnalyzeRetainedInstruction`
cannot be resolved from the Web or Worker host today**, because A04's
`IReadLogicalDocumentVersion` implementation does not exist in `src/`. Recommended, and
cheap: add a composition assertion that the Web host can resolve
`IAnalyzeRetainedInstruction`, marked with a `Skip` naming A04, so the day the reader lands
the gap closes itself instead of relying on someone remembering. Whatever form it takes,
the sentence "these tests resolve the command the Web host itself resolves" must not stand
unqualified in the report.

### F-004 — MINOR — the report's reason for "zero Cases" is wrong

The report states: "the Case is created later by `IAllocateIntake`/`AcceptIntake`, which
neither `UploadAndProcessAsync` nor `DrainStagedAsync` runs". It does run:
`ProcessQueuedIntake` takes `IAllocateIntake` (`DurableIntake.cs:542`) and calls
`allocateIntake.AttemptAutomaticAsync` at `:625` and `:788`; the DI comment at
`DependencyInjection.cs:190-193` says as much ("allocation is no longer a staff action:
the Worker's processing path creates the case for a definitive instruction"). The tests
are still correct, for a different and stronger reason: a manual upload presents no
transport sender, so `EvaluateMailRoute` returns null (`ProcessIntake.cs:1081-1087`),
`EstablishPrincipalContext` returns null (`:1022-1030`), and the assessment terminates at
`NeedsSorting` — "No accepted intake route established the principal for automatic case
creation" (`:771-772`) — before any extraction policy is consulted. Correct the report;
as written it would let a genuine allocation regression look expected.

### F-005 — MINOR — the QDOS exclusion cites a behaviour the tested channel cannot reach

Test 2 excludes QDOS because "QDOS keeps its automatic allocation, already proved on
genuine material by `QdosIntakeWebTests`". That allocation is a *mail-route* behaviour;
the test drives *manual upload*, where QDOS cannot allocate either, for the reason in
F-004. Either include the five QDOS originals with the same assertions — they cost nothing
and widen the proof to 19 samples — or restate the comment as "QDOS's automatic allocation
belongs to the accepted mail route and is proved there by `QdosIntakeWebTests
.StaffForwardedEmailStrongContentBeatsSenderAndRendersPersistedDraft`; through manual
upload no profile allocates, QDOS included." Separately worth stating as a limitation of
this proof: because manual upload never establishes a principal, it cannot distinguish
"a confident document profile does not allocate" from "this channel never allocates". The
sharper negative the plan describes for SBL and BC — a document that a profile identifies
arriving through an *accepted* route for a different principal — is not covered here and
belongs to C03/C04.

### F-006 — MINOR — the principal-disposition assertion tolerates a state production cannot produce

`ProposedPrincipal` fails only when the disposition is neither `Usable` nor `Ambiguous`
(`:663-670`). On the `Analyzed` path the principal candidate's disposition is
`forceReviewOnly ? Ambiguous : Usable` (`AnalyzeRetainedInstruction.cs:516`), and
`forceReviewOnly` has **no call site** that passes `true` — it is a defaulted parameter at
`:491` only. So production always emits `Usable` here and the tolerance is unreachable.
Assert `Assert.Equal(SourceCandidateDisposition.Usable, principal.Disposition)` (as a
failure line), or keep the set and say in the comment that `Ambiguous` is currently
unreachable, so a future reader does not read the looser check as a known-failure
allowance. (The dead `forceReviewOnly` parameter itself is production code and out of
scope for this slice — worth a note to whoever owns `AnalyzeRetainedInstruction`.)

### F-007 — NOTE — the drain and the analysis run in different containers

`AnalyseRetainedOriginalAsync` uploads with `client` (created from `host`, the
`WithAnalysis` factory) but drains with `IntakeWebDriver.UploadAndProcessAsync(factory, …)`,
whose `ProcessQueuedAsync` uses `factory.Services` (`IntakeWebTestSupport.cs:388`). The
base container has no `IReadLogicalDocumentVersion`, so `CreateProcessor` (`:704-711`)
gives the Worker processor the **refusing** double while the analysis in the same test uses
the real stand-in. Two hosts are therefore built over one LocalDB and
`DevelopmentOfflineInitialization` runs twice. It works (the database is shared through the
factory's single `LocalDbTestDatabase`), and the pattern is inherited from the pre-existing
`RetainAsync`, but it is confusing and wasteful. Passing `host` as the first argument to
`UploadAndProcessAsync` makes one composition serve upload, drain and analysis and removes
a host build per test.

### F-008 — NOTE — hash equality is only proved case-insensitively

The pack hash and the asset store use `Convert.ToHexStringLower`; the stand-in reader
returns `Convert.ToHexString` (upper). Every comparison in the new test is
`StringComparison.OrdinalIgnoreCase`, so the casing that actually lands in
`IntakeSourceCandidateEntity.SourceSha256` is whatever the reader chose and is never
asserted. If persisted casing is part of "exact source SHA-256", assert it once; if not,
say so.

### F-009 — NOTE — cite the Core test that discharges the "Ambiguous" plan bullet

`tests/Pegasus.Core.Tests/Intake/AnalyzeRetainedInstructionTests.cs:148`. See (7b).

### F-010 — NOTE — the proof runs the no-lookup composition

`VehicleRegistrationCandidateLookup` is registered only in
`src/Pegasus.Worker/WorkerDependencyInjection.cs:102`, so the Web host resolves
`AnalyzeRetainedInstruction` with its optional lookup null (the contract the Core test
`OrdinaryAnalysisWorksWithoutVehicleLookupComposition:473` covers). Fine for embedded-text
originals, which is all the corpus contains — but it means this proof says nothing about
INTK-049 candidate expansion, and the report should not let the phrase "the host's own
command" imply otherwise.

## Exact test names, filter and preconditions

Fully-qualified names (both `[ReferencePackFact]` + `[Trait("Category","Corpus")]`, with
the class trait `Category=SqlServer` also applying):

- `Pegasus.IntegrationTests.RetainedInstructionAnalysisTests.EveryGenuineOriginalReachesRetainedAnalysisWithoutAllocating`
- `Pegasus.IntegrationTests.RetainedInstructionAnalysisTests.NoGenuineNonQdosOriginalIsAllocatedAutomaticallyThroughNormalIntake`

```powershell
$env:PEGASUS_REFERENCE_PACK_ROOT = 'C:/Users/PGUSER/documents/github/pegasus/pegasus_pack'
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj `
  --configuration Release --no-build `
  --filter "FullyQualifiedName~RetainedInstructionAnalysisTests"
```

`PEGASUS_REFERENCE_PACK_ROOT` **must** be set to the reference pack root and the directory
must exist, or `ReferencePackFactAttribute` (`PrincipalSourceManifestTests.cs:389-404`)
sets `Skip` and both tests report as skipped — **INCONCLUSIVE, which is not a pass**. A
real SQL LocalDB is also required (`LocalDbTestDatabase`). This filter selects **6 test
cases**: the 4 pre-existing tests plus these 2; the 81 originals and 162 analyses all live
*inside* the first of the two, so "6 tests" is the expected shape, not a sign the corpus
did not run (the runner's note on lane 2 reads it as a shortfall — it is not).

Artifact to collect after a complete run:
`artifacts/evaluation/v1-intake/retained-analysis-corpus.md` (git-ignored) — 81 matrix rows,
the measured per-profile disposition table, and any Failures / (after F-002) Inconclusive
sections.

## Residual risk if the two blockers are fixed

The design is sound and the discipline is real: production composition, real upload and
real Worker drain, one shared expectation list, member-level assertions, no per-profile
escape hatch, and a measured matrix that gates nothing. What remains after F-001 and F-002
is honesty about scope — F-003's open reader port, and the fact that a manual-upload
channel can only prove the negative it proves. Neither is a reason to weaken an assertion;
both are reasons to write two more sentences in the C01 report.

Full attestation file:
`C:\Users\PGUSER\AppData\Local\Temp\claude\C--Users-PGUSER-documents-github-pegasus\5adc2fb3-f15d-4145-84ed-948eb9fde4e4\scratchpad\takeover\c01-all15-review.md`
