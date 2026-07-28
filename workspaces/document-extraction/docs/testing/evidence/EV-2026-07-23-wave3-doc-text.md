# EV-2026-07-23 — Wave 3 direct DOC text subset

Scope: direct managed binary Word text parsing through CFB/FIB/CLX/Pcdt/PlcPcd. No DOCX/XML intermediate, Office automation, external process or native parser is used. This supports row-specific implementation/local synthetic evidence only, not complete DOC extraction.

Implemented behaviour includes Word97/pre-97 classification, effective selected `nFib` versions, encryption/table flags, checked FIB arrays and `fc/lcb` ranges, logical CP piece ordering, compressed FC transformation, Windows-1252 and UTF-16 pieces, eight-story ranges, conservative control-token projection and exact CP/story-CP/FC-byte/piece provenance. Unsupported secondary anchors, PRM/formatting, pictures, secondary FIB, active/embedded content and ambiguous controls force non-complete issues.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Writer.Tests\CollisionDocNet.Writer.Tests.csproj --configuration Release --no-restore
```

After independent review and correction, the combined Writer suite reached 43/43 with a positive owned raw-v3-CFB DOC integration fixture, exact resource-boundary classifications and whole-result retry determinism. Genuine DOC corpus evidence remains absent.

A read-only secondary implementation was consulted for selected binary-Word behaviours, but its identity, revision and licence were not recorded and cannot presently be reconstructed. It is therefore quarantined as non-authoritative and must not be relied on for future research or implementation unless its provenance is recovered and reviewed. It was not executed, copied or modified, and the managed implementation remains specification-led.

Remaining gates include codepage/font-property resolution, simple-file fallback, story anchors, formatting/assets (Wave 4), raw-CFB integration, specification conformance, differential comparison, fuzzing, genuine cohort/holdout and performance acceptance.
