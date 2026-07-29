# Packaging, release, update, rollback and support

This document is the sole owner of package composition, local release-candidate construction, inspection, release readiness, updates, rollback and support policy for CollisionDocNet.

A locally built candidate is not thereby published, deployed, caller-proved or accepted. Intended variants, implemented packaging automation, successful local execution, caller acceptance and deployment are separate evidence states. Capability and format support remain governed by the [capability evidence](../../../docs/capabilities.md) and [requirements](../../../docs/requirements.md); packaging cannot advance those claims.

## Supported product surfaces

The product surfaces are:

- `CollisionDocNet.Extraction`, the public managed extraction library;
- its explicit managed format and storage package dependencies; and
- `CollisionDocNet.Cli`, a machine-oriented, one-shot application bundle over the same library.

The CLI is not packaged as a NuGet tool. It requires no desktop, web or Office workload.

A future accepted Pegasus integration is intended to call the public library directly rather than spawn the CLI. No Pegasus adapter, caller, production deployment, or acceptance currently exists. Caller applications own integration and deployment; Pegasus would own adapter deployment, traffic selection, and business rollback, while this workspace owns no caller database migration or service state.

Support is feature-row based. An input or behaviour is supported only where the compatibility evidence records it as supported. `Complete` applies only to the declared supported subset observed during that extraction. Unsupported or partially implemented features must remain visible as issues or non-complete outcomes; they must not silently invoke another engine.

Desktop UI, Office or Outlook automation, external office-suite runtimes, web hosting, mailbox access and format conversion are outside support.

## Package variants

The current packaging baseline is an unsigned, framework-dependent `net10.0` package for Windows. The caller supplies a compatible Microsoft .NET 10 runtime.

The first package shape consists of the console executable and its managed dependencies. Windows x64 and Linux x64 are the first planned host classes, but the current packaging unit does not verify Linux execution. Linux therefore remains unsupported until its own evidence exists.

The following are deferred variants of the same CLI, not alternative extraction engines:

- RID-specific self-contained packages;
- RID-specific single-file packages, after startup, signing and temporary-extraction analysis; and
- Native AOT packages, after trim and AOT analyzers and every format and encoding path pass for each target RID.

Each variant and RID requires a fresh restore and publish plus its own package inspection, startup, extraction, security and performance evidence. Self-contained, single-file and Native AOT outputs are neither built nor claimed by the baseline procedure. Additional RIDs require separate release evidence.

## Local release-candidate build

Run from PowerShell 7, replacing the placeholder with the candidate version:

```powershell
.\scripts\Build-ReleaseCandidate.ps1 -Version <candidate-version>
```

The packaging script:

1. refuses an existing destination;
2. refuses output outside `artifacts/`;
3. performs a locked restore;
4. builds the Release configuration;
5. runs the Microsoft.Testing.Platform solution suite;
6. packs each production library;
7. publishes the framework-dependent CLI;
8. runs the Windows CLI `version` smoke test;
9. creates a sorted, fixed-timestamp CLI ZIP;
10. creates a canonical dependency inventory from NuGet lock files; and
11. creates a SHA-256 package manifest.

A successful invocation proves only that the local candidate was produced and that the checks executed by that invocation passed. It does not prove caller acceptance, supported-format completeness, distribution authorisation or deployment.

The ignored output layout is:

```text
artifacts/packages/<candidate-version>/
  cli-framework-dependent/
  collisiondocnet-cli-<candidate-version>-framework-dependent.zip
  dependency-manifest.v1.json
  nuget/*.nupkg
  nuget/*.snupkg
  package-manifest.v1.json
```

Every candidate must use a new immutable version directory. Never overwrite a prior candidate.

## Candidate inspection

Before a candidate can be considered for caller validation:

1. Confirm that restore was locked and that the Release build, solution tests and Windows `version` smoke completed successfully.
2. Confirm that each production library has the expected `.nupkg` and `.snupkg`.
3. Inspect `CollisionDocNet.Extraction` as the public library package and verify that format and storage packages remain explicit managed dependencies.
4. Inspect the CLI directory and ZIP as a framework-dependent application bundle, not a NuGet tool or self-contained publication.
5. Confirm that the ZIP entries are sorted and use fixed timestamps.
6. Recompute and verify the package-manifest SHA-256 values.
7. Compare the dependency inventory with the NuGet lock files.
8. Confirm the declared package, extractor, schema, configuration and target-framework identities.
9. Run the applicable format, security, performance, fuzz, differential, genuine-data holdout and platform checks required by the candidate’s claimed scope.
10. Record any unverified row as unsupported, partial or an open gate rather than inferring support from a successful pack.

