# Checklist — MAIL-12

- [ ] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [ ] Implement the minimal Core contract/policy with fail-closed validation.
- [ ] Implement the mailbox-scoped persistence/projection/adapter boundary with idempotency and durable evidence.
- [ ] Wire the real caller without duplicating business rules.
- [ ] Add focused acceptance tests for recipient/content validation, reply/thread identity, attachments, operation replay, visible failure and Sent evidence reconciliation.
- [ ] Run `dotnet restore` and `dotnet build --configuration Release`.
- [ ] Run focused tests and the relevant full suite.
- [ ] Run and record the four-lens simplification pass.
- [ ] Update governing/current-state documentation only to the evidence tier actually reached.
- [ ] Write the post-implementation report with commands, results, residual risks and deployment qualification.

- [ ] Immediately before live sending, record exact approval for the sender mailbox, reply/forward source messages, attachment, final content, complete recipient set, and permitted sends; approved test recipient is digital@collisionengineers.co.uk.
- [ ] Run and evidence the full production draft/compose/reply/forward/send journey, including confirmation, signature, attachment, idempotent replay/retry, attribution, and Sent-evidence reconciliation.
