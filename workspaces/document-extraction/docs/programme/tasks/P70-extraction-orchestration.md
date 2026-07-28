# P70 — public orchestration, nesting and cross-format security

## Scope

Expose the five handlers through one versioned managed-library boundary and one thin headless CLI. Ensure the text-and-image-only payload, serialization, process outcomes, nesting, active content, external references and resource accounting behave consistently across formats. No desktop, browser, web API, hosted service, daemon, watcher or mailbox surface is owned here.

## Owned units

- `EXT-API-001` public request/result API, detection dispatch and failure boundary.
- `EXT-API-002` versioned deterministic JSON and evidence-bundle manifest.
- `EXT-CLI-001` one-input headless command, file/stdin, exit codes and Ctrl+C.
- `EXT-CLI-002` atomic caller-owned output bundles and stable image names.
- `EXT-CLI-003` framework-dependent baseline and optional publish variants.
- `EXT-NEST-001` recursive supported-attachment text/image extraction and cumulative budgets.
- `EXT-SEC-001` active-content, external-reference, path/process and logging denial.

## Required outputs

- One stream/bytes API using untrusted hints and the authoritative byte detector.
- Deterministic configuration/version identity, issue ordering and technical-failure containment.
- Parent/child source and image identities for nested PDF, DOC, DOCX, MSG and EML.
- Per-item and cumulative depth, bytes, decoded output, object count, memory, CPU and time enforcement.
- Cross-format proof that input content cannot execute, retrieve, select local paths or leak extracted content to logs.
- Library/CLI result equivalence, stdout/stderr discipline and deterministic `result.json` plus image-only `assets/` bundles.
- Framework-dependent Windows/Linux baseline; any self-contained, single-file or Native AOT target receives separate RID-specific evidence.

## Exit evidence

Routing, cancellation, timeout, deterministic retry, nested-failure propagation and cross-format security suites pass. Each handler conforms to the shared result/outcome contract without hidden fallback behaviour.
