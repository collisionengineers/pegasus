# Implement immediate estimate upload and drag-and-drop

Estimated change: approximately 10–14 existing files across the Case page, JavaScript, styles, tests, and documentation. No database migration, new storage service, or background worker is expected.

## 1. What the finished feature must do

An authorised Engineer can either:

- click **Import**, choose a file, and have it imported immediately; or
- drop a file anywhere inside the **Estimate section**, and have it imported immediately.

The action must work from read mode: acquire the normal Case edit lease automatically as part of importing.

On success:

1. The original document is stored through the existing Case document upload mechanism, which uses Box in the deployed configuration.
2. The existing provider parser reads it.
3. Pegasus creates a named Draft with the imported lines.
4. The Estimate section displays that Draft and its lines.
5. The Engineer remains in edit mode.
6. The original file appears under Case Files.

There must be no import confirmation dialog, “Retained estimate sources” panel, or **Complete import** button.

Import continues to create a Draft. **Use estimate** remains the separate professional acceptance action that makes an estimate Current.

### Terms used in this plan

| Term | Meaning |
| --- | --- |
| Case version | A number used to reject changes based on an outdated Case. |
| Edit lease | The existing token proving who currently has authority to edit the Case. |
| Document occurrence | The record associating a particular stored file with this Case. |
| Document version | The exact stored revision of that file. |
| SHA-256 | A fingerprint of the file bytes, used to identify the source and prevent duplicate estimate imports. |
| Custody confirmed | The storage mechanism has successfully retained the file and recorded its identities. |
| Draft | A saved estimate that has not necessarily been accepted or made Current. |

## 2. Preparation and file map

Use PowerShell 7 from the repository root:

```powershell
Set-Location C:\Users\Alex\Documents\GitHub\pegasus
git status --short
```

Preserve unrelated changes and untracked files. Do not modify `corpus/`.

Read the repository instructions, verification policy, and the relevant UI skills before implementation:

- `AGENTS.md`
- `docs/engineering.md`
- `docs/runbook.md`
- `.agents/skills/pegasus-ui-guardrails/SKILL.md`
- `.agents/skills/razor-pages-ui-implementation/SKILL.md`
- `.agents/skills/razor-pages-ui-review/SKILL.md`

Follow any references those skills require. Use the applicable .NET testing skills before changing or running tests.

### Production files

| File | Required work |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Replace the two-step upload orchestration; support read-mode import; remove pending-source UI data and completion handler. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml` | Replace the import dialog and completion panel with a direct file input/form and section drop surface. |
| `src/Pegasus.Web/wwwroot/js/case-workspace.js` | Bind the picker/drop behavior and integrate with existing submission, dirty-form preservation, and remounting. |
| `src/Pegasus.Web/wwwroot/css/case-workspace.css` | Add narrowly scoped drag-over and import-progress presentation. |
| `src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs` | Replace obsolete completion labels with upload, progress, and validation messages. |

Read and reuse these existing owners:

- `src/Pegasus.Core/Assessment/EstimateImport.cs`
- `src/Pegasus.Core/Assessment/Estimates.cs`
- `src/Pegasus.Core/Documents/DocumentContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs`
- `src/Pegasus.Web/wwwroot/js/site.js`

Do not create another provider parser, Box upload client, or estimate calculation implementation.

## 3. Step-by-step implementation

### Step 1 — Establish the current failure before removing its UI

In `Details.cshtml.cs`, follow:

```text
OnPostImportEstimateAsync
    → IAddCaseDocument.ExecuteAsync
    → acquire replacement edit lease
    → ImportRetainedEstimateAsync
    → IImportRawEstimate.ExecuteAsync
```

The existing handler already attempts an immediate import. The screenshot could represent a parsing failure, an interrupted action, or replay handling; it does not prove that every upload deliberately requires two clicks.

Inspect and extend the existing import tests to distinguish:

- successful storage followed by successful parsing;
- successful storage followed by parser rejection;
- successful storage followed by lease/version conflict;
- replay of an upload operation;
- storage failure.

Where the specific Audatex documents are available and authorised for local inspection, run them through the existing parser locally and record the actual refusal. Do not claim those particular PDFs are fixed based solely on synthetic test fixtures.

A genuinely unsupported or unreadable document must still produce a clear failure rather than fabricated lines.

### Step 2 — Define the direct upload form

Edit `_CaseEstimate.cshtml`.

1. Replace the two current Import branches—dialog opener and claim-lease-only form—with one Import control for an eligible Engineer.
2. Add a separate multipart form with:
   - ID `case-estimate-import-form`;
   - `data-estimate-import-form`;
   - handler `ImportEstimate`;
   - Case route ID;
   - `section=estimate`;
   - operation key;
   - expected Case version;
   - edit-lease token, empty when legitimately in read mode;
   - one file input named `estimateFile`.
3. Keep the form separate from the estimate editor form. Do not nest HTML forms.
4. Retain the accepted extensions and media types for PDF, XML, and JSON.
5. With JavaScript enabled, hide the native file input accessibly and make **Import** open it.
6. On selection, submit immediately. There is no separate confirmation button.
7. With JavaScript disabled, expose a native file input and submit button as the ordinary accessible fallback.
8. Remove the entire `import-estimate-dialog` markup.
9. Remove the entire retained-source panel and its completion forms.
10. Keep estimate tabs, editable rows, totals, Glass’s controls, and **Use estimate** in their existing locations.

Render the form for an eligible Engineer in both read and edit modes. Do not offer it for a lifecycle or actor that cannot edit the assessment.

### Step 3 — Add the section drop surface

In `_CaseEstimate.cshtml`, add a dedicated drop-target attribute to `#section-estimate` and a hidden overlay containing:

