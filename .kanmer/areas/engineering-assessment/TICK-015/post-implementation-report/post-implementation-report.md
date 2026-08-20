## Post-implementation report — TICK-015 (CASE-21)

**Retrospective backfill.** This capability was implemented and merged to `dev`/`main` before this ticket's board documents were written; this report records what was verified after the fact, not a process that ran live during the change.

### What shipped
- Core policy: once-per-case "First sent to Engineer" handoff proxy recorded on the first successful manual EVA bundle generation for a case, never on later revisions.
- `src/Pegasus.Infrastructure/Eva/LocalEvaHandoffProxy.cs` — `RecordFirstGenerationAsync`; the offline proxy's receipt is checked and the call throws `InvalidDataException` if `ClaimsExternalDelivery` or `ClaimsEngineerAssignment` is ever true, so the proxy structurally cannot claim EVA delivery or named-Engineer assignment.
- `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs:566-612` — gating: `hasFirstProxy` is read from `EvaFirstHandoffProxies` for the case; `policy.DecideRevision(...)` decides whether this generation is the first (records the proxy) or a revision (does not). Persisted permanently once recorded.
- Schema: `src/Pegasus.Core/Eva/EvaBundleSchema.cs`.
- Migration: `src/Pegasus.Infrastructure/Persistence/Migrations/20260729182000_EvaHandoffPersistence.cs` — present in the repo at `2325ed4a` (production release 13 = release 12 exact-SHA fast-forward), confirmed by `git cat-file -e 2325ed4a:<path>`.
- MCP surface: `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` `pegasus_eva_bundle_generate` — generates the bundle through the identical Core path and blocking/idempotency rules as the staff UI action, records the same first-proxy event.

### Tests
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --filter FullyQualifiedName~Eva --no-restore` → **Passed: 40, Failed: 0** (2026-08-20, local `dev`).

### Verified against production
- `git merge-base --is-ancestor 2325ed4a origin/main` → true; `2325ed4a` is also an ancestor of `origin/dev`. The migration and proxy files are present in the tree at `2325ed4a`.

### Residual (named, not blocking this ticket per its own capability text)
- Live EVA receipt and named-Engineer assignment are EVA's own system and are out of Pegasus's scope by design.
- Deployment and operator drag-and-drop acceptance of the resulting bundle remain a separate operator-side acceptance step (tracked with EXT-03/TICK-022), not blocking the implemented once-per-case proxy behaviour itself.
