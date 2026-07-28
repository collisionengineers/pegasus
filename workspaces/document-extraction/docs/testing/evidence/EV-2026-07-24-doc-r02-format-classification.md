# EV-2026-07-24 — DOC-R02 format classification

Claim: byte-level recognition, Word-family classification and cross-format acquisition outcomes are mapped and specified before production changes. This evidence is **Mapped**, **Specified** and **Locally verified**. It is not implementation, conformance, pre-97 parser support, caller use or release acceptance.

```yaml
claim_id: EV-2026-07-24-DOC-R02
date_utc: 2026-07-24
managed_commit_or_tree_hash: unavailable-not-a-git-working-tree
port_units: [EXT-DOC-001, EXT-DOC-012]
formats: [DOC, PDF, DOCX, MSG, EML, encrypted-OOXML-wrapper]
scope: byte-owned routing, executable CFB/Word/legacy/MSG/encrypted-OOXML predicates, acquisition subtypes, damage thresholds, hint policy, unrelated containers, ambiguity and interruption/resource outcomes
specifications:
  - MS-DOC 12.5 / 2026-02-17 / sha256 2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a
  - MS-CFB 12.0 / 2024-04-23 / sha256 2d650184072a148ba98ad0b68072fd5ad7780e46f3528d7f263f3127b2dadab5
  - MS-OFFCRYPTO 14.0 / 2026-02-17 / sha256 9b7a67eb5d0408566a61f218792fcd21536dbc970d83695ad94365e535533f33
  - MS-OXMSG 18.0 / 2025-05-20 / exact revision recorded but no retained publication hash
classification_contract:
  profile_predicates: 5
  cases: 26
  supported_effective_nfib: [0x00C1, 0x00D9, 0x0101, 0x010C, 0x0112]
  canonical_sha256: c84fa08b0ebc67aa6b023e925093a27de2e1e95ddfd2d04a79a476306f7e8871
  fixture_groups: [DOC-T01, DOC-T02, DOC-T03, DOC-T04]
commands:
  - command: pwsh -NoProfile -File .\scripts\Test-DocFormatClassification.ps1
    exit_code: 0
    input_class: committed machine-readable classification matrix
    boundary: exact four-source authority identity, five executable predicates, 26 complete case tuples, joint ownership, hint policy, five supported versions and fixture-group identity
    limitations: verifies the specification contract, not current production routing
test_gap_analysis:
  method: static pseudo-mutation review of FileFormatDetector, DocumentExtractor and MSTest detector/public suites using the dotnet-test test-gap-analysis guidance
  sampled_high_risk_mutations: 15
  killed: 5
  survived: 5
  no_coverage: 5
  required_new_kills:
    - exact effective-nFib membership rather than the current inclusive range
    - selected 0Table and 1Table routing
    - Standard, Extensible and Agile encrypted-wrapper grammar rather than names
    - generic application/xml rejection and root OPC relationship ownership
    - template, AutoText and repair-state variants
    - CFB profile pair/triple collisions, stable ordering and hints not resolving ambiguity
    - public DOC, MSG, encrypted-OOXML, ambiguity and unrelated-container outcomes
results:
  passed: offline contract verifier and independent closure review
  failed: 0
  skipped: production implementation, manifested binary fixtures, conformance, differential and genuine-data evaluation
known_implementation_gaps:
  - detector accepts base nFib 0x0065 through 0x0112 instead of exact effective layouts
  - public legacy-marker route collapses to generic corruption
  - valid unrelated CFB/ZIP containers can be called corrupt
  - encrypted OOXML is name-matched, loses public format identity and causes false hint mismatch
  - DOCX detection accepts generic application/xml and misses required variants/relationship ownership
  - ambiguity and public DOC/MSG/encrypted routes lack direct tests
open_gates:
  - ADR-0006 is Proposed and pre-97 parsing remains unsupported
  - MS-OXMSG fixture publication requires a retained hash-pinned source and owning provenance approval
security_boundary: no parser is selected for ambiguity; hints never route; active/extensible provider URLs are never retrieved; sample-doc-files and private corpus were not accessed
reviewer: independent read-only agent closure review passed after executable-threshold, authority and canonical-verifier corrections
```

The repository root is not a Git worktree, so no commit or Git tree hash is available.
