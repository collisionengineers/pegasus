# EV-2026-07-24 — DOC-R00 specification-source freeze

Claim: the technical DOC specification baseline is pinned, locally reproducible and accepted for internal specification-led implementation. This evidence is `Mapped` and `Locally verified`; it is not product-licence acceptance, parser implementation, conformance, distribution approval or format support.

```yaml
claim_id: EV-2026-07-24-DOC-R00
date_utc: 2026-07-24
managed_commit_or_tree_hash: unavailable-not-a-git-working-tree
repository_owned_input_manifest_sha256: 149de21e5eba9209b9891e5f388407b1b54c60bef8c2362906f105d92aab90fd
port_units: [EXT-DOC-001]
formats: [DOC]
scope: nine-specification acquisition, revision verification, SHA-256 freeze, clean-room boundary and repository provenance record
explicit_exclusions:
  - legal or patent advice
  - product-licence acceptance
  - redistribution permission
  - generated normative tables
  - parser implementation or support claims
specifications:
  - name: MS-DOC
    revision_or_date: 12.5 / 2026-02-17
    sha256: 2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a
  - name: MS-CFB
    revision_or_date: 12.0 / 2024-04-23
    sha256: 2d650184072a148ba98ad0b68072fd5ad7780e46f3528d7f263f3127b2dadab5
  - name: MS-ODRAW
    revision_or_date: 12.4 / 2025-08-19
    sha256: 9ead8f1f3805cf6d4f5597bed516bf7604e330b803f64d28d9b7a0a9dba9a2fc
  - name: MS-OLEDS
    revision_or_date: 13.0 / 2024-04-23
    sha256: 42e666e9f1b1c437972bbe601d302ec25e45557eb309c7d854e54facfeddb134
  - name: MS-OLEPS
    revision_or_date: 9.0 / 2024-04-23
    sha256: 4343243993cd16bda98e5abe5383a82db5f2eea0b34b54dc7d93978a372844ea
  - name: MS-OSHARED
    revision_or_date: 11.1 / 2025-11-13
    sha256: 3a17ec72868a7ba8c9c987995c8902e832a42d66eecbf149101a4e6c7255f87c
  - name: MS-OFFCRYPTO
    revision_or_date: 14.0 / 2026-02-17
    sha256: 9b7a67eb5d0408566a61f218792fcd21536dbc970d83695ad94365e535533f33
  - name: MS-OVBA
    revision_or_date: 15.0 / 2026-05-19
    sha256: 31fb68ac3ef209cb32247a3060ff775cc0517c4120137cb39945690448b46c79
  - name: MS-OFORMS
    revision_or_date: 9.1 / 2025-08-19
    sha256: 7bbbbdc43407524fe2af99c070dfc358cc67404e5224b56d5cdabbc4736c9158
secondary_sources: []
fixture_manifest: not-applicable
fixture_ids: []
commands:
  - command: pwsh -NoProfile -File .\scripts\Acquire-DocSpecifications.ps1 -VerifyOnly
    exit_code: 0
    input_class: 18 ignored Microsoft specification artifacts (nine date-stamped DOCX publications and nine current PDFs)
    boundary: exact file presence and SHA-256 verification
    limitations: network retrieval was performed separately; current PDF URLs are not immutable and are pinned only by hash
  - command: pwsh -NoProfile -File .\scripts\Invoke-RepoCheck.ps1
    exit_code: 0
    input_class: repository source, documentation and tests; no private corpus or sample-doc-files input
    boundary: locked restore, format, Release build, MTP tests, JSON parsing and local Markdown links
    limitations: 533 passed, one explicitly opt-in EML cohort test skipped; this command does not perform legal review or full JSON Schema validation
environment:
  os: Microsoft Windows 10.0.26200
  dotnet_sdk: 10.0.302
  architecture: X64
results:
  passed: 18 artifact hashes; repository check with 533 tests
  failed: 0
  skipped: 1 opt-in EML cohort test unrelated to DOC-R00
differential_oracles: []
security_and_resource_limits: downloads remain under ignored artifacts/research; acquisition never runs from the offline repository check
known_gaps:
  - MS-OLEDS and MS-OLEPS exact-revision patent coverage is unresolved
  - product licence and distribution approval remain unresolved
  - the historical unnamed secondary implementation cannot be identified and is prohibited as a future source
artefacts:
  - docs/licensing/doc-source-provenance.json
  - docs/licensing/doc-source-rights.md
  - docs/decisions/ADR-0005-doc-source-and-clean-room-boundary.md
  - scripts/Acquire-DocSpecifications.ps1
  - ignored artifacts/research/doc/2026-07-24/specifications/
reviewer: technical checks performed by Codex with two independent read-only agent audits; internal implementation authorized by repository-owner direction; named release/licensing reviewer absent
```

The repository-owned input-manifest hash is SHA-256 over the following UTF-8 lines, in this order and with a final newline. It excludes this self-referential evidence file.

```text
f41544502d59a3204d8a07ef2e8dbb0a409a60fbfeb2f4d811f14e3157044c5a  docs/architecture/source-baseline.md
f0934f1ec6f108261cea70f9326a4ebb5171e946aaa410f76143f5929aa18a2b  docs/decisions/ADR-0005-doc-source-and-clean-room-boundary.md
383e13e2480021222fe75d23eac00b4a08b1fd3a2b14188203a8940baac8116d  docs/licensing/provenance-manifest.schema.json
55aebfe917dd1456ecf0027de9718dea7ac2e4794920fcef2c7a0ed6a835eb95  docs/licensing/doc-source-provenance.json
dbb486155da29843f8a0900466787aa076125793cd005ce46f5d715dd07c0dd9  docs/licensing/doc-source-rights.md
132e347d7d7a67e9b3affbdc56b884a163e2ccf3fcfec75823580884687cc398  docs/programme/tasks/P31-doc-comprehension-and-completion.md
4bc0f289249f005770b8e3086d5bb059fb7af12fd58dea23184b59f113a58186  docs/testing/evidence/EV-2026-07-23-wave3-doc-text.md
79837caf027394700515041c33197493aeaf915754e6310866b08e5eb9164b91  scripts/Acquire-DocSpecifications.ps1
b8208a685a51d9d7cfbe1e585c8b6a72e55263aad6b65436fed7a436682e0285  scripts/Invoke-RepoCheck.ps1
```

The repository root is not a Git worktree, so no commit or Git tree hash is available.
