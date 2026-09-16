\# Estimate import route: make refusals visible, fix dialog/in-place mechanics, accept Glass's rounding



\## Context



Operator report: "Import estimate button not responsive — doesn't close after choosing file", with

`artifacts/estimatehar.har` and two screenshots; then "`C:\\Users\\Alex\\Desktop\\calc.pdf` also wouldn't

import but it should be set up".



What the HAR and an offline replay of the deployed bytes (Playwright serving every response from the

HAR) actually show:



1\. The POST `?section=estimate\&handler=ImportEstimate` was sent by `fetch` (in-place submit), took

&#x20;  \*\*5.9 s\*\* on the server, returned 302 → `?section=estimate`, and the page was swapped in place.

&#x20;  During those 5.9 s the submit button has no busy state (`form\[aria-busy]` is unstyled) and repeat

&#x20;  clicks are silently dropped (`inplaceSubmitting` guard) — the "not responsive" part.

2\. The server retained the PDF but the parser refused it. The refusal text

&#x20;  ("This file was not recognized as an Audatex estimate report, so nothing was imported.") rendered in

&#x20;  `\[data-case-notices]` \*\*at the top of the page\*\*; the in-place swap keeps the scroll anchor at the

&#x20;  Estimate section, so the operator never saw it. Only success notices are toasted

&#x20;  (`case-workspace.js:700-704`). The section then shows "Retained estimate sources … Complete import",

&#x20;  which re-runs the same refusal, again invisibly.

