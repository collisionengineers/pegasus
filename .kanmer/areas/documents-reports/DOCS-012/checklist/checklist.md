# Checklist — DOCS-012

## Before any code

- [ ] Ask the operator Q1 (plan, Open questions): the third-party vehicle evidence
      control sets no semantic role — it is the only way to keep an image out of the EVA
      bundle. Confirm before removing it.
- [ ] Obtain the authentic Lucide v0.344.0 `trash-2` vector. Do not draw one, do not
      substitute.

## Design authority (`docs/design/README.md`) — do first

- [ ] Rewrite the Evidence/document panel contract row at `:617`.
- [ ] Amend `:731` so staff upload names the Upload surface, not the case page.
- [ ] Add the `trash-2` row to the glyph table (`:353-376`) with its SHA-256.
- [ ] Update the sprite SHA-256 at `:346` from the committed bytes.
- [ ] Change "sixteen" to "seventeen" at `:346` and `:1065`.
- [ ] Restate `:350` as a rule (a non-decorative icon carries its own accessible label)
      rather than an observation that every icon today is decorative.

## Icon asset

- [ ] Add `trash-2` to `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg`.
- [ ] Add the matching `<symbol id="icon-trash-2" viewBox="0 0 24 24">` to
      `src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml`.
- [ ] Confirm both carry identical vectors.

## The automatic note

- [ ] Add a private `AddRemovalNote` helper to `EfDocumentCustodyStore` writing one
      `CaseWorkflowEventEntity` — **`context.CaseWorkflowEvents`, not `CaseHistory`**.
- [ ] Capture `BeforeVersion` before `CaseMutationGuard.Complete`, `AfterVersion` after.
- [ ] Call it inside the existing transaction, before `SaveChangesAsync` (`:458`).
- [ ] Confirm the replay early-return (`:437-445`) happens before the note is written.
- [ ] Add `"case_document_removed" => "File removed"` to `OperatorLabels.HistoryEvent`.

## The panel — `_CaseDocuments.cshtml` lines 1–134 only

- [ ] Heading `Document custody` → `Files`.
- [ ] Box folder link becomes `.btn .btn--dark` with the `arrow-right` glyph; keep the
      existing `CustodyFolderState` fallback for a folder that is not confirmed.
- [ ] Replace the triple loop with one row per occurrence joined on
      `occurrence.VersionId`.
- [ ] Filter `IsCurrent && !IsLogicallyRemoved && CustodyStatus == Confirmed`.
- [ ] Columns `File | Type | From | Size | Added` using `OperatorLabels.DocumentRole`,
      `DocumentOrigin`, `FileSize`, `OfficeTime`.
- [ ] Delete the export form, the `Export`/`Revision state`/`Custody` columns, the
      `EVA eligibility` cell, both inline reason inputs, both buttons, the per-occurrence
      hidden forms and the `Retain document` form.
- [ ] Delete the empty-state paragraph; render nothing when there are no rows.
- [ ] Add the per-row trash button: `.btn .btn--icon`,
      `data-dialog-open="remove-doc-<occurrenceId>"`,
      `aria-label="Remove <filename>"`, `aria-hidden` svg.
- [ ] Add a per-row `_ReasonDialog` partial with the five hidden fields the current form
      posts and **no** `DialogConsequence`.
- [ ] No new operator-facing sentences anywhere in the diff.
- [ ] **Verify lines 136–167 are byte-identical** — CASE-022 owns them.

## Retirements

- [ ] `Custody.cshtml.cs`: delete `OnPostUploadDocumentAsync`, the `IAddCaseDocument`
      ctor param, `MaximumStaffUploadBytes`, `SafeMediaType`.
- [ ] `Details.cshtml.cs`: delete `DocumentSemanticRoles`.
- [ ] `CaseMutationPageModel.cs:81`: drop `"semanticRole"`.
- [ ] `Documents/Export.cshtml.cs`: delete `OnPostAsync`, `MaximumSelections`,
      `MaximumArchiveBytes`, `TryParseSelections`, `LogUnsafeDocumentExport`, the
      manifest-validation block and the `IExportCaseDocuments` ctor param. Keep
      `OnGetAsync`, `IExportCaseBundle`, `SafeArchiveName`, `IsSafeArchiveName`.
- [ ] `OperatorLabels.cs`: delete `CustodyState` (now callerless). Keep
      `CustodyFolderState`.
- [ ] Confirm `IAddCaseDocument`, `IExportCaseDocuments`, `ILogicallyRemoveDocument` and
      `IConfirmThirdPartyVehicleEvidence` are all still registered and resolvable — no
      Core or Infrastructure deletions.

## Tests

- [ ] New test in `DocumentCustodyDurabilityTests.cs`: removal writes exactly one
      `CaseWorkflowEvents` row with `case_document_removed`, the staff actor and the
      reason.
- [ ] Same test reads it back through `CaseDetails.History` — the row alone is not proof.
- [ ] Same test: a replayed removal adds no second row.
- [ ] Same test: two removals on one case both succeed (pins the
      `(CaseId, AfterVersion)` unique index clearance).
- [ ] `CaseCustodyWebTests.cs`: remove the upload assertions and `UploadForm`; rename the
      test off "Upload".
- [ ] `QdosCustodialWebTests.cs`: delete
      `CanonicalExportOwnerPostsSelectedVersionsToOneCoreCommand` and its unused support.
- [ ] `DependencyDirectionTests.cs:339`: remove the `IAddCaseDocument` assertion; leave
      `:340-341`.

## Verification

- [ ] `dotnet restore`.
- [ ] `dotnet build --configuration Release` clean — no unused `using` left behind.
- [ ] `dotnet test tests/Pegasus.Core.Tests` green.
- [ ] `dotnet test tests/Pegasus.ArchitectureTests` green.
- [ ] Integration suite green (~28 min, chunked, full log kept), specifically:
      `EvaHandoffPersistenceTests`, `AutomationDocumentIngressTests`,
      `AutomationMcpIngressTests`, `AssessmentEstimateImportWebTests`,
      `CustodyOutboxIntegrationTests`, `ProductionCompositionTests`,
      `ReadinessEndpointTests`.
- [ ] Browser check (`DevelopmentOffline` + LocalDB): one row per occurrence on a
      two-version document; Box button renders; trash button keyboard-reachable and
      announced; dialog traps focus, closes on `Escape`, returns focus; removal succeeds
      and the note appears on the **Notes** tab as `File removed` with the staff username;
      panel absent on a case with no confirmed files.
- [ ] Recomputed sprite SHA-256 matches what `docs/design/README.md:346` claims.
- [ ] Simplification pass run (`/simplify` + `code-simplifier`), findings and
      dispositions recorded in the plan under a dated heading.

## Follow-ups to file, not to fix here

- [ ] `custody_confirmed` / `custody_failed` are written to `CaseHistory` and never reach
      the Notes tab, though `OperatorLabels.cs:393-394` labels them.
- [ ] `AddCaseNote.MaximumLength = 2000` exceeds the `Reason` column's 500, and
      `_CaseHistory.cshtml:48` advertises 2000 to the operator.
- [ ] `EfCaseQueryStore.cs:181-195` truncates the timeline at 200 entries silently.
- [ ] Image documents appear both in the new table and in the "Instruction photographs"
      gallery below it.
