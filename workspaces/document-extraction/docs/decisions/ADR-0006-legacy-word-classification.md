# ADR-0006: Legacy Word and format-classification boundary

Status: **Proposed; required before pre-97 implementation**

Date: 2026-07-24

## Context

The product supports direct extraction from the Word 97-family binary format defined by pinned `[MS-DOC]` 12.5. The current source contains four older marker constants but no approved source maps them to Word 6, Word 95, Word 2/earlier or Macintosh structures. Guessing a product version or applying modern FIB offsets would create silent evidence corruption.

DOC also shares CFB with MSG, encrypted OOXML and unrelated formats. Filename extensions cannot safely resolve those profiles or polyglots.

## Proposed decision

- Implement only the five `[MS-DOC]` 12.5 effective FIB layouts in the current programme.
- Under a strong CFB/root-Word profile, classify `0xA59B`, `0xA59C`, `0xA5DB` and `0xA5DC` generically as `UnverifiedLegacyWordIdentifier` and return `UnsupportedFeature` without parsing payload.
- Do not expose product/version names for those values until an exact licensed and hash-pinned authoritative source is approved.
- Return `UnsupportedFormat` for unknown flat, pre-CFB or Macintosh/resource-fork candidates rather than guessing.
- Reopen pre-97 parsing only after its grammar, encodings, packaging, fixture provenance, security model and independent oracle are separately accepted.
- Treat a valid unrelated CFB as `UnsupportedFormat`, an identified damaged profile as `Corrupt`, and multiple strong profiles as `UnsupportedFeature`/`AMBIGUOUS_FORMAT` with no parser invocation.
- Treat byte/hint mismatch as informational evidence only.

## Consequences

Word 97-family delivery is not delayed by an undocumented legacy parser. Known old files get a precise fail-closed result rather than generic corruption, while unverified marker-to-product claims are removed. The decision is reversible when authoritative evidence is available.

## Acceptance boundary

The classification matrix and synthetic modern fixtures can become `Specified` and `Locally verified`. Pre-97 semantic support cannot exceed `Mapped` until this ADR is accepted together with the required provenance. Distribution and external-fixture rights remain separate gates.
