# Checklist

- [ ] Confirm INTK-042/INTK-040 are merged and take a fresh worktree.
- [ ] Add one-per-approved-Inbox SQL subscription state/migration without clientState persistence.
- [ ] Add targeted mailbox wake and subscription-maintenance use cases reusing the existing lease/delta owner.
- [ ] Add Graph basic subscription/renewal/lifecycle client without resource data.
- [ ] Add the bounded Web validation/notification callback and identifier-only enqueue.
- [ ] Add Worker wake trigger, six-hour maintenance, and five-minute fallback.
- [ ] Add secret/configuration/RBAC/IaC with unchanged Web warmth and no Functions always-ready.
- [ ] Add protocol, security, persistence, lifecycle, sender, idempotency, composition, and IaC tests.
- [ ] Run Release verification and simplification lenses.
- [ ] Report, commit, push, open the PR to `dev`, and move to Review.
