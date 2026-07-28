# Security and robustness tests

`CollisionDocNet.Security.Tests` is the deterministic offline EXT-SEC-001 gate.
It uses only owned synthetic bytes and drives PDF, DOC, DOCX, MSG and EML
candidates through the public `DocumentExtractor` boundary. It neither reads
`sample-doc-files/` nor performs external retrieval.

Run the bounded regression suite with .NET 10 and Microsoft.Testing.Platform:

```powershell
dotnet test --project tests/security/CollisionDocNet.Security.Tests/CollisionDocNet.Security.Tests.csproj --configuration Release
```

The deterministic mutation loops are intentionally small enough for the normal
offline gate. Continuous fuzzing is not yet claimed: a future opt-in harness
must run out of process with per-case time and memory enforcement, persist only
non-sensitive synthetic reproducers, and must not add a third-party format
engine or silently become part of ordinary restore/build/test.
