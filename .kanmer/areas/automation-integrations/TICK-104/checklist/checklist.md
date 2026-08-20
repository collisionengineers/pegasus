# Checklist — MCP-07

- [ ] Core connector settings record, rules owner and store port; options validation delegates to the one rules owner.
- [ ] Infrastructure columns, mapping, migration (no new grants needed) and Ef store with protected token + attributed history.
- [ ] Transport resolves effective settings per call; administration overrides configuration.
- [ ] Administration connector section (status, base URL/timeout, token entry/rotation) per design README.
- [ ] Core + integration tests: validation bounds, history attribution, token never echoed, transport override, configuration fallback regression.
- [ ] `dotnet restore` + Release build (0 warnings); focused tests green.
- [ ] Simplification pass recorded in the plan with dispositions.
- [ ] Post-implementation report.
