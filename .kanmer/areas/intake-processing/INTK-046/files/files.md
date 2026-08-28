# Files — INTK-046 (lane C2)

## Changed

- `src/Pegasus.Web/Pages/Triage/Details.cshtml` — full port onto the
  record frame (§1.5). Model unchanged apart from label helpers already
  present.
- `src/Pegasus.Web/Pages/Unidentified/Details.cshtml` — full port (§1.6):
  record frame, warning notice, retained source panel, history timeline,
  resolve dialog.
- `src/Pegasus.Web/Pages/Intake/Details.cshtml` — Received workbench
  restyle onto the design system; every handler binding unchanged.
- `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml` — image record
  restyle; back link `/Cases?tab=not_ready`; gallery retained.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — add the one
  `UnidentifiedResolutionTargetKind` label map (new concept, one list).

## Owned tests

- `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` — added
  to this lane's owned record (2026-08-28). It asserts directly on the
  `Pages/Triage/Details.cshtml` markup this ticket owns — the Complete
  control's named condition, the post-send correction's name and the
  permanent-history panel — so it is this lane's regression gate. No
  assertion was changed; the three it caught were port regressions and
  the markup was fixed instead. Recorded here rather than in the epic's
  `waves.md`, which this ticket does not own.
- `tests/Pegasus.IntegrationTests/{TriageEvidenceImages,QdosIntake,
  GroupedIntake,ImageIntake,ImageViewing}WebTests.cs` — owned; no
  assertion needed to change for the port.

## Not changed (verified not this lane's)

- `Pages/Triage/Index.*`, `Pages/Unidentified/Index.*` — PLAT-029/CASE-025
  stubs and queues.
- `Pages/Intake/Asset|Image|Source.cshtml(.cs)` — byte-serving routes,
  no markup.
- `Pages/Upload*`, `Pages/Uploads/**` — lane G (INTK-047).
- `site.css` / `site.js` — no new CSS or scripts (rule). The `.gated`
  tooltip shape the fix reuses already exists there.
