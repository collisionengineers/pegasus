# Checklist — UI-14

- [ ] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [ ] Implement the minimal Core contract/policy with fail-closed validation.
- [ ] Implement the mailbox-scoped persistence/projection/adapter boundary with idempotency and durable evidence.
- [ ] Wire the real caller without duplicating business rules.
- [ ] Add focused acceptance tests for Receiving/Queries/Other plus distinct Needs sorting/Triage, counts, paging and preserved filters.
- [ ] Run `dotnet restore` and `dotnet build --configuration Release`.
- [ ] Run focused tests and the relevant full suite.
- [ ] Run and record the four-lens simplification pass.
- [ ] Update governing/current-state documentation only to the evidence tier actually reached.
- [ ] Write the post-implementation report with commands, results, residual risks and deployment qualification.
