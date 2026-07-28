# Headless CLI contract

## Purpose and ownership

`CollisionDocNet.Cli` is a machine-oriented, one-shot adapter over `CollisionDocNet.Extraction`. It exists for scripted extraction, isolated corpus evaluation, diagnostics and operator verification. CollisionSpike uses the library directly; it does not spawn this process in production.

The CLI never detects or parses a format itself. Its only responsibilities are caller-controlled I/O, argument validation, cancellation, result serialisation, safe image materialisation and process exit status.

## Commands

```text
collisiondocnet detect  --input <path|-> [--name <filename>] [--media-type <hint>]
collisiondocnet extract --input <path|-> --output <new-directory> [limits]
collisiondocnet version
collisiondocnet help
```

`-` means standard input. When standard input is used, `--name` is required as an untrusted hint. The CLI accepts exactly one source per invocation and never expands globs, enumerates directories, watches paths, opens mailboxes or follows relationships found inside a document.

Paths are resolved before use. URI inputs are rejected. UNC/network input is rejected by default and can be considered only through a later explicit caller-policy decision. The output directory must not already exist; the CLI never recursively deletes or silently overwrites a destination.

## Extraction bundle

A full extraction writes a deterministic logical bundle:

```text
<output>/
  result.json
  assets/
    <stable-image-id>.<safe-extension>
```

`result.json` is UTF-8 without a byte-order mark and conforms to a versioned schema. It contains ordered text, image descriptors, relative stable-ID image paths, SHA-256 values and the control evidence required to explain outcome and provenance. Original filenames and content-disposition names are metadata only and never become filesystem paths. Images are written once, verified against their recorded hash and ordered in the result independently of filesystem enumeration. Non-image attachments and embedded-object bytes are never materialised.

The CLI creates only an extractor-owned staging directory beneath the caller-selected output parent. On completion it atomically publishes the bundle where supported. On cancellation or technical failure it removes only the resolved staging path it created. A structured failure result is retained only when it can be published without misrepresenting an incomplete bundle.

## Standard streams

- `detect` and `version` write exactly one UTF-8 JSON document to standard output on a non-usage invocation.
- `extract` writes a small machine-readable completion envelope to standard output identifying the outcome and result path; extracted text and image bytes never go to standard output or logs.
- Progress and diagnostics go to standard error. They contain stable issue codes, correlation identity and bounded measures, never extracted content, sensitive source names or attachment names.
- `--quiet` suppresses non-error diagnostics but not the completion envelope.

JSON property order, enum spelling, number representation, timestamps and line endings are versioned and deterministic. [Source-generated `System.Text.Json` metadata](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation-modes) is the planned implementation route; reflection-based serialisation is not assumed.

## Exit codes

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
| `70` | `TechnicalFailure` |
| `64` | Invalid CLI usage or configuration; no extraction result exists |

Expected document outcomes are not converted into unstructured exceptions. A technical failure is contained at the process boundary and returns a safe issue/result where possible.

## Limits and cancellation

CLI switches select a named versioned resource class and may lower, but not silently raise, its limits. The eventual surface includes input bytes, decoded bytes, object/part/stream counts, text characters, image count/bytes/pixels, nesting depth, CPU/elapsed deadline and working-memory ceilings.

`Ctrl+C` requests cancellation through the same `CancellationToken` as the library caller. Traversal and decoder loops observe it. A second interrupt may terminate immediately; tests must record whether a valid result bundle can still be guaranteed in that case.

## Packaging

The first supported package is a framework-dependent `net10.0` console executable plus its managed dependencies. It requires no desktop, web or Office workload.

Later release candidates may add:

- RID-specific self-contained packages;
- RID-specific single-file packages after startup, signing and temporary-extraction analysis; and
- Native AOT packages after trim/AOT analyzers and every format/encoding path pass on each target RID.

These are packaging variants of the same CLI, not alternative engines. Windows x64 and Linux x64 are the first planned host classes; additional RIDs require their own release evidence.

## CLI evidence

- argument and usage snapshot tests;
- path-containment, existing-output, symlink/reparse-point and interruption tests;
- standard-input and file-input equivalence;
- stable JSON and image bundle hashes across retries;
- complete exit-code/outcome mapping;
- stdout/stderr content-leak tests;
- framework-dependent Windows/Linux smoke tests; and
- separate opt-in publish tests for each self-contained, single-file or AOT RID.
