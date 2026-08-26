# Checklist

- [ ] Create one targeted approved-mailbox Core entry point that reuses the existing lease/delta/intake implementation.
- [ ] Add one-per-approved-Inbox subscription state, focused Core use cases/ports, EF store, migration and least-privilege grants without storing clientState.
- [ ] Add exact-scope Graph basic `created` subscription create, PATCH renew/reauthorize and recreate handling.
- [ ] Add the shared identifier-only `mailbox-wake` Queue transport.
- [ ] Add the bounded anonymous Web validation/notification endpoint with exact token response, constant-time checks, 202-after-send and 5xx-on-send-failure.
- [ ] Add Worker wake, poison and six-hour maintenance triggers; change Inbox polling to five-minute recovery.
- [ ] Add Key Vault/configuration/RBAC/IaC and smoke-plan checks while preserving Web 1/1 replicas and Worker zero always-ready.
- [ ] Add protocol, security, persistence, lifecycle, delta, retry, poison, sender, idempotency, composition and IaC tests.
- [ ] Add non-secret stage telemetry that separates Graph delivery latency from Pegasus processing latency.
- [ ] Run locked restore, Release build, focused/full tests and deployment-plan validation.
- [ ] Run and record the required simplification pass; remove duplicate or speculative machinery.
- [ ] Write the implementation report, commit/push, open the PR to `dev`, and move to Review; leave deployment and live proof to DELIV-021.
