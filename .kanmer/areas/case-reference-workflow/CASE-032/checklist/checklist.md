# Checklist — CASE-032

- [ ] Step 1: Obtain and record the operator-approved Triage reference source, provider source, and absent-value rules.
- [ ] Step 2: Extend `ImageIntakeSummary` and its existing `ProjectAsync` projection with Core-facing custody.
- [ ] Step 3: Preserve the image summary reconstruction and render the custody label through `OperatorLabels`.
- [ ] Step 4: After the answers, extend `TriageSummary`, the existing list projection, and `TriageRow`.
- [ ] Step 5: Extend `TriageQueuesWebTests` for image custody and all four Triage row halves.
- [ ] Update only required summary-constructor test helpers.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build`.
- [ ] Run `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`.
- [ ] Run `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests"`.
- [ ] Write the post-implementation report.
- [ ] Open a PR with `Kanmer: CASE-032`.
