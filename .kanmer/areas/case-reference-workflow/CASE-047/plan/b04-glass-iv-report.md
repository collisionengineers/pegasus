# B04 phase 2b-iii-b + G22, and B08 residuals — implementation record (2026-09-07)

## Commits on `task/pegasus-v1-casework`

- `66932bcdb` — B08 residuals (squash of `b-work/b08r` bc2285181, 05efa46ee):
  `Pages/Cases/Shared/_CaseReportImagePreparation.cshtml` +
  `ReportImagePreparationView.cs` render the preparation cards and forms in
  both Files and Report from one partial (each form posts back to its own
  section; `RedirectToPreparation` returns there);
  `wwwroot/js/case-workspace.js` (included from `Details.cshtml`'s
  `@section Scripts`) drag-reorders Supporting images by submitting the same
  `SaveAssetPreparation` command the Move up/Move down forms submit
  (adjacent drop = two exchanged orders; otherwise the re-sequenced set),
  cloned from the card's own rendered form; read-only cards carry no
  controls and no drag payload. `CaseAssetPreparationWebTests` extended.
- `bd833637f` — B04 2b-iii-b (squash of `b-work/glass6` 4507acea1,
  6516fdb57, 70892589e): `DetailsModel.OnPostLaunchGlassAsync` /
  `OnPostResumeGlassAsync`; Estimate section shows the launch control only
  for an Engineer with an enabled Glass's account plus that Engineer's
  session for the Case; `Integrations/Glass/Callback/{correlation}` (GET and
  POST, `[IgnoreAntiforgeryToken]`, `[ResponseCache NoStore]`) resolves the
  one-use correlation by digest through the new Infrastructure
  `IGlassRepairEstimateSessionReader` on the EF store, requires the
  signed-in owner, never reads identity from the provider query; 404
  unknown, 403 stranger (correlation unspent), sign-in challenge with the
  full return URL; A's `GlassCallback` rate policy proved (429).
  `GlassProviderFixture` is the one scripted provider for both suites.
  `CaseWorkspaceLabels.GlassSession` labels.
- `2320d82a3` — G22 `86afac41e` merged as the same object.
- `074e9fe52` — G22 adaptation: Infrastructure `ResumeRequest` /
  `CallbackDelivery` records, scalar `ResumeAsync` and refusing Core
  `CompleteAsync` overload deleted; `CompleteAsync` reads
  `callback.RawQuery`; `GetEstimatorUrlAsync` is the port's; both pages
  depend on `IGlassRepairEstimateGateway` only.

## Evidence

- B08 helper on the shared base 025c60dd7 + B head: build 0/0,
  Architecture 109/109, `CaseDetailsWebTests | CaseAssetPreparationWebTests
  | CaseCustodyWebTests | ImageViewingWebTests` 99 PASS / 0 FAIL.
  `Test-UiCatalogue.ps1` failed only on A's then-missing Glass snapshot
  (since committed by A in c56e5805f).
- Glass's page helper on the same base: build 0/0, Architecture 109/109,
  Glass's suites + `CaseDetailsWebTests` + `ProductionCompositionTests`
  225 PASS / 3 FAIL. The three (`TheProvidersReturnLandsTheDraft…`,
  `TheSameReturnDeliveredTwice…`, `AReturnThatLostTheCasesEditAuthority…`)
  share one cause outside B: `ImportRawEstimateRequest.OccurrenceId` is an
  occurrence id, `CaseArtifactCustodyResult` carries only
  `DocumentId`/`VersionId`, and A's `EfCaseArtifactCustody` mints the two
  ids independently, so the import cannot find the retained XML and the
  session settles `Failed`/`glass.export.unreadable`. G23 requested on
  PR 672 (5564739345): add `OccurrenceId` to the custody result.
- Standalone after G22 adaptation: build 0/0, Architecture 100/100,
  Gateway + Persistence + XmlParser 140/141 (cross-store key proof needs
  A's store).
- Combined proof of 074e9fe52 (isolated tree: f0e1f4b7e + G22 local merge
  + B delta + DI patch) — recorded in scratch when finished.

## Handoffs

- DI lines for A (`DependencyInjection.cs`): options via lazy factory,
  `glass.mva` named client (no redirects, no cookies), case authority,
  session reader, `IGlassRepairEstimateGateway` → `GlassRepairEstimateGateway`;
  patch 2151 bytes, SHA-256 7948247b8b7b14415c1c3a42125c096130d81582043d245cb1fe8f852ccb1708.
- G23 request as above.

## Open

- A review 5564520110 item 1: durable callback claim before side effects —
  helper `b-work/glass7` in `../pegasus-worktrees/v1-casework-glass7`.
- Snapshot refresh for the changed Case page and the new callback route
  (combined host only), B09 fresh review.

## Judgement calls recorded

- Launch control absent (not disabled) without an enabled account; only a
  `bool` from the credential reader is kept.
- Launch/resume keep the edit lease; refusals land on `?section=estimate`
  through the existing `TempData` notices.
- Each preparation command still ends edit mode (B06 decision unchanged).
