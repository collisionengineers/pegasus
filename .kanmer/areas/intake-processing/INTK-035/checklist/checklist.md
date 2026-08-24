# Checklist — INTK-035

- [x] Worktree `pegasus-worktrees/intk-035-open-triage` on
      `task/intk-035-open-triage` from `task/intk-033-triage-from-intake`,
      upstream unset
- [x] `ProcessIntake.IsTriageRequest` widened to `public`
- [x] `Details.cshtml.cs`: `ITriageQueries`, `ICreateTriageFromIntake`,
      `ReconcileUnidentifiedDestinations` injected
- [x] `Triage` + `CanOpenTriage` loaded beside the Image-intake destination
- [x] `OnPostOpenTriageAsync` via `ExecuteCommandAsync`, with
      `StaffAuthorization.Require(actor, PerformCasework)`
- [x] Origin from `IImageIntakeOriginResolver`, mapped field-for-field to
      `TriageOrigin`
- [x] Accepted-match evidence passed back as the receipt's own record
- [x] `ResolveForReceiptAsync` called; recoverable faults swallowed with the
      sweep named as backstop
- [x] `Details.cshtml` panel — labels and values only, no guidance sentence
- [x] Integration test: no registration → Unidentified → staff supplies →
      Triage opens → Unidentified resolves to it
      (`StaffSupplyingTheRegistrationOpensTheTriageAndClosesTheUnidentifiedItem`)
- [x] `dotnet build --configuration Release` clean — 0 warnings, 0 errors, on
      the final commit `26e463ee` (rebuilt *after* the simplifier's edits)
- [x] `dotnet test tests/Pegasus.Core.Tests` green — 947/947
- [x] `dotnet test tests/Pegasus.ArchitectureTests` green — 99/99
      *(added: not in the original list, but run because the diff widens a
      Core member's visibility)*
- [x] Integration tests green — **not** the planned
      `--filter "Category!=Corpus"`. Actually run:
      `--filter "FullyQualifiedName~Triage|FullyQualifiedName~Intake|FullyQualifiedName~Unidentified"`
      → 141 passed / 0 failed / 6 skipped, plus the new test alone → 1/1.
      The **full** suite is delegated to CI's three shards on the pushed SHA;
      CI is the authority and its shard results were still pending when the
      report was written.
- [x] Simplification pass run; findings and dispositions dated in the plan
      (5 applied, 6 declined, each with a reason)
- [x] Post-implementation report written
- [x] PR opened with `--base task/intk-033-triage-from-intake`, stacking stated
      — **PR #533**, stacks on #525, must merge after it
