# Stream C source manifest (C01 step 1) — 2026-09-06

Read-only pass over `pegasus_pack/` on CEMATTYPC; outputs under the C worktree's ignored `artifacts/evaluation/v1-intake/` (`source-manifest.json`, `source-manifest-report.md`). Nothing modified, renamed, copied into git or uploaded.

```
instructions 81/81
reports 29/29
eva 14/14
```

All 124 items resolved locally; every computed SHA-256 matched every hash recorded for it (method.md, `top15-method-evidence.json`, `third-party-source-inventory.json`, `Aggregate_findings.json`, `MANIFEST.sha256`). Zero missing, zero mismatches, zero ambiguous references. The 81 count agrees item-for-item between the `## Immutable sample references` bullets in the 15 method.md files and `sample_sources[]` in `top15-method-evidence.json`: 14 principals x 5 + MP x 11 (`MP PDF 01-04`, `MP Weird 01-02`, `MP Word 01-05`). Rejected alternatives: 75 (naive 15x5) and 77 (incomplete `fable_output/.../_corpus-text/` derivative set).

providers-worked-on.xlsx, two hashes: `principal-and-repairer-info/providers-worked-on.xlsx` (EVA report copy, in MANIFEST) 5,582,614 B `555a3f3ba5b81ce54af491b22fd49724d49d77b01f5b3c0a0fa8b758a03b4a33`; `astra_output/source/{dev,main}/reference/workproviders-and-repairers/providers-worked-on.xlsx` (pinned snapshot, NOT in MANIFEST) 5,594,848 B `4d3d847d3ae15a7b9e89d48a50649a1d7c901eb6d322012ec62fc2342e867775`. Resolving by name alone picks the wrong workbook.

`evacases.xlsx` = `principal-and-repairer-info/every_eva_case.xlsx`, identical bytes, 4,440,193 B, `cf8bfe83b9325158c10e43be1b957fd5712c188d93f2c3b54540e5c068aedf84`; it is the prior export, recorded in the JSON `special` block outside `eva 14/14`.

E01–E28 (28, contiguous): all `unavailable`, never `passed`. Box web links only; no local filename, size or hash. Referenced in `more_docs/PRINCIPAL_DOCUMENT_MAPS.md` (all 28) and `more_docs/QDOS_IDENTIFICATION_AND_FIELDS.md` (E01, E06, E20–E24, E27).

Traps (not verification failures): EVA-001 filename divergence (`backup_of_ce_job_sheet_260429.xlsm` is locally `ce-docs/job-sheet-current.xlsm`, same bytes `a52b5df2…b983b`, resolved by hash); `MANIFEST.sha256` is comma-delimited with unquoted paths containing commas — split the last two fields from the right; 729 lines = 1 header + 728 records; the manifest pins 728 of 11,727 pack files (all of `astra_output/`, `more_docs/`, `fable_output/` unpinned plus `doc-extraction-reference/original-extractor/all_top15_legacy_rules.txt`); audits dir 30 vs 29 is the folder README; OAK 01–05.DOC are RTF, byte-identical to their `fable_output` "derived text" (that derivative carries no extraction signal); 19 of 124 items have identical-byte twins, all recorded as `occurrences` (nothing deduplicated).

Design note for `tests/Pegasus.IntegrationTests/PrincipalSourceManifestTests.cs`: `corpus/` is absent on this workstation; existing Corpus-category tests resolve `PEGASUS_CORPUS_ROOT` or repo-root `corpus/` and skip when absent. The manifest test must read the pack by an injected root (env var), never embed corpus bytes, and report unavailable sources as skipped/unavailable, not passed.
