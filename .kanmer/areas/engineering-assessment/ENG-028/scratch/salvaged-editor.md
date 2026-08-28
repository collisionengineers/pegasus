## 2026-08-28 — multi-estimate editor salvaged off ENG-025

The estimate-editor surface was implemented on ENG-025's branch by
mistake and has been moved here. Nothing is lost; nothing was rewritten.

**Where the work is**

- Branch `task/eng-028-estimate-editor`, pushed, worktree
  `../pegasus-worktrees/eng-028-estimate-editor`.
- One commit, `6b4d11db` ("feat(assessment): salvage the multi-estimate
  editor from ENG-025 (ENG-028)"), based on `origin/dev` @ `9868cf58`.
- The original commit is `5611f316` on `task/eng-025-assessment-shell`
  (subject mislabelled "(ENG-026)"); still reachable — ENG-025 removed it
  with a revert commit (`bc16d8fa`), not a history rewrite.

**What it contains**

Estimate tabs (tablist), the per-estimate editor (Delete estimate,
Duplicate, Use estimate / Current chip, Save estimate; name, source,
repair days, labour/paint rates, paint materials, other costs, VAT %,
lines table, notes, totals), the Import-estimate dialog's name and source
fields, and two `RepairSpecificationSourceRoute` label arms (`Json`,
`AiDraft`) in `Presentation/OperatorLabels.cs`.

**State — read before working**

- `Pages/Cases/Assessment/Index.cshtml` and `Index.cshtml.cs` were taken
  **whole** from `5611f316` (a cherry-pick onto `dev` conflicted, because
  the editor is written against ENG-025's ported page, which is not on
  `dev` yet). So this commit's diff against `dev` also contains ENG-025's
  unmerged shell port. Once ENG-025's PR merges, `git merge origin/dev`
  here and the remaining delta is the editor alone. If ENG-025's shell
  changes in review, re-take those two files from the merged shell and
  re-apply the editor hunks.
- `OperatorLabels.cs` carries only the two added label arms (the UTF-8 BOM
  that `5611f316` introduced on that shared file was stripped); PLAT-023's
  additions from `dev` are preserved.
- `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs` is
  likewise the whole `5611f316` version (ENG-025 retarget + editor
  assertions).
- **Not built, not tested here** — the branch is a salvage, not a
  deliverable. No PR opened; ENG-028 stays blocked on ENG-025 and ENG-027
  and belongs to wave 4.
- Still to do on this ticket beyond the salvage: the Send to Claude job
  dialog (direction, target % slider, disabled without an Engineer's
  Value) and report-draft generate/preview per context.md §1.9. ENG-025
  already ships a Send to Claude dialog against `ICreateAiJob`; check it
  before rebuilding one.
