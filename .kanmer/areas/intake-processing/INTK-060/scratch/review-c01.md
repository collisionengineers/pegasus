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
