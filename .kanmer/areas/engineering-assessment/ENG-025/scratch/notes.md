## 2026-08-28 — handed to review

PR [#616](https://github.com/collisionengineers/pegasus/pull/616) → `dev`,
open, not merged. Branch `task/eng-025-assessment-shell` @ `5d3b658c`,
`origin/dev` @ `9868cf58` merged in.

For the reviewer, in order of interest:

1. The scope split — `5611f316` (the multi-estimate editor, mislabelled
   "(ENG-026)") reverted by `bc16d8fa` and salvaged to
   `task/eng-028-estimate-editor` (`6b4d11db`, pushed, no PR). Confirm the
   revert is complete and that the shell needs none of it.
2. `5d3b658c` — the report-draft condition now takes the Current estimate.
   Worth a second pair of eyes: it changes when Generate/Preview report
   draft are offered.
3. `22dd1870` — four assertions corrected, none weakened; the reasoning is
   in the commit message and the post-implementation report.
4. Open: the 1580/1100/760 clipping walk is not proven (the page's browser
   test runs at 1920×1080). The orchestrator's wave gate owns it.
5. Open, another lane's: `RepairSpecificationSourceRoute.Json` and
   `.AiDraft` have no `OperatorLabels.RepairSpecificationRoute` arm on
   `dev`, so they read "recorded before source tracking".
