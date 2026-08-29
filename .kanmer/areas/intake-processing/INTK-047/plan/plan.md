# Plan — INTK-047

A restyle. Every step names what it reuses; nothing here changes an upload
limit, an outcome, or a handler.

## 1. Labels

Append `OperatorLabels.Upload` — a new nested static class at the end of
`Presentation/OperatorLabels.cs`, nothing above it touched. Holds the drawn
dropzone words and builds the accepted-files line from
`IntakeEnvelopeLimits.MaximumContentLength` / `MaximumBatchFileCount` through
the existing `OperatorLabels.FileSize`. **Reuses:** `OperatorLabels.FileSize`,
`IntakeEnvelopeLimits`.

## 2. `Upload.cshtml`

Header only (`page-header` / `page-title`), one `panel` > `panel-body`, the
dropzone with its drawn `<h2>` label and value line, the `.file-list` readout,
then `button-row` with Upload (`btn btn--primary`) and Clear
(`<button type="reset" class="btn">` — native, works with no script).
Delete the "What happens next" panel and the "Accepted files" list: §1.10
draws neither, and both are how-it-works copy the page-economy rule forbids.
**Reuses:** PLAT-029's `.dropzone` / `.file-list` / `.btn` rules, the existing
`data-dropzone` / `data-dropzone-browse` / `data-dropzone-file` /
`data-upload-progress` script contract, `asp-validation-summary` (which is
what emits the `validation-summary-errors` Test UI needle).

## 3. `site.js` — the `[data-dropzone]` block only

Row markup moves to the design vocabulary (`file-row`, `<strong>` name,
`<small>` size, `.status` chip), each in-flight row gains an indeterminate
`<progress class="progress">`, a `reset` listener re-renders (so Clear empties
the rows), and the readout lookup falls back to the enclosing form so the
`.file-list` can sit outside the dashed area where the design puts it.
`data-file-row-status` / `data-state` — the attributes the browser tests key
on — are kept. **Reuses:** the existing `describe()` / `setRowStatus()`
functions and PLAT-029's `progress.progress`, `.status--*`, `.file-row` rules.
This is the one file another (merged, not in-flight) lane owns; see
`files.md`.

## 4. `UploadStatus.cshtml`

`page-header` with the eyebrow and `<h1>@Model.Heading</h1>` kept on one line
(three test classes pin `<h1>Received</h1>` etc. exactly), Refresh and Upload
another file as `btn` in `page-actions`, the facts as `definition-list` /
`definition`, `data-auto-refresh` untouched. **Reuses:** INTK-001's
`AutomaticRefreshMilliseconds`, `_UploadOutcome`, `OperatorLabels.OfficeTime`.

## 5. `UploadGroupStatus.cshtml`

Same header treatment; the submission-decision panel restyled to
`panel-head` / `panel-body` + `field` + `btn`; the member list becomes
`.file-list` with one `.file-row` per member, the per-member outcome partial
inside the row's content cell exactly as the prototype draws it.
**Reuses:** every handler, `_UploadOutcome`, `_StatusChip`, the existing
`data-case-search` contract.

## 6. `_UploadOutcome.cshtml`

Already on `btn btn--small`; only the `muted` state line and the attach
disclosure need the current vocabulary. `.upload-attach` / `.case-search-list`
class names stay — the script and the browser test bind to them and there is
no replacement in the new system (reported in `files.md`).

## 7. `Uploads/Request.cshtml`

External card body: `eyebrow` "Secure file request", `<h1>`, the compact
dropzone, `btn btn--primary` submit. Delete the paragraph "Documents submitted
here go directly to Collision Engineers…" — operator/recipient-facing
explanation, banned. `status-card` → `notice notice--success` (legacy →
current). Reference and expiry are **not** added: FRD-02 forbids it (see
`research.md`). **Reuses:** `_LayoutExternal` (which already supplies
`external-shell`, `auth-card`, the company logo), the existing `IGetRequestUpload`
policy view.

## 8. Tests

`UploadDropzoneBrowserTests`: the "inside the panel, outside the dashed area"
drop target moves from `#upload-title` (a section heading §1.10 removes) to
`.button-row`, and the off-panel target from `.page-heading` to `.page-header`.
Both are the same assertions against renamed markup.
`UploadRowsBrowserTests`: row selectors follow the new vocabulary and the
in-flight assertion **gains** a check that each row carries an indeterminate
`<progress>` (no `value` attribute — nothing fabricated). Strictly stronger.

## Verification

- `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false`
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "(FullyQualifiedName~UploadConfirmationWebTests|FullyQualifiedName~QdosIntakeWebTests|FullyQualifiedName~RecoveryTests|FullyQualifiedName~QdosCustodialWebTests|FullyQualifiedName~UploadOutcomeQueriesTests)&Category!=Corpus&Category!=Browser"`
- No full suite, no Browser category, no snapshot script (orchestrator-owned).

## Simplification pass — 2026-08-29

Run over the branch diff after implementation; findings and dispositions are
recorded below.

### Findings and dispositions — 2026-08-29

Run over `git diff origin/dev...HEAD` by an independent agent, four lenses.
All four findings **fixed**; none rejected, deferred or accepted as risk.

| Lens | Finding | Disposition |
| --- | --- | --- |
| Reuse | `Uploads/Request.cshtml` still held `Choose file` inline while every other word on that page had moved to `OperatorLabels.Upload` — the page's list was split across two places | **Fixed** — `RequestChoose` |
| Reuse | Same for the limit line `Up to {size}.` | **Fixed** — `RequestLimit(string)` |
| Simplification | `UploadGroupStatus.cshtml` wrote the same condition twice as De Morgan duals (`A is not null \|\| B is not null` and `A is null && B is null`), which have to be kept in step by hand | **Fixed** — one `reported` local |
| Efficiency | `memberState` (`Humanise(status.ToString())` plus a dictionary lookup) was computed for every member but read only on the still-moving branch | **Fixed** — computed inside that branch |

The pass reported no correctness bug and no scope problem.

### Not findings of the pass — declared separately

- `site.js` is edited here and belongs to PLAT-029. Reasoning and the
  ownership check are in `files.md`; it is not a simplification finding.
- Four legacy-block CSS rules keep the Upload surfaces as their only callers
  (`files.md`). Deferred to UIIMP-009, whose file it is and whose token
  rewrite it needs — disposition 4, with the reason.
- The public page's own `FormatBytes` is not folded into
  `OperatorLabels.FileSize`: the per-request limit can be sub-megabyte and
  `FileSize` is deliberately MB-only ("under 0.1 MB" for a 1 KB limit).
  **Rejected with reason** — the shared helper is genuinely unfit here.
