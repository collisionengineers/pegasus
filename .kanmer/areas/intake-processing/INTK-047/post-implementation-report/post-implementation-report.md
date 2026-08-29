# Post-implementation report — INTK-047

Branch `task/intk-047-upload-pages`, worktree
`../pegasus-worktrees/intk-047-upload-pages`, based on `origin/dev` at
`55e23b02`. Head `940a4053`.

## What changed

| Path | Change |
| --- | --- |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs:1049` | New nested `static class Upload` appended at the end. Nothing above it moved or reordered. |
| `src/Pegasus.Web/Pages/Upload.cshtml` | Ported to §1.10: `page-header` + one `panel`, the drawn dropzone words, the `.file-list` readout, Upload (`btn--primary`) and a native Clear. The "What happens next" steps and the "Accepted files" chips are deleted. |
| `src/Pegasus.Web/Pages/Upload.cshtml.cs:36-41` | `AcceptedFiles` replaces the view's two hand-composed limit sentences; `MaximumFileCount` (its only caller was that markup) is gone. No validation or limit changed. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml` | `page-header` with the state as `<h1>`, Refresh and Upload another file in `page-actions`, `panel-head`/`panel-body`, `definition-list`. `data-auto-refresh` and the exact `<h1>` shape preserved. |
| `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml` | Same header treatment; the decision card on `panel-head`/`field`/`btn`; the member list as `.file-list` with one `.file-row` per member carrying its outcome. |
| `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml` | Already on the current `btn`/`field`/`muted` vocabulary; only a redundant `stack` class removed. |
| `src/Pegasus.Web/Pages/Uploads/Request.cshtml` | External card: eyebrow, heading, dropzone, Submit. The paragraph explaining where the document goes is deleted. |
| `src/Pegasus.Web/wwwroot/js/site.js` | `[data-dropzone]` block only: `.file-row` vocabulary, the indeterminate native `<progress>`, a `reset` handler for Clear, form-scoped readout lookup. |
| `tests/…/Browser/UploadRowsBrowserTests.cs` | Row selectors follow the ported markup; **adds** the `<progress>` assertions. |
| `tests/…/Browser/UploadDropzoneBrowserTests.cs` | Two drop-target selectors follow the renamed markup. |

## Rule 14 — every named capability and its production caller

| Capability the ticket names | Production caller |
| --- | --- |
| `Pages/Upload.cshtml` ported | Route `/Upload` (`Upload.cshtml:1`), reached from the rail's Upload link and Ctrl U (`site.js:1382`) |
| Dropzone copy exactly as drawn | `Upload.cshtml:31-33`, rendered on every GET of `/Upload` |
| Multi-file rows with native `<progress>` | `site.js:236-240` builds one per row on the `change` of the `/Upload` file input; `site.js:331` reveals them on the real submit; the row container is `Upload.cshtml:38` |
| Upload + Clear | `Upload.cshtml:41` posts to `UploadModel.OnPostAsync`; `Upload.cshtml:45` is a native `type="reset"`, re-rendered by `site.js`'s reset listener |
| Per-file outcomes reusing `UploadCaseDecision` links | `UploadStatus.cshtml:69` and `UploadGroupStatus.cshtml:110,149` render `_UploadOutcome`, whose Attach form posts to `UploadConfirmationPageModel.OnPostAttachAsync`; the group card posts to `OnPostAttachGroupAsync` (`UploadGroupStatus.cshtml:65,87`) and `OnPostRegisterGroupAsync` (`:42,58`), and the search list is served by `OnGetCaseSearchAsync` via `:68` |
| `UploadStatus` / `UploadGroupStatus` ported | Routes `/Upload/Status/{id:guid}` and `/Upload/Group/{id:guid}`, which `UploadModel.OnPostAsync` redirects to |
| Public request card on the external shell | Route `/Uploads/{token}` (`Request.cshtml:1`), anonymous; submit at `:41` posts to `RequestModel.OnPostAsync` |
| The label list | `OperatorLabels.Upload` is read by all four views |

No capability here sits behind a feature gate, a disabled control, or a
registration with no consumer.

## Disabled seams drawn

**None.** §1.10 draws no uncomposed integration on any of these surfaces, so
nothing inert is rendered and no `.gated` span was added.

## Three reviewed divergences from the drawn contract

