# EV-2026-07-23 — five-format scope and plan validation

Date: 2026-07-23, Europe/London.

## Claim and boundary

This record validates the repository-wide research plan, five-format port-unit coverage, headless library/CLI boundary, current .NET/MSBuild organisation and unchanged CFB v3 fixed-header test boundary. It does not claim an end-to-end PDF, DOC, DOCX, MSG or EML implementation.

Inputs were repository-owned source, project files and documentation only. `sample-doc-files/` was not enumerated or read. The retained `core/` checkout was inspected only for revision/clean status; no file in it was changed and it is no longer required by the ordinary repository check.

Environment:

- Microsoft Windows 11 Pro `10.0.26200`, x64;
- PowerShell `7.6.3`;
- resolved .NET SDK `10.0.302` (`10.0.300` also installed);
- Microsoft.Testing.Platform selected in `global.json` with `MSTest.Sdk` `4.0.2`; and

## Requested .NET skill findings

- `.NET SDK`: the pinned global SDK already resolves, so a repository-local SDK installation would add duplication without improving reproducibility. No SDK or plugin package was installed.
- `MSBuild`: `Directory.Build.props` centrally owns language, nullability, deterministic build, analyser, warning and locked-restore policy. With two projects and no shared package version, generated-source target or custom build step, neither `Directory.Build.targets` nor `Directory.Packages.props` is yet justified.
- `.NET diagnostics/performance`: a static hot-path checklist over the current owned C# source found no string slicing/case-normalisation, repeated string search/replacement, LINQ materialisation, regex, async/synchronous file-I/O, `params` allocation or eligible unsealed-class finding. This applies only to the small fixed-header parser; algorithmic, allocation and memory budgets for future parsers remain unmeasured.
- `.NET testing`: the runner was detected from repository configuration and invoked with Microsoft.Testing.Platform solution syntax.

## Commands and results

### SDK and test-platform detection

```powershell
dotnet --version
dotnet --list-sdks
Get-Content -Raw global.json
```

Exit: `0`. The resolved SDK was `10.0.302`; `global.json` selects Microsoft.Testing.Platform and `MSTest.Sdk` `4.0.2`.

### Evaluated MSBuild policy

```powershell
dotnet msbuild src/CollisionDocNet.Storage/CollisionDocNet.Storage.csproj -nologo -getProperty:TargetFramework,LangVersion,Nullable,Deterministic,EnableNETAnalyzers,AnalysisLevel,EnforceCodeStyleInBuild,TreatWarningsAsErrors,RestorePackagesWithLockFile
```

Exit: `0`. Evaluated values were `net10.0`, `latest`, `enable`, `true`, `latest-recommended`, `true`, `true` and `true` respectively.

### Current-source performance-pattern scan

```powershell
$patterns = [ordered]@{
  string_slicing_or_normalisation = '\.Substring\(|\.ToLower(?:Invariant)?\(|\.ToUpper(?:Invariant)?\('
  repeated_search_or_replace = '\.IndexOf\(|\.StartsWith\(|\.EndsWith\(|\.Contains\(|\.Replace\('
  linq_materialisation = '\.(ToList|ToArray|Count|Any|First|FirstOrDefault|Single|SingleOrDefault)\('
  regex = '\bRegex\.|new\s+Regex\('
  async_or_sync_io = '\basync\b|ReadToEnd|ReadAllBytes|ReadAllText|WriteAllBytes|WriteAllText'
  params_arrays = '\bparams\s+'
}
foreach ($entry in $patterns.GetEnumerator()) {
  $matches = @(rg -n --glob '*.cs' --glob '!**/bin/**' --glob '!**/obj/**' -- $entry.Value src 2>$null)
  "$($entry.Key)=$($matches.Count)"
}
$eligible = @(rg -n --glob '*.cs' --glob '!**/bin/**' --glob '!**/obj/**' '^\s*(public|internal)\s+(?!static|abstract|sealed|record|enum|struct|interface)class\s+' src 2>$null)
"unsealed_eligible_classes=$($eligible.Count)"
```

