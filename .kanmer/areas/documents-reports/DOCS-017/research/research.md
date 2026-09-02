# Research — DOCS-017 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

## Wrapper check (Claude, 2026-09-02)

Spot-checked in the main checkout (`dev`) after Codex ran read-only in the
detached `.worktrees/research` checkout (origin/dev; `git status --porcelain`
clean afterwards):

- CONFIRMED `src/Pegasus.Core/Reports/AssessmentReportRendering.cs:163` holds the only accepted tuple `["andy_patterson"] = ("A Patterson", "M.Inst.IAEA")`; `ReportEngineer(Name, Qualifications, SignatureKey)` at lines 112-115; `Validate()` re-checks the key at line 237 (`grep -n "andy_patterson\|record ReportEngineer\|SignatureKey" src/Pegasus.Core/Reports/*.cs`).
- CONFIRMED `src/Pegasus.Core/Assessment/AssessmentContracts.cs:63-65` defines `engineer.name` / `engineer.qualifications` / `engineer.signature`; `AssessmentReportProjection.cs:117-125,171-208` reads them and calls `TryResolveAcceptedEngineer`.
- CONFIRMED `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs:56-58` resolves `brand.signatures.{SignatureKey}.png` as an embedded resource; `Pegasus.Infrastructure.csproj:53-54` embeds only `andy_patterson.png`; `docs/design/brand/signatures/` holds `andy_patterson.png`, `ed_mawdsley.png`, `neil_oreilly.png`.
- CONFIRMED `docs/design/assets/report-renderer/templates/assessment_report.scriban:14` prints `{{ engineer }} — {{ qualifications }}` unconditionally.
- CONFIRMED `PegasusIdentityUser` (`PegasusDbContext.cs:1022-1027`) carries only `IsEnabled` and `MustChangePassword`; `CaseWorkflowEntities.cs:9` has `AssignedEngineerId`; `IDocumentContentStore` at `DocumentContracts.cs:271`; `Pages/Cases/Assessment/Index.cshtml:245,253` posts `GenerateReportDraft` / `PreviewReportDraft`.
- CONFIRMED FRD-11 lines 73-82 still state the fixed tuple and "Signature-policy changes are deferred to `DOCS-017`"; last FRD-11 commit `fb68225b`; no `D31` / `sign-off` text in FRD-11 or FRD-04 yet — [[DELIV-041]] (the blocker) has not landed on `dev`.
- Correction: `EfCaseWorkflowStore.AssignEngineerAsync` is at line 479, not 482.
- Addition: `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs:104` builds `AssessmentReportProjectionInput` via target-typed `new(`, so the `rg -l "new AssessmentReportProjectionInput"` risk command under-counts call sites; the Files table already lists that file.
- Owned-path note: the real change set widens the approximate owned paths to `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`, `docs/design/assets/report-renderer/templates/assessment_report.scriban` and `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`; none is a shared-lock path.
- CONFIRMED mockup: default rule at `05-state.js:152-153`; fixtures `04-fixtures.js:33-35` (Andy `M.Inst.IAEA`, Ed `ATA VDA AQP`, Neil empty qualifications, all three `signature: true`); the Report-section selector at `22-case-engineer.js:116` also filters on qualifications, which contradicts D31 as noted below; `Pegasus_UI_v2_notes.md:42-48,110-111` records the `sign_off_engineer` flag + qualifications + signature image as a backend gap.

## Codex research (gpt-5.6-terra, medium)

All repository observations below are **VERIFIED** with the stated read-only
command. No files were changed; `git diff --quiet` reported a clean worktree.

### Current behaviour

- **VERIFIED** — report signing is currently a fixed Core allowlist, not a
  Case/account projection. `AssessmentReportSnapshot` contains
  `ReportEngineer(Name, Qualifications, SignatureKey)` at
  `src/Pegasus.Core/Reports/AssessmentReportRendering.cs:112-115`; its only
  accepted entry is `andy_patterson → A Patterson | M.Inst.IAEA` at lines
  160-164. `Validate()` rejects an unknown or mismatched tuple at lines
  237-243. Command:
  `rg -n -C 12 "AcceptedEngineers|TryGetAcceptedEngineer|ReportEngineer|AssessmentReportPresentation" src/Pegasus.Core/Reports/AssessmentReportRendering.cs`

- **VERIFIED** — Core projection currently gets name, qualification and
  signature-key from mutable assessment fields, then constructs
  `ReportEngineer` at
  `src/Pegasus.Core/Reports/AssessmentReportProjection.cs:117-130,171-208`.
  It exposes the data-source seam
  `IAssessmentReportProjectionSource` at lines 303-312. Command:
  `rg -n -C 12 "EngineerName|EngineerQualifications|EngineerSignature|record AssessmentReportProjectionInput|interface IAssessmentReportProjectionSource" src/Pegasus.Core/Reports/AssessmentReportProjection.cs src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`

