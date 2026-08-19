## Drag-and-drop (coordinator-assigned, done)

- [x] Document-level `dragover`/`drop` safety net in `site.js`.
- [x] Effective drop target widened to the containing panel.
- [x] Regression test: drop inside dashed zone (page-level synthetic) — green.
- [x] Regression test: drop inside dashed zone (CDP native) — green.
- [x] Regression test: drop on panel, outside dashed zone — red before the fix, green after.
- [x] Regression test: drop off the panel does not misroute files (navigation claim not provable via CDP in this harness — noted honestly).

## Part 1 — per-file rows

- [ ] `Upload.cshtml` renders one row per selected file (name, size), no run-on line.
- [ ] `site.js` fetch-submits the form; rows enter "uploading" together; tick together on a successful response; per-file failure text shown on a validation failure, matched by index.
- [ ] `_StatusChip` extended with `uploading`/`stored` keys; no colour-only state.
- [ ] No-JS fallback: native multipart POST/redirect still works unchanged.
- [ ] New test: rows render per file, spinner class present during submit, tick/failed class present after response.

## Part 2 — copy

- [ ] `UploadGroupStatus.cshtml:16` mechanics sentence deleted.
- [ ] Full re-read of `Upload.cshtml`/`UploadStatus.cshtml`/`UploadGroupStatus.cshtml` (post-edit) for any remaining "intake"/"receipt"/"custody"/GUID/mechanics wording — none found beyond the deleted line, confirmed after step 6-7 edits land.

## Part 3 — confirmation step

- [ ] `UploadOutcome.cs` view-model builder implements the seven-branch table; unit-testable independent of the page.
- [ ] `_UploadOutcome.cshtml` partial renders each branch's text/action without a raw enum/GUID.
- [ ] `UploadStatusModel`/`UploadGroupStatusModel` wired to the builder; confirmation shows once a member is terminal.
- [ ] Test: located/already-attached case → report + "Open case" link + reversal link.
- [ ] Test: no case, image group → Image-initiated report + `/VehicleImages` link.
- [ ] Test: no case, instruction document → "Create a case" offer → `/Cases/Create?receiptId=`.
- [ ] Test: ambiguous match → "Review and attach" offer → `/Intake/Details`; override reachable through that existing page.
- [ ] Test: failed file → failure state, no offer.
- [ ] Test: split group (one member attached, sibling Unidentified) renders both outcomes independently, not one group-wide outcome (INTK-011 awareness).

## CASE-003

- [ ] `GET /Cases/Create` (no `receiptId`) → 404, not 500.
- [ ] Existing `Cases/Create?receiptId=...` journey unchanged (existing tests still green).

## Docs

- [ ] FRD-02 updated (per-file states, confirmation decision table).
- [ ] FRD-12 updated (operator-facing surface description).
- [ ] `docs/capabilities.md` checked; new row added only if the review finds this surface uncovered by INT-28/INT-32 (expected: no new row, disposition recorded in plan.md step 10).

## Verification (whole ticket)

- [ ] `dotnet build --configuration Release`.
- [ ] Core tests green.
- [ ] Integration filters `Upload|GroupedIntake|IntakeWeb|Cases` green.
- [ ] Browser suite green (including `UploadDropzoneBrowserTests`, `AccessibilityTests` where applicable).
- [ ] Visual pass at 1920 (or honestly stated if not runnable in this environment).
- [ ] Simplification pass run and dated in plan.md, dispositions recorded.
- [ ] `post-implementation-report` written.
- [ ] PR opened to `dev`, not merged.