Exit: `0`. Every reported count was zero.

### Microsoft.Testing.Platform suite

```powershell
dotnet test --solution CollisionDocNet.slnx
```

Exit: `0`. Result: 30 passed, 0 failed, 0 skipped. Input class: generated fixed CFB v3 header cases only.

### Deterministic repository gate

```powershell
.\scripts\Invoke-RepoCheck.ps1
```

Exit: `0`. Locked restore, formatting verification, Release build, Release Microsoft.Testing.Platform run, JSON parsing and local Markdown-link validation passed. Build result: 0 warnings and 0 errors. Test result: 30 passed, 0 failed, 0 skipped. The command did not invoke `core/` or an external process oracle.

After writing this evidence record, the final documentation state was rechecked with:

```powershell
.\scripts\Invoke-RepoCheck.ps1 -SkipRestore
```

Exit: `0`. Formatting, Release build, 30/30 tests, JSON parsing and all local Markdown links passed again.

### Port-unit consistency

```powershell
function Get-UnitIds([string[]]$Paths) {
  foreach ($path in $Paths) {
    $text = Get-Content -Raw -LiteralPath $path
    [regex]::Matches($text, 'EXT-[A-Z]+-[0-9]{3}') | ForEach-Object Value
  }
}
$formatPaths = Get-ChildItem -LiteralPath 'docs/formats' -Filter '*.md' -File |
  Where-Object Name -ne 'README.md' | ForEach-Object FullName
$formatIds = @(Get-UnitIds $formatPaths | Sort-Object -Unique)
$catalogueIds = @(Get-UnitIds @('docs/programme/port-unit-catalogue.md') | Sort-Object -Unique)
$matrixIds = @(Get-UnitIds @('docs/compatibility/feature-matrix.md') | Sort-Object -Unique)
$missingCatalogue = @($formatIds | Where-Object { $_ -notin $catalogueIds })
$missingMatrix = @($formatIds | Where-Object { $_ -notin $matrixIds })
"format_unit_count=$($formatIds.Count)"
"catalogue_unit_count=$($catalogueIds.Count)"
"matrix_unit_count=$($matrixIds.Count)"
"format_units_missing_from_catalogue=$($missingCatalogue.Count)"
"format_units_missing_from_matrix=$($missingMatrix.Count)"
```

Exit: `0`. The five detailed format plans define 64 unique format units; 0 are missing from the catalogue and 0 are missing from the compatibility matrix. Shared, governance, API, CLI, QA, packaging and integration units account for the additional catalogue/matrix entries.

### Repository and retained-research state

```powershell
git rev-parse --show-toplevel
git -C core rev-parse HEAD
git -C core status --short
Select-String -LiteralPath scripts/Invoke-RepoCheck.ps1 -Pattern 'external process|third-party parser'
```

The first command exited `128`, confirming that the top-level directory is not a Git repository. The remaining commands exited `0`: `core/` resolved to the recorded revision, produced no dirty-status output, and the ordinary repository-check script contained no retained-research invocation.

## Planning result

- PDF is scoped as the observed PDF 1.0–2.0 family, including extensions and passive profile evidence, rather than as PDF 2.0-only input.
- DOC has a direct binary CFB/FIB/CLX path and a separate pre-97/mislabelling decision; DOCX/XML is never an intermediate.
- DOCX is a required independent ZIP/OPC/XML handler.
- MSG preserves the generic MAPI property bag and covers declared non-mail item classes.
- EML covers the RFC 5322/MIME tree, internationalisation, reports, TNEF and protected content.
- The product has only a managed library and one-shot headless CLI surface.

## Known gaps

- Only the strict CFB v3 fixed header is implemented; CFB traversal and every end-to-end handler remain pending.
- No conformance, differential, fuzz, coverage, genuine-data, performance/concurrency or publish-variant run was performed.
- No operator or independent reviewer has accepted a format slice.
- Specification web revisions are planning baselines until downloaded artefacts and hashes are recorded in the owning evaluation manifests.
