# EV-2026-07-24 — DOC-R03 text, pieces and stories

Claim: the direct binary DOC text, encoding, piece and story algorithms are mapped, specified and exercised by an independent test-only oracle before production implementation. This evidence is **Mapped**, **Specified** and **Locally verified**. It is not production conformance, differential verification, genuine-data acceptance, caller use or release acceptance.

```yaml
claim_id: EV-2026-07-24-DOC-R03
date_utc: 2026-07-24
managed_commit_or_tree_hash: unavailable-not-a-git-working-tree
port_units: [EXT-DOC-003, EXT-DOC-004]
format: DOC
scope: current CLX authority, PRC/PRM, PlcPcd, CP/FC mapping, compressed and UTF-16 decoding, seven document parts, guards, headers/footers, AutoText and passive control projection
specification:
  name: MS-DOC
  revision: 12.5
  published: 2026-02-17
  sha256: 2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a
contract:
  outcome_cases: 39
  compressed_overrides: 24
  document_parts: 7
  control_tokens: 18
  canonical_sha256: 85529c714ded2e4776c0930ef82e5d3c099c6822d156c06bf94b8248e0529c31
  fixture_groups: [DOC-T01, DOC-T02, DOC-T03]
commands:
  - command: pwsh -NoProfile -File .\scripts\Test-DocTextStoryContract.ps1
    exit_code: 0
    input_class: committed machine-readable R03 contract
    boundary: exact source identity, cases, mappings, parts, header order, quick-save partition, controls and fixture ownership
    limitations: verifies the frozen specification contract, not the production parser
  - command: dotnet test --project .\tests\unit\CollisionDocNet.Writer.Tests\CollisionDocNet.Writer.Tests.csproj --filter "FullyQualifiedName~DocR03ExecutableSpecificationTests" --configuration Release --no-restore
    exit_code: 0
    input_class: independently serialized synthetic specification fixtures; no production parser or fixture constants used
    boundary: 92 cases spanning all five exact nFib layouts and both fComplex states, CLX/Prc/Pcdt/PlcPcd, Prm0/Prm1, piece encodings and bounds, all compressed bytes, malformed UTF-16, all seven parts, headers, AutoText and 18 controls with property/owner states
    result: 92 passed, 0 failed, 0 skipped
  - command: pwsh -NoProfile -File .\scripts\Invoke-RepoCheck.ps1
    exit_code: 0
    input_class: full committed solution plus contract documents
    boundary: locked restore, formatting, Release build, MTP suites, JSON contracts and local Markdown links
    result: 626 total; 625 passed; 1 unrelated opt-in EML cohort skipped; 0 failed; 0 build warnings or errors
test_gap_analysis:
  method: static pseudo-mutation review using dotnet-test test-gap-analysis and the .NET/MSTest extension
  sampled_high_risk_mutations: 47
  killed_by_preexisting_suite: 13
  survived: 12
  no_coverage: 22
  qualification: one nominal preexisting kill asserted the wrong pre-footnote guard; the independent R03 oracle replaces that rule but does not claim a production mutation score
results:
  passed: offline contract verifier, independent executable oracle and independent read-only closure review
  failed: 0
  skipped: production implementation, conformance fixtures, differential oracle, fuzz, security, genuine-data and caller acceptance
known_production_differences:
  - reserved FibBase bytes are misread as charset and text-bound fields
  - five versions share one invalid synthetic FIB shape
  - reserved3 is exposed as a Macro part and the outside U+000D is placed before Footnote
  - compressed text uses a fabricated CP1252-like path and cbMac is not enforced
  - malformed UTF-16 is not diagnosed and PRC issues are not reconciled
  - controls ignore effective properties and public locations lose CP, part and correct stream identity
security_boundary: no field evaluation, link retrieval, active-content execution, sample-doc-files access or private-corpus access occurred
reviewer: independent read-only agent returned PASS after source and oracle corrections
```

The repository root is not a Git worktree, so no commit or Git tree hash is available.
