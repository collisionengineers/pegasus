# Testing, fixtures and dated evidence

## Purpose and claim discipline

This document is the canonical owner for the document-extraction test programme, fixture controls and dated evidence. Product intent and support boundaries are owned by [requirements](../../../docs/requirements.md), [capabilities](../../../docs/capabilities.md), [architecture](architecture.md) and [open decisions](../../../docs/open-decisions.md). Build and contribution policy is owned by [engineering](../../../docs/engineering.md); runbooks and operational handling are owned by [operations](../../../docs/operations.md) and [operator notes](../../../docs/operator-notes.md).

Related canonical indexes are the [documentation map](../../../docs/index.md), [decision records](../../../docs/decisions/README.md), [change records](../../../docs/changes/README.md), [design index](../../../design/README.md), [reference index](../../../docs/reference/README.md), [Azure index](../../../docs/azure/README.md) and [workspace index](../README.md).

Evidence terms are deliberately distinct:

- **Intended** describes a contract or planned capability.
- **Code-present or locally verified** means a named source boundary was exercised by named checks.
- **Caller-proved** requires a real caller path, not only direct unit tests.
- **Deployed** requires evidence from the deployed target.
- **Accepted** requires the named reviewer or authority and accepted scope.
- A green suite, fixture count, percentage estimate or successful happy path does not establish complete format support.
- A dated record supports only its named scope. “All tests passed” never extends to unlisted features, revisions, item classes, platforms, nesting, callers or packages.

The records below are dated evidence, not an evergreen status or completion ledger. Historical failures and later corrections remain visible where they explain the evidence boundary.

## Test lanes

| Lane | Purpose | Entry condition |
|---|---|---|
| T0 harness | Deterministic builds, discovery, manifest/schema safety and evidence capture | Repository foundation |
| T1 contracts | Detection, outcomes, deterministic identities/order, limits and cancellation | Shared foundations |
| T2 storage and decoding | CFB, ZIP/OPC, PDF filters, MIME transfer decoders, RTF and code pages | Owning foundation or parser |
| T3 format conformance | Specification-derived PDF, DOC, DOCX, MSG and EML cases | Each format unit |
| T4 end-to-end and nesting | Handler-to-common-result projection, ordered text, images, nested supported files and issue propagation | Vertical slice |
| T5 differential | Semantic comparison with exact-version independent tools | Oracle and comparator approved |
| T6 security and fuzz | Malformed input, expansion/depth limits, active-content denial and parser fuzzing | Every parser milestone |
| T7 genuine corpus | Approved manifested operator cohort and implementation-author-hidden holdout | CollisionSpike-facing subset |
| T8 performance | Allocation, throughput, expansion, cancellation and concurrency budgets | Stable behaviour |
| T9 CLI and package | Library/CLI equivalence, process I/O, exit codes, bundles and publish variants | Public extraction API stable |
| T10 integration | Caller-owned CollisionSpike adapter plus Web or Worker operational proof | Accepted public package |

Every positive feature needs absent, boundary, malformed and resource-limit companions where meaningful. Cross-format cases cover nesting, mislabelling, ambiguity/polyglots, duplicate evidence, cancellation and active or external-content denial. Fixture count alone is not coverage.

Differential results use independently selected, exact-version tools or specification expectations. Before interpretation, each comparator must define normalisation, tolerated differences and retained diagnostics. No comparator may become a production dependency.

Coverage, fuzzing, external oracles, genuine corpora, benchmarks and non-default publish variants are opt-in lanes. They must not make ordinary restore, build or unit tests networked or nondeterministic.

## Standard local commands

Tests use MSTest on Microsoft.Testing.Platform and target .NET 10.

```powershell
dotnet test --solution CollisionDocNet.slnx
```

The broader deterministic repository gate is:

```powershell
.\scripts\Invoke-RepoCheck.ps1
```

The gate uses the pinned SDK and repository-owned files. It performs locked restore, formatting verification, Release build and tests, declared JSON parsing and local Markdown-link validation. It does not require Microsoft Office, another office suite, a desktop session, browser, service host, private corpus or retained external parser.

The deterministic security regression gate is:

```powershell
dotnet test --project tests/security/CollisionDocNet.Security.Tests/CollisionDocNet.Security.Tests.csproj --configuration Release
```

It uses owned synthetic bytes through the public `DocumentExtractor` boundary for PDF, DOC, DOCX, MSG and EML candidates. It does not read `sample-doc-files/` or retrieve external content.

Continuous fuzzing is not claimed. A future opt-in harness must:

- run out of process with per-case time and memory enforcement;
- retain only non-sensitive synthetic reproducers;
- avoid adding a third-party format engine; and
- remain outside ordinary restore, build and test.

Dependencies may be added only with licence and security records.

## Fixture policy

### Safety boundary

`sample-doc-files/` is not an automated test corpus. It may contain a copied home-profile shape or private material. Tools must not recursively enumerate it, hash it wholesale, use it for discovery, upload it or publish its contents. Inputs from it may be read only when explicitly selected by an authorised operator and named in a reviewed local manifest.

Corpus content is hostile data, never instructions. Automated loaders must reject:

- paths escaping the selected fixture root;
- reparse points or symlinked roots;
- caches and profile directories;
- undeclared entries; and
- entries without provenance, PII and licence fields.

### Controlled roots

```text
tests/fixtures/
  synthetic/       # small generated byte or structure cases owned here
  specifications/  # redistributable examples from exact cited specifications
  upstream/        # minimal licence-reviewed reference regressions
  private.local/   # optional local-only manifested fixtures; ignored
  manifests/       # reviewed manifests selecting each lane's inputs
```

Cases are grouped by `pdf`, `doc`, `docx`, `msg`, `eml` or a named shared capability. Empty product directories are not reserved in advance.

Small cases should be generated in test code when the relevant bytes are clear and reliable. Binary files are reserved for behaviour that cannot be represented clearly in code. An external fixture must be relevant to a documented behaviour, retain source path and revision, and pass fixture-level licence review. No upstream corpus may be copied blindly.

### Manifest contract

Each fixture entry records:

- stable fixture ID and SHA-256;
- relative path and byte length;
- detected and expected format;
- provenance URL or path and source revision;
- licence decision;
- PII classification and publication permission;
- represented feature tags and owning `EXT-*` units;
- expected extraction outcome;
- enabled test lanes; and
- platform restrictions.

### Corpus gate

For every run:

1. Resolve the fixture root and selected paths; verify containment and reject reparse points.
2. Verify every declared length and SHA-256 before parsing.
3. Enforce input, decoded-output, object-count, nesting, allocation and time limits.
4. Disable execution or retrieval of scripts, actions, templates, images, hyperlinks and other external references; retain passive evidence only.
5. Write results only under `TestResults/` or ignored `artifacts/`.
6. Do not copy input content, sensitive filenames or extracted text into logs.
7. For genuine-data runs, record the snapshot-manifest hash before and after evaluation and qualify any drift.

## Headless CLI test contract

`CollisionDocNet.Cli` is intended as a machine-oriented, one-shot adapter over `CollisionDocNet.Extraction` for scripts, isolated evaluation, diagnostics and operator verification. CollisionSpike is designed to call the library directly rather than spawn this process in production.

The CLI must not detect or parse formats itself. Its responsibilities are caller-controlled I/O, argument validation, cancellation, result serialization, safe image materialisation and process exit status.

