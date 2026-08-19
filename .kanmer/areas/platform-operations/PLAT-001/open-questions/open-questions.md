# Open questions — Claude Design UI implementation

## Resolved

- [x] **How should prototype sections showing deferred capabilities be treated?**
  `screens/Assessment.html` and parts of `screens/Case.html` show `UI-15`,
  `EXT-09`, `EXT-10`, `EXT-12`, `EXT-13` (all `Later / 1.0.0`) plus a vehicle
  history check, vehicle lookup, engineer queries and the upload-link dialog,
  which are not in `docs/capabilities.md` at all.
  **Operator decision, 2026-08-17: ship them as unbound design markup** — the
  surface renders, nothing is wired. This extends the precedent already recorded
  in `Pages/Cases/Assessment/Index.cshtml.cs` for `UI-15`.

  *Corrected 2026-08-17, same day.* The first implementation read "unbound" as
  licence to leave sections out where the prototype showed data that could not
  be invented, and shipped only three disabled buttons on the assessment bar.
  That narrowed an explicit instruction. The operator's word was **inactive**,
  not absent. Every section now renders: estimate tabs and the add control, the
  vehicle history check on both the case and the assessment, vehicle lookup,
  engineer queries, the damage-location vehicle plan, and report images. Each
  names what it is for, states that nothing has been recorded, and stores
  nothing.

- [x] **Should the top navigation bar be replaced by the design's left rail?**
  **Operator decision, 2026-08-17: yes**, keeping `aria-current="page"` plus a
  weight change so the route is never signalled by colour alone, and recording
  the divergence in `docs/design/README.md`.

- [x] **The fourteen PNG marks: adopt or keep the Lucide sprite?**
  **Operator instruction, 2026-08-17: adopt them. They were commissioned for
  this work.**

  *This replaces an earlier wrong answer.* The first pass declined them, reading
  `screens/shared.jsx` as prototype scaffolding and citing this repository's
  "no decorative imagery" line and its ban on "decorative or generated
  replacement icons". Both rules were written against *generated or substitute*
  glyphs and marketing photography. Purpose-drawn marks supplied by the operator
  are neither, and it was not this ticket's place to decide otherwise.

  Adopted as a second, distinct class of imagery: a Lucide glyph names an action
  or a state inside a row, a mark names a whole surface. Every mark is
  decorative — `aria-hidden`, empty `alt`, always beside text carrying the same
  meaning — so the one-icon-per-meaning rule is untouched. `docs/design/README.md`
  now records them and narrows the two rules accordingly.

- [x] **The mark files themselves are not in the tree.**
  Resolved 2026-08-18: the operator placed the full Claude Design folder at
  `PegasusDesign/` in the worktree. The ten placed marks were downscaled to
  128×128 (Lanczos) from the 1024×1024 sources and committed to
  `src/Pegasus.Web/wwwroot/images/marks/`. Their SHA-256 values are recorded in
  the marks README and in the design authority's source-to-runtime mapping. The
  four unplaced marks (`activity`, `brand`, `calendar`, `casefolder`) are not
  referenced by any markup and were not copied.

## Parked (explicitly deferred)

- Wiring the inactive sections to real save paths. That is the `UI-15`
  activation task named in `Pages/Cases/Assessment/Index.cshtml.cs`, and it
  needs `EXT-09` formula authority and `EXT-13` adapter acceptance first.
- The vehicle history check has no capability ID. Before it is more than markup
  it needs an inventory entry, a supplier contract and an accepted ADR.
- Engineer queries are not allocated: raising, replying to and resolving a query
  is its own workflow, not a panel.

## Operator resolution — Experian AutoCheck — 2026-08-19

- [x] The vehicle-history control represents a real future capability, not markup to retire. [[ENG-001]] is the backlog owner. It remains inactive until its capability authority, exact supplier/API contract, test evidence, Core behavior, and integration boundary are accepted.

## Operator correction — post-report queries — 2026-08-19

- [x] Engineers do **not** raise queries. Queries are raised **to** the responsible Engineer after a report has been sent. [[CASE-002]] is the backlog owner for that workflow and for keeping case notes separate. Any prototype wording or inactive markup implying Engineer-originated queries must be corrected when the capability is activated.

## Operator resolution — case notes — 2026-08-19

- [x] Case notes are a real future capability. [[CASE-004]] owns their separate behavior and activation; [[CASE-002]] now owns post-report queries only. The inactive notes surface remains non-persistent until CASE-004 is accepted.

## Operator resolution — four unplaced supplied marks — 2026-08-19

- [x] `activity`, `brand`, `calendar`, and `casefolder` are intended for use, not retirement. [[PLAT-008]] owns reviewing the actual artwork, mapping each mark to the appropriate genuine surface, recording its checksum mapping, and placing it without inventing a feature or placeholder solely to consume an asset.
