# PLAT-048 checklist

- [ ] `ViewOperationalReports` right (Administrator) in `StaffAuthorization.cs`
- [ ] `IEvaSubmissionQueries.GetRecentFailuresAsync` / `GetActivityAsync` + EF implementation
- [ ] `Core/Operations/ServiceHealth.cs` — vocabulary, ports, policy, `GetServiceHealth`
- [ ] `Core/Reports/EngineerActivityReport.cs` — port, use case, CSV shape
- [ ] `EfServiceHealthQueries`, `EfEngineerActivityQueries`, DI registration
- [ ] Web `AutomationIngressStatusQueries` adapter + registration
- [ ] Core tests: health mapping/composition; report rules and CSV
- [ ] Integration tests (SqlServer) for both EF queries
- [ ] `dotnet build ./Pegasus.slnx --configuration Release` green
- [ ] `git merge origin/dev`, simplification pass recorded in plan
- [ ] Post-implementation report, PR opened against `dev`, ticket → review
