# Research — DOCS-012

All paths repository-relative to `C:\Users\Alex\Documents\GitHub\pegasus`, read on
branch `dev`.

## 1. The automatic-note mechanism — found, and it is not the obvious one

**There are two case history tables. Only one is the "Notes" the operator reads.**

| Table | Entity | Written by | Read by |
| --- | --- | --- | --- |
| `CaseWorkflowEvents` | `CaseWorkflowEventEntity` (`src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs:36-52`) | 16 stores | **`EfCaseQueryStore.cs:181-195`** → `CaseDetails.History` → the Notes tab |
| `CaseHistory` | `CaseHistoryEntity` (`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:1163-1175`, `ToTable("CaseHistory")` at `:617`) | 15 sites | **nothing operator-facing** — one existence check at `EfTriageStore.cs:701` |

The UI surface: `src/Pegasus.Web/Pages/Cases/Details.cshtml:143-146` renders the third
tab as `Notes <span class="count">@details.History.Count</span>` on
`asp-route-tab="history"`; `Details.cshtml:236` renders
`src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml`, whose heading is
`<h2 …>Notes</h2>` (`_CaseHistory.cshtml:10`). Its table is
`When | Event | Actor | Detail` (`_CaseHistory.cshtml:19`), rendering
`OperatorLabels.OfficeTime(entry.OccurredAtUtc)`,
`OperatorLabels.HistoryEvent(entry.EventType)`, the actor (an `Automation` actor gets
a `status-chip` at `:27-30`) and `entry.Reason` as the note body.

**So "notes" = `CaseWorkflowEvents`.** The partial's own comment says it outright
(`_CaseHistory.cshtml:6-8`, CASE-017): "one timeline. What Pegasus did to the case and
what an operator wrote about it belong in the same account, so a note is a history
entry like any other and the Actor column is what tells them apart."

### The trap, already paid for once

`src/Pegasus.Infrastructure/Persistence/EfCaseNoteStore.cs:13-18` documents it:

> It must be `CaseWorkflowEventEntity` specifically: the Notes tab reads
> `CaseWorkflowEvents` (`EfCaseQueryStore`), and the first version of this store
> wrote to `CaseHistory` instead — a different table with a different purpose. The
> note was persisted, the page reported success, and the timeline stayed empty.

**Consequence for this ticket:** the document custody events that already exist —
`custody_confirmed` / `custody_failed`, written at
`EfQueuedCustodyProcessor.cs:601` and `EfExternalWorkStore.cs:454,612` — go to
`CaseHistory` only. They are **not on the Notes tab today**, even though
`OperatorLabels.HistoryEvent` carries labels for them (`OperatorLabels.cs:393-394`,
"Document stored" / "Document storage failed"). Those two label entries are
currently unreachable. That is a pre-existing gap; this ticket does not have to
close it, but the plan must not copy that pattern.

### The shape of an automatic note

Canonical example, `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs:512-577`
(private `AddHistory`), which writes the full triple in one unit of work:
`context.CaseWorkflowEvents.Add(...)` (the note), `context.ActionHistory.Add(...)`
(the audit ledger), `context.CaseHistory.Add(...)` (the versioned ledger). Required
fields on the note: `EventType`, `OperationKey`, `RequestHash`, `ActorKind`,
`ActorSubjectId`, `ActorRolesJson`, `Reason`, `OccurredAtUtc`, `BeforeVersion`,
`AfterVersion`.

Constraints that bind (`src/Pegasus.Infrastructure/Persistence/CaseWorkflowModelConfiguration.cs:50-64`):

- `Reason` `HasMaxLength(500)` — the note body is capped at 500 characters. The
  existing removal-reason input is already `maxlength="500"`.
- `EventType` `HasMaxLength(100)`.
- `HasIndex(CaseId, OperationKey).IsUnique()` — one event per operation key.
- `HasIndex(CaseId, AfterVersion).IsUnique()` — **one event per case version.** A
  mutation that calls `CaseMutationGuard.Complete(workflow)` claims a version; only
  one workflow event may carry it.

There is **no shared cross-store helper**: each store has its own private
`AddHistory` / `AppendHistory` (`EfCaseDataStore.cs:516`, `EfTriageStore.cs:857`).
Adding one to `EfDocumentCustodyStore` follows the convention rather than inventing
an abstraction.

