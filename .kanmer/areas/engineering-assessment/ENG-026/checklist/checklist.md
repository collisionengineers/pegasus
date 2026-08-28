# Checklist — ENG-026

- [x] Core model: states, routes, version fields, line PaintWorkUnits/Quantity
- [x] Core Estimates.cs: details, policy, operations, totals, use cases, port
- [x] Report projection reads the Current estimate; readiness reason renamed
- [x] Persistence entities, configuration, store methods, workspace source
- [x] Migration NamedEstimates + snapshot + migration list
- [x] JsonEstimateParser + registration
- [x] MCP pegasus_estimate_save / pegasus_estimate_list
- [x] Tests: Core, integration store, MCP ingress, JSON parser
- [x] Build green; grant script and deployment plan script pass — orchestrator
  wave loop 2026-08-28 on head `1edc7b70` (dev re-merge with the applied-
  migrations census union): restore `--locked-mode` exit 0; Release build
  exit 0; Core 1119/1119; Architecture 100/100; Integration 1010/1010
  (exit 0, no flakes); `Test-MigrationGrants.ps1`: 82 migration files
  checked, every created table granted or exempted. CI on GitHub to confirm.
- [x] Merge origin/dev; simplification pass; post-implementation report; PR
