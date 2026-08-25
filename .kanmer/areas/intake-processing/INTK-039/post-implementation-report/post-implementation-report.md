# Post-implementation report — INTK-039

## Delivered

- Added SQL Server migration `20260825121453_GrantWorkerImageIntakeLifecycleEvents` so the Worker can replay and append image-intake lifecycle events while UPDATE and DELETE remain denied.
- Updated the deployment bootstrap permission matrix to require the same append-only contract for Web and Worker.
- Changed grouped image outcomes with no settled Case, Image Intake, or Unidentified destination to remain Processing, with automatic refresh and no premature staff decision controls.
- Added non-sensitive failure classification and error status to the existing image-intake activities.
- Updated the affected upload decision test to invoke the existing grouped-image reconciliation boundary explicitly.

## Validation

- `dotnet restore Pegasus.slnx --locked-mode` — passed.
- `dotnet build Pegasus.slnx --configuration Release --no-restore -nodeReuse:false` — passed, 0 warnings and 0 errors.
- Focused integration filter covering upload outcomes/pages, runtime-role migration, migration census, custody, and queues — 54 passed, 0 failed, 0 skipped.
- `dotnet ef migrations has-pending-model-changes ... --no-build` — no model changes pending.
- `Test-AzureDeploymentPlan.ps1 -Mode Local` — passed.
- `dotnet test Pegasus.slnx --configuration Release --no-build --no-restore --verbosity minimal`:
  - Core: 981 passed, 0 failed, 0 skipped.
  - Architecture: 99 passed, 0 failed, 0 skipped.
  - Integration: 956 passed, 0 failed, 16 skipped.
- `git diff --check` — passed (repository line-ending conversion warnings only).
- Simplification pass — passed; findings and dispositions are recorded in the plan.

## Remaining release proof

Production migration permission read-back, the fresh screenshot journey, queue count/row equality, custody fold, and selective pre-release reset remain verification work after the reviewed PR merges and the exact-SHA release receives its required approvals.

## Independent review corrections

Codex review of `c205afb0` found two actionable races. The implementation now reports a resolved Unidentified destination as a terminal, non-actionable outcome and suppresses every submission-level action while any member remains Working. Direct upload tests pass 25/25; the complete affected slice passes 56/56; the post-fix Release build passes with 0 warnings/errors. Fresh CI and rereview are required on the updated commit.

## Second rereview corrections

The group-action gate now reuses the page's existing refresh predicate, covering both nonterminal queue members and terminal Working outcomes with one condition. The grouped-image Processing guard now applies only to `NeedsSorting`, preserving terminal Blocked intake. Direct tests pass 26/26, the affected slice passes 57/57, and the Release build remains warning-free.
