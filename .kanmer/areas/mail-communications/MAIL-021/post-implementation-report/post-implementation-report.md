# MAIL-021 post-implementation report

## Delivered

Commit 267b45a0 on `task/mail-021-staleafter-rationale` (worktree
`../pegasus-worktrees/mail-021-staleafter-rationale`): the `StaleAfter`
`<remarks>` in `src/Pegasus.Core/Intake/RetainedMail.cs` now states that Graph
change notifications are the primary wake, the recovery poll
(`InboxRecoveryFunction`, `ApprovedInboxPollSchedule` = `0 */5 * * * *`) runs
every five minutes, and fifteen minutes is three consecutive missed recovery
ticks. The PROVISIONAL / open-decisions sentence is retained. No behaviour
change; `TimeSpan.FromMinutes(15)` untouched. Plan checklist fully ticked.

## Verification

- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit 0,
  0 warnings
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "Category!=Corpus"` — final serial run exit 1
  (`artifacts/mail-021/test-full.log`, `test-exit.txt`): Core 1001/1001,
  Architecture 100/100, Integration 985/987. Earlier concurrent runs failed 5
  and 10 tests on SQL execution/login timeouts and Playwright timeouts while
  other lanes used LocalDB (attempt 1 log kept as
  `test-full.attempt1-killed.log`). The two remaining failures,
  `LocalDbTemplateDatabaseTests.TheTemplateIsBuiltOncePerProcessAndEveryDatabaseIsItsOwn`
  and `RestoringTheTemplateMatchesMigratingTheDatabase` (expected "Template",
  actual "Migrated"), came from a concurrent process rebuilding the shared
  LocalDB template; a single re-run of that class alone passed 14/14, exit 0
  (`artifacts/mail-021/test-localdbtemplate-rerun.log`). Verification was
  driven by the controller; no further runs were made.

## Deviations

None in scope. Test evidence is a full-suite run with two workstation-contention
failures plus a targeted clean re-run, not a single all-green run; the reviewer
should weigh CI on the PR as the clean signal.

## Next

Independent review of the PR (kanmer-review); the author does not merge.
