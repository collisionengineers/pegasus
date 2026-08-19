# Checklist — MAIL-03

- [x] Revalidated prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [x] Confirmed the minimal Core contract/policy is the MAIL-04 `CorrectRetainedMailClassification` owner with fail-closed validation; no duplicate production code was added.
- [x] Confirmed the mailbox-scoped persistence/projection boundary uses the existing retained-mail transaction with optimistic concurrency and durable append-only evidence.
- [x] Confirmed the real message-detail caller is wired through that Core use case without duplicated business rules.
- [x] Added focused acceptance tests for cross-mailbox invariance, ambiguity and unsupported/stale message failures.
- [x] Ran `dotnet restore ./Pegasus.slnx --locked-mode` and `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [x] Ran focused tests and the relevant Core, architecture, and retained-mail persistence suites.
- [x] Ran and recorded the four-lens simplification pass.
- [x] Updated governing/current-state documentation only to the local evidence tier actually reached.
- [x] Wrote the post-implementation report with commands, results, residual risks and deployment qualification.
- [x] Proved the shared policy against two distinct mailbox identities in local/integration tests.
- [x] Recorded the production-evidence qualification: production currently has one linked mailbox; no live check was performed or claimed here, and first real second-mailbox evidence remains with [[TICK-036]], [[TICK-037]], or [[TICK-038]] when connected.