**Actor.** "Created by system" in the operator's sense means *not typed by a person*,
not *attributed to nobody*. Every `CaseWorkflowEvents` writer records the command's
own actor (`request.Actor.Kind.ToString()` / `.SubjectId`), resolved to a name at read
time by `ActorDisplayNames.Resolve` (`src/Pegasus.Core/Actors/ActorDisplayNames.cs:51`).
`ActorKind.Automation` renders as the chip; a staff actor renders as the username.
A staff-initiated removal therefore records the staff actor, and the note is
"automatic" because the system wrote it, not the operator. `AddCaseNote` (the typed
note) is explicitly the *other* thing and refuses a non-`Staff` actor
(`src/Pegasus.Core/Cases/CaseNotes.cs:49-56`), event type `operator_note`
(`CaseNotes.cs:40`).

**No document operation writes a note today.** `EfDocumentCustodyStore` never touches
`CaseWorkflowEvents`. Logical removal (`EfDocumentCustodyStore.cs:419-461`) writes
*nothing* beyond the version flags. Third-party confirmation
(`EfDocumentCustodyStore.cs:542-551`) writes only an `ActionHistoryEntity` via
`DocumentActionHistory.Succeeded` — an internal audit/replay ledger, invisible to the
operator.

## 2. Which document changes must write a note

Recommended minimal set: **logical removal only.**

- **Removal — yes.** The operator asked for it, it is the change that makes a file
  disappear from the tab, and it is the one that replaces the `Revision state`
  column. Without it, removal becomes invisible.
- **A new version / a document arriving — no.** Custody already records
  `custody_confirmed` once per case source custody
  (`EfQueuedCustodyProcessor.cs:601`); a note per attachment would put ten rows on
  the timeline for one e-mail. If the operator later wants arrivals on the timeline,
  the cheap fix is to route the *existing* `custody_confirmed`/`custody_failed`
  writes to `CaseWorkflowEvents` as well — a separate ticket, and the labels already
  exist.
- **Third-party confirmation — no** (and see §4: the control's future is an open
  question).

## 3. What replaces the removal form

**The reason is not optional.** `LogicallyRemoveDocumentCommand.Reason` is a
non-nullable positional member (`src/Pegasus.Core/Documents/DocumentContracts.cs:145-153`),
enforced by `ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason)`
(`EfDocumentCustodyStore.cs:424`), persisted to `version.RemovalReason` (`:456`) and
part of the idempotent-replay identity — replaying with a different reason throws
(`:438-441`). A bare trash icon cannot satisfy it.

**Existing convention for a reasoned destructive action:**
`src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml` (56 lines), configured through
`ViewData` keys `DialogId`, `DialogTitle`, `DialogActionUrl`, `DialogHiddenFields`,
`DialogConsequence` (`:11-22`). It renders the backdrop, an `alert-triangle` glyph, an
optional consequence `<p class="notice">`, an antiforgery token, the hidden fields, a
`Reason for action` label with required marker, a `required` `<textarea name="Reason">`
and `Cancel` / `Confirm Action` (`:23-54`). Focus management, `Escape`, backdrop click
and focus-return are already implemented in
`src/Pegasus.Web/wwwroot/js/site.js:694-769` (bound by `[data-reason-dialog]`, opened
by `[data-dialog-open="<id>"]`, no inline handler — the deployed CSP forbids those).
It is the design authority's own component contract row (`docs/design/README.md:620`).

Its only callers today are two dialogs on `src/Pegasus.Web/Pages/Mail/Message.cshtml`
(`:429`, `:571`). Nothing under `Pages/Cases/` uses it. `DialogConsequence` is
supplied only where an approved sentence exists (`Message.cshtml:555-557` uses the
approved "Unlinking this email cancels case <reference>."); its own comment
(`_ReasonDialog.cshtml:17-21`) says absent otherwise, "so no dialog gains explanatory
prose it does not need". **Document removal has no approved consequence sentence, so
it passes none.**

**Icons.** `docs/design/README.md:334-376` — Lucide is the only approved system, the
sprite is checksummed (`src/Pegasus.Web/wwwroot/images/lucide-sprite.svg`, SHA-256
recorded at `:346`), inlined once per page by
`src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml` from `_Layout.cshtml:38`, and used
as `<svg class="icon" aria-hidden="true"><use href="#icon-…"/></svg>`
(e.g. `src/Pegasus.Web/Pages/Administration/Access/Index.cshtml:9`). `.icon` is
`site.css:753-765`.

