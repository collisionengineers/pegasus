# Dependency and licence review

Scope: `EXT-PKG-001`, local framework-dependent release-candidate packaging on 2026-07-24. This is an engineering inventory, not legal advice or distribution approval.

## Production boundary

The production projects contain no third-party `PackageReference`. They use the .NET 10 shared framework and BCL for format detection, containers, parsing, JSON and the headless CLI. No Office, Outlook, external office suite, hosted service, native extraction engine or third-party format parser is packaged.

The .NET SDK is pinned to `10.0.302` by `global.json`. Framework-dependent packages require a compatible .NET 10 runtime on the target host. Microsoft runtime and SDK redistribution terms must be reviewed for the chosen deployment route; the framework itself is not embedded in the baseline candidate.

## Test and tooling boundary

| Dependency | Pinned version | Scope | Recorded package licence | Treatment |
|---|---:|---|---|---|
| `MSTest.Sdk` | 4.0.2 | test build/run only | MIT | Pinned by `global.json`; absent from production package dependency groups. |
| `BenchmarkDotNet` | 0.15.8 | opt-in performance executable only | MIT | Directly referenced only by `tests/performance`; its transitive graph is recorded in the generated dependency manifest and is not a production dependency. |

The licence expressions above were read from the restored NuGet package metadata. Transitive test/tool packages remain governed by their own package metadata; the generated manifest is an inventory, not a licence conclusion.

## Product ownership and distribution blocker

No product `LICENSE` or authorised `PackageLicenseExpression` exists. Local `.nupkg`, `.snupkg` and CLI ZIP outputs are evaluation artefacts only. They must not be published, pushed to a feed or represented as an accepted release until an authorised owner selects the product licence, confirms ownership/provenance, reviews notices and accepts the declared feature set. `PackageRequireLicenseAcceptance=false` records that there is presently no authorised licence text for a consumer to accept; it grants no rights.

Local sample and corpus material are excluded from build, package and dependency-manifest inputs.