- the existing upload SVG icon;
- “Drop estimate to import”;
- “One PDF, XML or JSON file, up to 10 MB.”

The overlay should appear only during a file drag over the Estimate section. It must not permanently occupy space or cover the editor during normal use.

In `case-workspace.css`:

- scope all new selectors to the Estimate section;
- reuse the existing upload/dropzone colours, border treatment, typography, and icon;
- position the overlay relative to the panel;
- use `pointer-events: none` on the visual overlay so it does not interfere with drag events;
- provide a distinct unavailable/busy state;
- preserve panel size and page layout.

In `case-workspace.js`, add a root-scoped, idempotent binder using the existing `pegasusMountBinders` convention. It must work after lazy mounting and after a server response replaces the section.

The binder must:

1. Find the section, upload form, input, Import button, and overlay.
2. Open the file picker when Import is activated.
3. Submit through `form.requestSubmit()` after a valid file selection.
4. Detect file drags through `dataTransfer.types`, not `dataTransfer.files` during hover.
5. Use a drag-depth counter to prevent flicker when crossing child elements.
6. Set `dropEffect = "copy"` when the section can accept a file.
7. Clear the overlay on drag leave, drop, drag end, and cancellation.
8. On drop, validate the actual file list before submitting.
9. Prevent duplicate submissions while another Case command or this import is running.

Validate locally:

- exactly one file;
- non-empty;
- no larger than 10 MB;
- supported extension.

A browser may not expose filenames during hover. In that case, the overlay means the section accepts file drops; final format validation happens on drop. Do not claim to have recognised Audatex or Glass’s before the server examines the content.

Use the existing Case notice mechanism for invalid files. Keep server validation authoritative.

### Step 4 — Support read-mode import without weakening other handlers

Edit `OnPostImportEstimateAsync` in `Details.cshtml.cs`.

Keep `GuardEstimateEditAsync` strict for existing estimate-edit commands. Add an import-specific path or a narrowly scoped optional parameter whose default preserves existing callers.

For this handler, perform these operations in order:

1. Resolve the authenticated actor.
2. Verify Engineer role and assessment access.
3. Refuse a non-editable or archived Case through the existing authority rules.
4. Validate the operation key and submitted Case version.
5. Require exactly one submitted file, including rejecting multiple multipart files sent directly to the server.
6. Enforce the 10 MB limit against both declared length and bytes actually read.
7. If a lease token was supplied, use that token and let the existing authority checks reject an expired or conflicting token.
8. If no lease token was supplied from a legitimate read-mode request, acquire a lease using the submitted Case version.
9. Store the acquired authority in the browser session using the existing helpers.

Do not replace a stale submitted version with the latest database version to make the upload pass. Do not silently acquire a new lease in response to an invalid supplied token.

### Step 5 — Reuse storage and complete the import in the same action

Keep `IAddCaseDocument.ExecuteAsync` as the storage owner.

For a new source, retain the existing values:

```text
Semantic role: Other
Document source: StaffUpload
Source occurrence identity: estimate-import:{operationKey}
Document operation key: {operationKey}-document
```

Continue to pass the original filename, media type, file bytes, actor, expected Case version, and lease.

After storage:

- A fresh document consumes one Case version. Use exactly `submittedVersion + 1` and acquire the replacement lease before calling Core import.
- A replayed document does not consume another version. Continue with validated current request authority; do not increment the version or return “source retained.”
- A non-confirmed result is a storage failure for this synchronous staff-upload journey. Do not report import success or display a completion task.

Call `IImportRawEstimate.ExecuteAsync` with the retained occurrence, document version, and SHA-256.

On success:

- select the returned estimate ID;
- preserve the existing Draft naming and provenance behavior;
- restore the normal edit session after a new estimate save;
- report “Estimate imported”;
- return the Case page with `section=estimate` and the selected estimate.