The package manifest hashes every candidate file except the manifest itself. It proves local byte identity only. It does not prove signing, provenance attestation, licence clearance, distribution authorisation, publication, deployment or acceptance.

## Version and compatibility identity

Release identity consists of:

- package version;
- extractor identity;
- result-schema identity;
- bundle-schema identity;
- configuration identity;
- target framework;
- dependency-manifest hash; and
- package-manifest hash.

Operators must retain these values with extracted evidence.

Package-version defaults are centrally owned by `Directory.Build.props` and may be overridden by the packaging command. The following remain separate compatibility axes:

- extractor semantic identity, currently `collisiondocnet/0.1`;
- result schema, currently `collisiondocnet-result/1`;
- bundle schema, currently `collisiondocnet-bundle/1`; and
- package version.

The versioned JSON Schemas under `docs/schemas/` define the public result envelope and CLI evidence bundle. A schema-breaking change requires a new schema identity and explicit migration and rollback review. Changing only the package version does not rewrite historical evidence.

Any change to schema, outcome semantics, ordering, stable identity or default resource limits is a compatibility change requiring explicit review.

## CLI package contract

The packaged executable exposes:

```text
collisiondocnet detect  --input <path|-> [--name <filename>] [--media-type <hint>]
collisiondocnet extract --input <path|-> --output <new-directory> [limits]
collisiondocnet version
collisiondocnet help
```

`CollisionDocNet.Cli` is a caller-controlled adapter for scripted extraction, isolated corpus evaluation, diagnostics and operator verification. It never detects or parses a format itself; the extraction library owns those behaviours. The CLI owns only:

- input and output handling;
- argument validation;
- cancellation;
- result serialization;
- safe image materialisation; and
- process exit status.

Each invocation accepts exactly one source. It never expands globs, enumerates directories, watches paths, opens mailboxes or follows relationships discovered inside a document.

`-` selects standard input. When standard input is used, `--name` is required and remains an untrusted hint. Paths are resolved before use. URI inputs are rejected. UNC and network inputs are rejected by default; enabling them requires a later explicit caller-policy decision.

The requested output directory must not already exist. The CLI never recursively deletes or silently overwrites a destination.

### Extraction bundle

A full extraction has this logical layout:

```text
<output>/
  result.json
  assets/
    <stable-image-id>.<safe-extension>
```

`result.json` is UTF-8 without a byte-order mark and conforms to a versioned schema. It records:

- ordered text;
- ordered image descriptors;
- relative stable-ID image paths;
- SHA-256 values; and
- control evidence explaining outcome and provenance.

Original filenames and content-disposition names are metadata only and never become filesystem paths. Images are written once and verified against their recorded hashes. Result ordering is independent of filesystem enumeration. Non-image attachments and embedded-object bytes are never materialised.

The CLI creates only an extractor-owned staging directory beneath the caller-selected output parent. Where supported, it atomically publishes the completed bundle. On cancellation or technical failure, it removes only the resolved staging path that it created. A structured failure result may be retained only when it can be published without misrepresenting an incomplete bundle.

### Standard streams and serialization

- `detect` and `version` write exactly one UTF-8 JSON document to standard output for a non-usage invocation.
- `extract` writes a small machine-readable completion envelope identifying the outcome and result path.
- Extracted text and image bytes never go to standard output or logs.
- Progress and diagnostics go to standard error.
- Diagnostics contain stable issue codes, correlation identity and bounded measures, but never extracted content, sensitive source names or attachment names.
- `--quiet` suppresses non-error diagnostics but not the completion envelope.

JSON property order, enum spelling, number representation, timestamps and line endings are versioned and deterministic. Source-generated `System.Text.Json` metadata is the planned implementation route; reflection-based serialization is not assumed.

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
| `64` | Invalid CLI usage or configuration; no extraction result exists |
| `70` | `TechnicalFailure` |

Expected document outcomes are not converted into unstructured exceptions. A technical failure is contained at the process boundary and returns a safe structured issue or result where possible.

### Limits and cancellation

