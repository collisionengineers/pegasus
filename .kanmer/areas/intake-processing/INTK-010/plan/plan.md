## Simplification pass (dated, run before the PR)

2026-08-20 — reuse decisions taken up front rather than found after the fact, per repo convention:
- Reused `_StatusChip.cshtml`'s existing tone/icon switch (extended, not duplicated) for per-file row state and per-outcome tone, instead of a second state table in `site.js`/CSS.
- Reused `IntakeDecisionPolicy.CanBecomeCase`, `IGetIntake`, `IQueuedIntakeStatusQueries`, `IImageIntakeQueries.GetByOriginReceiptAsync`, `IUnidentifiedStore.GetByOriginAsync` — all existing read ports — instead of a new query surface.
- Reused `/Intake/Details`, `/Cases/Create`, `/Cases/Details`, `/VehicleImages/{id}`, `/Unidentified/{id}` as the confirmation step's action targets instead of re-implementing attach/create/reverse inline (a second business implementation of a leased, replay-protected mutation is a stop condition per CLAUDE.md).
- Reused the existing `pegasus-spin` keyframe and `icon-refresh-cw`/`icon-check-circle`/`icon-alert-circle` glyphs; no new sprite glyph.
- One view-model builder shared by both status pages instead of two copies of the same seven-branch decision (one list per concept).

A second, post-diff simplification pass runs at the end of implementation (step 9) and its findings are appended here.

## Steps

1. **Drag-and-drop fix (done).** `site.js` document-level drop safety net + panel-wide effective target. Reuses the existing `.dropzone.is-dragover` CSS and the existing per-zone listener shape, widened. Tests: `UploadDropzoneBrowserTests.cs` (4, green).

2. **Per-file rows — markup and no-script fallback.** `Upload.cshtml`: replace the crammed `dropzone__file` readout with a `<ul>`/`<ol>` of rows (name, size, state), still populated by `site.js`'s existing `describe()`-equivalent so the no-script path (server-rendered list is not possible pre-submit, since files are chosen client-side; the no-script fallback is the native `<input multiple>` itself, unchanged) is unaffected — this mirrors the current file's own doc comment ("Nothing here is required: without script the input is simply visible"). Reuses `_StatusChip` for the per-row state chip (new keys below).

