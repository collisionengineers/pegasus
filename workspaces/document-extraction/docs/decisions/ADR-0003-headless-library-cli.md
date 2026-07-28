# ADR-0003: headless managed library and command-line executable

Status: **Accepted**

Date: 2026-07-23

## Context

The extractor runs in unattended server workflows and must also be directly testable and operable without a desktop session. A graphical application, Office add-in, shell extension, web host or long-running service would add deployment and attack surface without improving format extraction.

CollisionSpike needs an in-process managed adapter. Developers and operators need a deterministic executable for isolated tests, corpus evaluation and scripted use.

## Decision

Ship only two product surfaces:

1. `CollisionDocNet.Extraction`, the public managed library entry point; and
2. `CollisionDocNet.Cli`, a thin one-input-per-process console adapter over that entry point.

The CLI owns argument validation, caller-selected file/standard-input access, output-bundle materialisation, cancellation wiring, machine-readable process reporting and exit-code mapping. It owns no format detection, parser, extraction semantics or business completeness policy.

There is no desktop UI, WindowsDesktop dependency, ASP.NET/web API host, Windows service, daemon, folder watcher, mailbox client or recursive filesystem scanner in this repository.

## Consequences

- The normal target is `net10.0`; no GUI or web workload is required.
- Framework-dependent output is the baseline portable build.
- RID-specific self-contained and [single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) packages are optional release artefacts after compatibility, startup, temporary-extraction and signing analysis.
- [Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) is a later optional packaging gate, not an implementation assumption. It requires trimming/AOT analyzers, [source-generated serialisation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation-modes) and platform-specific publish evidence.
- CollisionSpike references the library rather than spawning the CLI. The CLI remains valuable as an independent engine proof and differential/corpus runner.
- Batch scheduling and concurrency belong to the caller. One CLI process handles one source and exits.
- Output bundles follow [ADR-0004](ADR-0004-text-and-image-output.md): JSON carries text and control evidence, while materialised asset files are images only.

## Acceptance gates

- CLI invocation and output schemas are versioned and documented.
- Standard output and standard error contracts are deterministic and content-safe.
- Every extraction outcome has an exit-code mapping and a structured result where safely possible.
- Interrupt, deadline and resource-limit tests prove prompt termination and extractor-owned cleanup.
- Framework-dependent release checks pass on supported Windows and Linux host classes before any self-contained, single-file or AOT claim.