- **VERIFIED** — the production adapter is
  `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`;
  it builds `AssessmentReportProjectionInput` at lines 101 onward. The
  renderer is
  `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`;
  it converts `snapshot.Engineer.SignatureKey` to an embedded resource at line
  56 and supplies name/qualifications at lines 57-58. Command:
  `rg -n -C 8 "AssessmentVocabulary.Engineer(Name|Qualifications|Signature)|return new AssessmentReportProjectionInput" src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`
  and
  `rg -n -C 8 "engineer|signature" src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`

- **VERIFIED** — the Scriban template prints the signature image followed by
  `{{ engineer }} — {{ qualifications }}` at
  `docs/design/assets/report-renderer/templates/assessment_report.scriban:14`.
  Empty qualifications would therefore currently render a dangling separator.
  Command:
  `rg -n -C 5 "engineer|qualifications|signature" docs/design/assets/report-renderer/templates/assessment_report.scriban`

- **VERIFIED** — only Andy's signature is embedded in
  `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj:53-54`; the
  repository also has `docs/design/brand/signatures/ed_mawdsley.png` and
  `neil_oreilly.png`, but neither is embedded. Command:
  `rg -n -C 4 "brand\\.signatures|EmbeddedResource|Reports.Assets" src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs`
  and
  `rg --files src/Pegasus.Infrastructure/Reports docs/design/brand/signatures | rg -i "(signature|andy|ed|neil|template|asset)"`