CLI limit switches select a named, versioned resource class. They may lower its limits but must not silently raise them.

The eventual limit surface includes:

- input and decoded bytes;
- object, part and stream counts;
- text characters;
- image count, bytes and pixels;
- nesting depth;
- CPU and elapsed deadlines; and
- working-memory ceilings.

`Ctrl+C` requests cancellation through the same `CancellationToken` used by a library caller. Traversal and decoder loops must observe it. A second interrupt may terminate immediately; whether a valid result bundle can still be guaranteed in that case remains an evidence question and must be recorded by tests.

## Required CLI and package evidence

Release evidence for the claimed platform and package shape includes:

- argument and usage snapshot tests;
- path-containment and existing-output tests;
- symlink and reparse-point tests;
- interruption tests;
- standard-input and file-input equivalence;
- stable JSON and image-bundle hashes across retries;
- complete exit-code and outcome mapping;
- stdout and stderr content-leak tests;
- framework-dependent Windows and Linux smoke tests where those platforms are claimed; and
- separate opt-in publish tests for every claimed self-contained, single-file or Native AOT RID.

The baseline packaging procedure includes a Windows framework-dependent `version` smoke. It does not by itself establish the remaining CLI evidence or Linux support.

## Update procedure

1. Build the candidate into a new immutable version directory. Do not overwrite a retained candidate.
2. Verify locked restore, build, tests, package contents, hashes and all format, security, performance and holdout gates applicable to the proposed scope.
3. Compare dependency and schema manifests with the current version.
4. Treat schema, outcome, ordering, stable-identity and default-limit changes as compatibility changes requiring explicit review.
5. Deploy the candidate beside the current version only through the caller’s authorised deployment process.
6. Direct only an authorised validation cohort to the candidate.
7. Promote only after caller-owned acceptance.
8. Do not silently fall back between extractor versions or engines within one operation.

Building, inspecting or staging a candidate is not promotion. Nothing in this repository’s local packaging output demonstrates publication or deployment into a caller environment.

## Rollback procedure

Rollback selects a previously retained, hash-verified framework-dependent package as a whole. Never mix assemblies from different package versions.

Rollback is required for:

- a corrupted package;
- a signature or hash mismatch;
- an unexpected increase in technical failures;
- nondeterministic results;
- a resource-bound regression;
- silent evidence loss; or
- caller acceptance failure.

Inputs and results produced by the withdrawn version must be preserved. Their recorded extractor, schema and configuration identities remain authoritative provenance and must not be rewritten.

Reprocessing is not part of rollback. It is a separate authorised operation and produces a separately linked derivative.

This repository owns no database migration or service state. Caller adapters own deployment, traffic selection and business rollback.

## Support and security reporting

There is currently no release-acceptance commitment or compatibility service-level commitment.

A support or security report should include:

- package-manifest hash;
- package version;
- extractor identity;
- result and bundle schema identities;
- configuration identity;
- detected format and outcome;
- bounded resource measures; and
- a non-sensitive correlation identifier.

Reports and logs must not contain extracted content or sensitive filenames. Retain the original input privately. Do not attach it to an issue or upload it without explicit data-handling authorisation.

Operational handling remains subject to the [operations guidance](../../../docs/operations.md) and [operator notes](../../../docs/operator-notes.md). Architecture and implementation changes must follow the [architecture](architecture.md), [engineering guidance](../../../docs/engineering.md), and applicable [decision records](../../../docs/adr/README.md).

## Open release gates and non-claims

No current candidate is claimed to be signed, notarised, uploaded, published, deployed or authorised for distribution.

Before any distribution or acceptance claim, the applicable unresolved gates must be closed with recorded evidence, including:

- package and dependency licence and redistribution review;
- signing, notarisation or provenance requirements selected by the release authority;
- declared format rows, which remain partial where the capability evidence says so;
- security review;
- fuzz and differential testing;
- genuine-data holdout evidence;
- Linux execution evidence for any Linux claim;
- RID-specific restore, publish, inspection and test evidence;
- trimming and AOT review for Native AOT;
- startup, temporary-extraction, signing, security and performance review for single-file packages; and
- independent caller-owned acceptance.

A successful build, test run or pack cannot by itself close these gates. Unresolved product and policy choices remain owned by [open decisions](../../../docs/open-decisions.md); accepted changes must be recorded through the [change process](../../../docs/changes/README.md).