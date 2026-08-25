# Plan — PLAT-044: Stop Assessment opening from repeating Review and content-store work

## Approach

Trust Review as the single lifecycle decision for instruction/image completeness. Replace the Assessment GET's composition of broad queries and the full report projection with one narrow EF-backed workspace projection. Actual generation reuses that relational projection, then uses PLAT-041's existing batch read for the bytes. Pass the already-persisted case-root remote id through the existing managed-content address so every Box content operation avoids root enumeration without a cache or fallback.

## Governing docs

- **FRD-01 — meets:** Review remains the one Core-owned instruction/image readiness gate; this change removes a downstream reinterpretation.
- **FRD-05 — meets:** content stays in the immutable Case/PO Box root with ancestry, hash and length checks; only lookup by persisted identity changes.
- **FRD-11 — modifies with explicit operator authorization:** readiness is narrowed to assessment/report-preparation work. Review prerequisites are no longer separately recalculated. The user explicitly supplied this correction on 2026-08-25.
- **Operator notes — modifies with explicit operator authorization:** record the supplied statement that reaching Review proves its entry requirements.

## Steps

1. Add an Assessment workspace query contract carrying the compact header, case-data projection, latest vehicle observation, assessment, current draft/accepted specifications, and latest AI request. Implement it with one EF context and at most six commands, reusing existing persistence mappers rather than restating value/state vocabularies.
2. Change the Assessment GET to use that projection once. Remove `PrepareAsync`, document queries and all content-store access from opening the screen; derive visible report readiness from the already-loaded assessment and report-only policy.
3. Remove instruction/image/identity/custody prerequisites from report readiness. Preserve assessment-field, confirmation, conditional outcome, accepted-signatory and accepted-cost blockers. Treat a violated Review invariant as generation failure, not a readiness item.
4. Convert `EfAssessmentReportProjectionSource` to reuse the workspace query and load eligible report-photo bytes through one ordered `ReadVersionsAsync` call.
5. Add required `CaseRootRemoteId` to `ManagedDocumentContentAddress`, populate every caller from `Cases.CustodyRootRemoteId`, and make Box reads/writes use it directly while retaining existing fenced child access. Keep single-file and streaming ZIP byte paths unchanged.
6. Update operator notes and FRD-11 to the resolved lifecycle/readiness rule, then add focused tests for zero GET-time content I/O, no Review-prerequisite readiness items, six-command workspace loading, batched report images and durable-root Box request counts/failures.
7. Run restore, Release build, focused Core/Web/Box tests, and the repository-compatible full test suite. Run the required simplification lenses over this ticket's diff and apply or record every finding before commit/PR.

## Verification

- A QDOS26016-shaped Assessment GET calls no document-content method and the workspace query issues no more than six commands.
- Review prerequisites do not appear in report readiness; real assessment/report blockers still do.
- Three report images produce one batch call, ordered bytes and unchanged integrity enforcement.
- Box single/batch tests prove no approved-root enumeration and fail closed for missing/invalid root ids.
- `dotnet restore`, Release build, focused suites, then full `dotnet test` using the detected repository test platform.

## Risks / open questions

- A stale Box root id must fail closed; no lookup-by-name fallback is permitted.
- Report generation still defensively validates the final immutable snapshot. That is an execution-boundary integrity check, not GET-time business-readiness recalculation.
- No open question remains.

## Simplification pass — 2026-08-25

- **Reuse:** The Assessment GET now has one Core query and one EF source; report generation reuses it. No second business policy, cache, compatibility path or name-based Box fallback was introduced.
- **Correctness:** Kept Review-entry checks in `EvaluateReadiness` for existing assessment consumers and added the explicitly named `EvaluatePostReviewReadiness` for report preparation. This avoids weakening the gate whose result is being trusted.
- **Efficiency:** Removed an extra EVA root-id query by carrying `CustodyRootRemoteId` through its existing document query. The requested .NET performance scan covered 16 changed production C# files: 0 sync-over-async, 0 culture-sensitive literal comparisons, 0 new HttpClient/serializer-options sites, and 0 unsealed leaf-class candidates. One hot-path allocation was fixed by replacing LINQ character enumeration in Box SHA validation with a direct loop.
- **Altitude:** A nullable root id remains valid at the generic/local content-store boundary; `BoxDocumentContentStore` alone enforces it before any remote request. This preserves the supported local store while keeping production Box access fail-closed.
- **Disposition:** No unapplied behavior-preserving findings remain. Existing EF-translated LINQ and small bounded projection materializations were retained because replacing them would add complexity without reducing the measured remote/SQL latency driver.

## Review correction — 2026-08-25

The P2 follow-up establishes a missing access invariant. Assessment is operator-accessible only in Review or Report preparation after a successful EVA export in the current Review cycle. Engineer assignment is optional allocation and is not an access/readiness requirement.

Implementation:
1. Persist the workflow version of every latest successful EVA export and reject recording an export built from a stale workflow snapshot.
2. Add one Core-owned access decision using lifecycle state, latest Review-entry version and latest exported version. Later assignment/workflow version increments do not invalidate an export; a new Review cycle does.
3. Apply the decision to the Case-page control, Assessment GET and all Assessment-page POST handlers, plus report generation. Preserve NotReady assessment/MCP writes outside this operator page.
4. Remove the unused eager full-readiness evaluation from the new workspace source and correct affected documentation/comments.
5. Add focused policy, persistence and Web tests, rerun simplification, restore/build/focused/full tests, then update this plan/report and PR.