3. **Per-file rows — spinner/tick during and after submit (site.js).**
   - Fetch-submit the existing form (`FormData`, `fetch(form.action || location.href, { method: 'POST', body })`) instead of a native submit, **only when script is available**; a no-JS submit still works exactly as today (native POST/redirect), so this is additive, not a replacement contract.
   - On submit: build the row list from `input.files` (already known client-side — name/size are real, not guessed); every row enters the "uploading" state together (spinner) — the honest bound established in research.md (no finer signal exists during a single POST).
   - On a successful response (still a same-origin redirect target, since the handler contract is unchanged): the response's final URL tells us which status/group-status page to navigate to. Before navigating, there is no server-returned per-member manifest available to the client without changing the handler's response shape — changing it is out of scope ("do not invent a second upload endpoint" — a JSON member list would be a second response *shape* for the same endpoint's success path, which the ticket's spirit disallows unless it's the minimum needed for honesty). **Decision**: rows tick together on a successful response (this is still honest — the response *proves* the whole batch stored, matching the sequential-but-atomic-from-the-client's-view storage confirmed in research.md), then the page navigates to the existing status/group-status page, whose confirmation section (steps 5-7) is the truthful per-member detail from here on. A validation failure (the handler returns the same page, HTTP 200 with `ModelState` errors) is detected by response URL staying `/Upload`; rows are matched back to the per-file error text already produced by `Upload.cshtml.cs:78-94` (`"File {index+1} ..."`) by array index, and only the implicated row(s) show the failure chip — the rest return to the pending (not-yet-submitted) state, since nothing was stored.
   - New `_StatusChip` keys (extends the existing switch, `Pages/Shared/_StatusChip.cshtml:34-82`): `"uploading"` → neutral, `icon-refresh-cw`, spin; `"stored"` → green, `icon-check-circle` (green is "confirmed completion" — storage is a real completion, matching the tone rule in the partial's own doc comment); a per-file `"failed"` reuses the existing `"failed"` key (red, `icon-alert-circle`) already defined.

4. **Kill the mechanics copy.** Delete `UploadGroupStatus.cshtml:16` (`"Each file has its own receipt and remains associated with this submission group."`) and re-read `Upload.cshtml`, `UploadStatus.cshtml` end to end for any other internal-mechanics phrase (design README:150-171 banned-terms list) — none found in research beyond this one line, but the final pass re-checks after all edits since new copy is being added in steps 5-7.

5. **View-model builder.** `Pegasus.Web/Presentation/UploadOutcome.cs`: given a `QueuedIntakeStatus` (+ actor, for the `IGetIntake` call), returns one `UploadOutcomeView` implementing the seven-branch decision table from research.md:
   - `Received`/`Processing` → "still working" (no offer; existing copy).
   - `Failed` → failure report (`OperatorLabels.IntakeFailure`), no offer.
   - `Complete` + `CaseId` present → report + "Open case" (`/Cases/Details/{id}`) + quiet "Not the right case?" → `/Intake/Details/{id}`.
   - `Complete` + `Decision == ImageIntakeRegistered` → report + link `/VehicleImages/{imageIntakeId}` (via `IImageIntakeQueries.GetByOriginReceiptAsync`).
   - `Complete` + an Open Unidentified item found (`IUnidentifiedStore.GetByOriginAsync`, checked at `Receipt(id)` then, for a grouped upload, `SubmissionGroup(groupId)`) → report + link `/Unidentified/{id}`.
   - `Complete` + `CaseMatchDecision?.Outcome == Ambiguous` → staff offer, "Review and attach" → `/Intake/Details/{id}`.
   - `Complete` + `IntakeDecisionPolicy.CanBecomeCase(Decision)` and none of the above → staff offer, "Create a case" → `/Cases/Create?receiptId={id}`.
   - `Complete` + `BlockedIntake`/`Unsupported`/`TechnicalFailure` and none of the above → report failure/refusal text (reuse the existing wording pattern from `Intake/Details.cshtml.cs` `DescribeRefusal`-equivalent language, not a new vocabulary).
   This function is called once per member; INTK-011's group-split race means two members of the same group can land in different branches, and the builder makes no group-level assumption — it is purely per-member.

6. **Confirmation partial.** `Shared/_UploadOutcome.cshtml` renders one `UploadOutcomeView`: state text (never a raw enum/GUID — `OperatorLabels`/existing label maps only), one action link where the table has one, a quiet secondary link where it has one. No new vocabulary invented — reuses "Open case", "Create a case" wording already present in `Cases/Create.cshtml`/`Intake/Details.cshtml`.

7. **Wire into both status pages.** `UploadStatusModel`/`UploadGroupStatusModel` gain `IGetIntake`, `IImageIntakeQueries`, `IUnidentifiedStore` (all already registered in DI for other pages — no new registration). The confirmation section renders once a member is terminal (`Complete`/`Failed`); `RefreshAutomatically` is unchanged (still governs the whole-page polling). `UploadGroupStatus.cshtml` renders the section per member inside the existing `@foreach`.

8. **CASE-003 fix.** `Cases/Create.cshtml.cs OnGetAsync`: guard `receiptId == Guid.Empty` (or route-unbound), `return NotFound();` before `LoadAsync` — exact approach CASE-003 specifies. One test: `GET /Cases/Create` (no query) → 404, existing `?receiptId=...` journey unchanged (already covered by existing Cases web tests, re-run to confirm no regression).

9. **Simplification pass over the whole branch diff** (`/simplify` + `code-simplifier`), findings and dispositions appended to this plan under a dated heading.

10. **Docs.** FRD-02 (upload/identity behaviour — per-file states, confirmation decision table verbatim from step 5) and FRD-12 (operator-facing surface description) updated in the same PR. `docs/capabilities.md` checked; INT-28's existing row already describes the automation this surface reports on, so no new capability ID unless the confirmation UI itself needs one distinct from INT-28/INT-32 — resolved during step 10 by reading `capabilities.md`'s existing INT-28/INT-32 rows against what changed (UI only, no new business rule) → **no new capability row expected**; recorded as a finding either way.

## Tests (see checklist.md for the itemised list)

- `dotnet build`; Core tests; integration filters `Upload|GroupedIntake|IntakeWeb|Cases`; Browser suite (already has Playwright browsers installed locally from this session's work).
- New: per-file row rendering (name/size, spinner→tick, failed-file state); confirmation offers for (a) located/attached case, (b) no case + images → Image-initiated report, (c) no case + instruction → create offer, (d) ambiguous match → attach-with-override offer; override reaches the chosen destination (exercised through the existing `/Intake/Details` link, not re-implemented); a failed file states failure; CASE-003 404.