**There is no trash/delete glyph.** The registry is sixteen glyphs
(`docs/design/README.md:353-376`): `search`, `user`, `refresh-cw`, `clock`,
`calendar`, `check-circle`, `alert-triangle`, `alert-circle`, `info`, `file-text`,
`filter`, `shield`, `chevron-right`, `arrow-right`, `upload`, `lock` — confirmed
against the sprite's sixteen `<symbol id="icon-…">` ids. `docs/design/README.md:1065`
closes the set: "Each semantic action or state uses one consistent icon everywhere,
drawn from the sixteen registered Lucide glyphs; generated or substitute replacement
glyphs are prohibited."

Two further rules bear on an icon-only control:

- `docs/design/README.md:350` — "Every icon rendered today is decorative and paired
  with a visible text label, so each carries `aria-hidden="true"`; **any future icon
  that is not decorative needs its own accessible label**." This is the sanctioned
  route for an icon-only button: `aria-label` on the button, `aria-hidden` on the svg.
- `docs/frd/frd-12-operator-experience.md:112-114` — the UI "never uses decorative
  glyphs as unlabeled controls". An `aria-label`led control is labelled.

`.btn--icon { padding: 5px 7px; }` exists at `src/Pegasus.Web/wwwroot/css/site.css:1638`
and has **zero usages in any `.cshtml`** — a dead class waiting for exactly this.
The compact button family is `.btn`, `.btn--dark`, `.btn--primary`, `.btn--light`
(`site.css:1614-1637`); the page-level family is `.primary-action` /
`.secondary-action` (`site.css:700-718`). The split is documented at
`site.css:1610-1611`.

No copy of Lucide exists in the repository outside the sprite itself (searched;
only `wwwroot/images/lucide-sprite.svg`, the `_LucideSprite.cshtml` partial and
build outputs). Obtaining an authentic `trash-2` vector is a real step, not an
assumption — see the plan.

## 4. "Semantic role shouldn't be user configurable"

**Correction to the ticket body.** The ticket's table pairs "Confirm third-party
vehicle evidence → Gone as a control" with "Semantic role is not
operator-configurable". Those are two different things and only one of them is a
semantic role.

**The only user-configurable semantic role in the Web project** is
`<select name="semanticRole">` at `_CaseDocuments.cshtml:130`, fed by
`DetailsModel.DocumentSemanticRoles` (`Details.cshtml.cs:107-108`, a bare
`Enum.GetValues<DocumentSemanticRole>()` with one caller and no other use anywhere
in the repository), bound at `Custody.cshtml.cs:79` and retained on refusal by
`CaseMutationPageModel.cs:81`. It dies with the "Retain document" form. There is **no
re-categorise action and no classification screen** — once an occurrence exists its
role is immutable through the UI, and `EfDocumentCustodyStore.cs:679` treats a
differing role on the same source identity as a replay mismatch. The role is
otherwise set automatically: `EfQueuedCustodyProcessor.cs:207` (`OriginalSource`),
`:311-313` (`Image` vs `Instruction` per attachment), `:348` (embedded photographs →
`Image`); `EfDocumentRequestStore.cs:264` and
`Pages/Cases/Assessment/Index.cshtml.cs:411` both hard-code `Other`. The MCP tool
`pegasus_document_add` also takes a role string (`src/Pegasus.Web/Mcp/DocumentMcpTools.cs:94`).

**The third-party confirmation does not set a semantic role.** It sets
`DocumentOccurrenceEntity.ThirdPartyVehicleConfirmedAtUtc` (+ reason + operation key)
at `EfDocumentCustodyStore.cs:535-537`. That field is read by:

1. `EvaHandoffStore.cs:90` — excludes the image from the EVA candidate query;
2. `EvaHandoffStore.cs:473` and `:768` — projects `IsThirdPartyVehicle`, filtered by
   `EvaHandoffPolicy.SelectEligibleImages` at
   `src/Pegasus.Core/Eva/EvaBundleSchema.cs:468`. Line `768` is on the
   `IExportCaseBundle` path — **the whole-case Export the ticket keeps depends on this
   field**;
3. `EfCaseQueryStore.cs:298-299` — the read projection for this very panel.

Nothing sets it automatically. Repo-wide, `EfDocumentCustodyStore.cs:535` is the only
assignment in `src/`, reachable only through
`CustodyModel.OnPostConfirmThirdPartyVehicleEvidenceAsync`. **Removing the control
removes the only means, human or machine, of excluding a third-party vehicle image
from the EVA bundle.** Proven by `tests/Pegasus.IntegrationTests/EvaHandoffPersistenceTests.cs:183`
(`StaffConfirmedThirdPartyVehicleImagesAreExcludedFromPreparationAndGeneratedBundle`).