The operation is one user action, but it still contains two existing persisted mutations. Do not hold a new database transaction open across Box upload and parsing or introduce a distributed transaction.

### Step 6 — Make retries reuse the source rather than uploading another copy

The previous plan overstated existing duplicate protection: the Core hash check prevents duplicate estimates, but it runs after document upload and does not by itself prevent duplicate stored documents.

Add source reuse to the Web orchestration:

1. Calculate the uploaded file’s SHA-256.
2. Under valid Case edit authority, inspect the Case’s confirmed live documents for an estimate-upload source with the same hash and length.
3. If one exists, use its occurrence and exact version directly with the canonical importer.
4. If several existing occurrences match, select the earliest by occurrence ordinal.
5. If none exists, use `IAddCaseDocument` normally.

Use the existing `CaseFiles.Live(...)` projection and `estimate-import:` source identity. Do not introduce a new query service or database index solely for this lookup.

This gives the normal retry action—choose or drop the same file again—the correct behavior:

- previously successful import → select the existing estimate;
- source stored but import interrupted → import that stored source;
- previous parser rejection → retry parsing the same retained source, without creating another Box copy.

The Core importer must still verify exact retained metadata and hash. A browser-supplied hash or occurrence is not authority.

Keep existing document-operation replay checks for repeated submissions of the same operation key.

### Step 7 — Preserve unsaved edits through the existing Case response mechanism

`case-workspace.js` already submits forms in place and preserves dirty editors using `data-editor-commit`. Use that mechanism rather than adding a separate fetch-and-replace implementation.

Changes required:

1. Recognise `case-estimate-import-form` as a Case command with its own operation key, submitted version, and lease.
2. Do not register it as an editable data form or mark choosing a file as unsaved Case data.
3. On a confirmed successful import, call `RecordEditorCommit` for the import form ID.
4. Keep the original submitted version in that acknowledgement, even though upload and import advanced the Case twice.
5. Advance other dirty forms’ authority only when the response confirms this operation and no intervening Case change occurred.
6. Preserve unrelated dirty Case and valuation forms and their input values.

If the estimate editor itself contains unsaved changes, stop before upload and show:

> Save or cancel the estimate changes before importing another estimate.

This prevents replacing the visible estimate editor and losing its unsaved rows. Other dirty sections do not block import.

On a refused or unknown response, preserve all dirty form values and original authority. Do not clear them merely because document storage may have succeeded.

Show an indeterminate “Importing estimate…” status during the command. Do not invent percentage progress for server-side Box storage or parsing.

### Step 8 — Remove the obsolete completion path

In `Details.cshtml.cs`, remove:

- `PendingEstimateSources`;
- `OnPostCompleteEstimateImportAsync`;
- the `"import-estimate"` dialog selection branch;
- code used exclusively to open that removed dialog.

Retain `ImportRetainedEstimateAsync` if it remains the shared private helper for the upload handler.

In `CaseWorkspaceLabels.cs`:

- remove `Complete`;
- remove `Sources`;
- remove `SourceRetained`;
- retain or update the success label;
- add the new drag, progress, invalid-file, and dirty-estimate messages.

Search the entire affected source and test tree for obsolete references:

```powershell
rg -n "PendingEstimateSources|CompleteEstimateImport|import-estimate-dialog|SourceRetained|Retained estimate sources" src tests docs
```

Remove obsolete callers and assertions. Do not retain an alias endpoint for the removed development-state UI.

Do not delete already-stored sources. They remain Case documents. There is no automatic bulk import or migration of previously incomplete sources; the Engineer can choose/drop the same file again.

### Step 9 — Keep failures visible and truthful

| Failure | Required result |
| --- | --- |
| Unsupported, empty, oversized, or multiple files | Show validation error; no upload or estimate. |
| Permission, lifecycle, lease, or version refusal | No upload; explain the refusal. |
| Box/document storage failure | No estimate; show storage failure. |
| Parser rejection after successful storage | Original remains in Case Files; show the parser reason; no partial Draft. |
| Conflict after storage but before estimate save | Retain source; preserve edits; show conflict. Retrying the upload reuses the source. |
| Lost HTTP response | Preserve unsaved edits; state that completion was not confirmed. A retry must not duplicate the source or estimate. |

Do not reinterpret parser failures as successful imports. Do not weaken parsing or reconciliation to make a PDF appear to work.

## 4. Documentation changes

Edit these existing owners:

1. **`docs/frd/frd-16-case-record-workspace.md`**
   - Replace whole-page drop wording with Estimate-section drop.
   - Document direct picker import and automatic read-mode lease acquisition.
   - State that successful import immediately displays the Draft’s lines.
   - Remove the pointer-only accessibility exception.

