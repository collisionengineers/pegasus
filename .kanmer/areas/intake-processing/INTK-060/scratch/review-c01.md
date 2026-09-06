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