- **VERIFIED** — the Web caller is the assessment workspace:
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:243-255` posts
  `GenerateReportDraft` and offers preview; its page model injects and calls
  `GenerateCaseAssessmentReportDraft` at
  `Index.cshtml.cs:52-58,529-593`. There is no sign-off selection control in
  the current report page. Command:
  `rg -n -C 6 "ReportDraft|GenerateAssessmentReportDraft|IAssessmentReport|report" src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs src/Pegasus.Web/Presentation/OperatorLabels.cs`

- **VERIFIED** — `Presentation/OperatorLabels.cs` has an existing
  `Engineer` role label at line 225 but no sign-off/signature label.
  It remains a shared-lock path and is not needed by this renderer-only lane.
  Command:
  `rg -n -i "OperatorLabels\\.(Engineer|Signing|Signature)|Engineer|Signature" src/Pegasus.Web/Presentation/OperatorLabels.cs`

- **VERIFIED** — the current staff model is only identity username, enabled
  state, password state and roles. `StaffRole` contains Administrator,
  Engineer and User at
  `src/Pegasus.Core/Identity/IdentityContracts.cs:5-10`;
  `StaffAccountSummary` begins at
  `src/Pegasus.Core/Identity/StaffAccountAdministration.cs:3`; and
  `PegasusIdentityUser` has only `IsEnabled` and `MustChangePassword` beyond
  `IdentityUser` at
  `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:1022-1027`.
  No staff-account sign-off, qualification or signature fields exist.
  Command:
  `rg -n -C 5 "public sealed class PegasusIdentityUser|public sealed record StaffAccountSummary|public enum StaffRole" src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs src/Pegasus.Core/Identity/StaffAccountAdministration.cs src/Pegasus.Core/Identity/IdentityContracts.cs`
  and
  `rg -n -i "signoff|sign-off|qualification|signature" src/Pegasus.Core/Identity src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs src/Pegasus.Infrastructure/Persistence/EfStaffAccountQueries.cs`

- **VERIFIED** — a Case workflow has `AssignedEngineerId`, not a separate
  sign-off Engineer: `CaseWorkflowEntity` line 9 and
  `EfCaseWorkflowStore.AssignEngineerAsync` line 479. Core currently requires
  only an eligible Engineer role/account for assignment. Command:
  `rg -n -C 6 "AssignedEngineerId|AssignEngineerAsync|record CaseLifecycle" src/Pegasus.Core/Lifecycle/CaseLifecycle.cs src/Pegasus.Core/Lifecycle/CaseCommandSeams.cs src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`

- **VERIFIED** — the old assessment vocabulary still owns
  `engineer.name`, `engineer.qualifications` and `engineer.signature` at
  `src/Pegasus.Core/Assessment/AssessmentContracts.cs:63-65,114-116`, and
  the current model snapshot permits those field paths. This ticket should
  stop report projection from reading them, but ownership/removal of those
  assessment fields needs an explicit decision outside this renderer contract.
  Command:
  `rg -n -C 5 "EngineerName|EngineerQualifications|EngineerSignature" src/Pegasus.Core/Assessment/AssessmentContracts.cs src/Pegasus.Core/Assessment/AssessmentWorkspace.cs src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs`
  and
  `rg -n -i "signoff|sign-off|qualification|signature" src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`

- **VERIFIED** — reusable binary-storage abstraction is
  `IDocumentContentStore` at
  `src/Pegasus.Core/Documents/DocumentContracts.cs:271`; its existing
  implementations are `LocalDocumentContentStore` and `BoxDocumentContentStore`.
  This is evidence of an existing storage pattern only; signature-image
  storage remains PLAT-068 scope. Command:
  `rg -n -C 4 "interface IDocumentContentStore|class LocalDocumentContentStore|class BoxDocumentContentStore" src/Pegasus.Core/Documents/DocumentContracts.cs src/Pegasus.Infrastructure/Custody/LocalDocumentContentStore.cs src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs`

- **VERIFIED** — current tests deliberately enforce the obsolete rule:
  Core rejects Neil with blank qualifications at
  `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs:67-80`;
  projection tests require the fixed tuple at
  `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs:101-108`;
  renderer integration tests assert Andy text and the single embedded resource
  at `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs:94-108`.
  Command:
  `rg -n -C 7 -i "Patterson|Signature|Signatory|Engineer" tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`

### Governing documentation

- **VERIFIED** — FRD-11 currently authorizes only the Andy tuple and says Ed
  and Neil cannot be selected until a qualification completes their tuple
  (`docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md:73-82`).
  It preserves deterministic, versioned, retained and review-gated generation
  at line 84. The most recent FRD-11 commit is
  `fb68225b docs: apply DELIV-040 operator review dispositions`; D31 has not
  landed on this checkout. FRD-04 also contains no D31/sign-off/signatory
  text. Commands:
  `rg -n -C 4 "Sign-off|sign-off|D18|D31|signature" docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md docs/frd/frd-04-parties-accounts-and-access.md`
  and
  `git log --oneline -5 -- docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`

### Mockup

- **VERIFIED** — staff shape is
  `{ id, name, username, role, state, lastSignIn, signs }`; a signatory has
  `signs: { qualifications, signature: true }`. Fixtures define Andy,
  Ed, and Neil; Neil has `qualifications: ''`, all three have a signature.
  Case fixtures separately hold `engineer` and `signoff` names. Source:
  `04-fixtures.js:33-35,136,199,277`. Command:
  `rg -n -i "signOff|signoff|qualifications|signature" C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/05-state.js C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/22-case-engineer.js C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/04-fixtures.js C:/Users/PC/Downloads/Pegasus_UI_v2_notes.md`

- **VERIFIED** — `signoffEngineers()` selects enabled Engineers with a
  signature; `defaultSignoff(engineerName)` uses the assigned Engineer when
  they have a signature, otherwise `a.patterson`, then the first qualifying
  signer (`05-state.js:152-153`). This matches D31's intended default at the
  prototype level.

- **VERIFIED** — the Report UI's own selector is inconsistent with that
  state rule: it filters to Engineers with both signature *and*
  qualifications (`22-case-engineer.js:116`), while its preview always prints
  `name — qualifications` (`:186`). Thus it does not yet model the required
  "Neil prints name alone" outcome. Command:
  `rg -n -C 8 "signOff" C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/05-state.js C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/22-case-engineer.js C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/04-fixtures.js`

### Gap list and proposed seam

| Status | Gap / implication |
| --- | --- |
| **VERIFIED** | The Core allowlist and assessment-field source hard-code D18-era policy; they cannot render Ed or qualification-less Neil. |
| **VERIFIED** | The infrastructure renderer can load only compile-time embedded signature assets and currently embeds Andy only. |
| **VERIFIED** | Case and staff persistence lack the Case sign-off FK and staff flag/qualification/image data required to source a tuple. |
| **ASSUMED — proposed DOCS-017 seam** | Reuse the existing `ReportEngineer` report contract, changing it from a signature key validated against a Core dictionary into a projection-supplied immutable tuple: display name, optional qualifications, and signature-image content/reference required by the renderer. `AssessmentReportProjectionInput` carries that tuple; `EfAssessmentReportProjectionSource` composes it from the Case sign-off Engineer and the flagged staff account. Core must allow empty qualifications and the template must omit the separator when empty. |
| **ASSUMED — ownership boundary** | [[CASE-040]] owns selecting/persisting the Case sign-off Engineer and rejecting an unflagged selection. [[PLAT-068]] owns staff sign-off eligibility, qualifications, image storage and its migration. DOCS-017 consumes their committed projection; it must not create a parallel staff-signing policy or migration. |

### Risks

- **VERIFIED basis / ASSUMED impact** — changing
  `AssessmentReportProjectionInput` affects its production source and test
  fakes; constructor call sites must be updated together.
  Command: `rg -l "new AssessmentReportProjectionInput" src tests` (plus the
  target-typed `new(` site in `AssessmentReadinessSummaryBrowserTests.cs`).

- **VERIFIED basis / ASSUMED impact** — replacing embedded assets needs a
  defined, retained image read at report-generation time; an arbitrary local
  path would contradict FRD-11's present fail-closed asset rule.

- **VERIFIED basis / ASSUMED impact** — signature selection cannot be
  independently proven until CASE-040 and PLAT-068 supply their contracts.
  DOCS-017 can prove projection/rendering with a supplied Ed tuple and blank
  qualifications, but cannot truthfully prove an unflagged Engineer is
  rejected without their implementation.

### Open questions for the operator

none