2. **`docs/frd/frd-12-operator-experience.md`**
   - Remove the estimate-import keyboard-accessibility exception.
   - Point to the keyboard-accessible Import action.

3. **`docs/frd/frd-25-repair-estimates-imports-and-glasss-sessions.md`**
   - Remove the explicit **Complete import** requirement.
   - Describe one-action staff upload, confirmed source storage, immediate parsing, and source reuse on retry.
   - Preserve canonical hash/provenance rules and Glass’s session-specific recovery.

4. **`docs/design/README.md`**
   - Replace whole-page drop and pointer-only wording.
   - Document the temporary section overlay, direct Import button, and existing upload visual language.

Do not add a new ADR or change deployed observations in `docs/operations.md` for this source-only feature change.

## 5. Tests and verification

### Automated tests to change

**`tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs`**

Update existing tests and add focused scenarios for:

- read-mode upload acquires authority and imports immediately;
- edit-mode upload uses the supplied authority;
- successful upload produces visible lines and selects the Draft;
- confirmed document replay continues automatically;
- dropping/selecting the same bytes again reuses the retained source and estimate;
- retry after retained-source failure reuses the original document;
- parser rejection retains the source with no partial rows;
- storage failure does not call the importer;
- stale authority, conflicting lease, non-Engineer, archived, and read-only Cases;
- multiple files, malformed formats, empty file, declared and streamed size limits;
- absence of the completion panel and endpoint;
- correct import commit acknowledgement.

Update the test doubles to model actual version/lease consumption and replay. Do not make the tests pass by returning a constant valid lease.

**`tests/Pegasus.IntegrationTests/CaseRecordFrameV26WebTests.cs`**

Add or update evidence for the import command acknowledgement and the existing frame’s response contract.

**`tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs`**

Update visibility expectations: eligible Engineers receive the direct Import action in read mode; ineligible actors and Cases do not.

Use existing Audatex, Glass’s PDF/XML, and JSON fixtures. Keep provider parser assertions in their existing test classes.

### Browser acceptance checklist

Run against the supported local profile with a disposable Case and an Engineer account:

1. In read mode, click Import and choose a valid Audatex PDF.
2. Confirm there is no intermediate confirmation or completion task.
3. Confirm imported rows appear and the Draft is selected.
4. Confirm the original appears in Case Files.
5. Drop a valid Glass’s PDF onto the middle, heading, and edge of the Estimate section.
6. Move the pointer across nested elements; confirm the overlay does not flicker.
7. Drag away or cancel; confirm the overlay disappears.
8. Drop outside the Estimate section; confirm no import and no browser navigation.
9. Try invalid and multiple files; confirm a clear error.
10. Repeat after lazy-loading the section and after an in-place response replaces it.
11. Leave an unrelated Case field unsaved, import, and verify the field value survives.
12. Leave estimate rows unsaved; verify import is refused before upload with the save/cancel message.
13. Use the keyboard to activate Import and select a file.
14. Repeat the same valid file; confirm no duplicate estimate or retained source.

Local storage or a mocked Box adapter proves application behavior only. Confirming an actual Box upload requires an explicitly authorised disposable Box target; do not use production evidence as a test destination.

### Commands

Name one person/agent as the heavy verifier and run commands sequentially. On this Windows workstation, use PowerShell 7 and the existing LocalDB setup.

Restore and build:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
```

Focused Core checks:

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~EstimateTests"
```

Focused integration checks:

```powershell
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Corpus&(FullyQualifiedName~AssessmentEstimateImportWebTests|FullyQualifiedName~CaseRecordFrameV26WebTests|FullyQualifiedName~CaseEngineerSectionsWebTests|FullyQualifiedName~AudatexEstimatePdfParserTests|FullyQualifiedName~GlassEstimatePdfParserTests|FullyQualifiedName~GlassEstimateXmlParserTests|FullyQualifiedName~AutomationAssessmentIngressTests|FullyQualifiedName~GlassRepairEstimateCallbackWebTests)"
```

Architecture and documentation:

```powershell
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
pwsh ./scripts/Test-DocumentationLinks.ps1
git diff --check
```

A successful HTTP test does not prove drag-and-drop works. Record the browser results separately.

## 6. Completion criteria and boundaries

The change is complete when selecting or dropping a supported estimate performs storage and import in one action, displays the parsed lines, preserves editing authority and unrelated pending edits, and leaves no completion panel.

Existing `IImportRawEstimate` and parser contracts remain the canonical import route. Native Glass’s session launch/callback recovery and MCP retain their current interfaces.

No database migration is planned. Persisting provider source totals, changing estimate arithmetic, and redesigning native Glass’s sessions are separate tasks.

Report the actual test results and any supplied-file parser limitations. Do not claim the two PDFs in the screenshot were successfully imported unless those files were actually exercised.
