# Files — TICK-216: accepted boundary evidence

| Path / record | Current authoritative result |
|---|---|
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Accepts only exact tuples; Andy is complete; Ed/Neil unavailable pending accepted qualifications. |
| `docs/open-decisions.md` | Keeps Ed/Neil qualifications and other absent wording open; prescribes fail-closed unavailability. |
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | One accepted engineer entry: `andy_patterson` / `A Patterson` / `M.Inst.IAEA`. |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | Embeds only the Andy signature resource. |
| `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs` | Rejects missing/unknown/mismatched engineer tuples before adapter invocation. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | Proves the Andy asset byte-for-byte and asserts Ed/Neil resources are absent. |
| [[SIMPLI-014]] PIR/proof | Owns and proves the merged implementation in PR #415. |

## Change boundary

TICK-216 needs only a Kanmer evidence correction and acceptance closeout. It makes no repository edit because the authoritative docs and merged implementation already express the correct narrow state. `reference/rendererref1/**` remains immutable.
