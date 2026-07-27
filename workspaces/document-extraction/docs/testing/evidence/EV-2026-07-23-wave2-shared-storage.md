# EV-2026-07-23 — Wave 2 shared storage and detection subset

Scope: implementation evidence with passing synthetic fixtures for bounded ZIP/ZIP64 Store/Deflate parsing, passive OPC graphs, common passive OLE property values/descriptors, bounded namespace XML and structural five-family detection. Independent review rejected broad row-level local-verification wording and identified missing invariants listed below. This is not complete format conformance, acceptance or production-caller evidence.

## Implemented boundary

- ZIP central/local validation, Store/Deflate, ZIP64 EOCD/locator, CRC, duplicates, path traversal/absolute/drive denial, overlap, entry/count/expanded-total/ratio limits, cancellation and explicit encrypted/unsupported-method results;
- passive OPC content types and internal/external relationship graphs, including normalised internal targets, package-escape denial and no external retrieval;
- common scalar OLE property-set projections with raw bytes retained, plus the common ANSI Ole10Native descriptor/payload form;
- namespace-aware XML events with input/depth/node/attribute/text limits, cancellation and DTD/entity/external-resolution denial; and
- structural PDF, DOC, DOCX, MSG, EML and encrypted-OOXML candidates, hint-mismatch issues, deterministic ambiguity and cancellation.

## Commands and results

```powershell
dotnet build src\CollisionDocNet.Storage\CollisionDocNet.Storage.csproj --configuration Release --no-restore
dotnet test --project tests\unit\CollisionDocNet.Storage.Tests\CollisionDocNet.Storage.Tests.csproj --configuration Release --no-restore
dotnet format src\CollisionDocNet.Storage\CollisionDocNet.Storage.csproj --verify-no-changes --no-restore --verbosity minimal
```

After two independent review/correction cycles, the primary agent repeated the Release Storage suite: exit `0`, 137 succeeded, 0 failed, 0 skipped. Corrections added central/local ZIP and ZIP64 descriptor consistency, exact Deflate consumption, full declared occupied ranges, CFB red-black/minor/orphan invariants, root-aware FIB/MSG detection, strict OPC namespaces/part names/source existence/content types, OLE section/property/native bounds, and UTF-32 DTD/cancellation-aware XML reads. Inputs were owned synthetic fixtures only; no genuine corpus content, network, external process, native parser, external office-suite runtime or Microsoft Office automation was used.

The requested static performance-pattern scan reported no actionable critical or moderate finding. It is not benchmark or allocation evidence.

## Explicit gaps

- multi-disk/encrypted ZIP, legacy non-UTF-8 names, methods other than Store/Deflate and full ZIP64 conformance combinations;
- OPC signatures, interleaving and mutation;
- OLEPS dictionaries, vectors, arrays and indirect values, plus broader OLEDS variants;
- XML byte-exact spans rather than current line/character positions;
- pre-97 DOC classification and tolerant damaged-container recovery; and
- independent specification fixtures, differential comparison, fuzzing, corpus, performance and acceptance review.
