# Checklist — AUTO-011

- [x] Core kinds/states/record/commands/ports/use cases in `Core/AiWork/AiJobs.cs` + `AiJobOperations.cs`
- [x] `AiJobEntity`, mapping, DbSet, `EfAiJobStore`
- [x] Migration `AiJobs` + grant migration; `Test-MigrationGrants.ps1` passes
- [x] DI registrations
- [x] `automation.jobs` scope, `AiJobMcpTools`, registration, consent descriptions (jobs, mail, assessment fix)
- [x] Core tests; integration store + tool tests; `ExpectedTools` inventory updated
- [x] `dotnet build ./Pegasus.slnx --configuration Release` green
- [x] Merge `origin/dev`; simplification pass recorded in plan
- [x] Post-implementation report; PR #590 opened against `dev`; ticket → review