Note what does *not* read it: the evidence gallery
(`EfIntakeReceiptStore.cs:1393-1416`) and the assessment/AI report projection
(`EfAssessmentReportProjectionSource.cs:49-107`) apply no third-party predicate.

## 5. What the Evidence tab should show afterwards

**The row set is currently wrong.** `_CaseDocuments.cshtml:44-49` loops
document → occurrence → **version** as a cartesian product, so a document with two
occurrences and three versions renders six rows, most of them pairing an occurrence
with a version it does not name. `DocumentOccurrence` carries `VersionId`
(`DocumentContracts.cs:52`): the correct row set is **one row per occurrence, joined
to the single version it names**.

The filter to use already exists and is the established convention — DOCS-007's
evidence-gallery query, `EfIntakeReceiptStore.cs:1393-1404`: `occurrence.VersionId`
join, `version.IsCurrent && !version.IsLogicallyRemoved && version.CustodyStatus ==
DocumentCustodyStatus.Confirmed`. That is literally the operator's rule — "If they
show here, they should be on box" — already implemented once. Applying the same
predicate in the partial needs **no new query**: `CaseDetails.Documents` already
carries every field (`EfCaseQueryStore` projects the full `CaseDocument` graph).

Concrete surviving fields, all available today:

| Column | Source | Label helper |
| --- | --- | --- |
| File (download link) | `version.FileName` → `/Cases/Documents/Download` | — |
| Type | `occurrence.SemanticRole` | `OperatorLabels.DocumentRole` (`OperatorLabels.cs:215-224`) |
| From | `occurrence.Source` | `OperatorLabels.DocumentOrigin` (`OperatorLabels.cs:227-236`) |
| Size | `version.ContentLength` | `OperatorLabels.FileSize` |
| Added | `occurrence.RecordedAtUtc` | `OperatorLabels.OfficeTime` |
| (action) | trash control, `mayEdit` only | — |

`OperatorLabels.DocumentRole` and `OperatorLabels.DocumentOrigin` are **both dead code
today** — zero callers anywhere in `src/` or `tests/`. The partial prints the raw enum
instead (`_CaseDocuments.cshtml:55`, `OriginalSource / Intake`), which is the "dev
speak leaking into UI" the operator named. Using them is reuse, not new work.

`OperatorLabels.CustodyState` (`OperatorLabels.cs:263-269`) has exactly one caller,
`_CaseDocuments.cshtml:58`, and becomes dead when the Custody column goes.
`OperatorLabels.CustodyFolderState` (`:277-281`) keeps its caller — the Box folder
line stays.

Observation, out of scope: image documents will appear both in this table and in the
"Instruction photographs" gallery below it (`Details.cshtml:176-203`, fed by
`EvidenceImages`), because both read the same occurrences. That duplication predates
this ticket and DOCS-011 owns the preview surface.

## 6. The design authority row, verbatim

Component contract table, header at `docs/design/README.md:605-606`
(`| Component | Required contract |`). The binding row, `docs/design/README.md:617`:

> `| Evidence/document panel | Original/source/version, logical removal and closed lock; Box/external state; issued report versions; exact Outlook evidence with separate discovery, link and sent times |`