### Commands and input rules

```text
collisiondocnet detect  --input <path|-> [--name <filename>] [--media-type <hint>]
collisiondocnet extract --input <path|-> --output <new-directory> [limits]
collisiondocnet version
collisiondocnet help
```

`-` means standard input. Standard input requires `--name` as an untrusted hint.

Each invocation accepts exactly one source. It must not expand globs, enumerate directories, watch paths, open mailboxes or follow relationships discovered inside a document. Paths are resolved before use. URI inputs are rejected. UNC or network input is rejected by default and requires a future explicit caller-policy decision. Device paths and reparse points are denied by the tested local boundary.

The output directory must not already exist. The CLI must never recursively delete or silently overwrite a destination.

### Extraction bundle

```text
<output>/
  result.json
  assets/
    <stable-image-id>.<safe-extension>
```

Required invariants:

- `result.json` is UTF-8 without a byte-order mark and follows a versioned schema.
- It contains ordered text, image descriptors, relative stable-ID image paths, SHA-256 values, outcome evidence and provenance.
- Original filenames and content-disposition names are metadata only; they never become filesystem paths.
- Images are written once, checked against their recorded hashes and ordered independently of filesystem enumeration.
- Non-image attachments and embedded-object bytes are never materialised as output assets.
- Unsupported binary content may retain a bounded hash descriptor without becoming an asset.
- The CLI creates only its own staging directory below the caller-selected output parent.
- Completion atomically publishes the bundle where supported.
- Cancellation or technical failure removes only the resolved staging path created by that invocation.
- A structured failure result is retained only when it can be published without representing an incomplete bundle as complete.
- The root and every nested result must share one collision-free asset-identity namespace before files are created.

Current image admission evidence covers PNG, JPEG, GIF, TIFF, BMP, WebP, ICO, WMF and EMF signatures. Full structural validation and safe dimension/pixel accounting remain required. SVG is deliberately not emitted until active/external-content handling and sanitisation are designed.

### Streams and serialization

- `detect` and `version` write exactly one UTF-8 JSON document to standard output for a non-usage invocation.
- `extract` writes a small machine-readable completion envelope containing outcome and result path.
- Extracted text and image bytes never go to standard output or logs.
- Progress and diagnostics go to standard error.
- Diagnostics contain stable issue codes, correlation identity and bounded measures, but not extracted content, sensitive source names or attachment names.
- `--quiet` suppresses non-error diagnostics, not the completion envelope.
- JSON property order, enum spelling, number representation, timestamps and line endings are versioned and deterministic.
- Source-generated `System.Text.Json` metadata is the intended serialization route; reflection-based serialization is not assumed.
- Volatile elapsed milliseconds may remain in memory as diagnostic telemetry but are excluded from canonical semantic JSON until a later diagnostic/bundle contract defines deterministic persistence.

### Exit codes

| Code | Meaning |
|---:|---|
| `0` | `Complete` for the declared supported subset |
| `10` | `Partial` |
| `20` | `UnsupportedFormat` |
| `21` | `UnsupportedFeature` |
| `22` | `Encrypted` |
| `23` | `Corrupt` |
| `24` | `ResourceLimitExceeded` |
| `25` | `Cancelled` |
| `26` | `TimedOut` |
| `64` | Invalid usage or configuration; no extraction result exists |
| `70` | `TechnicalFailure` |

Expected document outcomes must not be converted into unstructured exceptions. Technical failures are contained at the process boundary and return a safe issue or result where possible.

### Limits and interruption

CLI switches select a named, versioned resource class. They may lower but must not silently raise its limits. The eventual surface covers:

- input and decoded bytes;
- object, part and stream counts;
- text characters;
- image count, bytes and pixels;
- nesting depth;
- CPU and elapsed deadline; and
- working-memory ceilings.

`Ctrl+C` passes cancellation through the same `CancellationToken` used by library callers. Traversal and decoder loops must observe it. A second interrupt may terminate immediately; process-host tests must determine whether a valid result bundle can still be guaranteed.

### Packaging lanes

The first intended package is a framework-dependent `net10.0` console executable and managed dependencies, without desktop, web or Office workloads.

Later candidates may add:

- RID-specific self-contained packages;
- single-file packages after startup, signing and temporary-extraction analysis; and
- Native AOT packages after trim/AOT analysis and every format and encoding path passes on each RID.

These are variants of the same CLI, not alternative engines. Windows x64 and Linux x64 are the first planned host classes. Each additional RID requires separate evidence. Self-contained, single-file and Native AOT evidence cannot be inherited from a framework-dependent build.

The T9 lane must cover argument and usage snapshots, path containment, existing output, reparse/symlink handling, interruption, stdin/file equivalence, library-result equivalence, stable JSON and asset hashes, complete outcome/exit mapping, stdout/stderr leakage, Windows/Linux framework smoke and separate opt-in publish tests per variant and RID.

## Evidence record contract

Use this structure for a completed port unit, caller boundary or release candidate:

```yaml
claim_id: EV-
date_utc:
managed_commit_or_tree_hash:
port_units: []
formats: []
scope:
explicit_exclusions: []
specifications:
  - name:
    revision_or_date:
    sha256:
secondary_sources: []
fixture_manifest:
fixture_ids: []
commands:
  - command:
    exit_code:
    input_class:
    boundary:
    limitations:
environment:
  os:
  dotnet_sdk:
  architecture:
results:
  passed:
  failed:
  skipped:
differential_oracles:
  - name:
    version:
    command:
    comparator:
    tolerances:
security_and_resource_limits:
known_gaps: []
artefacts: []
reviewer:
```

Every compatibility claim names the `EXT-*` unit, exact specification and oracle revision, fixture IDs, command and exit result, input class, exercised boundary and known gaps. A secondary oracle is recorded by exact name and revision only when one was used.

Top-level repository evidence from 2026-07-23 and 2026-07-24 could not record a Git commit or tree because the repository root was not a Git worktree. That limitation must not be replaced with a fabricated identity.

## Dated evidence

### EV-2026-07-23 — foundation tooling audit

This record covers only the managed CFB v3 fixed-header reader. It is local toolchain, static-review and unit evidence, not full CFB conformance, complete DOC extraction, independent differential evidence or CollisionSpike caller proof.

Environment: Windows `10.0.26200` x64, .NET SDK `10.0.302`, MSBuild `18.6.11`, `net10.0`, MSTest.Sdk `4.0.2` and Microsoft.Testing.Platform `2.0.2`.

`global.json` resolved exactly to SDK `10.0.302`; no repository-local SDK was installed because the pinned stable SDK was already available. `.dotnet/` remained ignored for a future isolated-SDK need.

At that point the two project files, solution, `Directory.Build.props` and `global.json` were inspected. Shared language, analyzer and restore policy was centralized in `Directory.Build.props`; framework and project references remained project-owned. There was no TFM-conditional property, custom target or ordinary package versioning need to justify `Directory.Build.targets` or Central Package Management. Lock files existed for both projects and the repository script restored them with `--locked-mode`.

```powershell
dotnet msbuild src\CollisionDocNet.Storage\CollisionDocNet.Storage.csproj -getProperty:TargetFramework -getProperty:LangVersion -getProperty:ImplicitUsings -getProperty:Nullable -getProperty:TreatWarningsAsErrors -getProperty:RestorePackagesWithLockFile
```

