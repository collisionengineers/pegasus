# Checklist — MAIL-10

- [ ] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [ ] Implement the minimal Core contract/policy with fail-closed validation.
- [ ] Implement the mailbox-scoped persistence/projection/adapter boundary with idempotency and durable evidence.
- [ ] Wire the real caller without duplicating business rules.
- [ ] Add focused acceptance tests for authorization, reason, before/after history, stale versions, relink and Case navigation return context.
- [ ] Run `dotnet restore` and `dotnet build --configuration Release`.
- [ ] Run focused tests and the relevant full suite.
- [ ] Run and record the four-lens simplification pass.
- [ ] Update governing/current-state documentation only to the evidence tier actually reached.
- [ ] Write the post-implementation report with commands, results, residual risks and deployment qualification.

- [ ] Immediately before live execution, record exact-target approval naming the retained message, initial Case, replacement Case, and approved unlink/relink reasons.
- [ ] Run and evidence the full production link → unlink → relink journey, including confirmations, versions, attribution, and append-only history; abort on stale state or mismatch.
