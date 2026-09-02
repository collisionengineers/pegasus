# Post-implementation report — DELIV-041

PR: https://github.com/collisionengineers/pegasus/pull/647 (base `dev`).
Branch `task/deliv-041-case-workspace-decisions`, worktree `.worktrees/deliv-041`.
Commit `632ec0c436e301023f3aa6a5e1f4e0e149a192b5`. Docs only; no code.
`docs/operator-notes.md` untouched.

## What changed, per file

- `docs/frd/frd-12-operator-experience.md` — route table: `/Cases/{id}` is
  the single-scroll Case record, `/Cases/{id}/Assessment` a 301 to
  `?section=estimate`, `/Operations` loses Service health (D29, D30, D37);
  Cases queues gain Pre-Case work "Awaiting instruction" and Not-ready rows
  no longer carry image-initiated projections (D38); § Case workspace rewritten
  to the eleven-section record with sticky ribbon/action bar/jump-nav, Send to
  EVA re-send and no Open Assessment / Download EVA package (D29–D34, D36,
  D39–D42); § Assessment becomes the redirect statement with the Estimate
  section content and the D16 drop retained; § Operations and
  § Administration for D37; keyboard exception and acceptance evidence
  reworded for the Case record; capability links extended.
- `docs/frd/frd-01-case-identity-and-lifecycle.md` — Case-owned data adds
  Sign-off Engineer, Engineer notes, storage location and inspect-at choice;
  Send to EVA bullet (D36); Engineer-sections read-only rule replaces the
  Assessment bullet (D30); new § Sign-off Engineer (D31, supersedes D18) and
  § Engineer notes (D32).
- `docs/frd/frd-04-parties-accounts-and-access.md` — Administrator column
  gains the Sign-off Engineer account setting; § Staff accounts describes the
  flag, qualifications, signature image, Administrator-only, Action Logs (D31).
- `docs/frd/frd-06-vehicle-and-engineering-evidence.md` — § Inspection
  address: inspect-at fast-update choice and storage location (D33);
  § Vehicle data: one "Look up DVLA & MOT" action with per-field chips (D34);
  new § Damage record (D39), § Valuation sources (D40), § Settlement (D41).
- `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` —
  assessment bundle carries the marked damage diagram and the sign-off tuple;
  § Initial renderer activation: tuple read from the Sign-off Engineer
  setting, D31 supersedes D18, DOCS-017 delivers; report-draft control on the
  Report section with the 301; availability paragraph replaced by the D30
  always-viewable/read-only rule; fee note preview (D42); AI Job List gains
  the MarketResearch kind (D35); Operations action and signature sentences
  updated.
- `docs/design/README.md` — design principle "sections as tabs" scoped to
  records other than the Case record plus the Case record scroll rule (D29);
  routes table (Case record sections, Assessment 301, Operations, Cases
  `awaiting-instruction`); component vocabulary adds `case-sticky`,
  `section-nav`, `section-link`, `suggest-btn`, `damage-diagram`, `impact`,
  `tyre-card`, `valuation-card`, `outcome-option`, `derived`, `report-image`,
  `cropper`; workspace contract for Cases, Case workspace (eleven sections),
  Assessment (redirect + Estimate section), Operations, AI job kinds; removed
  surfaces; prototype-defect row for D11; deferred seams (AutoTrader inside
  Pegasus, layout switch); source/runtime map rows for the design contract of
  record and Engineer signatures (D31).
- `docs/capabilities.md` — provenance note; new rows `UI-17`, `UI-18`,
  `UI-19`, `CASE-32`, `CASE-33`, `CASE-34`, `ENG-03`, `ENG-04`, `AI-11`,
  `RPT-06` with canonical owner (FRD section) and the EPIC-012 ticket ids
  (CASE-038, ENG-034, ENG-035, ENG-036, CASE-039, CASE-040, PLAT-068,
  CASE-041, AUTO-018, PLAT-069, CASE-042, DOCS-018, UIIMP-014; existing
  CASE-029, ENG-029, ENG-031, DOCS-017, CASE-009); boundary notes on
  `EXT-01` (D34), `EXT-10` (D40, allocation unchanged), `CASE-30` (D36),
  `UI-15` (D30/D39/D41), `RPT-02` (D31); ordered-release bullet.
