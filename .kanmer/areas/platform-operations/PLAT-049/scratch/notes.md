- 2026-08-28 PLAT-023 review disposition: this ticket also owns the EVA
  handoffs panel (Case/Route/Engineer/State/Result) — it needs a new Core
  listing query over EVA submissions + bundle exports (existing
  IEvaSubmissionQueries are per-case/failures/counts only and carry no
  Route or Engineer). Name that query as a dependency when this lane
  starts. The Service-health "View" control (prototype toast only) is also
  revisited here.

## 2026-08-29 round 3 — blocker for the orchestrator

`origin/dev` at `cba29a4f` **does not compile**. INTK-001 (`6c648c59`, merged
`8e4f9346`) removed `Guid? CaseId` from `QueuedIntakeStatus` after TICK-058
(`0d985c9e`) had already merged a caller that passes it:
`tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs:284` — CS1739.

Every lane branched off `dev` inherits this. No PR can be green until INTK-001
or TICK-058 repairs it. Reported, not fixed — outside PLAT-049's Owns list.

Verifier N+1 finding is in `Pages/Administration/Automation/Activity.cshtml.cs`,
last touched by `5dd27a27 (AUTO-006)`. Not this lane's file → reported to
AUTO-006, not fixed. `Pages/Operations/**` has no `IGetCase` call at all.
