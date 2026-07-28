# Five-format extraction programme

This folder is the persistent scope and dependency catalogue for PDF, `.doc`, `.docx`, `.msg` and `.eml` extraction. Stable `EXT-*` units link specifications, managed ownership, tests and evidence. The harness `update_plan` tracks the current executable work; these documents define durable programme boundaries rather than claiming that every item is active at once.

The [dependency-ordered implementation sequence](implementation-sequence.md) describes the start-to-finish delivery path. Phase numbers below group ownership; they do not require completing all of PDF before the business-priority `.doc` vertical slice.

The [requested .NET plugin skill inventory and wave routing](dotnet-plugin-skill-map.md) records which build, diagnostic and testing skills apply to each implementation stage and which are conditional on an observed failure or performance problem.

## Evidence labels

| Label | Meaning |
|---|---|
| `Unmapped` | Responsibility, specification or dependency evidence is missing. |
| `Mapped` | Responsibility, primary references, dependencies and security surface are recorded. |
| `Specified` | Managed behaviour, outcomes and acceptance tests are reviewable. |
| `Implemented` | Managed source exists in the working tree. |
| `Locally verified` | Stated checks pass on stated inputs and host. |
| `Conformant` | Declared specification tests pass. |
| `Differentially verified` | Semantic comparison against an exact-version independent oracle passes within stated tolerances. |
| `Called` | The intended real caller reaches the implementation. |
| `Accepted` | An authorised reviewer accepts the stated evidence. |

No format is described as completely supported while a declared compatibility entry is unsupported, partial or unverified.

## Programme phases

1. [P00 — governance, specifications, licensing and provenance](tasks/P00-governance-and-source-map.md)
2. [P10 — shared foundations, storage, detection and result model](tasks/P10-foundations-storage-and-detection.md)
3. [P20 — PDF extraction](tasks/P20-pdf-extraction.md)
4. [P30 — legacy Word `.doc` extraction](tasks/P30-doc-extraction.md)
5. [P40 — WordprocessingML `.docx` extraction](tasks/P40-docx-extraction.md)
6. [P50 — Outlook `.msg` extraction](tasks/P50-msg-extraction.md)
7. [P60 — RFC 5322/MIME `.eml` extraction](tasks/P60-eml-extraction.md)
8. [P70 — public orchestration, nesting and cross-format security](tasks/P70-extraction-orchestration.md)
9. [P80 — conformance, release and CollisionSpike integration](tasks/P80-testing-release-and-integration.md)

## Execution rule

A unit can enter implementation when its dependencies are sufficiently specified and its specification, licence, security and resource boundaries are understood. Its tests and compatibility entries change in the same implementation slice. Parallel format research is permitted, but no handler bypasses the shared detector, outcome model or cumulative resource controls.

Current locally verified implementation boundary:

```text
shared bounded Core / immutable Model / byte-level detection
  -> CFB v3/v4, ZIP/ZIP64, OPC, OLEPS and secure XML storage primitives
  -> managed PDF / binary DOC / DOCX / MSG / EML handlers
  -> one five-format extraction API with cumulative nesting budgets
  -> deterministic JSON and atomic evidence bundles
  -> headless Windows framework-dependent CLI
  -> hostile-input, deterministic fuzz, performance and packaging checks
  -> opt-in CollisionSpike Web adapter; default legacy path remains enabled
```

The definitive repository check currently reports 523 tests: 522 passed, one intentionally skipped opt-in opaque EML cohort test and zero failures. All twelve authorised local PDF/EML/MSG samples complete deterministically across two runs. These results prove the declared local slices only: the compatibility matrix still contains partial and mapped capabilities and no row is yet labelled `Conformant`.

The [port-unit catalogue](port-unit-catalogue.md) is authoritative for IDs and dependencies; the [compatibility matrix](../compatibility/feature-matrix.md) is authoritative for live capability status. Behaviour and evidence, not translated source counts or raw test counts, determine support.
