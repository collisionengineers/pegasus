# Files — INTK-047

## Owned by this ticket, edited here

| Path | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Upload.cshtml` | Port to §1.10: header only, one panel, drawn dropzone copy, `.file-list` readout, Upload + Clear |
| `src/Pegasus.Web/Pages/Upload.cshtml.cs` | Accepted-files line moves to `OperatorLabels.Upload`; no behaviour change |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml` | Design-system header, panel, `definition-list`, `btn` family |
| `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml` | Same, plus `.file-list`/`.file-row` member rows |
| `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml` | Restyle to the `btn`/`muted` vocabulary |
| `src/Pegasus.Web/Pages/Uploads/Request.cshtml` | External card: eyebrow, heading, dropzone, Submit; explanatory paragraph deleted |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | **Append only**, inside a new nested `static class Upload`. No existing member reordered |
| `tests/Pegasus.IntegrationTests/Browser/UploadDropzoneBrowserTests.cs` | Two selectors follow the renamed markup |
| `tests/Pegasus.IntegrationTests/Browser/UploadRowsBrowserTests.cs` | Row selectors follow the new vocabulary; adds the `<progress>` assertion |

Not edited, though owned: `UploadConfirmationPageModel.cs`,
`Presentation/UploadOutcome.cs`, `UploadCaseDecision.cs`,
`UploadConfirmationWebTests.cs`, `MultiFormatIntakeWebTests.cs`,
`Browser/UploadCaseSearchBrowserTests.cs`,
`Browser/UploadStatusRefreshBrowserTests.cs`. Their behaviour and their
assertions are unchanged by a restyle, which is the point.

## Not owned by this ticket, edited anyway — declared loudly (D19 §2)

| Path | Owner | Why |
| --- | --- | --- |
| `src/Pegasus.Web/wwwroot/js/site.js` | PLAT-029 (wave 1, **merged**) | The Upload page's file rows are built here and nowhere else. The ticket names "multi-file rows with native `<progress>`"; without this edit that capability has no caller at all (rule 14 / D20). Change is confined to the `[data-dropzone]` block: row vocabulary `dropzone__file-row` → `file-row`, an indeterminate `<progress class="progress">` while the batch is in flight, a `reset` listener so Clear empties the rows, and the readout lookup widened to the form. |

Ownership checked before editing: `git diff --name-only origin/dev...<branch>`
over every in-flight lane (PLAT-025, PLAT-026, PLAT-027, PLAT-049, ENG-027,
DELIV-034, CASE-027) shows **none** of them touches `site.js`. `git branch
--list 'task/*'` and `git worktree list` show no TICK-223 branch or worktree.

## Not edited — reported instead

| Path | Owner | Finding |
| --- | --- | --- |
| `src/Pegasus.Web/wwwroot/css/site.css` | PLAT-029 / UIIMP-009 | After this port the Upload surfaces are the **only** remaining callers of four legacy-block rules — `.upload-attach`, `.case-search-list`, `.upload-thumb`, `.upload-outcome-list` — and there is no new-vocabulary equivalent. They are written against legacy tokens (`--sp-*`, `--border`, `--charcoal`, `--paper`, `--focus-ring`) that the new palette does not define, so promoting them is a token rewrite, not a move. UIIMP-009 must promote, not delete. Separately, `.accepted-list` (`site.css:545`) loses its last caller here — §1.10 draws no "Accepted files" panel. |
| `docs/design/README.md` | UIIMP-006 | §Component map lists `dropzone`, `file-list`, `file-row`, `upload-outcome` for Upload but not `upload-attach`, `case-search-list` or `upload-thumb`, which the confirmation surface genuinely needs. |
| `src/Pegasus.Core/Documents/RequestUploadPolicy.cs` | no lane | Multi-file public upload and request-reference/expiry disclosure are Core changes, and the second is forbidden by FRD-02. Out of lane; see `research.md`. |
| `docs/design/test-ui/**` | UIIMP-005 / the merging branch | Snapshots deliberately not regenerated. |

## No overlap

No in-flight lane owns any path in the first two tables. `OperatorLabels.cs` is
the known shared file (`decisions-2026-08-29.md` §Two shared files); PLAT-025,
PLAT-026, PLAT-027 and PLAT-049 also append to it. Mine is a new nested class
appended at the end — a textual conflict at worst, never a semantic one.
