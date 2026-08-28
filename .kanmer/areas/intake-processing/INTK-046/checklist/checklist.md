# Checklist — INTK-046

- [x] P3 `OperatorLabels.UnidentifiedResolutionTarget` map added (one list).
- [x] P1 Triage Details ported to the record frame; determinations post
      `record_finding`/`supersede_finding`; await/complete/cancel/reopen/
      link dialogs post existing handlers; vehicle images section kept.
- [x] P2 Unidentified Details ported; retained source + history panels;
      resolve dialog posts `OnPostResolveAsync`; no invented kinds.
- [x] P4 Received Details restyled; every existing handler still bound;
      empty-only sections absent when empty.
- [x] P5 Image record restyled; back link `/Cases?tab=not_ready`;
      gallery + evidence viewer retained; close dialog posts
      `OnPostCloseAsync`.
- [x] Build green (`restore --locked-mode`, `build -c Release`, 0
      warnings; re-verified after merging origin/dev).
- [x] Every button posts an existing handler; no inert control.
- [x] No new CSS/JS, no inline styles/scripts; shell classes only.
- [x] Exactly one `<main>` (the shell's); no stray `aria-current`/
      `aria-pressed`.
- [x] Labels via `OperatorLabels` (no raw enum names operator-facing).
- [x] Simplification pass recorded in the plan under a dated heading.
- [x] Slices committed `feat(intake): … (INTK-046)`; PR #605 opened to
      `dev`; stopped at the open PR (no merge, no proof).
