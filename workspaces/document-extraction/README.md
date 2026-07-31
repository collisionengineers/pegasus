# CollisionDocNetExtractor

CollisionDocNetExtractor is an independent, completely managed C#/.NET source workspace for deterministic PDF, legacy Word `.doc`, WordprocessingML `.docx`, Outlook `.msg`, and RFC 5322/MIME `.eml` extraction. It does not automate Office, launch an office suite, delegate extraction to a hosted engine, render, OCR, edit, or convert documents.

Public payloads are ordered text and discrete independently recognized images. Metadata, relationships, hashes, issues, measurements, completeness, and provenance are control evidence; arbitrary attachments and embedded-object bytes are not emitted. Current support is capability-specific and belongs only to the [compatibility matrix](docs/compatibility/feature-matrix.md).

## Owners

- [Architecture and logical-to-physical map](docs/architecture.md)
- [Five-format intended contract](docs/formats.md)
- [Compatibility matrix](docs/compatibility/feature-matrix.md)
- [Testing, fixtures, and dated evidence](docs/testing.md)
- [Programme gaps and activation gates](docs/programme.md)
- [Packaging, update, support, and release](docs/packaging.md)
- [Licensing and source rights](docs/licensing.md)
- [Accepted decisions](docs/decisions/ADR-0002-five-format-extractor.md)

## Build and verify

```powershell
.\scripts\Invoke-RepoCheck.ps1
```

The script owns locked restore, build, tests, links, and package-schema checks. Local release candidates under ignored `artifacts/` are inspection evidence, not authorized releases. `sample-doc-files/` is sensitive copied-profile material, not an approved fixture corpus, and must not be recursively processed.

This workspace is not referenced or loaded by Pegasus, is not a Pegasus caller or deployment unit, and owns no Pegasus business policy. Its current integration status and activation conditions are recorded in the [workspace integration register](../README.md#integration-status-register). Integration requires the separately accepted contract and real application caller listed there.
