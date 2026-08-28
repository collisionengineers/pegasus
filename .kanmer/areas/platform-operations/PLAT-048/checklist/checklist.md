# PLAT-048 checklist

- [x] `ViewOperationalReports` right (Administrator) in `StaffAuthorization.cs`
- [x] `IEvaSubmissionQueries.GetRecentFailuresAsync` / `GetActivityAsync` + EF implementation
- [x] `Core/Operations/ServiceHealth.cs` — vocabulary, ports, policy, `GetServiceHealth`
- [x] `Core/Reports/EngineerActivityReport.cs` — port, use case, CSV shape
- [x] `EfServiceHealthQueries`, `EfEngineerActivityQueries`, DI registration
- [x] Web `AutomationIngressStatusQueries` adapter + registration
- [x] Core tests: health mapping/composition; report rules and CSV
- [x] Integration tests (SqlServer) for both EF queries
- [x] `dotnet build ./Pegasus.slnx --configuration Release` green
- [x] `git merge origin/dev`, simplification pass recorded in plan
- [x] Post-implementation report, PR #591 opened against `dev`, ticket → review
