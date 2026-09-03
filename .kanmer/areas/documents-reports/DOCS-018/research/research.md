# Research — DOCS-018 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in the shared detached checkout `.worktrees/research` at
`origin/dev` = `897db953`; `git status --porcelain` was empty afterwards. The
Kanmer MCP tools returned only project metadata this session, so the board
reads were taken from the board worktree files and the writes were checked on
disk. Spot-checked against `origin/dev` with my own commands, all confirmed:

- `fee.agreed_fee` / `fee.description_lines` vocabulary entries at
  `src/Pegasus.Core/Assessment/AssessmentContracts.cs:66-67` and `:117-118`
  (`grep -n -i fee src/Pegasus.Core/Assessment/AssessmentContracts.cs`).
- `FeeNet` / `FeeVat = FeeNet * 0.20m` / `FeeTotal` at
  `src/Pegasus.Core/Reports/AssessmentReportRendering.cs:273-275`;
  `VatNumber = "262 0937 10"` at line 9; `FeeTerms` (89 days) at line 15
  (`grep -n -i fee src/Pegasus.Core/Reports/*.cs`).
- `OnGetPreviewReportDraftAsync` at
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:579` returns
  `File(result.Draft!.Assessment.Pdf, "application/pdf")` at line 593; no
  handler returns `Draft.FeeNote` (`git grep -n "FeeNote" origin/dev -- src/Pegasus.Web`
  is empty).
- Embedded template `docs/design/assets/report-renderer/templates/assessment_fee_note.scriban`
  (csproj lines 45-46), `VAT @ 20%` on template line 5; fallback description
  at `PlaywrightAssessmentReportRenderer.cs:186`; migration
  `20260803205759_SendToAiAssessmentToolset.cs:71` allows both field paths;
  FRD-11 D42 paragraph at lines 130-133 on `origin/dev`;
  `scripts/Test-UiCatalogue.ps1:20` allowed classifications.

Wrapper finding for the planner (not an operator question): Codex's Files
document leaves DOCS-018 owning only `_CaseFeeNote.cshtml` and hands the
include line, the fee-note preview handler and the Web test to [[ENG-029]].
That leaves DOCS-018 with no production caller of its own ("Done means
wired"), while ENG-029 and DOCS-018 are allocated to the same wave-4 lane set
(ENG-036 · ENG-031 · ENG-029 · DOCS-018). The plan must settle one of:
(a) sequence DOCS-018 after ENG-029 merges and let DOCS-018 add the include
line and the `PreviewFeeNote` handler in the Case handler host under the
`Pages/Cases/Shared/*` capacity-one lock; or (b) ENG-029's plan carries the
handler and include as a named hand-off and DOCS-018 delivers the partial plus
the wave's test. The mockup's "Payment within 30 days" and long default
description differ from the code contract (89 days, generic fallback); the
ticket body says to use the report contract's fee terms, so the code contract
governs and no operator decision is needed.

## Scope and evidence basis

VERIFIED — this checkout is detached at `897db953` (`git rev-parse HEAD`;
`git log -1 --oneline`). The working tree is clean (`git status --short`).
All findings below are from read-only inspection.

VERIFIED — DOCS-018 is not an absent renderer feature. The report renderer
already produces a fee-note PDF alongside the assessment PDF. The remaining gap
is making that existing artifact reachable as the Report-section preview without
taking paths owned by [[ENG-034]] or [[ENG-029]].

## Current behaviour

### Core ports, contract, and assessment data

VERIFIED — `AssessmentReportContract` contains the fee terms and payment
identity: VAT number, account name, bank, sort code, account number, remittance
e-mail, `FeeTerms`, and `AdditionalFeeTerms` at
`src/Pegasus.Core/Reports/AssessmentReportRendering.cs:9-16`
(`rg -n 'VatNumber|AccountName|...|FeeTerms' ...`).

VERIFIED — the report snapshot already carries `AgreedFee` and
`FeeDescriptionLines` at
`src/Pegasus.Core/Reports/AssessmentReportRendering.cs:154-155`; it computes
`FeeNet`, `FeeVat`, and `FeeTotal` at lines 273-275. `FeeVat` is explicitly
`FeeNet * 0.20m`, rounded away from zero.

VERIFIED — the assessment vocabulary already persists
`fee.agreed_fee` and `fee.description_lines`:
`src/Pegasus.Core/Assessment/AssessmentContracts.cs:66-67`. They are a positive
money field and a 2,000-character text field at lines 117-118
(`rg -n -i 'AgreedFee|FeeDescriptionLines|...' src/Pegasus.Core/Assessment`).

VERIFIED — `AssessmentReportProjection.Prepare` projects both values into the
renderer snapshot at
`src/Pegasus.Core/Reports/AssessmentReportProjection.cs:209-210`. The existing
assessment record, rather than a new DOCS-018 record or port, is therefore the
data source.

VERIFIED — the database constraint has allowed both field paths since
`20260803205759_SendToAiAssessmentToolset`:
`src/Pegasus.Infrastructure/Persistence/Migrations/20260803205759_SendToAiAssessmentToolset.cs:71`
(`rg -n -i 'fee\.agreed_fee|fee\.description_lines' .../Migrations`).
No migration is required.

VERIFIED — D9/D17 apply the Current estimate's VAT percentage to the rendered
assessment repair-cost subtotal, not the fee note:
`docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md:312-328`.
The fee note has a separate, fixed 20% calculation in Core. Do not couple the
fee-note VAT to an estimate's VAT percentage.

### Infrastructure adapter and template

VERIFIED — `PlaywrightAssessmentReportRenderer` implements
`IAssessmentReportRenderer` and produces both artifacts in one render:
`src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs:23-79`
(`rg -n -i 'FeeNote|RenderPdfAsync|...' ...Renderer.cs`).

VERIFIED — the existing embedded template is
`docs/design/assets/report-renderer/templates/assessment_fee_note.scriban`;
the project embeds it at
`src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj:43-51`
(`git ls-tree -r --name-only HEAD docs/design/assets/report-renderer`).

VERIFIED — the pipeline renders Scriban HTML in memory, calls Playwright
`SetContentAsync`, then returns only a PDF from `PdfAsync`; it has no HTML
artifact or HTML preview endpoint:
`PlaywrightAssessmentReportRenderer.cs:97-128`.

VERIFIED — the template already has the logo, date, case and provider
references, fee description, net/VAT/total rows, payment details, VAT number,
and terms. It renders `VAT @ 20%` at template line 5
(`git show HEAD:docs/design/assets/report-renderer/templates/assessment_fee_note.scriban`).

VERIFIED — a blank saved description currently falls back to
"Independent automotive engineering assessment" in
`PlaywrightAssessmentReportRenderer.cs:183-188`. This differs from the mockup's
long vehicle-specific default.

### Current Web entry point and partial conventions

VERIFIED — the present Assessment page offers "Generate report draft" and
"Preview report draft" at
`src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:243-256`. The preview is
an anchor to `PreviewReportDraft` and opens in the browser.

VERIFIED — `OnGetPreviewReportDraftAsync` calls the existing draft-generation
use case and returns `Draft.Assessment.Pdf` as `application/pdf`, inline:
`Index.cshtml.cs:575-594`. It neither saves nor sends.

VERIFIED — the existing fee-note artifact is generated but discarded by both
current Web handlers: the POST returns `Draft.Assessment`, and the GET preview
returns `Draft.Assessment`. No current handler returns `Draft.FeeNote`.

VERIFIED — `Pages/Cases/Shared/` contains case-section partials, but no
`_CaseFeeNote.cshtml`; `Pages/Shared/_ShellDialogs.cshtml` provides the
application dialog structure using `data-dialog-open`, `data-dialog`, and
`data-dialog-close` (`rg --files ...`; `Get-Content .../_ShellDialogs.cshtml`).

VERIFIED — the current product convention for the report preview is the
browser's native PDF viewer, not an in-page dialog. A fee-note preview should
reuse that convention rather than introduce a second HTML renderer or dialog
JavaScript.

VERIFIED — operator presentation formatting lives in
`src/Pegasus.Web/Presentation/OperatorLabels.cs`; `OfficeDate` and `OfficeTime`
format operator instants in Europe/London at lines 681-722. Report rendering
uses `dd/MM/yyyy` with invariant culture in
`PlaywrightAssessmentReportRenderer.cs:144-150` and the local `Money` helper
uses `£#,##0.00` with `en-GB` at lines 283-284.

### Tests

VERIFIED — Core coverage is in
`tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs`; it proves
both artifacts are returned and validates the snapshot, including fee inputs
(`rg -n -i 'FeeNote|AgreedFee|...' tests/Pegasus.Core.Tests/Reports`).

VERIFIED — renderer integration coverage is in
`tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs:25-58`.
It extracts the fee-note PDF text and asserts "FEE NOTE", net, `VAT @ 20%`,
total, payment details, and the VAT number.

VERIFIED — current Web coverage is in
`tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`.
It proves a complete case returns a PDF and an incomplete case fails closed, but
only tests the assessment artifact. Its fast fake already returns a fee-note
artifact at lines 287-290.

VERIFIED — a new routed Razor page would require a same-PR
`docs/design/test-ui/catalogue.json` entry because
`scripts/Test-UiCatalogue.ps1:20-57` rejects unclassified routed pages. That
path is owned by [[UIIMP-014]], so DOCS-018 must not introduce a routed page.

### FRD and mockup

VERIFIED — FRD-11 already states that the initial renderer includes its fee
note at lines 64-70, that draft generation returns both artifacts at lines
101-105, and that the Report section renders the D42 fee-note preview at lines
130-133 (`rg -n -C 4 -i 'fee|D42|VAT|draft|preview' docs/frd/...`).

VERIFIED — the mockup has Report fields "Agreed fee" and "Fee description" and
a "Preview fee note" action; `DIALOGS['fee-preview']` shows a wide fee-note
dialog with logo, date, references, description, 20% VAT, total, VAT number,
30-day payment wording, and Close
(`rg -n -C 12 'fee-preview' .../22-case-engineer.js`).

VERIFIED — mockup fixtures use fee values of 100 or 150 and include
`feeLines` examples at source lines 172 and 223
(`rg -n -C 3 'feeLines|fee: ' .../04-fixtures.js`).

## Gap list

- VERIFIED — expose `Draft.FeeNote.Pdf` through a read-only preview handler;
  today only `Draft.Assessment.Pdf` is returned.
- VERIFIED — add the Report-section "Preview fee note" control after
  [[ENG-034]] creates the section and [[ENG-029]] owns its body and handlers.
- VERIFIED — use the existing renderer and PDF-preview convention; no Core
  contract, adapter, template, dependency, or migration change is needed.
- VERIFIED — reconcile the mockup's "Payment within 30 days" with the current
  contract's "within 89 days" before altering contract text. D42 says to use
  agreed report-contract fee terms; the present contract is authoritative in
  code until changed by an authorized requirement.
- VERIFIED — reconcile the mockup's long default description with the current
  renderer's generic fallback before changing fallback policy.

## Reuse and hand-off

VERIFIED — reuse `GenerateCaseAssessmentReportDraft`, the
`IAssessmentReportRenderer`/`GenerateAssessmentReportDraft` pipeline,
`AssessmentReportDraft.FeeNote`, and
`PlaywrightAssessmentReportRenderer`; all are already wired through
`AddPegasusReportRendering`
(`src/Pegasus.Infrastructure/DependencyInjection.cs:503`,
`src/Pegasus.Web/Program.cs:663`).

VERIFIED — the conflict-free hand-off is a self-contained
`_CaseFeeNote.cshtml` partial owned by DOCS-018. [[ENG-029]] adds its one-line
include to its owned `_CaseReport.cshtml`, supplies the preview URL/handler from
its owned Case handler host, and extends its owned assessment Web test. This
does not touch the shared-label lock or create a routed page. (See the wrapper
finding above: the plan must name which lane lands the handler so DOCS-018 has
a production caller.)

ASSUMED — [[ENG-029]] will expose a model property or route value sufficient
for the partial to create its anchor. The exact property name cannot be fixed
until [[ENG-034]]'s section host exists.

## Risks

- VERIFIED — [[ENG-034]] is the stated blocking sections move, and it owns the
  initial `_CaseReport.cshtml`; implementation must wait for its hand-off.
- VERIFIED — [[ENG-029]] owns the Report body, assessment handlers, and Web
  tests, so DOCS-018 cannot independently wire or verify the visible control
  in those files unless the plan sequences it after ENG-029 merges.
- VERIFIED — a routed fee-note page would force a Test UI catalogue change,
  conflicting with [[UIIMP-014]] ownership.
- VERIFIED — changes to `OperatorLabels.cs`, migrations, shared partials, or
  test UI assets breach EPIC-012's single-capacity shared locks.

## Open questions

none
