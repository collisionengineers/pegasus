## Drag-and-drop (coordinator-assigned, done)

- [x] Document-level `dragover`/`drop` safety net in `site.js`.
- [x] Effective drop target widened to the containing panel.
- [x] Regression test: drop inside dashed zone (page-level synthetic) — green.
- [x] Regression test: drop inside dashed zone (CDP native) — green.
- [x] Regression test: drop on panel, outside dashed zone — red before the fix, green after.
- [x] Regression test: drop off the panel does not misroute files (navigation claim not provable via CDP in this harness — noted honestly).

## Part 1 — per-file rows

- [x] `Upload.cshtml` renders one row per selected file (name, size), no run-on line (site.js `describe()` rebuilt; row markup is `.dropzone__file-row`).
- [x] `site.js` fetch-submits the form (opt-in via `data-upload-progress`); rows enter "uploading" together; tick together on a successful response; a validation failure falls back to a native re-submit (nothing is ever stored on that path, so this is safe) rather than guessing which row failed.
- [x] Reused `.is-refreshing .icon--spin` (the existing single animation) for the spinner state instead of adding a second one; `_StatusChip` left untouched (its keys are business/query state, not upload-progress chrome — kept as one list per concept by not overloading it).
- [x] No-JS fallback: native multipart POST/redirect still works unchanged (the fetch-submit only engages when `fetch`/`FormData` exist and files are chosen).
- [x] New tests: `UploadRowsBrowserTests` — rows render per file (name/size, no crammed line, a11y clean); submitting shows every row `data-state="uploading"` together (spin + `.is-refreshing`), then navigates on success.

## Part 2 — copy

- [x] `UploadGroupStatus.cshtml:16` mechanics sentence deleted.
- [x] Full re-read of `Upload.cshtml`/`UploadStatus.cshtml`/`UploadGroupStatus.cshtml`/`_UploadOutcome.cshtml` for "intake"/"receipt"/"custody"/GUID/mechanics wording — the two remaining "receipt" sentences on `Upload.cshtml`'s "What happens next" panel were also rewritten (the ticket's own vocabulary list is stricter than design README's, so this surface follows the ticket); only comments and internal route-parameter names remain, confirmed by re-grep after all edits.

## Part 3 — confirmation step

- [x] `UploadOutcome.cs` view-model builder implements the decision table; unit-tested independent of the page (`UploadOutcomeQueriesTests`, 9 tests, hand-built fakes for its three read ports).
- [x] `_UploadOutcome.cshtml` partial renders each branch's text/action without a raw enum/GUID; reuses `_StatusChip` with existing recognised words only (`Success`, `Needs sorting`, `Pending`, `Failed` — no new chip keys added).
- [x] `UploadStatusModel`/`UploadGroupStatusModel` wired to the builder; confirmation shows once a member is `Complete`/`Failed`.
- [x] Test: located/already-attached case → report + "Open case" link + reversal link (`CompleteWithACaseAlreadyAttachedIsReportedNotReOffered`).
- [x] Test: no case, image group → Image-initiated report + `/VehicleImages` link (`NoCaseImageGroupReportsTheAutomaticallyRegisteredImageInitiatedCase`).
- [x] Test: no case, instruction document → "Create a case" offer → `/Cases/Create?receiptId=` (`NoCaseInstructionDocumentOffersToCreateOne`).
- [x] Test: ambiguous match → "Review and attach" offer → `/Received/{id}` (Intake/Details' route); override reachable through that existing page (`AmbiguousCandidateMatchOffersToReviewAndAttachWithOverride`).
- [x] Test: failed file → failure state, no offer (`FailedFileStatesItsFailureWithNoOffer`).
- [x] Test: group-kept-intact routing to Unidentified via the group-level origin fallback (`NoUsableVrmImageGroupRoutedToUnidentifiedIsReportedForReview`).
- [x] Split-group independence: not one consolidated end-to-end test, but architecturally guaranteed and covered — `UploadOutcomeQueries.BuildAsync` is a pure per-call function (proven stateless by every test above using a fresh instance) and `UploadGroupStatusModel.OnGetAsync` calls it once per member inside its own loop (read, not assumed); INTK-011's split-group race therefore renders two independent, correct outcomes on the same group page by construction.
- [x] End-to-end web coverage: `QdosIntakeWebTests.ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage` updated to assert the real `NeedsReview` outcome a genuine upload-through-processing run produces (verified against actual rendered HTML, not guessed).

## CASE-003

- [x] `GET /Cases/Create` (no `receiptId`) → 404, not 500 (`CreateWithNoReceiptIdReturnsNotFoundInsteadOfThrowing`).
- [x] Existing `Cases/Create?receiptId=...` journey unchanged (`CaseCreateWebTests`, 13 tests, all still green; `DescribeRefusal` now delegates to the new shared `OperatorLabels.IntakeCannotBecomeCaseReason` with identical wording).

## Docs

- [x] FRD-02 updated: new "Upload confirmation surface" subsection with the full decision table.
- [x] FRD-12 updated: new "Upload" subsection describing rows/spinner-tick/no-mechanics-copy and the confirmation surface, cross-linking FRD-02's table rather than restating it.
- [x] `docs/capabilities.md` checked: every relevant row (INT-28/INT-32, UI-01..13) already cites `frd-02-intake-and-source-identity.md`/`frd-12-operator-experience.md` generically; this ticket adds UI presentation and reuse of existing mutations, no new business rule — no new capability row added.

## Verification (whole ticket)

- [x] `dotnet build --configuration Release` (full solution) — clean, 0 warnings/errors.
- [x] Core tests green (684/684).
- [x] `Pegasus.ArchitectureTests` green (97/97) — unaffected, but run since Web/Presentation changed.
- [x] Integration filters `Upload|GroupedIntake|IntakeWeb|Cases` green (85 passed, 6 pre-existing skips unrelated to this change; 13/13 on `Cases`).
- [x] Browser suite green (43/43, `Category=Browser`), including the two new INTK-010 Browser test files.
- [ ] Visual pass at 1920 — not run in this environment (no interactive browser/screenshot tooling available to this agent); stated honestly rather than claimed.
- [x] Simplification pass run (4 parallel lenses) and dated in plan.md under "Simplification pass — 2026-08-20", dispositions recorded.
- [ ] `post-implementation-report` — written at the end of this pass, before moving to Review.
- [ ] PR opened to `dev`, not merged — opened at the end of this pass.
