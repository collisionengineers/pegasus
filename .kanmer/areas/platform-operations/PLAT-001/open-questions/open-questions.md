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

## Blocked

- [x] **The mark files themselves are not in the tree.** Not a decision — a
  transport limit. `DesignSync.get_file` is capped at 256 KiB and every source
  PNG is larger, so all fourteen downloads returned `truncated: true` at exactly
  196,608 bytes. Committing one would have committed a corrupt image.

  Everything else is done: markup, stylesheet, and the design authority all
  expect the final filenames, and `wwwroot/images/marks/README.md` lists each
  file and where it is used. Copying the originals into that folder is the only
  remaining step. Until then each `<img>` renders as its empty `alt`, which
  breaks nothing because the marks are decorative — confirmed by the full axe
  suite passing with the files absent.

## Parked (explicitly deferred)

- Wiring the inactive sections to real save paths. That is the `UI-15`
  activation task named in `Pages/Cases/Assessment/Index.cshtml.cs`, and it
  needs `EXT-09` formula authority and `EXT-13` adapter acceptance first.
- The vehicle history check has no capability ID. Before it is more than markup
  it needs an inventory entry, a supplier contract and an accepted ADR.
- Engineer queries are not allocated: raising, replying to and resolving a query
  is its own workflow, not a panel.
