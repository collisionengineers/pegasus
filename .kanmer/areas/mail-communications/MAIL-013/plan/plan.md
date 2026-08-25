# Plan — wake approved mailbox intake through Graph notifications

## Chosen approach

Use the already warm, externally reachable Web Container App as a narrow anonymous Microsoft Graph protocol endpoint. It answers validation directly, validates bounded basic/lifecycle notifications against persisted subscription identity and Key Vault-backed clientState, and enqueues only stable subscription/mailbox ids through INTK-042's shared Infrastructure sender. A Worker queue trigger revalidates the approved mailbox and enters the existing per-mailbox lease/delta/retention path.

Add one SQL subscription record per approved Inbox, a six-hour Worker maintenance timer with a 48-hour renewal margin, lifecycle/delta recovery, and five-minute fallback polling. Web does not read Graph or process mail. Worker remains the only mailbox cursor/intake owner. No Function always-ready or Web scale change: Web is already `minReplicas: 1`.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: preserve identifier-only queues, durable receipt, idempotency, and Worker-only processing.
- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: implement exact callback, subscription, renewal, lifecycle, fallback, approval/fresh-start, and neutral unresolved-sender rules.
- `docs/adr/0032-near-real-time-durable-intake-triggering.md`: implement Graph wake-up in the existing hosts/SQL/queues; no new runtime or inline processing.
- ADR-0024: keep `ApprovedMailbox.Id` as durable identity and Graph coordinates as replaceable scope.

## Ordered steps

1. Wait for INTK-042 and INTK-040; create a fresh worktree from `origin/dev` and resolve the shared queue adapter path.
2. Add SQL subscription state keyed one-to-one to approved mailbox, with bounded ids/resource/expiry/lifecycle facts and no clientState column; add migration/grants.
3. Add Core subscription maintenance and targeted-mailbox wake use cases that reuse existing approval, lease, cursor, delta, retention, and poison behaviour.
4. Add Graph basic subscription create/renew/lifecycle client using existing credential/HTTP conventions and no resource data.
5. Map `POST /hooks/microsoft-graph/mail` in Web: exact plain-text validation handshake; bounded payload; constant-time clientState check; active subscription/scope validation; identifier-only enqueue; prompt bounded response.
6. Add Worker wake queue trigger and six-hour maintenance timer; make current Inbox timer a five-minute recovery poll.
7. Add Key Vault-backed clientState configuration, queue/settings, least-privilege SQL/queue access, and unchanged Web warmth to IaC. Do not deploy/create subscriptions.
8. Add Web protocol/security tests, Graph contract tests, SQL concurrency/lifecycle tests, targeted/fallback/duplicate Core tests, sender regression, and architecture/IaC ownership tests.
9. Run Release/full relevant validation, migrations checks, simplification lenses, and update source-level current architecture/operations only where repository convention requires an implemented-not-deployed record.
10. Report, commit, push, open the PR to `dev`, and move to Review.

## Proof

Tests prove validation body/content type, secret/scope rejection, bounded request handling, identifier-only queueing, one subscription per approved Inbox, renewal/recreation, lifecycle delta recovery, targeted lease reuse, duplicate/fallback idempotency, neutral sender, no Web Graph reader/processor, unchanged Web replica settings, and no Function always-ready. Deployment/subscription/runtime proof belongs to DELIV-021.

## Risks and mitigations

- **Anonymous callback abuse:** bounded body/count, exact protocol route, secret and active subscription/scope validation, no detailed errors.
- **Secret leakage:** configuration only, constant-time compare, never SQL/queue/log/proof.
- **Two cursor owners:** both wake and fallback delegate to one SQL lease/delta use case.
- **Subscription expiry/missed events:** six-hour maintenance, 48-hour margin, lifecycle recreation plus delta.
- **Privilege creep:** Web only validates and sends; Worker alone reads Graph/writes lifecycle.
- **Hosting cost confusion:** preserve already-warm Web; do not warm Functions.
