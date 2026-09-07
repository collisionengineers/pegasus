# Pegasus source workspaces

The source imports are retired. Their accepted renderer and document-reader
slices were integrated into the application under ADR-0025. This index retains
their provenance; it does not describe active independently buildable projects,
runtime services, deployment units, or business-policy owners.

## Integration status register

| Workspace | Role | Integration status | Activation conditions | Owner |
| --- | --- | --- | --- | --- |
| `document-extraction/` | CollisionDocNet document/email extraction libraries and CLI | **Integrated and retired (SIMPLI-013, [ADR-0025](../docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md)): the compound-file, legacy Word, and Outlook-item readers were folded into `Pegasus.Infrastructure` behind `IIntakeSourceReader` for `.doc`/`.msg` intake; the unused PDF/EML/DOCX/CLI projects were not carried (PdfPig, MimeKit, and OpenXml remain the live implementations)** | Met by the integrating change: Core contract (`IIntakeSourceReader`), real caller (intake reader), imported parser test suites, fail-closed fallback | Retired |

## Source provenance

| Workspace | Source provenance | Imported source manifest |
| --- | --- | --- |
| `document-extraction/` | Local source snapshot `../collisiondocnetconverter`; no `.git` metadata was present, so branch, remote, and commit are unavailable | 202 files, 2,232,305 bytes, SHA-256 `e5d3bd118e567d54c2a793a0e75a4f3c528da62bd1caa9289f48297c9c96b5f2` |
| `report-renderer/` | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer` | 108 files, 604,228 bytes, SHA-256 `a3b9b665b23b08b9dd61276d48b9f3a3c551a005213225e7941d0adf6d504471` |

The report-renderer snapshot was retired after its caller-backed engine was
integrated into the application by ADR-0025. The document-extraction snapshot
was retired the same way when SIMPLI-013 integrated its `.doc`/`.msg` reader
slice into `Pegasus.Infrastructure`. Both immutable import provenance records
remain here and in Git history; neither is a live workspace, and no live
workspace currently exists.

The manifest hashes each tracked Git index path in UTF-8 immediately followed
by its staged blob payload, in ordinal path order. A manifest proves source
identity, not application integration, deployment, or acceptance.

Each manifest describes the snapshot **at import time**. Current file counts
come from `git ls-files`; do not recalculate an import record merely because a
workspace later diverges.

## Ownership and activation

- `Pegasus.Core` owns every business rule and accepted case outcome. Web and
  Worker are the application composition roots; Infrastructure implements Core
  ports.
- Workspace validation is independent. Application build, publish, and deploy
  must not compile, reference, dynamically load, invoke, or package workspace
  code without a separately accepted contract and actual caller.
- Historical `CollisionSpike` names inside dated workspace evidence identify
  the predecessor only. Current or future application integration contracts
  target Pegasus.
- Generated output, packages, caches, nested repository metadata and CI,
  private datasets, local settings, copied corpora, sample case material, and
  model weights remain excluded. Updating a source import requires a reviewed
  provenance change; never infer upstream acceptance from a local build.
