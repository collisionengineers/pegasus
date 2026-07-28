# P10 — shared foundations, storage, detection and result model

## Scope

Build the hostile-input boundaries used by all five formats. CFB v3/v4 is shared by `.doc`, `.msg` and encrypted OOXML wrappers; ZIP/ZIP64 and OPC are owned for `.docx`; bounded XML, detection and the result model have one owner each.

## Owned units

- `EXT-FND-001` bounded input, budgets, cancellation/deadlines, hashing, issues and stable identities.
- `EXT-FND-002` checked binary/text primitives, encodings, dates and offsets.
- `EXT-FND-003` deterministic normalisation, ordering, registries and source locations.
- `EXT-DET-001` byte-level five-format detection and ambiguity evidence.
- `EXT-STO-001` complete strict read-only CFB v3/v4 traversal.
- `EXT-STO-002` strict bounded ZIP/ZIP64 and OPC traversal.
- `EXT-STO-003` passive OLE property sets and embedded-object descriptors.
- `EXT-STO-004` bounded namespace-aware XML with entity denial and source spans.
- `EXT-MOD-001` shared extraction request/result/evidence/outcome model.

## Required outputs

- Per-operation and cumulative limits with checked arithmetic and prompt cancellation.
- Deterministic issue ordering, source hashing and stable image identity rules.
- Container readers that reject cycles, cross-links, invalid ranges, path traversal and expansion abuse.
- Structural identification of PDF, DOC, DOCX, MSG and EML without trusting extensions.
- An immutable model representing ordered text, image assets, control evidence, text/image-only nesting and explicit outcomes.

## Current evidence

The fixed CFB v3 header portion of `EXT-STO-001` is implemented and locally verified by 30 focused tests. FAT/DIFAT, mini-stream, directory and stream traversal are not implemented, so the unit and dependent formats remain incomplete.

`EXT-FND-001` through `EXT-FND-003` and `EXT-MOD-001` now have managed implementations and focused local verification. The implemented surface includes checked random and bounded sequential reads, cumulative counters, cancellation/deadline state, SHA-256 and length-prefixed identities, active Unicode/Windows-1252/FILETIME primitives, versioned NFC/LF normalisation, immutable request/evidence/result types, deterministic issue/evidence ordering and source-generated stable JSON. This does not complete `P10`: detection, full containers, XML, additional format-specific encodings/dates, fuzz/property campaigns and independent API review remain open.

## Exit evidence

Each foundation has unit, conformance, corrupt-input, cancellation and resource-boundary tests. Storage fuzzing and deterministic retry evidence pass. The public format handlers cannot bypass these owners.
