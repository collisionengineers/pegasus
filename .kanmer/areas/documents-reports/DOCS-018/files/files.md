# Files — DOCS-018 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

Wrapper note: every path below was confirmed with `git ls-tree` /
`git grep` on `origin/dev` = `897db953` (`_CaseFeeNote.cshtml` confirmed
absent). Revised by the plan wrapper (2026-09-02, gpt-5.6-terra high): the
plan settles the hand-off as option (a) — DOCS-018 is sequenced after
[[ENG-029]] merges and then owns the whole D42 feature itself, so the include
line, handler and Web test rows moved from the ENG-029 hand-off table into
the DOCS-018 table, and `_CaseFeeNote.cshtml` is not created (a one-anchor
partial has no second caller; the existing Report action cluster is the
convention). The snapshot-capture row follows the plan's wrapper correction 1.

## DOCS-018-owned changes (after CASE-038 → ENG-034 → ENG-029 merge)

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | change (sequential whole-file lease after [[CASE-038]] and [[ENG-029]]) | Add the read-only `OnGetPreviewFeeNoteAsync` GET handler returning `Draft.FeeNote.Pdf` inline. | `OnGetPreviewReportDraftAsync` shape (`Assessment/Index.cshtml.cs:579-594` before the move), `TryGetActor`, `GenerateCaseAssessmentReportDraft`. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` | change (capacity-one `Pages/Cases/Shared/*` lease after [[ENG-029]]) | One `Preview fee note` anchor beside `Preview report draft`, same visibility condition. | Existing preview anchor markup and the browser-PDF preview convention. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change (capacity-one lease) | One `PreviewFeeNote` key in [[ENG-034]]'s `CaseWorkspace.EngineerSections` group. | `OperatorLabels` as the sole label owner. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` | change | Prove `?handler=PreviewFeeNote` returns the fee-note PDF and stores/sends nothing. | `Compose`, `FakeRenderer` (already returns a fee-note artifact, lines 286-296), `ThrowingDocumentContentStore`. |
| `docs/design/test-ui/pages/case-details--*.html` | change only if the regenerated capture differs | `TestUiSnapshotTests` byte-compares captures; the Case capture composes `_CaseReport.cshtml`. Capacity-one lease shared with [[UIIMP-014]]. | `Update-TestUiSnapshots.ps1`. |

VERIFIED — no Core, Infrastructure, renderer-template, migration, catalogue
or routed-page file changes for DOCS-018. Existing fields, calculation,
template, embedded logo, and fee-note PDF artifact already meet those layers
of the requirement (`rg -n -i 'AgreedFee|FeeDescriptionLines|FeeNote|...' src
tests`).

## Dependencies (other tickets' deliverables DOCS-018 builds on)

| Owning ticket | Required change | Why |
| --- | --- | --- |
| [[CASE-038]] | `DetailsModel` hosts the moved Assessment handlers including `OnGetPreviewReportDraftAsync`; `_CaseReport.cshtml` exists as a shell. | DOCS-018's handler sits beside the report preview in the same host. |
| [[ENG-034]] | Fills `_CaseReport.cshtml` with the Generate/Preview report-draft controls and adds `OperatorLabels.CaseWorkspace.EngineerSections`. | DOCS-018 adds one anchor to that cluster and one key to that group. |
| [[ENG-029]] | Report field editors (agreed fee, description lines) in `_CaseReport.cshtml`. | The values the fee note renders are edited there; DOCS-018 takes the file only after ENG-029 merges. |

## Files DOCS-018 must not touch

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseFeeNote.cshtml` — intentionally
  not created (plan decision 2)
- `src/Pegasus.Web/Pages/Cases/Details.cshtml` — CASE-038
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` — CASE-038
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml` — ENG-034 then
  ENG-029
- `src/Pegasus.Web/Pages/Cases/Shared/*` other than the one anchor in
  `_CaseReport.cshtml` — capacity-one lock
- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml`, `Index.cshtml.cs` —
  ENG-034 (retired to a 301 stub)
- `src/Pegasus.Web/Pages/Shared/*` — capacity-one lock
- `src/Pegasus.Web/wwwroot/css/site.css`, `wwwroot/js/site.js` — CASE-038
- `docs/design/test-ui/catalogue.json` and every test-ui file other than a
  stale `case-details--*.html` capture — UIIMP-014
- `src/Pegasus.Infrastructure/Persistence/Migrations/**` — serialized
  migration lane
- `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` —
  DELIV-041 (done)
- `docs/design/assets/report-renderer/templates/assessment_fee_note.scriban`
  — no change needed
- `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`
  — DOCS-017 (signature block)
- `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` — DOCS-017 /
  ENG-035
