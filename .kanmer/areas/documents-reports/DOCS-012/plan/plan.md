# Plan — DOCS-012

Cut the Evidence tab down to the files that are on Box, replace the removal form
with a trash control, record the removal as an automatic note on the Notes tab, and
rewrite the design-authority row that currently says otherwise.

## Not in scope — say so plainly

- **`_CaseDocuments.cshtml:136-167`, the public upload-request section.** It belongs
  to **CASE-022**, which is blocked on whether INT-31 is delivered at all. Do not
  edit, move, reformat or re-indent those lines. The diff must show zero change
  below line 134.
- **DOCS-011's preview work.** The trigger sits on a row inside this table; this
  ticket decides which surface survives and stops there. No preview, no lightbox, no
  thumbnail.
- **The "Instruction photographs" gallery and "Vehicle images" sections**
  (`Details.cshtml:176-233`). Image documents will appear both in the new table and
  in that gallery. That duplication predates this ticket.
- **Routing `custody_confirmed` / `custody_failed` onto the Notes tab.** They are
  written to the wrong table today (see research §1) and their labels are unreachable.
  Real, but a separate ticket.

## Governing docs touched

`docs/design/README.md` only — the Evidence/document panel contract row (`:617`), the
staff-upload line (`:731`) and the Lucide glyph registry (`:346`, `:353-376`, `:1065`).
Authority: the operator's message of 2026-08-24 quoted in the ticket body. No FRD
changes (verified none mandate the removed columns), no ADR, no
`docs/operator-notes.md`.

---

## Step 1 — Amend the design authority first

Do this before the code, so no commit contradicts a governing document.

**1a. The panel contract row.** Replace `docs/design/README.md:617`, currently:

> `| Evidence/document panel | Original/source/version, logical removal and closed lock; Box/external state; issued report versions; exact Outlook evidence with separate discovery, link and sent times |`

with:

> `| Evidence/document panel | The stored case files themselves — name, role, origin, size, arrival, download; the Box case folder; a reasoned per-file removal recorded on the case timeline; issued report versions; exact Outlook evidence with separate discovery, link and sent times |`

What changed and why, recorded in the diff message: per-file custody state, revision
state and logical-removal state leave the panel because the operator ruled them
internal vocabulary; the closed lock is unchanged behaviour (`mayEdit` already gates
every action on `workflow.Archive is null`) and stops being a rendered column;
"recorded on the case timeline" is the replacement history route.

**1b. The staff-upload line.** `docs/design/README.md:731` reads "Provide authorised
staff upload, view, download, and export actions." Amend to name where each lives:
upload on the Upload surface (`src/Pegasus.Web/Pages/Upload.cshtml`, which already
offers "Add to an existing case" with a reason at
`src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml:52-73`), view/download/export on
the case. This is a location correction, not a capability removal — verified the
capability survives.

**1c. The glyph registry.** See Step 2.

*Reuses:* nothing to build — this is editing prose in the file that already owns it.

## Step 2 — Add the `trash-2` glyph to the registry

`docs/design/README.md:353-376` registers exactly sixteen glyphs; there is no delete
glyph, and `:1065` closes the set. The operator asked for a trash icon, so the
registry grows by one — it is not a "generated or substitute replacement glyph"
provided the vector is the authentic upstream one.

1. Obtain the **Lucide v0.344.0 `trash-2`** SVG — the same release named at
   `docs/design/README.md:339`. No copy exists in the repository (verified); fetch it
   from the upstream release. **Do not hand-draw it** — `docs/design/README.md:344`
   bans hand-drawn icons and `:1065` bans substitutes.
2. Add it to `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg` in the same `<g>`
   form as its neighbours, and as `<symbol id="icon-trash-2" viewBox="0 0 24 24">` in
   `src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml`. Both must carry identical
   vectors (`docs/design/README.md:346-348`).
3. Recompute and update **two** SHA-256s: the sprite file's, at
   `docs/design/README.md:346`, and the new glyph's, in the table row. Add the row:
   `` | `trash-2` | <sha> | Per-file removal control on the case evidence panel (labelled by `aria-label`) | ``
