# Open questions — Claude Design UI implementation

## Resolved

- [x] **How should prototype sections showing deferred capabilities be treated?**
  `screens/Assessment.html` and parts of `screens/Case.html` show `UI-15`,
  `EXT-09`, `EXT-10`, `EXT-12`, `EXT-13` (all `Later / 1.0.0`) plus Experian
  AutoCheck, vehicle lookup, engineer queries and the upload-link dialog, which
  are not in `docs/capabilities.md` at all. `docs/design/README.md` says deferred
  capabilities get "no alpha navigation, control, workflow or placeholder".
  **Operator decision, 2026-08-17: ship them as unbound design markup** — static,
  no page-model binding, no POST handler — extending the precedent already
  recorded in `Pages/Cases/Assessment/Index.cshtml.cs` for `UI-15`. Each section
  carries an HTML comment naming its capability ID and allocation.

- [x] **Should the top navigation bar be replaced by the design's left rail?**
  The rail is new CSS held in prototype-local `<style>`, not in the design
  system's stylesheet, and it changes the current-route signal from an underline
  to a left border. **Operator decision, 2026-08-17: yes, switch to the left
  rail**, keeping `aria-current="page"` plus a weight change so the route is
  never signalled by colour alone, and recording the divergence in
  `docs/design/README.md`.

- [x] **PNG icon marks or the approved Lucide sprite?**
  The prototypes use 14 PNGs through `Mark`/`MarkLabel`/`MarkEmpty`, which are
  defined in `screens/shared.jsx` — screen-local scaffolding, not design-system
  components. The design system's own README says "Icons come only from `Icon`
  (16 Lucide glyphs)"; `docs/design/README.md` carries a checksummed register of
  exactly those glyphs, says "No brand or decorative imagery is needed for the
  internal Web application", and prohibits "decorative or generated replacement
  icons". Two authorities and the design system agree against the scaffolding.
  **Resolved from the repository: keep the Lucide sprite**, and use the existing
  approved brand asset `wwwroot/images/logo_no_margin.png` for the rail lockup.
  No new binary assets and no register change.

## Parked (explicitly deferred)

- Wiring the unbound sections to real save paths. That is the `UI-15` activation
  task named in `Pages/Cases/Assessment/Index.cshtml.cs`, and it needs `EXT-09`
  formula authority and `EXT-13` adapter acceptance first. Out of scope here:
  this ticket is presentation.
- Experian AutoCheck has no capability ID. Before it can be more than markup it
  needs an inventory entry, a supplier contract and an accepted ADR. Recorded so
  it is not silently absorbed into a UI ticket.
