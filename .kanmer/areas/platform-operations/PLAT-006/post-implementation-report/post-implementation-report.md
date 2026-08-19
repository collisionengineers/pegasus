# Post-implementation report — PLAT-006

Branch `task/plat-006-shell-upload` (2 commits: `737fefce`, `50151330`), worktree `../pegasus-worktrees/plat-006-shell-upload`, cut from `origin/dev` `60fde326`.

## What shipped

Presentation only — 6 files under `src/Pegasus.Web` + `docs/design/README.md`. `Pegasus.Core`, `Pegasus.Infrastructure`, `Pegasus.Worker`, page models and tests untouched.

| Change | Where |
| --- | --- |
| Content region centred in the space beside the rail (`margin-inline: auto` on the existing 1280px cap); one-column reflow gets `grid-template-rows: auto minmax(0,1fr)` so the shell's 100vh minimum no longer stretches a blank band under the rail | `site.css` rail block + ≤1023px block |
| Dropzone: whole dashed area is a drop target, `Choose file` button (`.btn`) drives the native picker, chosen name + size read back into a `role="status"` live region, `is-dragover` / `has-file` states, tinted glyph disc; native input becomes `.sr-only` **only when script enhances it** and stays in the tab order with `:focus-within` ring on the zone | `site.js` (`[data-dropzone]` block), `site.css`, `Upload.cshtml`, `Uploads/Request.cshtml` |
| Upload screen: two-column `.upload-layout` (form + "What happens next" / "Accepted files" panel), one column ≤1023px | `Upload.cshtml`, `site.css` |
| Upload status page shares the bounded `form-column--wide` | `UploadStatus.cshtml` |
| Design authority: "The content region is bounded and centred" recorded under the rail section | `docs/design/README.md` |

Copy in the new panel is traceable: receipt-before-redirect (`Upload.cshtml.cs`), self-refreshing status page (`UploadStatus` `data-auto-refresh`), Complete → case or retained receipt (`UploadStatus.cshtml`), formats from the `accept` list, size from `MaximumSizeLabel`. No invented data.

## Verification (this branch, Release, `--no-build` after a clean solution build)

| Suite | Result |
| --- | --- |
| `dotnet build Pegasus.slnx -c Release` | 0 warnings, 0 errors |
| Browser (axe + Playwright journeys, incl. `/Upload`, `/Uploads/{token}`, no-`[style]` guard) | 32 passed — run twice, before and after the simplification pass |
| Integration, filter `Upload|Intake|Request` (non-Corpus, non-Browser) | 127 passed |
| Integration, full non-Corpus non-Browser | 513 passed (9 m 33 s) |
| Local `DevelopmentOffline` at 1920, 1366, 1000: Upload, Upload status, Dashboard, Cases, Queues, Inbox, Operations, Administration | equal gutters at 1920; no change at 1366; one column + no blank band at 1000; drop of a `File` via `DataTransfer` selects it, readout `name · size`, drag-over turns the zone red even with a file already chosen; `[style]` count 0 |

Screenshots kept in the session scratchpad (`plat006-after-upload-1920.png`, `plat006-after-dashboard-1920.png`); the proof will carry the production capture.

## Simplification pass

Six findings, all applied — recorded with dispositions in `plan/` ("Simplification pass — 2026-08-19"). One was a defect in this branch's own diff (`.has-file` declared after `.is-dragover` masked the drag feedback) and was fixed here rather than ticketed.

## Not done / follow-ups

- Record pages (Case detail, Assessment, New case) were not in the sweep — the visual-QA database holds no cases; `/Cases/Create` without a `receiptId` returns 500 (`ArgumentException` from `LoadAsync`) rather than the designed status page. Small robustness follow-up, not this ticket.
- The Queues/Inbox empty-state marks render as 44px discs whose glyph is faint at that size — [[PLAT-004]] territory (mark placement), noted only.
- `docs/design/system` copies `site.css` at build time; a `/design-sync` refresh is owed once this merges (design-tool output, not runtime).

## Verification hand-off (kanmer-verify, on merged `main`)

- `dotnet restore ./Pegasus.slnx --locked-mode && dotnet build ./Pegasus.slnx -c Release --no-restore`
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj -c Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2`
- Production, after the release: at ≥1600px viewport `getComputedStyle(document.querySelector('.app-rail-main')).marginLeft` > 0 and equal to `marginRight`; `/Upload` shows the two-column layout, `Choose file` button visible (script enhanced) and no `[style]` attributes; screenshot at 1920.