3\. The refusal itself is correct: `802659183541\_\_AB000328.AudatexMS.pdf` (SHA `6f74872e…`, found at

&#x20;  `C:\\Users\\Alex\\Documents\\GitHub\\audatexanalysis\\audatex\\audatex\\`) is an Audatex \*\*Customer Estimate\*\*

&#x20;  summary — descriptions and section totals only, no per-line work units or prices. FRD-06 refuses

&#x20;  unsupported estimates. Operator decision: keep refusing, keep the wording.

4\. The Import control is `<a href="…?dialog=import-estimate" data-dialog-open>`; the shared opener

&#x20;  (`site.js:1313`) never `preventDefault`s, so every click opens the dialog \*\*and\*\* does a full page

&#x20;  reload with the dialog server-rendered open. That server-opened dialog never ran `open()`, so

&#x20;  \*\*Escape does not close it\*\*, nothing is `inert`, no focus trap, and the URL keeps `dialog=` (a

&#x20;  reload/F5 re-opens it). Same for Send to AI, Compare and Discard, and the Unidentified/EVA anchors.

5\. Latent defect exposed by fixing 4: when a script-opened dialog inside a swapped root submits in

&#x20;  place, the swap detaches the dialog with its `inertOutside` release closure, leaving

&#x20;  `header.app-rail`, `section.utility-bar`, `nav.workspace-tabs`, the sticky ribbon and the toast

&#x20;  region \*\*permanently `inert`\*\* (reproduced offline). Case-frame dialogs escape this only because they

&#x20;  post to other paths and navigate.

6\. `calc.pdf` (SHA `368272a4…`, a Glass's Calculation printed via PDFium) is refused by

&#x20;  `GlassEstimatePdfParser.Complete()` line 320: Paint rows print labour 66.62+133.25+141.58+24.98+24.98

&#x20;  = \*\*391.41\*\* but Glass's prints the section labour as \*\*391.42\*\* = round(4.70 h × £83.28) — the

&#x20;  source rounds once from section hours × rate; the parser demands the sum of per-row rounded values.

&#x20;  All five reference PDFs happened to reconcile (rate 0.00, integer rate 80.00, or lucky sums).

&#x20;  Everything else in calc.pdf reconciles (verified row-by-row: identity, £ glyph U+00A3, operations,

&#x20;  parts/positions appendices, summary, VAT, gross). Pegasus's own `EstimateTotals.Compute` also costs

&#x20;  labour as Σhours × rate, so the parser's row-sum rule is the odd one out.



\## Branch



`task/estimate-import-route` from `origin/dev`, worktree `../pegasus-worktrees/estimate-import-route`

(`git branch --unset-upstream` after creating). PR to `dev`.



\## Changes



\### A. Open dialogs in place from anchor openers — `src/Pegasus.Web/wwwroot/js/site.js` `bindDialogOpeners` (\~1304-1315)

Inside the click listener: if the control is an `<a>` (has `href`), `event.preventDefault()` before

`open(control)`. The href stays the no-script fallback (unchanged server route `?dialog=`). Affects the

four Estimate anchors, the Unidentified `?dialog=` anchors and the EVA handoff anchor — all have their

dialog on the page, so they now open in place as the `\_CaseDialogs.cshtml` header already describes.



\### B. Server-opened dialogs get script semantics — `src/Pegasus.Web/Pages/Cases/Shared/\_CaseEstimate.cshtml` (lines 694-695, 728-729, 775-776, 807-808)

Replace `hidden="@(Model.OpenDialog == "x" ? null : "hidden")"` with the `\_WorkCentreBody.cshtml:273`

pattern: when open, emit `data-dialog-open-on-load="true"` and no `hidden`; otherwise `hidden`. A direct

`?dialog=` load (or no-script) still renders it open; with script `bindBackdropDialogs` calls `open()`

so Escape, inert and focus work. Test `AssessmentEstimateImportWebTests.ImportDialogHasAStaticTargetWhenJavaScriptIsUnavailable`

keeps its `DoesNotContain("hidden=")` assertion; add `Contains("data-dialog-open-on-load=\\"true\\"")`.



\### C. Release inert when a swap replaces an open dialog — `src/Pegasus.Web/wwwroot/js/case-workspace.js` `swap()` (\~654-663)

In the `swapRoots.forEach` loop, immediately before `current.replaceWith(next)`:

`current.querySelectorAll('\[data-dialog]:not(\[hidden]), \[data-reason-dialog]:not(\[hidden])')` → call

`pegasusClose()` on each. `close()` runs the `inertOutside` release, pops `openDialogStack` and removes

the keydown listener. (Only roots actually replaced are touched, so the `noticesOnly` refusal path keeps

its dialog.)



\### D. Toast refusals like confirmations — `case-workspace.js` `swap()` (\~700-704) and `showActionError` (\~763-774)

Next to the existing `\[data-case-notices] \[data-confirmation]` toast, also toast the text of a swapped-in

`\[data-case-notices] \[role="alert"]` with `window.pegasusToast(text, 'danger')` (`.toast--danger` already

exists in `site.css:784`). `showActionError` toasts its message the same way after writing the notice.

The page notice stays at the top as the persistent record; no new copy is written.



\### E. Busy affordance while an in-place submit is in flight — `src/Pegasus.Web/wwwroot/css/site.css` (\~211)

Extend the existing `.btn.is-disabled,.btn:disabled{…}` rule with `form\[aria-busy="true"] button\[type="submit"]`

so the submit button takes the disabled treatment (and `pointer-events:none`) for the duration

`submitInPlace` already marks with `aria-busy`. No JS change; Cancel stays usable.



\### F. Glass's section labour reconciles to the source's own rule — `src/Pegasus.Infrastructure/Assessment/GlassEstimatePdfParser.cs` `Complete()` (\~314-325)

Replace `own.Sum(item => item.Labour ?? 0) != sum.Labour` with

`decimal.Round(sum.SummaryHours.Value \* sum.SummaryRate.Value, 2, MidpointRounding.AwayFromZero) != sum.Labour`.

Keep every other check: per-row `round(hours × rate) == row labour`, hours sum, material sum,

`Labour + Material == Total`, `sum.Labour == SummaryLabour`, and the document totals. Update the

class summary comment to state that printed section labour is section hours × rate rounded once.

Doc line in `docs/frd/frd-06-vehicle-and-engineering-evidence.md` § Glass's calculation PDFs (\~346):

section labour reconciles to the printed rate × section hours as the source computes it.



\### G. Tests

\- `tests/Pegasus.IntegrationTests/GlassEstimatePdfParserTests.cs`: a `VisualRow\[]` synthetic complete

&#x20; document (identity rows, one Body row, one Auxiliary row, Paint rows 0.80/1.60/1.70/0.30/0.30 @ 83.28

&#x20; with printed section labour 391.42, Summary, Parts, Positions, `Abbreviations`) asserting the parse

&#x20; succeeds with `SourceTotals.Net` reconciled; a sibling case where a \*row\* labour is off by 1p still

&#x20; refuses ("main rows disagree"). Model it on `AnUnreadableAmountRefusesTheWholeGlassTable`.

\- `AssessmentEstimateImportWebTests`: the on-load flag assertion from B.

\- No JS test harness exists in the repo; JS is verified by the walk below.



\## Verification



1\. Build + focused tests (one host slot): `dotnet build` the solution, then

&#x20;  `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName\~AssessmentEstimateImportWebTests|FullyQualifiedName\~GlassEstimatePdfParserTests|FullyQualifiedName\~CaseDetailsWebTests"`

&#x20;  with `PEGASUS\_REFERENCE\_PACK\_ROOT=<repo>/reference-evidence` so the four Glass's reference oracles run

&#x20;  (they exist locally) and stay green after F.

2\. Prove calc.pdf through the real parser: a throwaway (uncommitted) xunit fact or `dotnet run` harness

&#x20;  calling `new PdfEstimateDocumentParser().Parse(File.ReadAllBytes(@"C:\\Users\\Alex\\Desktop\\calc.pdf"))`

&#x20;  → 22 rows, Net 3,034.39 / VAT 606.88 / Gross 3,641.27, 3 part numbers. Also assert the Audatex

&#x20;  Customer Estimate still refuses with the existing message. Neither file is committed.

3\. Offline replay: re-run the HAR-replay Playwright harness (from this session) serving the modified

&#x20;  `site.js` / `case-workspace.js` / `site.css` instead of the HAR copies, and confirm: Import anchor opens

&#x20;  the dialog with no navigation; Escape closes a `?dialog=` loaded dialog; submit shows the busy state;

&#x20;  after the swap the dialog is hidden, a danger toast carries the refusal, and `\[inert]` is empty.

4\. Live walk (DevelopmentOffline, own LocalDB, per memory): import calc.pdf → a Glass's Draft appears;

&#x20;  import the AB000328 Customer Estimate → refusal toast + notice; Send to AI / Compare / Discard open in

&#x20;  place; Unidentified and EVA anchors still open their dialogs.

5\. `scripts/Test-MarkdownPlacement.ps1` is not needed (no new Markdown files); FRD-06 edit is prose-only.



\## Out of scope / flagged

\- The 5.9 s server round trip (blob retain + lease + blob re-read + PdfPig + GetCase on App Service) is

&#x20; not addressed here.

\- The Audatex "Estimate Report" layout (2 of 60 sampled local files) was not checked against the parser.

\- Optional: add calc.pdf to the private `reference-evidence/glasses-integration/glass\_ref\_docs` pack with

&#x20; an independent row oracle and a fifth `InlineData` row — operator-owned collection, not done by default.



