# EV-2026-07-23 — Wave 5 public Extraction and CLI subset

Scope: one custom five-format managed dispatch boundary and one headless one-input CLI. Independent review rejected the initial ten-test implementation; the correction aligned the documented CLI contract, expanded format projection and added outer operation context, path/bundle and outcome controls. This remains partial because handler APIs do not yet consume the same live cumulative budget and MSG embedded items do not expose original bytes.

Implemented behaviour includes byte-first detection with no fallback engine, bytes/stream input, source/filename/media/policy provenance, PDF/DOC/DOCX/MSG/EML projection, signature-preserving corrupt results and outcome-specific exception mapping. The assembly/root namespace is `CollisionDocNet.Extraction`; the physical project path is retained rather than adding a parallel wrapper.

The CLI supports `help`, `version`, `detect`, `extract`, `--input <path|->`, required stdin `--name`, `--quiet`, lower-only named limit classes, documented success/partial/error exit codes, deterministic completion envelopes, safe new-directory staging, relative asset paths, safe extensions, post-write SHA-256, and URI/UNC/device/reparse denial.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Conversion.Tests\CollisionDocNet.Conversion.Tests.csproj --configuration Release --no-restore
dotnet test --project tests\unit\CollisionDocNet.Cli.Tests\CollisionDocNet.Cli.Tests.csproj --configuration Release --no-restore
```

Final live results: Extraction 11/11 and CLI 27/27 passed, both exit `0` with full project-reference builds. Owned formatting checks passed. Static-dependency review found filesystem calls isolated in the physical CLI filesystem boundary, Console confined to process entry, and no time/environment/network/process static coupling. No critical static performance pattern was found.

Remaining gates include handler-level live shared budgets/deadlines before allocation, honest MSG embedded recursion bytes, exact boundary/concurrency/malformed handler tests, output-parent reparse races, second Ctrl+C process-host proof, Windows/Linux framework smoke, schema migration, full library/CLI projection equivalence and nesting/security/performance acceptance.
