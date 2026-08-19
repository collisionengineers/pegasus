# Checklist — MCP-05

- [ ] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [ ] Implement the minimal Core contract/policy with fail-closed validation.
- [ ] Implement the mailbox-scoped persistence/projection/adapter boundary with idempotency and durable evidence.
- [ ] Wire the real caller without duplicating business rules.
- [ ] Add focused acceptance tests for scope denial, attribution, replay/version parity, read tools and only staff-equivalent delivered mutations.
- [ ] Run `dotnet restore` and `dotnet build --configuration Release`.
- [ ] Run focused tests and the relevant full suite.
- [ ] Run and record the four-lens simplification pass.
- [ ] Update governing/current-state documentation only to the evidence tier actually reached.
- [ ] Write the post-implementation report with commands, results, residual risks and deployment qualification.

- [ ] Inventory every user-facing email-workspace option and expose a thin Automation MCP tool over the same Core query/command; record any owning capability not yet landed as a dependency, not an omission.
- [ ] Prove authorization, confirmation, exact-target, version, idempotency, attribution, failure, recovery, and destructive-action parity for every mutation tool.
- [ ] After deployment, run the full inventory through the live Automation MCP client; exercise all reads and execute writes only under each owning MAIL ticket's separately recorded exact-target approval.
