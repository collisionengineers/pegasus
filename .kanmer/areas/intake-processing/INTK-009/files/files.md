## Files this ticket touches

### Web — Queues page (Unidentified tab, Not ready origin filters)
- `src/Pegasus.Web/Pages/Triage/Index.cshtml` — add `queue=unidentified` tab; add
  Unidentified kind subtabs (Images/E-mails/All); add Not-ready origin subtabs
  (Instruction-initiated/Image-initiated/All); render Unidentified rows and the
  Image-initiated rows table.
- `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs` — extend `Queue` validation to
  accept `unidentified`; add `Kind`/`Origin` bound query params; call
  `IUnidentifiedStore.ListQueueAsync`/count and `IImageIntakeQueries.ListAsync`
  for the new tab/filters; add row-label helpers.

### Web — retire the standalone Unidentified list, rebuild Details
- `src/Pegasus.Web/Pages/Unidentified/Index.cshtml` + `.cshtml.cs` — replaced
  with a redirect-only page model (`/Unidentified` → permanent redirect to the
  Queues tab).
- `src/Pegasus.Web/Pages/Unidentified/Details.cshtml` + `.cshtml.cs` — rebuilt
  to the design rule: one H1 (the U-reference), what/when/reason, the retained
  file/message with an operator-meaningful handle, resolution form kept. Drops
  the "Intake receipt — {guid}" origin line, the asset/evidence link walls, and
  the "custody detail" link text.
- `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` — remove the top-level
  "Unidentified" nav entry.
- `src/Pegasus.Web/Pages/Index.cshtml` — point the "Unidentified N" dashboard
  card at the new tab route.

### Core — new read-model contracts and policy
- `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs` — add
  `UnidentifiedMediaKind` enum, `UnidentifiedQueueRow` record, and
  `IUnidentifiedStore.ListQueueAsync(...)`.
- `src/Pegasus.Core/Intake/IntakeDecisionPolicy.cs` — add
  `UnidentifiedMediaKindPolicy.Classify(IntakeSourceChannel, string mediaType)`
  (or a small new file beside it), reusing the existing channel/media-type
  vocabulary rather than inventing a second one.

### Infrastructure — the query owner
- `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs` — implement
  `ListQueueAsync`: join `UnidentifiedItemEntity` (OriginKind=Receipt) against
  `IntakeReceiptEntity` on `OriginId == receipt.Id` in the same
  `PegasusDbContext`, classify media kind, and read filename/subject/sender off
  the joined receipt.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` — widen
  `ReadSubject` and `ParseSourceChannel` from `private` to `internal` so
  `EfUnidentifiedStore` reuses the existing subject-extraction and
  channel-parsing routines instead of duplicating them (same assembly).

### Presentation
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — add
  `UnidentifiedMediaKind(...)` label ("Image"/"E-mail"/"Document"); retire the
  banned `UnidentifiedOriginKind` "Intake receipt" wording (superseded by the
  media-kind label — no operator-facing surface needs the origin kind/GUID any
  more).

### Docs
- `docs/frd/frd-12-operator-experience.md` — replace the "Unidentified queue
  and detail" subsection with the tab/filter structure; fix any other text
  describing Unidentified as a separate page.

### Tests
- `tests/Pegasus.IntegrationTests/UnidentifiedPersistenceTests.cs` — add
  `ListQueueAsync` coverage: media-kind classification and filter correctness
  for seeded e-mail/document/image-shaped receipts.
- `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` or a new
  `TriageQueuesWebTests.cs` — Not-ready origin filter rows (seed an
  Instruction-initiated and an Image-initiated case), the `/Unidentified`
  redirect, and a targeted no-"intake"/no-GUID assertion on the rendered
  Unidentified tab markup.
- `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` — swap
  `/Unidentified`'s absence for the new tab route(s); keep one route per
  distinct rendered shape.

## Existing conventions reused (not reinvented)
- The `queue=` tab pattern and its `NotFound()` validation
  (`Triage/Index.cshtml.cs`).
- The Triage state subtabs pattern (`nav.subtabs`) for the two new filter
  rows.
- `OperatorLabels.OfficeTime`/`OfficeDate`/`OfficeClock` for every timestamp.
- `CaseSearchFilters`/`ISearchCases` unchanged for Instruction-initiated rows.
- `IImageIntakeQueries.ListAsync` unchanged for Image-initiated rows (already
  used by `/VehicleImages`).
- `EfIntakeReceiptStore`'s subject/channel parsing, reused via `internal`
  rather than copied.
