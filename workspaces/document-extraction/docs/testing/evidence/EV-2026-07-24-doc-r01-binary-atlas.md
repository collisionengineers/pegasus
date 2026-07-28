# EV-2026-07-24 — DOC-R01 binary structure atlas

Claim: the Word 97-family CFB/FIB storage boundary is mapped and specified for the five accepted `nFib` layouts. This evidence is **Mapped**, **Specified** and **Locally verified**. It is not production implementation, conformance, differential verification, caller use or format acceptance.

```yaml
claim_id: EV-2026-07-24-DOC-R01
date_utc: 2026-07-24
managed_commit_or_tree_hash: unavailable-not-a-git-working-tree
port_units: [EXT-DOC-002, EXT-DOC-004, EXT-DOC-005, EXT-DOC-006, EXT-DOC-007, EXT-DOC-008, EXT-DOC-009, EXT-DOC-010, EXT-DOC-011]
formats: [DOC]
scope: CFB ownership, complete versioned FIB descriptor atlas, secondary FIB and AutoText, quick-save, encryption classification branches, explicit payload and failure ownership
specification:
  name: MS-DOC
  revision_or_date: 12.5 / 2026-02-17
  sha256: 2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a
descriptor_counts:
  total: 183
  layouts: 5
  table_stream: 143
  word_document_stream: 4
  no_stream: 35
  fib_filetime: 1
source_field_sequence_sha256: a7494e994901be57ee06e602eed824d99ea50699b82ab8a89f790ad34938ae8f
reviewed_canonical_atlas_sha256: c0afee25a88147efe5d4acc599a4d9876893a152f797c50958bd5669d0baf75b
commands:
  - command: pwsh -NoProfile -File .\scripts\Generate-DocFibAtlas.ps1
    exit_code: 0
    input_class: pinned ignored MS-DOC 12.5 DOCX publication
    boundary: publication hash, five exact FIB paragraph bands and 183 generated descriptor records
    limitations: generation is research tooling and does not alter the production parser
  - command: pwsh -NoProfile -File .\scripts\Test-DocFibAtlas.ps1 -SpecificationPath .\artifacts\research\doc\2026-07-24\specifications\MS-DOC-12.5-260217.docx
    exit_code: 0
    input_class: generated atlas plus independently re-read pinned publication
    boundary: exact field order, versions, offsets, ownership/policy invariants and reviewed canonical sequence
    limitations: structural specification verification, not binary DOC parser execution
results:
  passed: 183 descriptors across five layouts; zero independent source/atlas stream-ownership mismatches
  failed: 0
  skipped: production parser implementation, conformance fixtures, differential tools and genuine-data evaluation
known_implementation_defects:
  - WordFibParser interprets reserved MS-DOC 12.5 FibBase bytes at offsets 20, 24 and 28 as characterSet, fcMin and fcMac
  - WordFibParser reads pnNext as signed and does not implement bounded secondary-FIB traversal
  - most FibRgW97 and FibRgLw97 values are discarded and most FibRgFcLcb descriptors remain anonymous or unimplemented
  - encryption subtype headers, AutoText and full quick-save semantics remain unimplemented
security_boundary: no content was executed, decrypted or retrieved; sample-doc-files and the private CollisionSpike corpus were not accessed
reviewer: independent read-only agent closure review passed after source-backed stream-ownership and semantic-policy reconciliation
```

The repository root is not a Git worktree, so no commit or Git tree hash is available. `DOC-I01` owns production implementation of this atlas and its independently generated fixture suite.