The evaluated values were `net10.0`, `latest`, `enable`, `enable`, `true` and `true`.

A static hot-path scan of `CompoundFileHeaderReader.Read(ReadOnlySpan<byte>)` found zero uses of unqualified string searches, substring allocation, culture casing, chained replacements, `params`, character LINQ, per-call list/dictionary allocation, regular expressions, async/task, I/O/serialization signals or eligible unsealed leaf classes. One eligible leaf record was sealed. Positive signals included `ReadOnlySpan<byte>`, compile-time span data and direct loops. This was not a benchmark or allocation measurement.

A grouped pseudo-mutation review covered branches, ranges, endian/arithmetic reads, output mapping and result invariants: 43 sites were killed by assertions, none survived, none lacked coverage and two were equivalent. The equivalent sites were initial immutable-builder capacity and a redundant `IsSuccess` conjunction under private construction invariants. Review-driven regressions covered signature, CLSID and reserved range ends; invalid values on both sides of ordered constants; multiple alignment remainders; oversized containers; every header DIFAT slot; little-endian DIFAT decoding; and the default error result.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Storage.Tests\CollisionDocNet.Storage.Tests.csproj --configuration Release --filter "FullyQualifiedName~CompoundFileHeaderReaderTests"
.\scripts\Invoke-RepoCheck.ps1
```

Both exited `0`. The focused generated-header boundary reported 30 succeeded, none failed or skipped. The repository gate reported locked restore, unchanged formatting, zero-warning/error Release build, passing tests, parseable declared JSON and valid local Markdown links. No corpus, filesystem input, network, external executable, office suite, native code, macro or OLE execution was involved.

The performance and pseudo-mutation findings were AI-assisted static review and may contain omissions or false positives. They do not substitute for benchmarks, a mutation runner or independent human review.

### EV-2026-07-23 — five-format scope and plan validation

This record validated the research plan, five-format port-unit coverage, managed library/CLI boundary, .NET/MSBuild organization and unchanged fixed-header test boundary. It did not establish end-to-end PDF, DOC, DOCX, MSG or EML extraction.

Inputs were repository-owned source, projects and documentation. `sample-doc-files/` was not enumerated. The retained `core/` checkout was checked only for revision and cleanliness, was not changed and was not required by the ordinary gate.

Environment: Windows 11 Pro `10.0.26200` x64, PowerShell `7.6.3`, .NET SDK `10.0.302`, with `10.0.300` also installed.

```powershell
dotnet --version
dotnet --list-sdks
Get-Content -Raw global.json
dotnet msbuild src/CollisionDocNet.Storage/CollisionDocNet.Storage.csproj -nologo -getProperty:TargetFramework,LangVersion,Nullable,Deterministic,EnableNETAnalyzers,AnalysisLevel,EnforceCodeStyleInBuild,TreatWarningsAsErrors,RestorePackagesWithLockFile
dotnet test --solution CollisionDocNet.slnx
.\scripts\Invoke-RepoCheck.ps1
.\scripts\Invoke-RepoCheck.ps1 -SkipRestore
```

The SDK and test-platform checks exited `0`. Evaluated production settings were `net10.0`, `latest`, nullable enabled, deterministic builds enabled, `latest-recommended` analysis, style enforcement, warnings as errors and lock-file restore. The current-source scan found no string slicing/case normalization, repeated search/replacement, LINQ materialization, regex, async/synchronous I/O, `params` allocation or eligible unsealed class in the small fixed-header boundary. Future parser allocation, algorithmic and memory budgets remained unmeasured.

The test and repository commands exited `0` for the generated CFB-header boundary. The ordinary gate did not invoke `core/` or an external process oracle. A lexical consistency check found 64 unique format units across the five detailed plans, none missing from the then-current catalogue or compatibility matrix; shared, governance, API, CLI, QA, packaging and integration entries accounted for additional units.

Repository checks confirmed the top-level directory was not a Git repository, the retained `core/` revision was clean, and no external-parser invocation appeared in the ordinary gate.

Planning conclusions were:

- PDF covers the observed PDF 1.0–2.0 family, extensions and passive profile evidence, not only PDF 2.0.
- DOC uses a direct binary CFB/FIB/CLX path; DOCX/XML is never an intermediate. Pre-97 and mislabelling require a separate decision.
- DOCX is an independent ZIP/OPC/XML handler.
- MSG preserves the generic MAPI property bag and declared non-mail item classes.
- EML covers the RFC 5322/MIME tree, internationalisation, reports, TNEF and protected content.
- Product surfaces are a managed library and a one-shot headless CLI.

At this evidence point only the strict CFB v3 fixed header had code-under-test evidence. No conformance, differential, fuzz, coverage, genuine-data, performance/concurrency or publish-variant run had occurred; no operator or independent reviewer had accepted a format slice; and web specification revisions remained planning baselines until downloaded artefacts and hashes were recorded.

### EV-2026-07-23 — local operational sample cohort preparation

A user-authorised script selected and copied PDF, EML and MSG samples from the adjacent CollisionSpike corpus into ignored local research storage. This was fixture preparation only: it did not establish extraction correctness, feature conformance, corpus stability, redistribution permission or DOC/DOCX coverage.

The adjacent corpus was treated as immutable hostile data. Aggregate discovery found 387 PDF, 286 EML and 23 MSG files. Sixteen exceeded the current 10 MiB CollisionSpike intake class and were excluded. Selection deduplicated by SHA-256 before copying. No source content, operational filename or identifier was printed or committed.

The ignored cohort contains twelve opaque files: four each of PDF, EML and MSG, all below 10 MiB. Selection used passive byte markers, copied without transformation and rechecked destination hashes. The ignored manifest records the source snapshot and per-input evidence without publication.

Markers covered PDF pages, fonts, images, metadata, annotations, classic xrefs, xref streams and object streams; EML multipart/MIME, HTML, inline, attachment, Base64, quoted-printable and nested-message shapes; and MSG message classes, recipients, attachments, named properties, transport headers and plain/HTML/RTF bodies. These markers were selection hints, not parser proof.

```powershell
pwsh -NoProfile -File .\scripts\Import-CollisionSpikeSamples.ps1 -CorpusRoot <adjacent-corpus> -DestinationRoot .\sample-doc-files\collisionspike-corpus-20260723
```

The command exited `0`: twelve files were copied, four per requested family. An aggregate check found twelve manifest entries, all source/copy hashes equal, no over-limit file and no reparse point. The script refuses reparse-point roots and existing destinations, preventing silent merge or overwrite. The samples and manifest remain ignored, must not be uploaded or published, and are not permission to redistribute.

### EV-2026-07-23 — hostile-input foundations

Scope: `EXT-FND-001`, `EXT-FND-002`, `EXT-FND-003` and `EXT-MOD-001`, using controlled in-memory values and hostile boundaries only.

The code-under-test boundary exercised:

- checked half-open ranges and random-access primitive reads;
- bounded sequential stream loading without stream ownership;
- cancellation of blocked reads and monotonic deadline expiry;
- cumulative input, decoded, object, text, asset and depth budgets;
- cooperative cancellation and monotonic deadlines;
- SHA-256 over already bounded memory;
- length-prefixed cross-platform stable IDs rejecting Windows device basenames;
- no unbounded stream-hashing API;
- strict/replacing UTF-8, UTF-16 LE/BE and Windows-1252 with every invalid byte offset;
- FILETIME-to-UTC conversion;
- versioned NFC and LF normalization;
- immutable inputs and policies;
- checked locations, validated evidence, nested results, all ten outcomes and result-local unique asset IDs; and
- source-generated deterministic semantic JSON with total evidence ordering, asset bytes excluded and elapsed milliseconds omitted from canonical JSON.

An independent review initially rejected this slice. Corrections addressed blocked-I/O cancellation, total ordering, elapsed-time determinism, checked ranges, unsafe stream hashing, materialisation, stable IDs, invalid-encoding offsets and invalid public states. Independent re-review remained required before restoring the source record’s `Locally verified` label.

```powershell
dotnet build src\CollisionDocNet.Core\CollisionDocNet.Core.csproj --configuration Release
dotnet build src\CollisionDocNet.Model\CollisionDocNet.Model.csproj --configuration Release
dotnet test --project tests\unit\CollisionDocNet.Core.Tests\CollisionDocNet.Core.Tests.csproj --configuration Release
dotnet test --project tests\unit\CollisionDocNet.Model.Tests\CollisionDocNet.Model.Tests.csproj --configuration Release
```

All commands exited `0`; builds had no warnings or errors. Core reported 58 passing cases, including blocked-stream cancellation/deadline and multi-error encoding-offset cases. Model reported 44 passing cases, including reversed-byte asset and nested-result checks, duplicate-ID rejection, portable tokens and invalid-state rejection.

The static scan found no critical listed pattern. Its one `params` occurrence was `params ReadOnlySpan<string>`, the source-generated JSON call supplied its generated context, and a four-byte `stackalloc` was outside loops in a short synchronous helper.

Not proved: format conformance, fuzzing, corpus evaluation, semantic differential comparison, benchmarks, concurrency-host measurement or independent API acceptance. The default 10 MB policy bounded retained input and a transient copy, but did not implement measured process-memory or CPU enforcement. Later work must decide deterministic persistence of volatile telemetry and preserve a single asset namespace through the root and nested results. Additional code pages, date syntaxes and parser-specific locations remained format-owned. The focused correction did not rerun the full solution because other projects were being changed concurrently.

### EV-2026-07-23 — CFB and shared storage evidence

#### CFB v3/v4 reader

The read-only, BCL-only `EXT-STO-001` boundary exercised strict v3/v4 headers, v4 sector sizing and padding; DIFAT/FAT/miniFAT; checked directory parsing; sibling ordering, colour and reachability; exact regular and mini-stream traversal; cycle, cross-link, duplicate-reference, orphan, reserved-value and range rejection; explicit resource limits and cancellation; deterministic stream-ID ordering; and immutable copied output.

The reader did not execute content, activate OLE, select filesystem paths, retrieve resources, launch processes or use native code.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Storage.Tests\CollisionDocNet.Storage.Tests.csproj --configuration Release --no-restore
```

