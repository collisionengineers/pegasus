# EV-2026-07-23 — Wave 2 CFB v3/v4 reader

Scope: BCL-only, read-only CFB v3/v4 structural parsing under `EXT-STO-001`. This historical record originally reported an independent-review request for equal-black-height validation. Later primary-specification and genuine-file evidence proved that request incorrect: MS-CFB 2.6.4 does not require equal black height and explicitly permits an all-black binary tree. The non-spec validation was removed on 2026-07-24 and replaced with a positive regression. This record does not claim full conformance, differential parity, fuzz resistance, performance acceptance or format-handler completeness.

## Implemented boundary

- strict v3/v4 header validation, including v4 sector sizing and header padding;
- DIFAT, FAT and miniFAT loading with checked sector references;
- directory parsing, sibling-tree ordering/colour/reachability validation;
- exact regular-stream and mini-stream traversal;
- cycle, cross-link, duplicate-reference, orphan-allocation, reserved-value and range rejection;
- explicit input/sector/directory/per-stream/cumulative limits and cancellation outcome; and
- deterministic stream-ID ordering with immutable copied output.

The reader does not execute content, activate OLE, choose filesystem paths, retrieve external resources, launch processes or use native code.

## Command and result

```powershell
dotnet test --project tests\unit\CollisionDocNet.Storage.Tests\CollisionDocNet.Storage.Tests.csproj --configuration Release --no-restore
```

Result: exit `0`; 49 succeeded, 0 failed, 0 skipped. Input class: owned in-memory synthetic CFB v3/v4 fixtures. Duration observed by the harness was under one second for the test assembly.

The implementing agent also reported clean `dotnet format --verify-no-changes --no-restore` checks for the Storage production and test projects, correct inherited .NET 10 MSBuild policy, and no critical findings from the requested static .NET performance-pattern scan. Those static results are not allocation or throughput measurements.

## Remaining evidence

- specification-derived independent fixtures;
- actual DOC and MSG storage-profile cases;
- fuzz/property and hostile regression corpora;
- differential comparison with independent CFB implementations;
- allocation, CPU, elapsed, cancellation-latency and concurrency measurements; and
- independent acceptance review.
