# Work Centre

- **Mockup route:** `pegasus_work_centre_v28.html` in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Index.cshtml` (partial `Pages/_WorkCentreBody.cshtml`, model `Pages/Index.cshtml.cs`)

- [**How it works**](how-it-works.md)

## Screenshots

- [s13-work-centre-1580.png](../../current/v28-shots/s13-work-centre-1580.png) · [1440](../../current/v28-shots/s13-work-centre-1440.png) · [760](../../current/v28-shots/s13-work-centre-760.png)
- [s14-work-centre-empty-1580.png](../../current/v28-shots/s14-work-centre-empty-1580.png) · [1440](../../current/v28-shots/s14-work-centre-empty-1440.png) · [760](../../current/v28-shots/s14-work-centre-empty-760.png)
- [s15-work-centre-unavailable-1580.png](../../current/v28-shots/s15-work-centre-unavailable-1580.png) · [1440](../../current/v28-shots/s15-work-centre-unavailable-1440.png) · [760](../../current/v28-shots/s15-work-centre-unavailable-760.png)

## Notes

- The Office/Mine scope switch and the seven kind-filter chips are rendered as
  the live page's real links, but this offline capture does not re-run a
  filtered list when they are followed (no server). The strip's "Kind filter"
  control demonstrates the one filter-active visual (`chip.on` plus the "All
  kinds" clear chip) rather than wiring every combination.
- The Needs attention list's default selection is the Today-group Review Case
  row (the fixture "hero" Case, `QDOS26214`) rather than literally the first
  row in due-day order, so the Today pane can show the hero's full fact grid.
  The live page always selects the first row of the ordered list; this is
  documented as a deliberate fixture simplification, not a captured behaviour.
- The Assign Engineer dialog is reachable from the mockup strip
  ("Assign Engineer dialog") rather than only by selecting the one
  Unassigned-Engineer row (`QDOS26205`) in the list, since this capture does
  not re-select rows dynamically. Its content (registration, claimant,
  Principal, Engineer, the Engineer picker, and the role-gated "Assign to me"
  button) is otherwise the live dialog's own markup and fixture-appropriate
  values.
- "Assign to me" inside that dialog is shown only when the strip's Role is
  set to Engineer, matching `WorkCentreAssignment.CanAssignToMe`
  (`actor.IsInRole(StaffRole.Engineer)`), which the Administrator default
  never satisfies.
- Only one AI job row of each state actually read by the page (Queued, Taken,
  Draft ready, Failed) is shown; Completed, Cancelled and Expired jobs are
  never listed here in the live page either (`ReadAiJobsAsync` filters to
  Queued/Taken/DraftReady plus Failed jobs from the last 7 days), so they are
  not fixture rows.
- Every reference used (`QDOS26214`, `QDOS26198`, `QDOS26177`, `QDOS26150`,
  `QDOS26205`, `T-2601`, `U-1140`) is drawn from the shared fixture sheet;
  `QDOS26090` is an additional Completed-tab example invented in the same
  numbering style because the fixture sheet names no Completed-state Case.
