# MAIL-021 plan

## Verified facts (read-only checks, 2026-08-27)

- `src/Pegasus.Core/Intake/RetainedMail.cs` lines 649-656: the `StaleAfter`
  remarks say "Inbound polling is a one-minute timer, so fifteen minutes is
  fifteen consecutive missed ticks".
- `src/Pegasus.Worker/MailboxFunctions.cs`: `InboxRecoveryFunction` runs on
  `[TimerTrigger("%ApprovedInboxPollSchedule%")]`;
  `local.settings.example.json` sets it to `0 */5 * * * *` (every five
  minutes). Graph change notifications are the primary wake path
  ([[EPIC-010]] context: webhook ingests in ~7 s).
- Under the current schedule 15 minutes is three consecutive missed recovery
  ticks.

## Change

One edit, comment only: rewrite the `<remarks>` on `StaleAfter` to state that
notification wakes are primary, the recovery timer polls every five minutes,
and 15 minutes therefore means three missed recovery ticks. Keep the
PROVISIONAL / open-decisions sentence. `TimeSpan.FromMinutes(15)` is
unchanged; no code, test, or doc changes.

Reuse: nothing to build; the change edits the existing remark in place.

## Checklist

- [x] Rewrite the `StaleAfter` remarks in `RetainedMail.cs`
- [x] `dotnet restore ./Pegasus.slnx --locked-mode` (exit 0)
- [x] `dotnet build ./Pegasus.slnx --configuration Release --no-restore` (exit 0, 0 warnings)
- [x] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` (log under `artifacts/mail-021/test-full.log`; see post-implementation report for the contention failures and the clean targeted re-run)
- [x] Commit, push `task/mail-021-staleafter-rationale`, PR to `dev` (267b45a0, PR #575)

## Simplification pass — 2026-08-27

Diff is six XML-doc comment lines in one file (commit 267b45a0); no code
changed, so the reuse, efficiency and altitude lenses have nothing to act on.
Simplification lens: the remark names the two symbols it depends on
(`InboxRecoveryFunction`, `ApprovedInboxPollSchedule`) so the next schedule
change is greppable; the PROVISIONAL / open-decisions sentence is retained
unchanged. No findings; nothing applied.
