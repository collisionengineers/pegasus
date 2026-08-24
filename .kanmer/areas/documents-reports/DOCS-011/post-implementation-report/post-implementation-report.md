# Post-implementation report — DOCS-011

**PR:** https://github.com/collisionengineers/pegasus/pull/535 → `dev`
**Branch:** `task/docs-011-evidence-preview` @ `992f5d42`, off `origin/dev@a6acc782`
**Diff:** 13 files, +541 / −46

## What shipped

Clicking an evidence photograph or a document filename now opens a viewer over
the case page — Previous/Next (buttons and ArrowLeft/ArrowRight), a bare `n / m`
position, Download, and Close — instead of replacing the page with a bare file
or starting a download.

1. **`Pages/Cases/Documents/Download.cshtml.cs`** — an additive `inline` flag.
   Same `[Authorize]` roles, same actor check, same `IDownloadCaseDocument` use
   case, same response validation; only the disposition differs. The existing
   attachment return is the untouched `else`.
2. **`Presentation/GalleryImage.cs`** — now `(Href, DownloadHref, FileName,
   MediaType)`. Preview and download are genuinely different URLs for a case
   document and the same URL for a retained receipt image.
3. **`Pages/Shared/_EvidenceViewer.cshtml`** — new; reuses
   `.reason-dialog-backdrop`.
4. **`wwwroot/js/site.js`** — one `[data-evidence-viewer]` block modelled on the
   live `[data-reason-dialog]` block, inheriting initial focus, focus
   containment, Escape and focus return.
5. **`_ImageGallery.cshtml`** — tiles become triggers and stay real `<a href>`s;
   the empty-state paragraph is gone.
6. **`_CaseDocuments.cshtml`** — one anchor plus one attribute, because DOCS-012
   is rewriting that file.

## Two corrections to the ticket, both verified

- **The file path was wrong.** `Pages/Shared/_ImageGallery.cshtml`, a global
  partial with three callers — not `Pages/Cases/Shared/`. Ticket body corrected.
- **Nothing opened a new tab.** No `target="_blank"` on any evidence link. The
  real faults were the receipt routes navigating the same tab away from the case,
  and the case-document route forcing `attachment` by naming the file. Both
  halves of the operator's complaint were real; the diagnosis was not.

## A gap this found in the existing suite

The brief stated that `QdosCustodialWebTests` asserts `attachment` on the plain
route, so a flipped default would break it. **Both halves were wrong** — the
request already carried `?versionId=`, and the assertion only checked that the
filename appears in the header, never the disposition *type*. Nothing in the
suite pinned it, so an accidental default flip would have shipped green. That
assertion is now added, which is what makes "additive" a checked claim.

## Deviation from plan — one Core field

`ImageIntakeImage` gained `MediaType`. The plan asserted no Core change was
needed; that premise was wrong — `CaseEvidenceImage` and `DocumentVersion` carry
a media type but `ImageIntakeImage` did not. The projection in
`EfImageIntakeStore` already *filtered* on that column, so it is three lines and
no new query. The alternative was emitting a fake `image/*` wildcard into the
DOM. Recorded in the file map.

## Defects found in this branch's own code — fixed, not deferred

The simplification pass was asked to report bugs rather than fix them. Four came
back, all mine, all fixed before the PR:

- **Paging could reach a non-previewable row** and set a hidden iframe's `src`,
  which **started a download the operator never asked for** and left the loading
  state stuck. The paging set is now filtered to previewable items.
- **`image/svg+xml` passed the inline allowlist.** SVG executes script when
  navigated to, and this ticket is what newly makes the operator-facing document
  link navigable-inline — so with no script, or a middle-click, it was the same
  stored-XSS class as `text/html`. Excluded on both sides, **with a regression
  test**.
- No `error` listener on the iframe — a failed PDF span forever.
- Focus-trap selector omitted `input, select, textarea` while claiming contract
  parity with the reason dialog.

## Simplification pass

Run across all four lenses; four cleanups applied (dead reduced-motion CSS that
`!important` already overrode; four `WebApplicationFactory` instances collapsed
to one; a double ternary flattened; an identity `ToDictionary` selector).
**Seven findings deliberately not applied, each named with a reason** in the
plan — the significant ones being the two-overlay extraction (permitted at two
copies, required at three; recorded as the trigger for whoever adds the third)
and the inline-safe media-type rule now living in two layers (drift risk, not a
present defect; flagged for a reviewer's opinion). Full dispositions under the
plan's dated "Simplification pass — 2026-08-24" heading.

## Verification

| Check | Result |
|---|---|
| `dotnet build --configuration Release` | succeeded, **0 warnings** |
| `dotnet test tests/Pegasus.Core.Tests` | **937 passed**, 0 failed |
| `dotnet test tests/Pegasus.ArchitectureTests` | **99 passed**, 0 failed |
| `QdosCustodialWebTests` + `ImageViewingWebTests` | **9 passed**, 0 failed |
| New inline test proved red before green | **yes** — with the inline branch disabled it fails on the disposition type; restored, it passes |

All re-run after the defect fixes. No flaky or suspicious failures occurred, so
no isolated re-run was needed. CI runs the full suite in three shards on the
pushed SHA and is the authority.

## Boundaries held

No new operator-facing copy — the viewer's entire budget is `Previous`, `Next`,
`Close`, `Download`, the filename and a bare `n / m`; "loading is explicit" is
delivered visually via `aria-busy` and a spinner reusing existing keyframes. No
banned word. No inline `<script>`, no `blob:`, no `<embed>`/`<object>`. **No
regex and no unbounded loop in the new JS** — checked, not assumed. Every
trigger is still a real `<a href>`, so the surface works with no script. No
migration, no new port, no new Core owner.

## Behaviour changes a reviewer should confirm

- **The document filename link no longer downloads without JavaScript** — it
  previews in-tab for images and PDFs. The route flag is additive; the link is
  not. This is the operator's stated ask, and with SVG excluded the inline set is
  inert, but it is a real change.
- Paging walks historical and logically-removed versions, because
  `data-evidence-set` is on the `<tbody>`. Judged correct — the viewer pages
  through exactly what the table shows.
- An image-initiated record with zero images renders its heading with nothing
  beneath, the intended effect of dropping the empty-state panel.

## Not claimed

Keyboard, screen-reader, forced-colours, reduced-motion, 1280+,
constrained-desktop and 200%-zoom inspection, which `docs/design/README.md`
§ Accessibility and acceptance requires for a UI capability. **This branch does
not carry that evidence.** No deployment; current-state docs need no refresh.

## Rebase expected

DOCS-012 (PR #532) is open and unmerged and rewrites `_CaseDocuments.cshtml`.
This diff over that file is one anchor plus one attribute to keep the rebase
trivial. **This PR needs a rebase after #532 merges.** No merge or cherry-pick
of #532 was attempted.

## Operator questions

**None.** The one place a sentence was tempting — a "preview not available for
this file type" notice — is answered by falling through to the existing
download, which needs no unapproved copy.
