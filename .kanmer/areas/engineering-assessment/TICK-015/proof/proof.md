## Proof — TICK-015 (CASE-21)

Retrospective proof — implemented in the commits that landed `20260729182000_EvaHandoffPersistence` and the offline EVA proxy; verified 2026-08-20 against `dev` at commit reachable from `2325ed4a`.

- Once-per-case gating: `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs:566-612` (`hasFirstProxy` check against `EvaFirstHandoffProxies`, `policy.DecideRevision`).
- Offline proxy cannot claim delivery/assignment: `src/Pegasus.Infrastructure/Eva/LocalEvaHandoffProxy.cs` (`RecordFirstGenerationAsync`); `EvaHandoffStore.cs` throws `InvalidDataException` if the receipt ever claims external delivery or Engineer assignment.
- MCP caller: `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` `pegasus_eva_bundle_generate` — same Core path as the staff UI.
- Tests: `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --filter FullyQualifiedName~Eva --no-restore` → Passed 40/40 (2026-08-20).
- Deployment: migration `20260729182000_EvaHandoffPersistence.cs` present at production commit `2325ed4a` (`git cat-file -e 2325ed4a:src/Pegasus.Infrastructure/Persistence/Migrations/20260729182000_EvaHandoffPersistence.cs` succeeds); `2325ed4a` is release 13's `main`/`dev` SHA (docs/operations.md).

**Residual (not blocking):** EVA's own receipt and named-Engineer assignment are external to Pegasus. Operator drag-and-drop acceptance of the bundle is tracked separately under EXT-03/TICK-022.