- `docs/boundaries.md` — rows: AutoTrader research inside Pegasus (D35),
  Case record layout switch (D29), `EXT-10` valuation adjustments (D40).
- `docs/open-decisions.md` — AutoTrader note (D35); report-wording and
  signatory rows closed by D31; § Later operator UI capabilities carries the
  D29/D30/D31 resolution.
- `docs/engineering.md` — § Test support gains "Case Workspace v2 fixture
  values (D43)": mockup-derived values may be used after operator sign-off;
  states plainly that they include real claimant names and telephone numbers;
  `corpus/` stays local, ignored and immutable.
- Board (not in the diff): EPIC-011 `context.md` §2 gained rows D29–D43 and
  the line "2026-09-02: D29–D43 recorded by DELIV-041; D18 superseded by
  D31; §1.8/§1.9 superseded for the Case record by EPIC-012 context.md."
  Written with LF line endings; the git index already stores the file as LF,
  so the tracked bytes are unchanged apart from the additions.

## Verification

- `git diff --stat` (before commit): 10 files changed, 398 insertions,
  147 deletions — the ten governing docs only; `git status` shows nothing
  else.
- `grep -c "D29\|D3[0-9]\|D4[0-3]"`: frd-01 6, frd-04 2, frd-06 5, frd-11 9,
  frd-12 26, design README 61, capabilities 17, boundaries 3,
  open-decisions 4, engineering 2. Per id, every D29–D43 greps in at least
  one governing doc (D43 in engineering, design README, capabilities) and in
  EPIC-011 `context.md`.
- `./scripts/Test-DocumentationLinks.ps1` → "All relative Markdown links
  resolve (86 files checked)." exit 0.
- `./scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD` →
  "Markdown placement passed for origin/dev..HEAD." exit 0.
- Markdown convention: H1 on line 1 untouched, blank line before every added
  heading, compact `| --- |` delimiters, added prose rewrapped near 78
  columns (link-dense lines and table rows run long, as the convention
  allows).
- No Markdown lint beyond those two scripts exists in `scripts/`; CI runs the
  placement test on every change set.

## Deviations and notes for review

- Kanmer 0.3.3 has no `get_execution_packet` tool; the ticket record
  (`get_item`) was used as the packet — it names the recorded branch and
  worktree used here.
- `docs/frd/frd-07-eva-and-external-engineering-handoff.md` still says
  "Export is available only while the case is in `Review`"; D36 (re-send in
  With Engineer) is recorded in FRD-01 and FRD-12 with FRD-07 cited for
  package mechanics. FRD-07 is outside this ticket's file list, so it was not
  edited; CASE-040 (or a follow-up docs ticket) should reconcile it.
- FRD-11 keeps its fail-closed rule for an incomplete signature tuple, while
  DOCS-017's ticket body says a missing qualification prints the name alone.
  D31 does not settle that; it is left for DOCS-017 to resolve in FRD-11.
- New capability ids were needed for the rows the plan asked for; ten were
  added (`UI-17`–`UI-19`, `CASE-32`–`CASE-34`, `ENG-03`, `ENG-04`, `AI-11`,
  `RPT-06`). D34 and D40 were recorded on the existing `EXT-01` and `EXT-10`
  rows rather than as new ids; `EXT-10`'s allocation is unchanged.
- `?tab=awaiting-instruction` and the `?section=` keys
  (`engineer-notes`, `inspection`, `damage`, `valuation`, `estimate`,
  `settlement`, `report`, `files`) are the natural kebab-case of the
  decision's names; the implementing tickets own the final route keys.
- Simplification pass: n/a — docs-only.
