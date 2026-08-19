# Plan — PLAT-006

Branch `task/plat-006-shell-upload`, worktree `../pegasus-worktrees/plat-006-shell-upload`, PR to `dev`. Presentation only; no Core/Infrastructure change.

## Premises (checked, not argued)

- Production (`d8de29cb`, release 10) serves byte-identical `site.css` to `origin/dev` — verified with `curl`/`diff` 2026-08-19; the defect reproduces locally under `DevelopmentOffline` at 1920×1000.
- The Claude Design prototype (`screens/shared.jsx`) has `main { max-width:1280px; padding:20px 24px 32px }` left-aligned in a `236px minmax(0,1fr)` grid — the app matches it, so the fix is a refinement recorded in the design authority, not a divergence to argue.
- Operator decision (2026-08-19, in-session): centred, bounded content region; not a stretch.

## Steps

1. **Shell** — `.app-rail-main { margin-inline: auto; }`. Reuses the existing 1280px cap and gutters; the ≤1023px block already resets padding and needs nothing.
2. **Dropzone enhancement** (`site.js`, new block in the same IIFE): for each `[data-dropzone]` find `input[type=file]`, `[data-dropzone-browse]`, `[data-dropzone-file]`; add `is-enhanced`; un-hide the browse button and wire `click → input.click()`; `dragenter/dragover → is-dragover`, `dragleave/drop` clears it; `drop → input.files = dataTransfer.files` then dispatch `change`; `change → has-file`, readout `name · size`, button label "Choose a different file". Reuses the file's data-attribute convention (`data-refresh-form`, `data-copy-target`).
3. **Dropzone CSS** — `.dropzone.is-enhanced input[type=file]` visually hidden but focusable (same recipe as `.sr-only`), `.dropzone:focus-within` carries the focus ring, `is-dragover` = red hairline + red tint (state channel already used by `[data-state=blocked]`), `has-file` = solid `--line-strong` border, `__glyph` = 40px tinted circle for the upload icon.
4. **Upload page** — `.upload-layout` (grid `minmax(0,1fr) 300px`, gap 24, `max-width` 1040, centred; one column ≤1023). Left: existing panel with the new dropzone. Right: panel "What happens next" — an ordered list of the real flow (kept as received → processed in the background, status page refreshes itself → open the case or the retained receipt) — and "Accepted files" (`.eml .msg .pdf .doc .docx .jpg .png`, up to `MaximumSizeLabel`). No fabricated data; every sentence is traceable to `Upload.cshtml.cs`, `UploadStatus.cshtml(.cs)` and the `accept` list.
5. **Public request page** — same dropzone markup; no aside (a third party sees only what `RequestUploadPublicView` exposes).
6. **Design authority** — one paragraph under "Authenticated shell: the operator rail".
7. **Visual sweep** — local `DevelopmentOffline` at 1440 and 1920: Dashboard, Inbox, Upload, Upload status, Queues, Cases, New case, Case detail, Assessment, Operations, Administration + one workspace. Fix what is plainly wrong in the same PR (behaviour-preserving CSS only); file anything larger.
8. **Verify** — `dotnet build -c Release`; `AccessibilityTests` + Browser journeys (Playwright); Web integration `*Upload*`/`Intake*` filters; then the full non-Corpus integration suite in the background before the PR.

## Acceptance

- At 1920 the content region has equal gutters either side; at ≤1516 nothing changes.
- Upload: dropping a file onto the dashed area selects it (readout shows the name); keyboard users reach the input via Tab and open the picker with Enter/Space; with script disabled the native input is visible and works.
- axe: no violations on `/Upload`, `/Uploads/{token}`; no `[style]` attributes.

## Simplification pass

_(recorded before the PR opens)_