4. Change "sixteen" to "seventeen" at `docs/design/README.md:346` and `:1065`.
5. Amend `docs/design/README.md:350` — it currently states that *every* icon rendered
   today is decorative. After this change one is not. The sentence already provides
   the route ("any future icon that is not decorative needs its own accessible
   label"); restate it as the rule rather than an observation about today.

*Reuses:* the existing sprite/partial/checksum mechanism end to end. No new asset
pipeline, no icon font, no inline one-off SVG.

## Step 3 — Write the automatic note on logical removal

In `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs`, inside
`ILogicallyRemoveDocument.ExecuteAsync` (`:419-461`), between
`CaseMutationGuard.Complete(workflow)` (`:457`) and `SaveChangesAsync` (`:458`), add
one `CaseWorkflowEventEntity` through a new **private** helper on the same class:

- `EventType = "case_document_removed"` (≤100 chars, snake_case like every neighbour)
- `ActorKind = command.Actor.Kind.ToString()`, `ActorSubjectId = command.Actor.SubjectId`,
  `ActorRolesJson = JsonSerializer.Serialize(command.Actor.Roles.OrderBy(r => r))`
- `Reason = command.Reason.Trim()` — the removal reason **is** the note body; column
  is `HasMaxLength(500)`, the input is already `maxlength="500"`
- `OperationKey = command.OperationKey`, `RequestHash = command.OperationKey`
- `BeforeVersion` captured **before** `Complete`, `AfterVersion` after it
- `OccurredAtUtc = timeProvider.GetUtcNow()` — the class already holds `timeProvider`

Then add `"case_document_removed" => "File removed"` to
`OperatorLabels.HistoryEvent` (`src/Pegasus.Web/Presentation/OperatorLabels.cs:374-398`).

Three things this must get right, all of them found in research and none obvious:

- **`CaseWorkflowEvents`, never `CaseHistory`.** The Notes tab reads
  `CaseWorkflowEvents` (`EfCaseQueryStore.cs:181-195`). Writing to `CaseHistory`
  persists the row, reports success and leaves the timeline empty — the exact failure
  documented at `EfCaseNoteStore.cs:13-18`. The existing `custody_confirmed` writes go
  to the wrong table and are invisible; do not copy them.
- **Same transaction.** The write already runs inside
  `BeginTransactionAsync`/`CommitAsync` (`:428`, `:460`). The note goes in it, so a
  removal is never recorded without its note or a note without its removal.
- **The unique indexes.** `(CaseId, OperationKey)` and `(CaseId, AfterVersion)` are
  both unique (`CaseWorkflowModelConfiguration.cs:61-62`). Exactly one event, carrying
  the version `Complete` just claimed. The early-return replay path at `:437-445`
  returns before any of this, so a replayed removal writes no second note.

*Reuses:* `EfCaseNoteStore.cs:48-63` for the entity shape; the private-`AddHistory`-
per-store convention (`EfCaseDataStore.cs:516`, `EfTriageStore.cs:857`); the
transaction, `timeProvider` and `CaseMutationGuard` already in the method. No new
port, no new Core type, no second note-writing implementation.

*Not added:* an `ActionHistoryEntity` or a `CaseHistoryEntity`. Neither is
operator-visible and neither is what the operator asked for.

## Step 4 — Rewrite the panel, lines 1–134

**Heading.** `Document custody` → `Files`. A label, not copy. ("Case custody" is
already taken by the Workflow tab section at `_CaseWorkflow.cshtml:352`, and "Case
evidence" is already a value there at `:359`.)

**Box folder.** Keep the existing condition at `:14-22` verbatim — a live link only
when `CustodyState == Confirmed` and a remote id exists, otherwise
`OperatorLabels.CustodyFolderState` plain text (design `:731` requires the
unavailable/pending states be shown rather than implying success). Turn the link into
the button the operator asked for by reusing the action-bar convention already on this
page — `class="btn btn--dark"` with `<svg class="icon" aria-hidden="true"><use
href="#icon-arrow-right"/></svg>`, exactly as `Details.cshtml:132` ("Open assessment")
does. `arrow-right`'s registered usage is already "Action transition and external link
indicator". Label text unchanged: `Open Box case folder`.

**Rows.** Replace the triple loop at `:44-49`. `DocumentOccurrence` names its version
(`DocumentContracts.cs:52`), so:

```
document → occurrence → the single version where version.Id == occurrence.VersionId
```

filtered `version.IsCurrent && !version.IsLogicallyRemoved && version.CustodyStatus ==
DocumentCustodyStatus.Confirmed`. That predicate is not invented: it is the DOCS-007
evidence-gallery filter at `EfIntakeReceiptStore.cs:1397-1401`, and it is the
operator's own rule — *"If they show here, they should be on box."* Every field is
already on `CaseDetails.Documents`; **no query changes**.

**Columns.** `File | Type | From | Size | Added` plus an unheaded action cell rendered
only when `mayEdit`.

| Cell | Expression |
| --- | --- |
| File | existing `/Cases/Documents/Download` link, `@version.FileName` |
| Type | `OperatorLabels.DocumentRole(occurrence.SemanticRole)` |
| From | `OperatorLabels.DocumentOrigin(occurrence.Source)` |
| Size | `OperatorLabels.FileSize(version.ContentLength)` |
| Added | `OperatorLabels.OfficeTime(occurrence.RecordedAtUtc)` |

`DocumentRole` and `DocumentOrigin` exist and have **zero callers today**
(`OperatorLabels.cs:215-236`). The current cell prints the raw enum pair
(`OriginalSource / Intake`, `:55`) — precisely the "dev speak leaking into UI" the
operator named. Giving two dead helpers their first caller is the reuse this step
owes.

**Gone:** the wrapping export form (`:30-33`, `:99-103`), the `Export` column
(`:42`, `:51-54`), `Revision state` (`:42`, `:56`), `Custody` (`:42`, `:58`),
`EVA eligibility` (`:42`, `:59-72`), the two inline reason `<label>`s and buttons
(`:74-90`), the per-occurrence hidden forms (`:105-122`) and the `Retain document`
form (`:125-133`).

**Empty state gone too.** Delete `:24-27`. `docs/design/README.md:437`: "In read-only
view, a section with nothing recorded and no available action is absent — not an
empty-state panel." With no upload control left there is no action, so when the filter
yields no rows the table simply does not render; the Box folder line still does.

**No new sentences anywhere.** Labels and values only. The necessary-copy list is
closed (`docs/design/README.md:396-410`) and nothing here qualifies.

*Reuses:* `.btn`/`.btn--dark` (`site.css:1614-1637`), `.icon` (`:753-765`),
`table-wrap`, `panel`, `section-label`, the existing download route, five existing
`OperatorLabels` methods, the DOCS-007 filter.

## Step 5 — The trash control and the reason dialog

Per row, when `mayEdit`:

```
<button type="button" class="btn btn--icon" data-dialog-open="remove-doc-@occurrence.Id"
        aria-label="Remove @version.FileName">
  <svg class="icon" aria-hidden="true"><use href="#icon-trash-2"/></svg>
</button>
```

and, once per row, `<partial name="Shared/_ReasonDialog" view-data="…" />` with

- `DialogId` = `remove-doc-<occurrenceId>`
- `DialogTitle` = `Remove file`
- `DialogActionUrl` = `Url.Page("/Cases/Custody", "RemoveDocument", new { id = workflow.CaseId })`
- `DialogHiddenFields` = `id`, `occurrenceId`, `expectedVersion`, `operationKey`
  (`CaseMutationPageModel.NewOperationKey()`), `editLeaseToken` — the same five the
  form at `:111-112` posts today
- `DialogConsequence` = **not set**

**Why a dialog and not a bare icon.** The reason is not decoration: `Reason` is a
non-nullable member of `LogicallyRemoveDocumentCommand`
(`DocumentContracts.cs:145-153`), rejected when blank
(`EfDocumentCustodyStore.cs:424`), persisted to `version.RemovalReason` (`:456`) and
part of the replay identity — a replay with a different reason throws (`:438-441`).
It is also now the note body (Step 3). `_ReasonDialog` is the design authority's own
component contract (`docs/design/README.md:620`) and is already complete: focus
placement, containment, `Escape`, backdrop dismiss and focus-return are in
`site.js:694-769`, bound by `[data-reason-dialog]` with no inline handler (the
deployed CSP drops those).

**No consequence sentence.** `_ReasonDialog:17-21` states the rule — a consequence is
supplied only from the approved necessary-copy list, absent otherwise. Removal has
none. Passing `null` is the correct and already-supported call.

**The handler is untouched.** `OnPostRemoveDocumentAsync` (`Custody.cshtml.cs:138`)
binds `string reason`; the dialog posts `name="Reason"`; ASP.NET model binding is
case-insensitive. Confirm this in the browser check, not by reasoning.

*Reuses:* `_ReasonDialog`, its JS, `.btn--icon` (`site.css:1638`, a class that exists
and has never been used), the `RemoveDocument` handler and its Core port, unchanged.

## Step 6 — Retire the upload and the export POST

**Upload.** Delete `OnPostUploadDocumentAsync` (`Custody.cshtml.cs:74-136`), the
`IAddCaseDocument` ctor param (`:19`), `MaximumStaffUploadBytes` (`:26`) and
`SafeMediaType` (`:263-269`); delete `DetailsModel.DocumentSemanticRoles`
(`Details.cshtml.cs:107-108`); drop `"semanticRole"` from `CaseMutationPageModel.cs:81`.

`IAddCaseDocument` itself **stays** — it has two other production callers, the
estimate import (`Pages/Cases/Assessment/Index.cshtml.cs:405-418`) and the MCP tool
`pegasus_document_add` (`Mcp/DocumentMcpTools.cs:146-159`). Nothing in Core or
Infrastructure is deleted.

Then `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs:339` fails, and
correctly: `WebCustodialPagesHaveNoDormantTransportPath` asserts every port injected
into `CustodyModel` has a live handler. Remove that one assertion; leave `:340-341`.

**Export POST.** Delete `ExportModel.OnPostAsync` (`Export.cshtml.cs:84-164`) and its
POST-only members — `MaximumSelections` (`:17`), `MaximumArchiveBytes` (`:18`),
`TryParseSelections` (`:179-216`), `LogUnsafeDocumentExport` (`:226-229`), the
manifest-validation block (`:121-136`) and the `IExportCaseDocuments` ctor param
(`:13`). Keep `OnGetAsync`, `IExportCaseBundle` and
`SafeArchiveName`/`IsSafeArchiveName` (`:166-177`), which the GET shares.

`IExportCaseDocuments` **stays** — the MCP tool `pegasus_document_export`
(`Mcp/DocumentMcpTools.cs:253-349`) calls it, and `CaseNotInReviewException`
(`DocumentContracts.cs:193-197`) is its rule. The header Export is a different port
entirely (`IExportCaseBundle`, `Export.cshtml.cs:14`, `:45`) and is untouched.

`docs/design/README.md:1045` already says Case evidence "does not contain EVA or
report-image selection/order controls", so this step brings the code *into* line with
the design authority rather than away from it. No amendment needed there.

## Step 7 — Tests

1. **New**, in `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`
   (beside `StaffConfirmationOfThirdPartyVehicleEvidenceIsDurableAndExactlyReplayable`,
   `:16-63`): a logical removal writes **exactly one** `CaseWorkflowEvents` row with
   `EventType = "case_document_removed"`, the staff actor's kind and subject, the
   trimmed reason, and `AfterVersion` equal to the case's new version — and it is
   visible through `CaseDetails.History`. Assert the *round trip*, not just the row:
   the `CaseHistory` mistake persisted a row and still showed nothing. Also assert a
   replayed removal adds no second row.
2. `tests/Pegasus.IntegrationTests/CaseCustodyWebTests.cs` — remove the upload half of
   `CustodyPageBindsRetryUploadRemovalThirdPartyEvidenceAndRequestLinks` (`:36-38`,
   `:69-79`, `:127-134`, `:137-157`) and rename the test. Keep removal (`:224-231`)
   and the request-link parts.
3. `tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs` — delete
   `CanonicalExportOwnerPostsSelectedVersionsToOneCoreCommand` (`:149-199`) and its
   unused support (`:157-158`). Coverage of `IExportCaseDocuments` survives in
   `AutomationDocumentIngressTests.cs`.
4. `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs:339` — as Step 6.

No test asserts any string in the partial (verified string by string), so the rewrite
needs no other test edits.

---

## Open questions — operator decisions, not mine

### Q1. The third-party vehicle evidence control

The ticket's table says:

> *"Confirm third-party vehicle evidence | **Gone as a control.** Semantic role is not
> operator-configurable."*

from the operator's:

> *"Semantic role shouldn't be user configurable"*

**The premise does not hold.** That control sets no semantic role. It sets
`ThirdPartyVehicleConfirmedAtUtc` (`EfDocumentCustodyStore.cs:535`), and that field is
the **only** thing that keeps a third-party vehicle's photograph out of the EVA
bundle — read at `EvaHandoffStore.cs:90`, `:473` and `:768`, filtered by
`EvaHandoffPolicy.SelectEligibleImages` (`src/Pegasus.Core/Eva/EvaBundleSchema.cs:468`),
proven by `EvaHandoffPersistenceTests.cs:183`. Line `:768` is on the
`IExportCaseBundle` path — the whole-case Export the ticket keeps. Nothing sets the
field automatically; the control is the only route, for a human or a machine. Removing
it means no one can ever exclude a third-party vehicle image again.

The actual user-configurable semantic role is the `<select name="semanticRole">` at
`_CaseDocuments.cshtml:130`, and it disappears anyway with the "Retain document" form
(Step 6). The operator's instruction is satisfied without touching this control.

The design authority also asks for it: `docs/design/README.md:1045` — "Case evidence
shows retained source images, their provenance, category, **staff-confirmed
third-party exclusions**, and advisory findings."

**Recommendation:** keep the confirmation as a per-row action; delete only the
`EVA eligibility` **column** (`:59-72`), which is banned how-it-works copy —
*"Eligible unless staff confirms third-party vehicle evidence"* is a sentence
explaining mechanics inside a table cell, exactly what
`docs/design/README.md:433-437` forbids. Render the confirmed state as a value, not a
sentence, and the action as a labelled control. If the operator confirms they want the
capability gone as well, that is a separate ticket with its own EVA answer — not a
side effect of a UI tidy-up. **Do not implement Step 6's removal of this handler
until answered.**

### Q2. A file whose storage has not confirmed

The chosen filter shows only `CustodyStatus == Confirmed` versions, which is the
operator's *"If they show here, they should be on box"* read literally and matches the
existing DOCS-007 gallery filter. Consequence: a version still storing, or one whose
storage failed, does not appear on the Evidence tab at all.

**Recommendation: proceed as planned, no question needed** — case-level custody state,
its failure reason and the `Retry custody` action are already on the Workflow tab
(`_CaseWorkflow.cshtml:350-380`), so a failure is visible and actionable there. Flagged
here so the reviewer sees the consequence was chosen rather than overlooked. If the
operator later reports a file "missing", this filter is the first place to look.

### Q3. Two names for the same thing

The heading becomes `Files`. `Case custody` (`_CaseWorkflow.cshtml:352`) and the value
`Case evidence` (`:359`) already exist on the Workflow tab, and the tab itself is
called `Evidence`. `Files` avoids all three. A label, so it is inside the rules — noted
only because the operator may prefer a different word.

---

## Verification

**Build.** `dotnet restore` then `dotnet build --configuration Release`. The removed
`IAddCaseDocument` / `IExportCaseDocuments` ctor params must not leave unused `using`
directives; Release build is warnings-as-errors on this repository's profile.

**Core tests.** `dotnet test tests/Pegasus.Core.Tests` — expected wholly unaffected;
run to prove Core was not touched.

**Architecture tests.** `dotnet test tests/Pegasus.ArchitectureTests` — must pass with
the single assertion removed. This is the test that proves no injected port is left
dormant.

**Integration tests.** The suite takes ~28 minutes; chunk it and keep the full log.
Assert specifically:

1. The new removal-note test passes and, crucially, reads the note back through
   `CaseDetails.History` — a row in the wrong table would still "pass" a
   row-count-only assertion.
2. `DocumentCustodyDurabilityTests` third-party tests still pass (they must, whichever
   way Q1 lands, because the port is untouched).
3. `EvaHandoffPersistenceTests.StaffConfirmedThirdPartyVehicleImagesAreExcludedFromPreparationAndGeneratedBundle`
   still passes — the guard on Q1.
4. `AutomationDocumentIngressTests` and `AutomationMcpIngressTests` pass — proof the
   MCP callers of `IAddCaseDocument` and `IExportCaseDocuments` are intact.
5. `AssessmentEstimateImportWebTests` passes — the other `IAddCaseDocument` caller.
6. `CustodyOutboxIntegrationTests.ExportingACaseProducesTheEvaFormatArchive` passes —
   the header Export.
7. `ProductionCompositionTests` and `ReadinessEndpointTests` pass — composition still
   resolves every port.

**Browser check** (local Web with `DevelopmentOffline` against LocalDB — the recorded
route for visual QA), on a case with at least one confirmed document and edit mode
held:

- the Evidence tab shows one row per occurrence, not a cartesian product — seed a
  document with two versions and confirm exactly one row;
- the Box button renders as a button with the arrow glyph;
- the trash button is reachable by keyboard, announces `Remove <filename>`, opens the
  dialog, traps focus, closes on `Escape` and returns focus to the button;
- confirming with a reason removes the file, the row disappears, and the note appears
  on the **Notes** tab as `File removed` with the reason as the detail and the staff
  username as the actor;
- the panel is absent entirely on a case with no confirmed files.

**Governing-doc check.** Diff `docs/design/README.md` and confirm the sprite SHA-256
recorded at `:346` equals the SHA-256 of the file actually committed. A stale checksum
is the failure mode this registry exists to prevent.

**Simplification pass.** Run `/simplify` plus the `code-simplifier` agent over the
branch diff before opening the PR; record findings and dispositions in this document
under a dated "Simplification pass" heading.

## Risks

| Risk | Handling |
| --- | --- |
| **The note lands in `CaseHistory` and the timeline stays empty.** The exact failure already made once (`EfCaseNoteStore.cs:13-18`), and the neighbouring `custody_confirmed` writes model the wrong pattern. | Step 3 names the table explicitly; the new test asserts the round trip through `CaseDetails.History`, not the row. |
| **Q1 ships a silent EVA regression.** Removing the third-party control makes the exclusion permanently unreachable while the header Export still depends on the field. | Blocked on the operator. Default is to keep the control and delete only the explanatory column. |
| **No authentic `trash-2` vector available.** The repository has no Lucide source and the design authority bans hand-drawn and substitute glyphs. | Step 2 fetches the v0.344.0 vector. If it cannot be obtained, stop and ask — do not draw one, and do not fall back to a text button without telling the operator their icon was declined. |
| **Checksum drift.** Two SHA-256s and a count word must all move together across `:346`, `:353-376` and `:1065`. | Explicit verification step; recompute from the committed bytes, not from the fetched file. |
| **CASE-022's section is disturbed.** It is in the same file, 31 lines below the edit. | Diff assertion: zero changes below line 134. |
| **An unused `using` or a dormant ctor param survives Step 6** and the architecture test passes for the wrong reason. | Release build plus the architecture suite; confirm the assertion was *removed* rather than the param kept. |
| **`_ReasonDialog` posts `Reason`, the handler binds `reason`.** Binding is case-insensitive, but that is a framework assumption, not a check. | Proven in the browser check, not by argument. |
| **N dialogs on a case with many files.** One hidden dialog per row. | The partial already renders one hidden form per occurrence, so the DOM cost is unchanged. A single shared dialog would need new JS and a new convention; rejected under "the existing convention wins". |