The dated synthetic run exited `0` with 49 passing cases and no failures/skips, using owned in-memory CFB fixtures. Harness duration was under one second. Formatting and static checks were reported clean, but were not allocation or throughput measurements.

An original independent-review request for equal black height was later proved incorrect. MS-CFB 2.6.4 permits an all-black binary tree and does not require equal black height. The non-spec check was removed on 2026-07-24 and replaced by a positive regression.

Not proved: specification-derived independent fixtures, genuine DOC/MSG profiles, fuzz/property corpora, differential comparison, allocation/CPU/elapsed/cancellation-latency/concurrency budgets or independent acceptance.

#### ZIP/OPC/OLE/XML and detection

The shared-storage synthetic boundary exercised:

- ZIP central/local consistency, Store/Deflate, ZIP64 EOCD/locator, CRC, duplicate denial, traversal/absolute/drive denial, overlap checks, entry/count/expanded-total/ratio limits, cancellation and explicit encrypted/unsupported-method outcomes;
- passive OPC content types and relationship graphs, normalized internal targets, package-escape denial and no external retrieval;
- common scalar OLE property-set values with retained raw bytes and the common ANSI Ole10Native descriptor/payload;
- namespace-aware bounded XML with depth/node/attribute/text limits, cancellation and DTD/entity/external-resolution denial; and
- structural PDF, DOC, DOCX, MSG, EML and encrypted-OOXML candidates, deterministic ambiguity, hint mismatch and cancellation.

After two review/correction cycles, the Storage suite reported 137 passing synthetic cases. Corrections included ZIP/ZIP64 descriptor consistency, exact Deflate consumption, occupied ranges, CFB minor/orphan invariants, root-aware FIB/MSG detection, strict OPC namespaces/part names/source existence/content types, OLE section/property/native bounds and UTF-32 DTD plus cancellation-aware XML reads. No genuine corpus, network, external process, office automation or native parser was used.

Not proved: multi-disk/encrypted ZIP, legacy non-UTF-8 names, methods beyond Store/Deflate, all ZIP64 combinations, OPC signatures/interleaving/mutation, OLEPS dictionaries/vectors/arrays/indirect values, broader OLEDS, byte-exact XML spans, pre-97 DOC, damaged-container recovery, independent fixtures, differential comparison, fuzzing, corpus evaluation, performance or acceptance.

### EV-2026-07-23 — format-specific synthetic slices

#### Direct DOC text and structured DOC

The direct managed binary Word boundary used CFB/FIB/CLX/Pcdt/PlcPcd without DOCX/XML, Office automation, native parser or external process. Synthetic tests exercised Word97/pre-97 classification, effective `nFib`, encryption/table flags, FIB arrays and ranges, logical CP piece order, compressed FC transformation, Windows-1252 and UTF-16 pieces, story ranges, conservative controls and CP/story-CP/FC/piece provenance. Unsupported anchors, PRM/formatting, pictures, secondary FIB, active content and ambiguous controls forced non-complete issues.

The combined Writer suite reported 43 passing cases after review and correction, including an owned raw-v3-CFB integration fixture, exact resource boundaries and retry determinism. The record nevertheless retained broader raw-CFB/conformance work among its open gates. Genuine DOC corpus evidence was absent at that point.

A read-only unnamed secondary implementation had been consulted for selected binary-Word behaviour. Its identity, revision and licence were not recorded and cannot be reconstructed. It was not executed, copied or modified. It is quarantined as non-authoritative and prohibited as a future source unless provenance is recovered and reviewed.

The structured layer exercised bounded ordered SPRMs with known meanings and retained unknown operands; simple/complex PRM routing; CHPX/PAPX BTE/FKP evidence with FC-to-CP provenance and paragraph style indices; passive FIB-range/PLC evidence; SHA-256 stream assets with OLE parent/class context and no activation; common bounded SummaryInformation metadata; cancellation and resource outcomes. Correction added semantic-control partials, precise resource outcomes, immutable storage-owned asset bytes, honest generic PLC status, FKP cross-validation and complete-result determinism.

Not proved: complete codepage/font resolution, simple-file fallback, all anchors, positive complex PRCs, full variable SPRMs/PAPX/Data, style/list/table/section semantics, nested field/bookmark/comment/revision semantics, OfficeArt/PICF/BLIP/equations, Ole10Native/package/VBA/forms semantics, advanced OLE metadata, mid-loop cancellation, fuzzing, differential comparison, genuine cohorts or performance acceptance.

#### EML

