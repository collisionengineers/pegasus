# EV-2026-07-23 — Waves 11–13 nesting, security and local performance

Scope: cross-format nesting, hostile-regression infrastructure and host-local performance scaffolding. These are partial local evidence only, not security acceptance, continuous fuzzing, production budgets or release acceptance.

## Nesting

`CollisionDocNet.Extraction` recursively dispatches materialised supported-format attachment bytes under cumulative input/decoded/object/text/asset/depth/deadline controls. It records occurrence paths, parent relationships, hashes and local/aggregate issues; duplicates remain distinct occurrences and unsupported assets remain hashed evidence. Conversion tests pass 19/19 and the then-current full solution passed 497/497.

Gaps: native MSG embedded storage does not expose original CFB bytes; deterministic mid-recursion cancellation/deadline and a finite ancestor-cycle fixture lack seams.

## Security

The solution-linked `tests/security/CollisionDocNet.Security.Tests` suite passes 21/21. It covers passive PDF actions, DOCX external/VBA evidence and XML/ZIP denial, EML remote/path/script passivity and nesting, rejected hostile DOC/MSG CFB markers, five-format cancellation/deadline/input/stream failures, content-free issues, 80 deterministic format mutations and 64 arbitrary seeds. The DOCX limit-diagnostic defect it found was corrected and both DOCX/security suites remained green.

Gaps: valid structured hostile DOC/MSG active-content fixtures, socket-level no-network instrumentation and maintained continuous fuzz execution.

## Performance

The isolated test-only BenchmarkDotNet 0.15.8 project is locked and uses MemoryDiagnoser. All ten cases pass list/Dry validation. On this Windows/.NET 10 host, Short measurements observed:

- 1 MiB DOCX detection: 7.256 ms mean, 4.04 MiB allocated;
- synthetic MSG dispatch: 127.7 microseconds mean, 141.19 KiB allocated;
- 20 five-format operations at degree four: stable canonical fingerprints; and
- blocked-stream cancellation: approximately 30 ms.

These Short measurements are leads, not accepted budgets. No 10 MiB end-to-end, Linux, sustained/nested load, larger class, independent repetition or authorised thresholds have been completed. Detailed local output remains ignored under `artifacts/performance/20260723-wave13/`.
