# Local release-candidate packaging

This document owns the implemented portion of `EXT-PKG-001`. It produces unsigned, framework-dependent local candidates for inspection; it does not publish or accept them.

## Baseline outputs

Run from PowerShell 7:

```powershell
.\scripts\Build-ReleaseCandidate.ps1 -Version 0.1.0-alpha.1
```

The script refuses an existing destination and any output outside `artifacts/`. It runs locked restore, Release build, the Microsoft.Testing.Platform solution suite, one pack per production library and a framework-dependent CLI publish. It then runs the Windows CLI `version` smoke, creates a sorted/fixed-timestamp CLI ZIP, a canonical dependency inventory from NuGet lock files and a SHA-256 package manifest.

Expected ignored layout:

```text
artifacts/packages/<version>/
  cli-framework-dependent/
  collisiondocnet-cli-<version>-framework-dependent.zip
  dependency-manifest.v1.json
  nuget/*.nupkg
  nuget/*.snupkg
  package-manifest.v1.json
```

`CollisionDocNet.Extraction` is the public library package. Its format/storage packages are explicit managed dependencies. The CLI is not packed as a NuGet tool; it is a thin framework-dependent application bundle over the same public library.

## Version and schema policy

- Package version defaults are centrally owned in `Directory.Build.props` and can be overridden by the packaging command.
- Extractor semantic identity (`collisiondocnet/0.1`), result schema (`collisiondocnet-result/1`), bundle schema (`collisiondocnet-bundle/1`) and package version are separate compatibility axes.
- The versioned JSON Schemas under `docs/schemas/` describe the public result envelope and CLI evidence bundle. Schema-breaking changes require a new schema identity and migration/rollback review; package version changes alone do not rewrite historical evidence.
- The package manifest hashes every candidate file other than the manifest itself. It proves local byte identity, not signing, provenance attestation or acceptance.

## Validation and non-claims

The baseline is Windows framework-dependent `net10.0`. Linux framework-dependent execution is not verified by this unit. Self-contained, single-file and Native AOT variants are not built or claimed. Each would require a named RID, fresh restore/publish, package inspection, startup/extraction/security/performance tests and separate signing/trimming/AOT review.

No current candidate is signed, notarised, uploaded, deployed or authorised for distribution. Format rows remain partial and the security, fuzz, differential, genuine-data holdout, Linux and independent acceptance gates remain open. A successful pack cannot advance those gates.
