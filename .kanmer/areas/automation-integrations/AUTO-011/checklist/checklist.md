# Checklist — AUTO-011

- [ ] Core kinds/states/record/commands/ports/use cases in `Core/AiWork/AiJobs.cs` + `AiJobOperations.cs`
- [ ] `AiJobEntity`, mapping, DbSet, `EfAiJobStore`
- [ ] Migration `AiJobs` + grant migration; `Test-MigrationGrants.ps1` passes
- [ ] DI registrations
- [ ] `automation.jobs` scope, `AiJobMcpTools`, registration, consent descriptions (jobs, mail, assessment fix)
- [ ] Core tests; integration store + tool tests; `ExpectedTools` inventory updated
- [ ] `dotnet build ./Pegasus.slnx --configuration Release` green
- [ ] Merge `origin/dev`; simplification pass recorded in plan
- [ ] Post-implementation report; PR opened against `dev`; ticket → review
