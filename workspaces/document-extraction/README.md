# CollisionDocNetExtractor

CollisionDocNetExtractor is a completely custom managed C#/.NET extractor for PDF, legacy Word `.doc`, WordprocessingML `.docx`, Outlook `.msg` and RFC 5322/MIME `.eml` files. It is a headless library and command-line product designed for deterministic, server-safe evidence extraction without automating Microsoft Office, launching an external office suite or delegating parsing to a hosted or third-party format-extraction engine.

The product returns only two extracted payload classes: ordered text and discrete images. Text includes document body/story text and textual message headers or participant fields needed by callers; images retain stable identity, media type and provenance. Metadata, relationships, hashes, issues, measurements and completeness are control evidence, not additional payload types. Arbitrary attachments and embedded binary objects are not emitted. The extractor does not render, OCR, edit or convert documents to other formats. There is no desktop UI or hosted web/service surface.

The repository contains staged, partial managed implementations for all five input families. Coverage is capability-specific: consult the compatibility matrix, because passing synthetic tests or producing text is not complete-format support or release acceptance.

## Start here

- [Product scope and format map](docs/architecture/format-scope-map.md)
- [Source baseline and primary specifications](docs/architecture/source-baseline.md)
- [Managed target architecture](docs/architecture/managed-target-architecture.md)
- [Headless CLI contract](docs/architecture/headless-cli-contract.md)
- [Five-format extraction decision](docs/decisions/ADR-0002-five-format-extractor.md)
- [Headless library and CLI decision](docs/decisions/ADR-0003-headless-library-cli.md)
- [Text-and-image-only output decision](docs/decisions/ADR-0004-text-and-image-output.md)
- [DOC source and clean-room boundary](docs/decisions/ADR-0005-doc-source-and-clean-room-boundary.md)
- [DOC binary structure atlas](docs/architecture/doc-binary-structure-atlas.md)
- [DOC format-classification contract](docs/architecture/doc-format-classification.md)
- [DOC text, piece and story semantics](docs/architecture/doc-text-story-semantics.md)
- [Complete plans for all five format families](docs/formats/README.md)
- [Programme and port-unit catalogue](docs/programme/README.md)
- [Compatibility matrix](docs/compatibility/feature-matrix.md)
- [Test programme and fixture safety](docs/testing/README.md)

## Build and test

The SDK is pinned by `global.json` and tests use Microsoft.Testing.Platform with MSTest:

```powershell
dotnet test --solution CollisionDocNet.slnx
```

Run the deterministic offline repository checks with:

```powershell
.\scripts\Invoke-RepoCheck.ps1
```

Build an unsigned local framework-dependent release candidate under ignored `artifacts/` with:

```powershell
.\scripts\Build-ReleaseCandidate.ps1 -Version 0.1.0-alpha.1
```

These packages are inspection artefacts, not authorised releases. See the [packaging readiness and limitations](docs/packaging/release-readiness.md), [dependency/licence review](docs/licensing/dependency-review.md), [update and rollback policy](docs/packaging/update-and-rollback.md) and [support policy](docs/packaging/support-policy.md).

`sample-doc-files/` is sensitive copied-profile material, is not an approved fixture corpus and must not be recursively processed.
