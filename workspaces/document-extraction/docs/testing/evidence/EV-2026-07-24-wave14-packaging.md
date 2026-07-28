# Wave 14 local packaging evidence

Scope: `EXT-PKG-001` framework-dependent local candidates, dependency/licence inventory, schemas, update/rollback and support documentation. This record must be completed with exact command results after validation. It cannot establish format acceptance or distribution authority.

Implemented inputs are central conditional version/package metadata, a package README, two versioned JSON Schemas, packaging contract tests and `scripts/Build-ReleaseCandidate.ps1`. The script creates ignored NuGet library candidates and a deterministic Windows framework-dependent CLI ZIP, dependency manifest and SHA-256 package manifest.

Distribution remains blocked by the absence of an authorised product licence and accepted release scope. Production PackageReferences: none. `MSTest.Sdk` 4.0.2 and BenchmarkDotNet 0.15.8 are test/tool-only and record MIT expressions in restored package metadata. Linux, self-contained, single-file, Native AOT, signing, SBOM standard compliance, independent holdout and authorised acceptance are not claimed.

## Validation

`dotnet restore CollisionDocNet.slnx` exited `0` to create the new packaging-test lock file. The release-candidate pipeline then used locked restore.

`dotnet build tests\unit\CollisionDocNet.Packaging.Tests\CollisionDocNet.Packaging.Tests.csproj --configuration Release --no-restore` exited `0` with zero warnings/errors. `dotnet test --project tests\unit\CollisionDocNet.Packaging.Tests\CollisionDocNet.Packaging.Tests.csproj --configuration Release --no-build` exited `0`: 4/4 passed.

`pwsh -NoProfile -File .\scripts\Build-ReleaseCandidate.ps1 -Version 0.1.0-alpha.3` exited `0`. Locked restore, Release build and Windows MTP tests passed 501/501; all nine production libraries produced `.nupkg` and `.snupkg`; framework-dependent CLI publish/version smoke passed. The ignored candidate contains 47 files: nine binary packages, nine symbol packages, a 26-entry CLI directory/ZIP, dependency manifest and package manifest. All 46 manifest file hashes are canonical SHA-256. ZIP entries are sorted with fixed UTC timestamps. The corrected dependency manifest records 38 test/tool packages and zero production NuGet packages.

The first `0.1.0-alpha.1` inspection exposed that internal project references were misclassified as production packages in the dependency inventory. The generator now excludes NuGet lock entries of type `Project`, and the corrected `alpha.3` result plus a fail-closed zero-production-dependency check verifies the fix. An intermediate `alpha.2` attempt exceeded the harness command timeout and is not evidence.

`pwsh -NoProfile -File .\scripts\Invoke-RepoCheck.ps1 -SkipRestore` passed format and clean Release build but its later concurrent full-suite run exited `1`: 501/502 tests passed and an unrelated newly present opaque-MSG CFB diagnostic failed with aggregate category `root-colour`. The same packaging pipeline had passed the then-current 501-test solution immediately beforehand. This external moving-worktree failure prevents a whole-repository acceptance claim but does not invalidate the focused packaging tests or the earlier immutable candidate hashes.

Assertion-quality review of the four packaging tests found no assertion-free, trivial-only or self-referential test. Equality, collection/deep, string, Boolean and negative assertions cover schema identities/enums, safe asset paths and central metadata. Exception/state assertions are not applicable to these read-only contracts. Pseudo-mutation review found the schema/version/enum/path/licence-presence mutations killed; the higher-risk surviving area was script output classification/count validation, addressed by fail-closed dependency scope, CLI contract and package-count checks. Full PowerShell mutation/property testing remains open.

## Later repository verification

The historical 501/502 failure above was corrected at the shared CFB boundary; it is retained as chronology. After all bounded format, nesting and packaging corrections, `pwsh -NoProfile -File .\scripts\Invoke-RepoCheck.ps1` exited `0`: locked restore and formatting passed, the Release build had zero warnings/errors, and 523 tests produced 522 passed, one deliberately skipped opt-in local EML cohort test and zero failures. Five JSON documents parsed and 55 Markdown files had zero broken local links. `dotnet test --solution .\CollisionDocNet.slnx --configuration Release` independently exited `0` with the same test result; focused packaging tests passed 5/5. This verifies the local repository state, not distribution authority or the open packaging gates listed above.
