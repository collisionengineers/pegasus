# Checklist — INTK-035

- [ ] Worktree `pegasus-worktrees/intk-035-open-triage` on
      `task/intk-035-open-triage` from `task/intk-033-triage-from-intake`,
      upstream unset
- [ ] `ProcessIntake.IsTriageRequest` widened to `public`
- [ ] `Details.cshtml.cs`: `ITriageQueries`, `ICreateTriageFromIntake`,
      `ReconcileUnidentifiedDestinations` injected
- [ ] `Triage` + `CanOpenTriage` loaded beside the Image-intake destination
- [ ] `OnPostOpenTriageAsync` via `ExecuteCommandAsync`, with
      `StaffAuthorization.Require(actor, PerformCasework)`
- [ ] Origin from `IImageIntakeOriginResolver`, mapped field-for-field to
      `TriageOrigin`
- [ ] Accepted-match evidence passed back as the receipt's own record
- [ ] `ResolveForReceiptAsync` called; recoverable faults swallowed with the
      sweep named as backstop
- [ ] `Details.cshtml` panel — labels and values only, no guidance sentence
- [ ] Integration test: no registration → Unidentified → staff supplies →
      Triage opens → Unidentified resolves to it
- [ ] `dotnet build --configuration Release` clean
- [ ] `dotnet test tests/Pegasus.Core.Tests` green
- [ ] `dotnet test tests/Pegasus.IntegrationTests --filter "Category!=Corpus"` green
- [ ] Simplification pass run; findings and dispositions dated in the plan
- [ ] Post-implementation report written
- [ ] PR opened with `--base task/intk-033-triage-from-intake`, stacking stated
