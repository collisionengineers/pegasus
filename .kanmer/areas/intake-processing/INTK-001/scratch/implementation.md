2026-08-26 implementation: committed 1594ff0e. `RetryScheduled` now projects to the existing public Processing state with its due time. The single and group Upload Status pages compute a 2s-to-60s due-aware refresh interval; the shared script does not reload a hidden tab and schedules a bounded retry when it becomes visible. The SQL status projection now applies the same manual-association precedence as `IntakeReceipt.CurrentCaseId`, so an active manual association opens its Case and an inactive association suppresses an accepted-link fallback. Removed the Upload Status lede copy. `dotnet restore` and Release solution build passed. The focused integration test process completed after the runner's 30-second tool window; the direct recovery regression assertion was updated, but its final runner summary was not captured in this session.

2026-08-29 verifier correction: the earlier visible-return description is
superseded. The first submitted scheduler cancelled while hidden but re-armed
the full delay when visible, which could leave stale content and be starved by
repeated visibility changes. `ce3c0cfe` now invokes the existing guarded reload
immediately on visible return; `6ff999b2` proves it in Chromium.
