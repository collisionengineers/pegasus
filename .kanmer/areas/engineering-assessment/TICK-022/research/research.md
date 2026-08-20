## Research — TICK-022 (EXT-03) — retrospective backfill

**Question:** Does `dev` need implementation for the deterministic 13-key UTF-8 EVA handoff bundle with SHA-256 manifest and ordered eligible custody-confirmed images?

**Findings (verified 2026-08-20):**
- `src/Pegasus.Core/Eva/EvaBundleSchema.cs` defines `EvaReplayFields` with exactly 13 members (`WorkProvider, Vrm, VehicleModel, ClaimantName, Reference, IncidentDate, InstructionDate, InspectionDate, InspectionAddress, AccidentCircumstances, VatStatus, Mileage, MileageUnit`) and `EvaBundleSchema.CreateOfflineReplay` writes the ordered JSON, provenance, and `manifest.sha256` deterministically (`WriteOrderedJson`, lines ~703-765).
- Contract test `tests/Pegasus.Core.Tests/Qdos/EvaBundleContractTests.cs` (`SameAcceptedInputsProduceIdenticalBundleBytesAndExactOrder`) asserts identical bytes/SHA-256 on replay, exactly 13 fields in the provenance manifest, and a fixed archive entry order. Re-run 2026-08-20: `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --filter FullyQualifiedName~EvaBundleContractTests --no-restore` → Passed 7/7.
- Callers: `src/Pegasus.Web/Pages/Cases/Eva/Download.cshtml.cs` (staff UI) and the MCP tool `pegasus_eva_bundle_generate` in `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` use the identical Core generation path.
- Runtime grant: migration `src/Pegasus.Infrastructure/Persistence/Migrations/20260819180000_GrantEvaHandoffDownloadOperations.cs` — release 12 fixed a prior grant gap (docs/operations.md: `EvaHandoffDownloadOperations` had been created with no permission rows since the 2026-08-11 migration, so the download path had been failing in production; both grants read back `Resolved` after release 12).
- No EVA network call and no Pegasus-owned image ordering are present anywhere in this path — matches the ticket's own scope statement.

**Implications:** EXT-03 is already implemented on `dev` and deployed (the grant fix landed in release 12, `main`/`dev` = `2325ed4a` at release 13). The residual is operator-side: a live drag-and-drop acceptance run of the produced ZIP against EVA's own intake, which is recorded as an outstanding operator acceptance step, not a missing capability.

**Open questions:** none — the residual is an operator acceptance action, not a design question.
