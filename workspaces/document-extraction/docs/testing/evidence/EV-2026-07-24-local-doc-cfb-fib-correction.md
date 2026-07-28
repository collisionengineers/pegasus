# EV-2026-07-24 — local DOC CFB/FIB compatibility correction

Scope: `EXT-STO-001`, `EXT-DET-001`, `EXT-DOC-002` and `EXT-DOC-003` diagnosis and correction using one caller-selected local 114,688-byte DOC. The input remains under `sample-doc-files/`; its name, content and hash are not reproduced here. The resulting bundles remain ignored under `artifacts/`. This is one genuine compatibility case, not a cohort, conformance result, hidden holdout or acceptance.

## Defects and corrections

The first CLI run stopped before DOC detection because the CFB reader imposed equal black height on sibling trees. MS-CFB revision 12.0 section 2.6.4 requires a black child-tree root, no consecutive red nodes, specification ordering and unique names, but not equal black height. The reader now enforces the specified rules without the extra rejection. Detection retains a bounded CFB diagnostic and structural index when CFB validation genuinely fails; filename/media mismatch is no longer asserted when no format was established.

The next run reached the FIB and exposed two independent MS-DOC assumptions:

- FibRgFcLcb97 entry 87 is `dwLowDateTime`/`dwHighDateTime`, not an offset-length pair, so it is excluded from Table-stream range validation and passive range projection; and
- PlcPcd requires one additional separator CP after the main document when any specialised document part is present. Extent validation and story starts now account for that CP without projecting it as story text.

Owned synthetic regressions cover all three corrections without copying genuine bytes into tests.

## Commands and evidence

```powershell
dotnet test --project .\tests\unit\CollisionDocNet.Storage.Tests\CollisionDocNet.Storage.Tests.csproj --configuration Release
dotnet test --project .\tests\unit\CollisionDocNet.Conversion.Tests\CollisionDocNet.Conversion.Tests.csproj --configuration Release
dotnet test --project .\tests\unit\CollisionDocNet.Writer.Tests\CollisionDocNet.Writer.Tests.csproj --configuration Release
dotnet run --project .\src\CollisionDocNet.Cli\CollisionDocNet.Cli.csproj --configuration Release --no-build -- extract --input <exact-local-doc> --output <new-ignored-bundle>
```

Focused results before the final repository gate: Storage 140/140, Extraction 19/19 and Writer 44/44 passed. The corrected local invocation returned `Partial` rather than `Corrupt`: detected `CompoundFile`/`WordBinary`, 130 ordered content segments, 1,745 projected characters, 32 metadata entries and three passive assets. Its 34 visible issues describe unimplemented structure/property/anchor semantics and one unsupported nested format; zero text is claimed for controls whose semantics remain partial. The output therefore remains correctly non-complete.

No Office automation, external office-suite process, external converter, network retrieval, macro/OLE activation or source-file mutation occurred.

## Final repository gate

`pwsh -NoProfile -File .\scripts\Invoke-RepoCheck.ps1` exited `0`. Locked restore and formatting passed; the Release build had zero warnings and errors; 525 tests produced 524 passed, one deliberately skipped opt-in EML cohort test and zero failed; JSON parsing and local Markdown-link validation passed. A final CLI invocation from that build reproduced the `Partial` result above and recorded specification identity `MS-DOC/2026-02-17`.
