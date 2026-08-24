# Files — DOCS-012

## Web — the panel itself

| Path | What changes | Why |
| --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml` | Rewrite **lines 1–134 only**. Box folder link becomes a `.btn` with the `arrow-right` glyph. Row set becomes one row per occurrence joined to `occurrence.VersionId`, filtered `IsCurrent && !IsLogicallyRemoved && CustodyStatus == Confirmed`. Columns: File / Type / From / Size / Added / action. Drop the export form, the `Custody` column, the `Revision state` column, the `EVA eligibility` cell, the reason inputs, the `Remove occurrence` button, the `Retain document` form and the empty-state sentence. Add a per-row trash control opening `_ReasonDialog`. | The operator's control-by-control table; the cartesian-product loop (`:44-49`) is a defect; `docs/design/README.md:437` forbids the empty-state panel and the how-it-works cell |
| — **lines 136–167 unchanged** | The public-upload-request section is **CASE-022**'s and is blocked. Not read, not moved, not reformatted. | Ticket sequencing note |

## Web — page models and presentation

| Path | What changes | Why |
| --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Custody.cshtml.cs` | Delete `OnPostUploadDocumentAsync` (`:74-136`), the `IAddCaseDocument` ctor param (`:19`), `MaximumStaffUploadBytes` (`:26`) and `SafeMediaType` (`:263-269`). Keep `OnPostRemoveDocumentAsync` (`:138-160`) unchanged — the dialog posts the same fields. Keep the third-party handler pending the open question. | "Retain document" goes; leaving an injected port with no handler is the dormant-transport-path defect the architecture test exists to catch |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Delete `DocumentSemanticRoles` (`:107-108`) | Single caller was the deleted `<select>`; no other use in the repository |
| `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` | Remove `"semanticRole"` from the retained-proposed-values set (`:81`) | The field no longer exists to round-trip |
| `src/Pegasus.Web/Pages/Cases/Documents/Export.cshtml.cs` | Delete `OnPostAsync` (`:84-164`) and its POST-only members: `MaximumSelections` (`:17`), `MaximumArchiveBytes` (`:18`), `TryParseSelections` (`:179-216`), `LogUnsafeDocumentExport` (`:226-229`), the manifest-validation block (`:121-136`), and the now-unused `IExportCaseDocuments` ctor param (`:13`). **Keep** `OnGetAsync` (`:30-82`), `IExportCaseBundle` (`:14`), `SafeArchiveName`/`IsSafeArchiveName` (`:166-177`). | Selective export goes; the header export is a different port and stays. `docs/design/README.md:1045` already forbids selection controls |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Delete `CustodyState` (`:263-269`) — its only caller was `_CaseDocuments.cshtml:58`. `DocumentRole` (`:215-224`) and `DocumentOrigin` (`:227-236`) gain their first callers. `CustodyFolderState` (`:277-281`) unchanged. | One list per concept; a label method with no caller is dead code |

## Web — icon asset

| Path | What changes | Why |
| --- | --- | --- |
| `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg` | Add the authentic Lucide v0.344.0 `trash-2` vector as a seventeenth glyph | The registry has no delete glyph; the operator asked for a trash icon |
| `src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml` | Add the matching `<symbol id="icon-trash-2" viewBox="0 0 24 24">` | The inline partial is the runtime delivery of the checksummed asset and must carry identical vectors (`docs/design/README.md:346-348`) |
| `src/Pegasus.Web/wwwroot/css/site.css` | **No change expected.** `.btn--icon` already exists at `:1638`; `.icon` at `:753-765`; `.reason-dialog*` already styled | The existing convention wins |

## Infrastructure — the automatic note

| Path | What changes | Why |
| --- | --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs` | In `ILogicallyRemoveDocument.ExecuteAsync` (`:419-461`), inside the existing transaction and before `SaveChangesAsync`, add one `CaseWorkflowEventEntity` via a new private `AddRemovalNote` helper: `EventType = "case_document_removed"`, actor from `command.Actor`, `Reason = command.Reason.Trim()`, `OperationKey`/`RequestHash` from `command.OperationKey`, `BeforeVersion` = pre-`Complete` version, `AfterVersion` = post-`Complete` version | The operator's rule 2. Shape copied from `EfCaseNoteStore.cs:48-63`; per-store private helper is the convention (`EfCaseDataStore.cs:516`, `EfTriageStore.cs:857`) — no new abstraction |

## Documentation — governing document amended

| Path | What changes | Why |
| --- | --- | --- |
| `docs/design/README.md` | (a) rewrite the Evidence/document panel row at `:617`; (b) amend `:731` so staff upload names the Upload surface, not the case page; (c) extend the icon registry — the count word at `:346` and `:1065`, the sprite SHA-256 at `:346`, and a new `trash-2` row with its glyph SHA-256 in the table at `:353-376` | Operator instruction of 2026-08-24 outranks the design authority, but the authority must be rewritten in the same change or the code contradicts a governing document |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | **No change.** Verified it mandates none of the removed columns | — |
| `docs/frd/frd-12-operator-experience.md` | **No change.** Verified its only custody-display requirement (`:118-120`) is the Image-initiated Case detail | — |
| `docs/operator-notes.md` | **No change** — protected | — |
| `docs/current-architecture.md`, `docs/operations.md` | **No change** — this ticket ships no deploy | Refresh is a post-deploy obligation |

## Tests

| Path | What changes | Why |
| --- | --- | --- |
| `tests/Pegasus.IntegrationTests/CaseCustodyWebTests.cs` | Drop the upload assertions and `UploadForm` (`:36-38`, `:69-79`, `:127-134`, `:137-157`), rename the test off "Upload" (`:17`). Keep the removal (`:224-231`) and request-link parts. Third-party part follows the open question | The handler it posts to is gone |
| `tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs` | Delete `CanonicalExportOwnerPostsSelectedVersionsToOneCoreCommand` (`:149-199`) and its now-unused support (`:157-158`) | The POST route it exercises is gone; `IExportCaseDocuments` stays covered by `AutomationDocumentIngressTests.cs` |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Remove the `IAddCaseDocument` assertion at `:339` from `WebCustodialPagesHaveNoDormantTransportPath` (`:332`). Leave `:340-341` | The port is no longer a `CustodyModel` dependency; keeping the assertion would demand the dormant path the test forbids |
| `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` | **Add** a test asserting logical removal writes exactly one `CaseWorkflowEvents` row with `EventType = "case_document_removed"`, the staff actor and the reason — and that it is visible through `CaseDetails.History` | This is the new behaviour; the `CaseHistory`-vs-`CaseWorkflowEvents` trap (`EfCaseNoteStore.cs:13-18`) was found only by running the page, so it must be pinned by a test |
