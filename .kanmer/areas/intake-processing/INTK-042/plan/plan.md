# Plan — publish committed intake and custody work immediately

## Chosen approach

Keep SQL work rows as the outbox. Immediately after each transaction commits, Core claims that exact durable identifier, sends it, and marks it dispatched. It never scans a backlog on the request/poll path. A recoverable send failure releases the exact item still due; the one-minute Worker sweep is recovery only and the committed acknowledgement remains truthful.

The shared Azure Queue sender adapters live in Infrastructure. Web composes them with only message-sender access to the two named queues; Worker continues to own queue triggers and all processing. The exact identifiers already returned by receipt, case-acceptance, replacement, vehicle, and image-intake outcomes are reused, so no second outbox or transaction-crossing queue call is introduced.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: commit-before-publish, immediate best-effort identifier publication, one-minute recovery, truthful acknowledgement, and Worker-only processing.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: preserve asynchronous custody, immutable Case/reference outcomes, and no automatic retry after terminal business failure.
- `docs/adr/0032-near-real-time-durable-intake-triggering.md`: one immediate-plus-recovery trigger architecture, not a five-second normal dispatch path.

## Ordered steps

1. Refresh from `origin/dev` after INTK-041, INTK-003, and INTK-040 clear.
2. Add exact-ID durable claim methods and reuse the existing claim/send/mark/release policy for receipt and external work.
3. Call the receipt publisher from `ReceiveIntake`, covering manual, grouped, and mailbox submissions through their existing shared route.
4. Call the external publisher after committed case acceptance/replacement, vehicle lookup request, and image-intake registration/merge.
5. Move queue sender adapters to Infrastructure; compose them in Web and Worker.
6. Rename the timer to recovery-only and make its schedule one minute.
7. Give Web only the two queue message-sender role assignments and service URIs. Do not deploy in this ticket.
8. Test exact ordering, failed-send recovery, composition, deployment template, mailbox/upload/custody integration, and simplify the diff.
9. Write the report, PR, review, and merged-dev proof. Runtime deployment and latency/cost evidence remain DELIV-021.

## Proof

Core tests prove exact-ID publication, enqueue-before-mark, no broad outbox scan, and a recoverable queue failure. Architecture/template tests prove both composition roots, exact function activation census, one-minute recovery, and sender-only Web RBAC. The mailbox/upload/custody integration subset is also run where the local SQL host is available.

## Risks and mitigations

- **False failure after commit:** recoverable publication failure releases the durable row and does not replace the business outcome.
- **Duplicate delivery:** claim ownership and id-only queue messages preserve the existing at-least-once, idempotent processor contract.
- **Privilege expansion:** Web can add messages only to `intake-work` and `external-work`; it cannot receive, delete, or process them.
- **Deployment state:** this branch changes source and Bicep only; no deployed-state document is altered until a deployment actually occurs.
