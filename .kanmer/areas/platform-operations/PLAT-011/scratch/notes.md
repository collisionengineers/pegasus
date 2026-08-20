## 2026-08-20 — post-review fix: CI sql-integration failure

Orchestrator reported PR #452 failing `sql-integration`: `SentEvidencePollPersistenceTests.ExactReplyPollAtomicallyLinksTriageAndReplayAllowsStaffCompletion` threw "Unable to resolve service for type UserManager`1[PegasusIdentityUser]" while activating `EfStaffAccountAdministration`.

Root cause: `GetTriage`/`GetRetainedMail` now depend on `IStaffAccountQueries` for display-name resolution, and both are reachable from the Worker (`PollSentEvidence` et al.), which composes Infrastructure without ASP.NET Identity. `EfStaffAccountAdministration` required `UserManager<PegasusIdentityUser>` in its constructor even though its `IStaffAccountQueries` methods (`ListAsync`/`GetAsync`) never touched it — so any host without Identity composed (Worker, the SentEvidencePoll test host) failed to construct it at all, not just in this new code path but latently for any future caller too.

Fix: split the read side into a new `EfStaffAccountQueries` (depends only on `PegasusDbContext`, no `UserManager`), registered directly for `IStaffAccountQueries`. `EfStaffAccountAdministration` keeps the `UserManager`-dependent mutations (create/disable/assign-roles/review) and reuses `EfStaffAccountQueries.Summary`/`ParseRole` (internal statics) rather than duplicating the mapping.

Verified: full build zero warnings/errors; `SentEvidencePollPersistenceTests` 2/2 (was failing); `WorkerCompositionTests` 4/4; full `Pegasus.ArchitectureTests` 97/97; full `Pegasus.Core.Tests` 701/701; `CaseDetailsWebTests` 23/23; `MailWorkspaceWebTests` 15/15; `AdministrationSearchAccountWebTests` 6/6.

Commit: `4d3073adcce77137670a973000a64193cf41b194`, pushed to `task/plat-011-actor-display-names`.
