# Plan — INTK-025

Branch `task/intk-025-qdos-report-rules` from origin/dev, worktree
`../pegasus-worktrees/intk-025`. PR to dev after MAIL-007's (serial merges).

1. Engine: `GuardedPrefixes` on `FieldDefinition`; regex lookbehind built
   per definition; delete the `TP ` literal. Behaviour identical for QDOS
   (which passes `["TP"]`), engine grammar-free.
2. QDOS policy: `WithReportFacts` — for fragments whose source label names a
   report file, synthesize `Our Client's Vehicle: {value}` from the
   `Vehicle:` line (cut at `Colour:`/`Speedo:`/`Reg No:`) and
   `Vehicle Mileage: {n}` from a digit-bearing `Speedo:` line; appended
   after all content fragments so the letter always outranks.
3. QDOS policy: `WithCircumstances` — the paragraph after the
   "…following accident circumstances?" line, terminated by
   `Damage Area`/`Pre-existing Damage`/`TP `/`If you need`, synthesized as
   `Accident Circumstances: {text}`.
4. Version 4; unit + corpus facts; suites; Release build 0/0.
5. Simplification pass before the PR.
