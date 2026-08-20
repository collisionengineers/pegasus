## Post-implementation report — TICK-022 (EXT-03)

**Retrospective backfill.** Implemented before this ticket's pipeline documents existed. No new code change was needed or made — see research/plan for the reconciliation approach.

### What exists
- Deterministic 13-key ordered EVA JSON + SHA-256 manifest: `src/Pegasus.Core/Eva/EvaBundleSchema.cs`.
- Real callers: `src/Pegasus.Web/Pages/Cases/Eva/Download.cshtml.cs` (staff), `pegasus_eva_bundle_generate` in `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` (Automation Actor).
- Runtime grant repair for the download path: migration `20260819180000_GrantEvaHandoffDownloadOperations.cs`, applied in release 12.

### Tests
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --filter FullyQualifiedName~EvaBundleContractTests --no-restore` → Passed 7/7 (2026-08-20).

### Deployment
- `git cat-file -e 2325ed4a:src/Pegasus.Infrastructure/Persistence/Migrations/20260819180000_GrantEvaHandoffDownloadOperations.cs` succeeds — present at release 13's SHA.
- `git cat-file -e 2325ed4a:src/Pegasus.Core/Eva/EvaBundleSchema.cs` succeeds.

### Residual (named, operator-side, not a missing capability)
Live operator drag-and-drop acceptance of the produced ZIP against EVA's own intake has not been run; this is recorded as an outstanding operator acceptance action per `docs/capabilities.md` EXT-03's own text ("ZIP/drag-drop container acceptance ... remain pending"), not a defect in the implemented capability. A future EVA API remains explicitly out of this ticket's scope.