The bounded BCL-only EML/RFC 5322 and MIME slice exercised ordered headers, folding, selected encoded-word/RFC2231/address forms, multipart and nested messages, incremental Base64 and quoted-printable, selected charsets, plain/inert HTML, passive assets/CID evidence, cumulative budgets, raw spans, sticky terminal outcomes and periodic cancellation/deadlines.

Unsupported, signed, encrypted, flowed, delivery-report and TNEF structures produced visible non-complete outcomes. HTML/script was not executed, external content was not retrieved, signatures were not verified, protected content was not decrypted and TNEF was not semantically decoded.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Email.Tests\CollisionDocNet.Email.Tests.csproj --configuration Release --no-restore --filter "TestCategory!=LocalCohort"
dotnet test --project tests\unit\CollisionDocNet.Email.Tests\CollisionDocNet.Email.Tests.csproj --configuration Release --no-restore --filter "TestCategory=LocalCohort"
```

Both dated commands exited `0`: 28 focused synthetic cases and one opt-in local-cohort case. The cohort case processed four opaque EML samples; each returned `Complete` with deterministic canonical retry JSON and non-empty evidence. It printed no content, name, path, hash or identifier.

A static scan found no critical API-pattern defect but identified a bounded HTML substring and bounded parser collections for later measurement.

Not proved: complete modern/obsolete RFC 5322 grammar, every MIME subtype/encoding/charset, complete alternative/related/flowed policy, DSN/MDN/feedback/TNEF semantics, exact signed octets, cryptographic verification/decryption, specification conformance, independent differential comparison, fuzz/property/parser-smuggling coverage, benchmarks, hidden holdout or independent acceptance.

#### DOCX

The custom BCL-only DOCX slice used shared ZIP/OPC/XML. Synthetic tests exercised Strict/Transitional packages and encrypted CFB wrappers; main/header/footer/footnote/endnote/comment stories; core paragraph/text/table/section/field/bookmark/hyperlink/deleted-revision evidence; properties/styles/numbering/settings/fonts/themes inventory; and passive media, embeddings, custom XML, VBA, ActiveX, signatures, charts and diagrams.

External relationships were not retrieved. DTD/entities were denied. Unsupported MCE, control binding, `altChunk`, drawing and dependency semantics forced visible `Partial` issues. Corrections added exact allowlists/reachability, source ordering, cumulative budgets/deadlines, orphan evidence and honest XML provenance.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Writer.OpenXml.Tests\CollisionDocNet.Writer.OpenXml.Tests.csproj --configuration Release --no-restore
```

The corrected synthetic run exited `0` with 31 passing cases. A later full-solution rerun was blocked by concurrent public-project renaming rather than a DOCX failure.

Not proved: full MCE, style/numbering resolution, content-control/forms/mail-merge semantics, complete fields/comments/revisions, graphics/OMML, signature assurance, nested embedded extraction, all shared OPC corrections, conformance, differential comparison, fuzzing, corpus evidence or performance acceptance.

#### MSG

The BCL-only MSG/MAPI slice used the managed CFB reader. Synthetic tests exercised bounded root/child property bags; fixed, variable and multivalued properties with raw unknown preservation; selected named properties and code pages; recipients; plain/HTML body policy; bounded MELA/LZFu and shallow passive RTF text; by-value/reference/OLE/embedded attachments; embedded-message depth; cancellation; protected-class recognition; and selected mail, report, meeting, calendar, contact, list, task, note and generic projections.

No OLE object, path, protected content or embedded program was activated. Unsupported properties and classes remained raw with issues. Corrections addressed variable multivalue layout, contextual one-pass decoding, cancellation, cumulative bounds, outcome/evidence preservation and storage/class projection.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Outlook.Tests\CollisionDocNet.Outlook.Tests.csproj --configuration Release --no-restore
```

The corrected synthetic run exited `0` with 42 passing cases.

Not proved: complete property catalogue and spans, broader code pages, EML transport-header delegation, full RTF/encapsulated HTML, shared stable asset IDs, live cumulative shared budgets, recurrence/time-zone and other item semantics, CMS/TNEF, genuine item-class corpora, conformance, differential comparison, fuzzing or performance acceptance.

#### PDF core and passive surface

The BCL-only PDF core exercised bounded COS lexical values and spans; direct/indirect objects and streams; classic/xref-stream/hybrid checks; bounded `/Prev` chains and object streams; ASCIIHex, ASCII85, LZW, Flate and RunLength with TIFF/PNG predictors and expansion limits; header/Catalog versions; trailer/root/page trees; common text/position operators; basic single-byte encodings; ToUnicode `bfchar`/`bfrange`; approximate deterministic geometric runs; encryption/media classification; cancellation, limits and visible bounded recovery.

Correction added authoritative xref state, strict stream/filter boundaries, operator/inline-image handling, Form budgets/cycles, encodings/CMaps and provenance corrections.

A reviewed secondary source delegated significant PDF parsing and rendering to external engines. No code was ported from that path and no external engine became a production dependency.

The passive surface then exercised bounded Info/XMP claims with validation explicitly not performed; tagged/marked content including MCID and ActualText; optional-content inventory; outlines, page labels and name trees; annotations; AcroForm/passive XFA; SHA-256 image, mask, embedded, associated, portfolio and media assets; passive actions, JavaScript, URI, launch, media and 3D evidence with execution/retrieval disabled; structural signature ByteRanges with `trust=false`; Standard/Adobe.PubSec classification without encrypted-content interpretation; inherited resources; bounded Form XObject recursion; and cancellation inside content and decoding loops.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Pdf.Tests\CollisionDocNet.Pdf.Tests.csproj --configuration Release --no-restore
```

The corrected synthetic PDF run exited `0` with 46 passing cases. Inputs were owned synthetic PDFs.

Not proved: complete revision/hybrid/linearization state, profile validation, digest/trust/revocation, decryption, indirect-length handling without recovery, inline-image payload parsing, DCT/JPX/JBIG2/media semantics, XFA semantics, Form matrix transforms, deep navigation semantics, marked-content property lists, complete Type0/CID/CMap/metric coverage, CTM/rotation/columns/bidi, complete security clauses, conformance, differential comparison, fuzzing, corpus evaluation or performance acceptance.

### EV-2026-07-23 — public extraction and CLI slice

The source record covered a custom five-format managed dispatch boundary and one-input CLI. It was local code-under-test evidence, not production deployment or CollisionSpike acceptance.

The exercised boundary included byte-first detection without fallback engines; byte/stream input; source, filename, media and policy provenance; PDF/DOC/DOCX/MSG/EML projection; signature-preserving corrupt outcomes; and outcome-specific exception mapping. The public assembly/root namespace was `CollisionDocNet.Extraction`.