1. **"up to 25 MB each · 10 files"** is not ported verbatim. The prototype's
   fixture numbers are not this product's limits; the line is built from
   `IntakeEnvelopeLimits` (10 MB, 20 files) through `OperatorLabels.FileSize`,
   which keeps the ticket's own condition — limits unchanged — true.
2. **The public page shows no request reference and no expiry.** FRD-02
   §Request-scoped upload links binds it to the upload fields and its own
   success or failure and forbids case or reference identity;
   `RequestUploadPublicView` carries neither. The FRD is the behavioural
   authority and the design README is downstream of it.
3. **"Submit files" reads "Submit file".** `IUploadToRequest` accepts one file
   per attempt. Multi-file public upload is a Core change, not a port.

## Evidence

Windows + PowerShell 7, in this worktree.

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` | Exit 0 — Build succeeded, 0 Warning(s), 0 Error(s); `grep -c "error CS\|warning CS"` = **0** |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "(FullyQualifiedName~UploadConfirmationWebTests\|FullyQualifiedName~QdosIntakeWebTests\|FullyQualifiedName~QdosCustodialWebTests\|FullyQualifiedName~RecoveryTests\|FullyQualifiedName~UploadOutcomeQueriesTests\|FullyQualifiedName~GroupedIntakeWebTests\|FullyQualifiedName~ImageIntakeWebTests)&Category!=Corpus&Category!=Browser"` | Exit 0 — **71 passed, 0 failed, 0 skipped** |
| `dotnet test … --filter "(FullyQualifiedName~MultiFormatIntakeWebTests\|FullyQualifiedName~GroupedIntakeWebTests\|FullyQualifiedName~IntakeWebNegativeTests\|FullyQualifiedName~ImageIntakeWebTests)&Category!=Corpus&Category!=Browser"` | Exit 0 — **52 passed, 0 failed, 0 skipped** (taken at `95223e8a`, one commit before head) |
| `node --check src/Pegasus.Web/wwwroot/js/site.js` | Exit 0 |

No test run failed at any point on this branch.

**Not run, deliberately:** the full suite, the `Browser` category,
`scripts/Update-TestUiSnapshots.ps1` and `scripts/Test-UiCatalogue.ps1` — all
orchestrator-owned per `waves.md`. The two browser test classes this ticket
edits are therefore **compile-verified and syntax-verified but not executed**;
their first real run is the wave loop's. That is the one claim in this report
that rests on something other than an observed exit code, and it is stated as
such.

Test UI snapshots are **not** regenerated. The `upload--*`,
`upload-status--*`, `upload-group-status--*` and `upload-request--*` HTML
files are stale by design and must be regenerated on the merging branch. The
scenario needles all still render: `validation-summary-errors` is emitted by
the `asp-validation-summary` tag helper itself (kept on `Upload.cshtml:22`),
`data-auto-refresh="2000"`, `<h1>Complete</h1>`, `Open case`, `needs a staff
decision` and `Choose a document to upload.` are unchanged.

No feature flag was touched and no activation performed (D26).

## One file edited outside the lane, declared

`src/Pegasus.Web/wwwroot/js/site.js` belongs to PLAT-029 (wave 1, merged). The
Upload page's file rows are built there and nowhere else, so the ticket's
named `<progress>` capability has no caller without it. Ownership was checked
first: `git diff --name-only origin/dev...<branch>` over PLAT-025, PLAT-026,
PLAT-027, PLAT-049, ENG-027, DELIV-034 and CASE-027 shows none of them touches
the file, and there is no TICK-223 branch or worktree. The change is confined
to the `[data-dropzone]` block.

## Reported, not fixed

- Four legacy-block `site.css` rules now have the Upload surfaces as their
  only callers — `.upload-attach`, `.case-search-list`, `.upload-thumb`,
  `.upload-outcome-list` — with no new-vocabulary equivalent and legacy tokens
  in their bodies. **UIIMP-009 must promote them, not delete them.**
  Separately `.accepted-list` (`site.css:545`) loses its last caller here.
- `docs/design/README.md` §Component map does not list `upload-attach`,
  `case-search-list` or `upload-thumb`, which the confirmation surface needs.
- FRD-12 §Upload says a grouped upload's members "are never collapsed into one
  group-wide outcome", while the shipped page has collapsed them into a single
  submission-level decision card since INTK-011. Pre-existing, untouched here,
  and a documentation-versus-behaviour question for an owner to settle.
