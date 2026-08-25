# Checklist — PLAT-044

- [x] Add the six-command Assessment workspace projection using shared mappings.
- [x] Remove GET-time report projection/content reads and narrow readiness to post-Review work.
- [x] Batch report photographs through `ReadVersionsAsync`.
- [x] Route all managed Box content through the persisted case-root remote id.
- [x] Update operator notes and FRD-11 to the resolved readiness rule.
- [x] Add focused readiness, Web/SQL-command, report-batch and Box-root tests.
- [x] Run simplification lenses and record dispositions in the plan.
- [x] Run locked restore, Release build, focused tests and the full compatible suite.

## Progress notes

Verification completed on 2026-08-25:

- `dotnet restore .\Pegasus.slnx` — succeeded.
- `dotnet build .\Pegasus.slnx --configuration Release --no-restore -nodeReuse:false` — succeeded with 0 warnings and 0 errors.
- Core — 974 passed, 0 failed.
- Architecture — 99 passed, 0 failed.
- Integration — 949 passed, 16 corpus-dependent skips, 0 failed.
- Focused regressions separately proved six workspace reader commands, zero Assessment GET document-content I/O, one ordered report-photo batch, post-Review readiness semantics, and Box request counts/root fencing.
