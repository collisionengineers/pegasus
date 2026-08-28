# Checklist — ENG-026

- [ ] Core model: states, routes, version fields, line PaintWorkUnits/Quantity
- [ ] Core Estimates.cs: details, policy, operations, totals, use cases, port
- [ ] Report projection reads the Current estimate; readiness reason renamed
- [ ] Persistence entities, configuration, store methods, workspace source
- [ ] Migration NamedEstimates + snapshot + migration list
- [ ] JsonEstimateParser + registration
- [ ] MCP pegasus_estimate_save / pegasus_estimate_list
- [ ] Tests: Core, integration store, MCP ingress, JSON parser
- [ ] Build green; grant script and deployment plan script pass
- [ ] Merge origin/dev; simplification pass; post-implementation report; PR
