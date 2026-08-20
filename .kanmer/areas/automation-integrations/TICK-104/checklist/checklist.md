# Checklist — MCP-07

- [x] Core connector settings record, rules owner and store port; options validation delegates to the one rules owner.
- [x] Infrastructure columns, mapping, migration (no new grants needed) and Ef store with protected token + attributed history.
- [x] Transport resolves effective settings per call; administration overrides configuration.
- [x] Administration connector section (status, base URL/timeout, token entry/rotation/removal) per design README.
- [x] Core + integration tests: validation bounds, history attribution, token never echoed, transport override, configuration fallback regression.
- [x] `dotnet restore` + Release build (0 warnings); focused tests green (Core AiWorkTests 31/31; SendToAiIntegrationTests 6/6; Test-MigrationGrants pass).
- [x] Simplification pass recorded in the plan with dispositions.
- [x] Post-implementation report.