CLI tests covered `help`, `version`, `detect`, `extract`, file/stdin input, required stdin name, `--quiet`, lower-only named limits, documented exit codes, deterministic completion envelopes, new-directory staging, relative image paths, safe extensions, post-write SHA-256 and URI/UNC/device/reparse denial.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Conversion.Tests\CollisionDocNet.Conversion.Tests.csproj --configuration Release --no-restore
dotnet test --project tests\unit\CollisionDocNet.Cli.Tests\CollisionDocNet.Cli.Tests.csproj --configuration Release --no-restore
```

The dated runs exited `0`: Extraction reported 11 passing cases and CLI 27. Static dependency review found filesystem calls isolated to the CLI filesystem boundary, `Console` confined to process entry and no time/environment/network/process static coupling.

Not proved: handler-level live shared budgets and pre-allocation deadlines, original bytes for native MSG embedded items, exact malformed/concurrency boundaries, output-parent reparse races, second-`Ctrl+C` host behaviour, Windows/Linux framework smoke, schema migration, complete library/CLI equivalence or nesting/security/performance acceptance.

### EV-2026-07-23 — cross-format nesting, security and local performance

The extraction boundary recursively dispatched materialised supported attachment bytes under cumulative input, decoded, object, text, asset, depth and deadline controls. It retained occurrence paths, parent relationships, hashes and local/aggregate issues. Duplicate bytes remained distinct occurrences; unsupported assets remained hashed evidence.

Conversion tests reported 19 passing cases. Open seams were native MSG embedded storage without original CFB bytes, deterministic mid-recursion cancellation/deadline cases and a finite ancestor-cycle fixture seam.

The deterministic security suite reported 21 passing cases. It covered passive PDF actions; DOCX external/VBA evidence and XML/ZIP denial; EML remote/path/script passivity and nesting; rejected hostile DOC/MSG CFB markers; five-format cancellation, deadline, input and stream failures; content-free diagnostics; 80 deterministic format mutations; and 64 arbitrary seeds. A DOCX limit diagnostic found by the suite was corrected.

Not proved: valid structured hostile DOC/MSG active-content fixtures, socket-level no-network instrumentation or maintained continuous fuzzing.

An isolated, locked BenchmarkDotNet `0.15.8` test project used `MemoryDiagnoser`. All ten cases passed list/Dry validation. Windows/.NET 10 Short observations were:

| Case | Observation |
|---|---:|
| 1 MiB DOCX detection | 7.256 ms mean; 4.04 MiB allocated |
| Synthetic MSG dispatch | 127.7 μs mean; 141.19 KiB allocated |
| Twenty five-format operations at degree four | Stable canonical fingerprints |
| Blocked-stream cancellation | Approximately 30 ms |

These were leads, not accepted budgets. No 10 MiB end-to-end, Linux, sustained/nested load, larger class, independent repetition or authorised threshold was completed. Detailed output remained ignored under `artifacts/performance/20260723-wave13/`.

### EV-2026-07-24-DOC-R00 — DOC specification-source freeze

This evidence was `Mapped` and `Locally verified` for internal specification-led implementation. It was not legal or patent advice, product-licence acceptance, redistribution permission, parser implementation, conformance, distribution approval or DOC support.

Repository-owned input-manifest SHA-256:

```text
149de21e5eba9209b9891e5f388407b1b54c60bef8c2362906f105d92aab90fd
```

Pinned sources:

| Specification | Revision/date | SHA-256 |
|---|---|---|
| MS-DOC | 12.5 / 2026-02-17 | `2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a` |
| MS-CFB | 12.0 / 2024-04-23 | `2d650184072a148ba98ad0b68072fd5ad7780e46f3528d7f263f3127b2dadab5` |
| MS-ODRAW | 12.4 / 2025-08-19 | `9ead8f1f3805cf6d4f5597bed516bf7604e330b803f64d28d9b7a0a9dba9a2fc` |
| MS-OLEDS | 13.0 / 2024-04-23 | `42e666e9f1b1c437972bbe601d302ec25e45557eb309c7d854e54facfeddb134` |
| MS-OLEPS | 9.0 / 2024-04-23 | `4343243993cd16bda98e5abe5383a82db5f2eea0b34b54dc7d93978a372844ea` |
| MS-OSHARED | 11.1 / 2025-11-13 | `3a17ec72868a7ba8c9c987995c8902e832a42d66eecbf149101a4e6c7255f87c` |
| MS-OFFCRYPTO | 14.0 / 2026-02-17 | `9b7a67eb5d0408566a61f218792fcd21536dbc970d83695ad94365e535533f33` |
| MS-OVBA | 15.0 / 2026-05-19 | `31fb68ac3ef209cb32247a3060ff775cc0517c4120137cb39945690448b46c79` |
| MS-OFORMS | 9.1 / 2025-08-19 | `7bbbbdc43407524fe2af99c070dfc358cc67404e5224b56d5cdabbc4736c9158` |

```powershell
pwsh -NoProfile -File .\scripts\Acquire-DocSpecifications.ps1 -VerifyOnly
pwsh -NoProfile -File .\scripts\Invoke-RepoCheck.ps1
```

Verification exited `0` for eighteen ignored artefacts: nine date-stamped DOCX publications and nine current PDFs. Current PDF URLs were not immutable and were pinned only by hash. Downloads remained under ignored research storage and acquisition was excluded from the offline repository gate.

Open questions remained:

- exact-revision patent coverage for MS-OLEDS and MS-OLEPS;
- product licence and distribution approval;
- the absence of a named release/licensing reviewer; and
- the historical unnamed secondary implementation, which remained prohibited.

Technical checks were performed with two independent read-only audits. Repository-owner direction authorised internal implementation only.

### EV-2026-07-24-DOC-R01 — binary structure atlas

This record was `Mapped`, `Specified` and `Locally verified` for the Word 97-family CFB/FIB boundary. It was not production implementation, conformance, differential verification, caller use or format acceptance.

The atlas covered `EXT-DOC-002` and `EXT-DOC-004` through `EXT-DOC-011`: CFB ownership, five accepted `nFib` layouts, secondary FIB/AutoText, quick-save and encryption classification.

| Identity | Value |
|---|---|
| Descriptor count | 183 across five layouts |
| Table-stream descriptors | 143 |
| WordDocument-stream descriptors | 4 |
| No-stream descriptors | 35 |
| FIB FILETIME descriptors | 1 |
| Source field-sequence SHA-256 | `a7494e994901be57ee06e602eed824d99ea50699b82ab8a89f790ad34938ae8f` |
| Reviewed canonical-atlas SHA-256 | `c0afee25a88147efe5d4acc599a4d9876893a152f797c50958bd5669d0baf75b` |

```powershell
pwsh -NoProfile -File .\scripts\Generate-DocFibAtlas.ps1
pwsh -NoProfile -File .\scripts\Test-DocFibAtlas.ps1 -SpecificationPath .\artifacts\research\doc\2026-07-24\specifications\MS-DOC-12.5-260217.docx
```

Both commands exited `0`. Generation verified the publication hash and exact FIB paragraph bands; independent re-reading verified order, versions, offsets, stream ownership and policy invariants. This research tooling did not alter the production parser.

Production differences identified at that date were:

- reserved FibBase bytes at offsets 20, 24 and 28 were interpreted as character set, `fcMin` and `fcMac`;
- `pnNext` was read as signed and secondary-FIB traversal was absent;
- most `FibRgW97`/`FibRgLw97` values were discarded;
- most `FibRgFcLcb` entries were anonymous or absent; and
- encryption subtype headers, AutoText and full quick-save semantics were absent.

No content was executed, decrypted or retrieved; private samples were not accessed.

### EV-2026-07-24-DOC-R02 — format classification

This record was `Mapped`, `Specified` and `Locally verified` for byte-owned routing and classification contracts. It was not production routing, conformance, pre-97 support, caller use or release acceptance.

Authority included the pinned MS-DOC, MS-CFB and MS-OFFCRYPTO hashes above plus MS-OXMSG 18.0 / 2025-05-20, whose exact revision was recorded without a retained publication hash.

The contract contained five profile predicates and 26 cases. Exact supported effective `nFib` values were:

```text
0x00C1  0x00D9  0x0101  0x010C  0x0112
```

Canonical contract SHA-256:

```text
c84fa08b0ebc67aa6b023e925093a27de2e1e95ddfd2d04a79a476306f7e8871
```

Fixture groups were `DOC-T01` through `DOC-T04`.

```powershell
pwsh -NoProfile -File .\scripts\Test-DocFormatClassification.ps1
```

The offline verifier exited `0` for source identity, predicates, case tuples, ownership, hint policy, versions and fixture-group identity.

A pseudo-mutation review sampled fifteen high-risk changes: five were killed, five survived and five had no coverage. Required future kills included exact `nFib` membership, 0Table/1Table routing, Standard/Extensible/Agile wrapper grammar, generic `application/xml` rejection, root OPC relationship ownership, template/AutoText/repair variants, profile collisions and stable ambiguity, hints not resolving ambiguity, and direct public DOC/MSG/encrypted-wrapper/unrelated-container outcomes.

Production differences identified at that date included an inclusive `nFib` range, legacy-marker collapse to generic corruption, unrelated CFB/ZIP containers called corrupt, name-only encrypted-OOXML detection with lost format identity, over-broad DOCX XML matching and missing direct ambiguity/public-route tests.

Hints never route ambiguous content; no parser is selected for ambiguity; active/extensible provider URLs are never retrieved. Pre-97 parsing remained subject to a proposed decision. MS-OXMSG fixtures required a retained hash-pinned publication and provenance approval.

### EV-2026-07-24-DOC-R03 — text, pieces and stories

This record was `Mapped`, `Specified` and `Locally verified` for direct DOC text algorithms through an independent test-only oracle. It was not production conformance, differential verification, genuine-data acceptance, caller use or release acceptance.

The contract covered current CLX authority, PRC/PRM, `PlcPcd`, CP/FC mapping, compressed and UTF-16 decoding, seven document parts, guards, headers/footers, AutoText and passive controls.

| Identity | Value |
|---|---|
| Outcome cases | 39 |
| Compressed overrides | 24 |
| Document parts | 7 |
| Control tokens | 18 |
| Canonical SHA-256 | `85529c714ded2e4776c0930ef82e5d3c099c6822d156c06bf94b8248e0529c31` |
| Fixture groups | `DOC-T01`, `DOC-T02`, `DOC-T03` |

```powershell
pwsh -NoProfile -File .\scripts\Test-DocTextStoryContract.ps1
dotnet test --project .\tests\unit\CollisionDocNet.Writer.Tests\CollisionDocNet.Writer.Tests.csproj --filter "FullyQualifiedName~DocR03ExecutableSpecificationTests" --configuration Release --no-restore
pwsh -NoProfile -File .\scripts\Invoke-RepoCheck.ps1
```

The contract verifier exited `0`. The independent synthetic oracle reported 92 passing cases across all five layouts and both `fComplex` states, including CLX/Prc/Pcdt/PlcPcd, Prm0/Prm1, encodings and bounds, all compressed bytes, malformed UTF-16, all seven parts, headers, AutoText and controls with owner/property states. It did not use production parser constants.

A 47-site pseudo-mutation review found 13 pre-existing kills, 12 survivors and 22 uncovered sites. One nominal pre-existing kill asserted the wrong pre-footnote guard; the independent oracle replaced that rule but did not claim a production mutation score.

Production differences recorded at that date included reserved FibBase misreads, one invalid shared synthetic FIB shape, `reserved3` exposed as a Macro part, misplaced outside `U+000D`, fabricated CP1252-like compressed decoding, absent `cbMac` enforcement, missing malformed-UTF-16 diagnostics, unreconciled PRC issues and public locations losing CP, part and stream identity.

No field evaluation, link retrieval, active execution or private-corpus access occurred.

### EV-2026-07-24 — local DOC CFB/FIB correction

One caller-selected local 114,688-byte DOC was used to diagnose `EXT-STO-001`, `EXT-DET-001`, `EXT-DOC-002` and `EXT-DOC-003`. Its name, content and hash were not published in this record. Outputs remained ignored. This was one genuine compatibility case, not a cohort, conformance run, hidden holdout or acceptance.

The first CLI attempt failed before DOC detection because the CFB reader required equal black height. The correction retained only the MS-CFB rules: black child-tree root, no consecutive red nodes, specified ordering and unique names. Detection retained a bounded CFB diagnostic and structural index for genuine CFB failure, and no longer reported filename/media mismatch when no format was established.

The next attempt identified two DOC assumptions:

- `FibRgFcLcb97` entry 87 is `dwLowDateTime`/`dwHighDateTime`, not an offset/length pair, and must not undergo Table-stream range validation or passive range projection.
- `PlcPcd` includes one separator CP after the main document when a specialised part exists. Extent and story starts must account for it without projecting it as story text.

Owned synthetic regressions covered all three corrections without copying genuine bytes.

```powershell
dotnet test --project .\tests\unit\CollisionDocNet.Storage.Tests\CollisionDocNet.Storage.Tests.csproj --configuration Release
dotnet test --project .\tests\unit\CollisionDocNet.Conversion.Tests\CollisionDocNet.Conversion.Tests.csproj --configuration Release
dotnet test --project .\tests\unit\CollisionDocNet.Writer.Tests\CollisionDocNet.Writer.Tests.csproj --configuration Release
dotnet run --project .\src\CollisionDocNet.Cli\CollisionDocNet.Cli.csproj --configuration Release --no-build -- extract --input <exact-local-doc> --output <new-ignored-bundle>
```

Focused runs reported Storage 140, Extraction 19 and Writer 44 passing cases. The corrected CLI invocation returned exit `10` / `Partial`, not `Corrupt`: `CompoundFile`/`WordBinary`, 130 ordered segments, 1,745 characters, 32 metadata entries and three passive assets. Thirty-four issues described unimplemented structure/property/anchor semantics and one unsupported nested format. Controls with partial semantics claimed no text.

No Office automation, external office suite/converter, network retrieval, macro/OLE activation or source mutation occurred. A final repository gate exited `0`, and a subsequent CLI invocation reproduced the `Partial` result with specification identity `MS-DOC/2026-02-17`.

### EV-2026-07-24 — opaque twelve-sample end-to-end and caller boundary

#### Initial and corrected local results

The Release CLI processed the twelve authorised opaque PDF/EML/MSG copies twice under the default 10 MiB policy and a 30-second internal deadline. All were below the input ceiling and none was a reparse point.

Across the first 24 corrected invocations:

- all retry pairs had identical canonical results and asset sets;
- all bundles passed JSON, asset hash/length, declared-file and path-containment checks;
- no exception or staging leak occurred; and
- observed wall time was 296–4,514 ms; peak working set was not measured.

Before cohort-specific corrections, EML produced one `Complete` and three `Partial`, PDF produced four `Partial`, and MSG produced four `Corrupt`. The first attempted batch had used an invalid policy identifier; usage validation rejected it, created no bundle or staging leak, and it was not counted as extraction evidence.

MSG diagnosis showed all four files used a red root directory entry permitted by MS-CFB. After correcting that boundary, a downstream Outlook decoding exception and PDF/EML issues remained, so the “all samples fully extract” gate was explicitly failed at that intermediate point.

After CFB root-colour, MAPI `PtypGuid`, nested DOCX, MIME boundary, PDF inline-image/XObject and cumulative nesting corrections, the same cohort was processed twice again:

- PDF: all eight invocations returned `Complete` and were deterministic; aggregate output contained 254 passive assets, 74,361 ordered segments and 97,534 characters. Thirty-two inline-image notices remained informational.
- EML: all eight invocations returned `Complete` and were deterministic; HTML references remained passive and no relationship was retrieved.
- MSG: all eight invocations returned `Complete` and were deterministic, including two embedded DOCX documents; dependency/drawing notices remained informational.
- All twelve samples were `Complete` on both runs, without timeout, exception, missing result or silent fallback engine.

The equivalent invocation was:

```powershell
dotnet run --project .\src\CollisionDocNet.Cli\CollisionDocNet.Cli.csproj --configuration Release --no-build -- extract --input <exact-sample-path> --output <new-ignored-bundle-directory>
```

One exact copied input and one new ignored output directory were used per operation. The final ordinary repository audit deliberately did not reopen the sensitive samples.

This closes only the authorised twelve-file sample gate. It is not format conformance, independent differential verification, hidden-holdout evidence, permission to publish, proof of every capability row or CollisionSpike acceptance.

#### CollisionSpike opt-in Web path

The adjacent repository contained an additive `CollisionDocNetQdosSourceReader` behind `Features:CollisionDocNetExtractor`, disabled by default.

The scoped caller-path evidence reported:

- five custom opt-in Web integration tests;
- 31 existing default multi-format Web tests;
- 29 architecture tests; and
- warning-free Infrastructure and Worker Release builds.

The custom tests covered EML/DOCX translation, ordered fragments, a synthetic real `POST /Intake/Qdos` Core assessment, unsupported mapping, cancellation, DI resolution and content-leak denial.

This is an opt-in caller-path exercise, not production deployment, live verification or caller acceptance. The Worker was not `Called`; it had no authorised Qdos trigger. Sibling project references required an adjacent converter checkout and were not a portable release dependency. Global activation remained unaccepted because legacy PdfPig/MimeKit expectations and broader outcome, asset and nesting gates were unresolved.

### EV-2026-07-24 — text/image output boundary

This record was `Locally verified` for `EXT-API-001`, `EXT-API-003`, `EXT-CLI-002` and `EXT-NEST-001` under the default 10 MB policy. It was not format conformance, differential verification, broad genuine-corpus acceptance or CollisionSpike acceptance.

The public entry point was `DocumentExtractor`; all five format-handler entry types were non-public. The output boundary was ordered text, recognized image assets and control evidence only.

Automated cases established that:

- mixed image/non-image MIME emits only recognized image bytes;
- non-payload binary bytes retain stable descriptors without becoming assets or making a result non-complete solely for being non-payload;
- a claimed image with no recognized signature is omitted and forces `Partial`;
- supported nested content is parsed before non-image parent bytes are removed;
- unsupported nested bytes are not emitted and do not double-charge root input budget;
- CLI bundles create only stable image files and omit `assets/` for non-image-only input;
- schema `kind` is pinned to `image` with a restricted media-type set;
- passive DOCX hyperlinks are informational; and
- external relationships that may hide required text or images remain incomplete.

A stale security assertion that expected a hostile non-image attachment as an asset was corrected to require zero assets, one bounded `nonPayload.binary` descriptor and `NON_IMAGE_ASSET_NOT_EMITTED`.

Caller-selected sample identities:

| Input | SHA-256 | Bytes | Result | Ordered text segments | Image files |
|---|---|---:|---|---:|---:|
| DOCX | `9873bdd8f79bc76534a4108fac70c708fee7d5f07ab28500831727f22213e673` | 217,648 | exit `0` / `Complete` | 193 | 7 |
| DOC | `30ba3639d8b2804010f077e125f287c0ffe9e763aee1224b44f5596a2cd447f6` | 114,688 | exit `10` / `Partial` | 130 | 0 |

The DOCX retained 18 informational dependency, passive-drawing and external-hyperlink notices. The DOC retained 37 explicit structural/semantic issues and three non-image descriptors. Ignored outputs were under `artifacts/evaluation/20260724-scope-pass-matty/` and `artifacts/evaluation/20260724-scope-pass-doc/`.

The DOC remained honestly `Partial`. Outstanding gates were structural image validation, safe pixel accounting, SVG policy, formal conformance, fuzz/property breadth, pinned differential tools, Linux, broader genuine cohorts/holdouts, accepted performance budgets, independent review and real caller acceptance.

### EV-2026-07-24 — local framework-dependent packaging

This evidence covered `EXT-PKG-001` framework-dependent local candidates, dependency/licence inventory, schemas, update/rollback and support documentation. It did not establish format acceptance or distribution authority.

The packaging inputs included central conditional metadata, package documentation, two versioned JSON Schemas, packaging contract tests and `scripts/Build-ReleaseCandidate.ps1`. The script produced ignored NuGet library candidates plus a deterministic Windows framework-dependent CLI ZIP, dependency manifest and SHA-256 package manifest.

Production `PackageReference` count was zero. MSTest.Sdk `4.0.2` and BenchmarkDotNet `0.15.8` were test/tool-only and restored with MIT licence expressions.

```powershell
dotnet restore CollisionDocNet.slnx
dotnet build tests\unit\CollisionDocNet.Packaging.Tests\CollisionDocNet.Packaging.Tests.csproj --configuration Release --no-restore
dotnet test --project tests\unit\CollisionDocNet.Packaging.Tests\CollisionDocNet.Packaging.Tests.csproj --configuration Release --no-build
pwsh -NoProfile -File .\scripts\Build-ReleaseCandidate.ps1 -Version 0.1.0-alpha.3
```

The focused packaging build exited `0` without warnings/errors; the initial contract suite reported four passing cases. The `0.1.0-alpha.3` pipeline exited `0`: locked restore, Release build, Windows tests, nine `.nupkg`, nine `.snupkg`, framework-dependent CLI publish/version smoke and deterministic package manifests.

The ignored candidate contained 47 files: nine binary packages, nine symbol packages, a 26-entry CLI directory/ZIP, dependency manifest and package manifest. All 46 manifest entries used canonical SHA-256. ZIP entries were sorted with fixed UTC timestamps. The corrected dependency manifest recorded 38 test/tool packages and zero production NuGet packages.

`0.1.0-alpha.1` exposed project references misclassified as production packages. The generator was corrected to exclude lock entries of type `Project`, with fail-closed zero-production-dependency, CLI-contract and package-count checks. An `alpha.2` attempt exceeded the harness timeout and was not evidence.

A concurrent repository run later failed one newly present opaque-MSG root-colour diagnostic after the immutable candidate had been built. That moving-worktree failure prevented a whole-repository acceptance claim but did not invalidate focused packaging tests or candidate hashes. The shared CFB defect was subsequently corrected, and focused packaging tests later reported five passing cases.

Assertion review found no assertion-free, trivial-only or self-referential packaging test. Contract tests covered schema identities/enums, safe asset paths and central metadata. Full PowerShell mutation/property testing remained open.

Distribution remained blocked by the absence of an authorised product licence and accepted release scope. Linux, self-contained, single-file, Native AOT, signing, standards-compliant SBOM, independent holdout and authorised acceptance were not claimed.