(The ticket body paraphrases this as "Original/source/version/logical removal/closed
lock"; the exact text is above.)

Two other passages bind and need attention:

- `docs/design/README.md:724-740`, "Documents and external evidence", including
  `:731` "Provide authorised staff upload, view, download, and export actions" —
  the one line that reads as mandating the case-page upload.
- `docs/design/README.md:1045` — "Case evidence shows retained source images, their
  provenance, category, staff-confirmed third-party exclusions, and advisory
  findings. **It does not contain EVA or report-image selection/order controls**; the
  focused alpha exports every eligible Case-vehicle image…". This *already forbids*
  the selective-export tickboxes, and it *already names* "staff-confirmed third-party
  exclusions" as something Case evidence shows.

**The FRDs mandate none of it.** `docs/frd/frd-05-documents-extraction-and-custody.md`
is 46 lines and contains no "EVA", "export" or "select"; its `:29` and `:31` are rules
about what the system must not do and must record, never about what a page renders.
`docs/frd/frd-12-operator-experience.md` never mentions selective export, revision
state, EVA eligibility or logical removal; its only "show custody" requirement
(`:118-120`) is scoped to the Image-initiated Case detail, and it places the staff
upload on the Upload surface (`:31-38`, `:40-56`), not the case page. So the only
governing text being amended is `docs/design/README.md`.

## 7. Blast radius, per handler

Architectural note that reframes several assumptions: **there is no Core use-case
class for any of these four.** Core holds only the port interfaces and command
records in `src/Pegasus.Core/Documents/DocumentContracts.cs`; the single
implementation is `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs`
(one class implementing six ports, `:16-21`), composed at
`src/Pegasus.Infrastructure/DependencyInjection.cs:404-416`. And
`src/Pegasus.Web/Mcp/DocumentMcpTools.cs` — registered in production at
`src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs:114` — is a **second live consumer of
three of the four ports**.

| Handler | Page model | Port | Other production callers | Verdict |
| --- | --- | --- | --- | --- |
| `RemoveDocument` | `Custody.cshtml.cs:138-160` | `ILogicallyRemoveDocument` (`DocumentContracts.cs:206`) | **none** | Port survives — the control is being *replaced*, not deleted |
| `UploadDocument` | `Custody.cshtml.cs:74-136` | `IAddCaseDocument` (`DocumentContracts.cs:171`) | **two**: `Pages/Cases/Assessment/Index.cshtml.cs:405-418` (estimate import) and `Mcp/DocumentMcpTools.cs:146-159` (`pegasus_document_add`) | Port **must stay**; only the handler + form go |
| selective export POST | `Pages/Cases/Documents/Export.cshtml.cs:84-164` | `IExportCaseDocuments` (`DocumentContracts.cs:199`) | **one**: `Mcp/DocumentMcpTools.cs:253-349` (`pegasus_document_export`) | Port **must stay**; only the Razor form + `OnPostAsync` go |
| `ConfirmThirdPartyVehicleEvidence` | `Custody.cshtml.cs:162-184` | `IConfirmThirdPartyVehicleEvidence` (`DocumentContracts.cs:213`) | **none**, and nothing else can set the field | See §4 — capability loss, open question |

**The header export is a different port.** `Details.cshtml:122-129` is an `<a>` (a
GET) to `ExportModel.OnGetAsync` (`Export.cshtml.cs:30-82`), which calls
`IExportCaseBundle` (`src/Pegasus.Core/Eva/EvaBundleSchema.cs:237`, implemented at
`src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs:676`). It does **not**
share `IExportCaseDocuments`; the doc comment at `Export.cshtml.cs:20-29` states the
split. So the header export is unaffected by removing the POST — but the port stays
alive because of MCP, not because of the header.

Dead-with-the-POST inside `Export.cshtml.cs`: `MaximumSelections` (`:17`),
`MaximumArchiveBytes` (`:18`), `TryParseSelections` (`:179-216`),
`LogUnsafeDocumentExport` (`:226-229`), the manifest-validation block (`:121-136`).
`SafeArchiveName` / `IsSafeArchiveName` (`:166-177`) are shared with the GET and must
stay. `CaseNotInReviewException` (`DocumentContracts.cs:193-197`) is enforced only on
the `IExportCaseDocuments` path and stays with it.

Also dying with the upload form: `Custody.cshtml.cs:26` (`MaximumStaffUploadBytes`),
`:263-269` (`SafeMediaType`, used only by that handler),
`Details.cshtml.cs:107-108` (`DocumentSemanticRoles`), and `"semanticRole"` in the
retained-values set at `CaseMutationPageModel.cs:81`.

## 8. Tests

**No test file anywhere references `_CaseDocuments.cshtml`.** Verified independently:
the only non-source mention of the filename is
`design/planning-and-old-designs/PegasusClaudeDesign/github.md:23`.

**No test asserts any of the partial's operator-facing strings.** Checked one by one —
`Document custody`, `Retain document`, `Remove occurrence`, `Export selected versions`,
`Confirm third-party vehicle evidence`, `EVA eligibility`, `Revision state`,
`logically removed`, `No document occurrences are retained`: every one occurs only in
the partial itself (the two `Document custody` hits under `tests/` are a code comment
at `ProductionCompositionTests.cs:112` and an XML doc at
`AssessmentEstimateImportWebTests.cs:19`, neither an assertion).

**But the handlers are tested directly, and three test files break.** The task brief's
"no test asserts any of its strings" is true and misleading:

1. `tests/Pegasus.IntegrationTests/CaseCustodyWebTests.cs` — the binding test
   `CustodyPageBindsRetryUploadRemovalThirdPartyEvidenceAndRequestLinks` (`:17`) posts
   `Custody?handler=UploadDocument` (`:137-157`, including `("semanticRole", "Image")`
   at `:151`), `handler=RemoveDocument` (`:224-231`) and
   `handler=ConfirmThirdPartyVehicleEvidence` (`:233-240`), and asserts the resulting
   commands (`:69-95`) plus the empty-upload refusal message (`:133`, asserting
   `Custody.cshtml.cs:94`).
2. `tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs:149-199` —
   `CanonicalExportOwnerPostsSelectedVersionsToOneCoreCommand` posts to
   `/Cases/{id}/Documents/Export` and asserts `MaximumArchiveBytes == 100 MiB`
   (`:193`). **Dies with the POST handler.**
3. `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs:339` —
   `WebCustodialPagesHaveNoDormantTransportPath` (`:332`) asserts
   `Assert.Contains(typeof(IAddCaseDocument), custodyPageDependencies)` where the
   dependencies are `CustodyModel`'s constructor parameters. **Dropping the ctor
   param fails this test.** It also pins `ICreateRequestUploadLink` /
   `IRevokeRequestUploadLink` (`:340-341`), which this ticket does not touch.

Unaffected (they exercise the ports, not the page):
`CustodyOutboxIntegrationTests.cs` (`:196`, `:512`, `:777`, `:1165`),
`DocumentCustodyDurabilityTests.cs:16-63`, `EvaHandoffPersistenceTests.cs:183`,
`AssessmentEstimateImportWebTests.cs`, `AutomationDocumentIngressTests.cs`,
`AutomationMcpIngressTests.cs`, `ProductionCompositionTests.cs:59-63`,
`ReadinessEndpointTests.cs:63-65`.

**No browser/journey test drives any of the four controls.** Every file under
`tests/Pegasus.IntegrationTests/Browser/` was checked; `OperatorJourneyTests.cs:359`
uses `IAddCaseDocument` only to seed a case, never through the UI.

No `Pegasus.Core.Tests` unit test covers any of the four commands directly — all
behavioural coverage is integration-level.

## Premises: verified vs assumed

**Verified by reading code or docs (all citations above):**

- The Notes tab reads `CaseWorkflowEvents`, not `CaseHistory`; the two tables are
  distinct and `CaseHistory` has no operator-facing reader.
- `custody_confirmed` / `custody_failed` go to `CaseHistory` and therefore never reach
  the Notes tab, despite having labels.
- No document operation writes to `CaseWorkflowEvents` today.
- Removal reason is required at Core, persisted, and part of the replay identity.
- The sixteen-glyph registry contains no trash/delete glyph; the sprite matches.
- `.btn--icon` exists in CSS with zero Razor usages.
- `_ReasonDialog` exists, is JS-complete, and has no caller under `Pages/Cases/`.
- `OperatorLabels.DocumentRole` and `DocumentOrigin` have zero callers.
- `DetailsModel.DocumentSemanticRoles` has exactly one caller.
- The row loop is a cartesian product; `DocumentOccurrence.VersionId` makes the join
  exact.
- `IAddCaseDocument` and `IExportCaseDocuments` each retain other production callers;
  `ILogicallyRemoveDocument` and `IConfirmThirdPartyVehicleEvidence` do not.
- The header export uses `IExportCaseBundle`, a different port.
- `ThirdPartyVehicleConfirmedAtUtc` is set nowhere but the control being removed, and
  is read by the EVA bundle that the kept header export produces.
- The three named test files break; no test asserts the partial's strings.
- A staff Upload surface exists at `src/Pegasus.Web/Pages/Upload.cshtml`
  (`@page "/Upload"`, `[Authorize]` at `Upload.cshtml.cs:25`) and can attach a file to
  an existing case with a reason
  (`src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml:52-73`, "Add to an existing
  case"). Losing the case-page upload does not remove staff upload from the product.
- The FRDs do not mandate any of the removed columns.

**Assumed, not verified:**

- That the operator's "notes" means the tab labelled Notes rather than the invisible
  `CaseHistory` ledger. Strongly supported (it is the only thing an operator can see
  and it is literally called Notes), but it is an inference from a UI reading, not a
  statement they made.
- That an authentic Lucide `trash-2` vector can be obtained for the sprite. No copy
  exists in the repository; this needs a fetch from the upstream release.
- That no consumer outside this repository reads the `Export` POST route.
- That the `(CaseId, AfterVersion)` unique index leaves the removal's post-`Complete`
  version unclaimed. True today because removal writes no event, but it must be
  asserted by a test rather than assumed.
