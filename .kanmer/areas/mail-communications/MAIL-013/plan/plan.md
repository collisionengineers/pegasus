# Plan — wake approved mailbox intake through Graph notifications

## Outcome

Replace the 15-second ordinary Inbox poll with a targeted Graph wake while retaining one Worker-owned mailbox/delta/intake implementation. Reuse INTK-043's unified warm queue and poison route. Keep one five-minute recovery timer, which also performs subscription maintenance only when its six-hour due time is reached.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: exact callback, subscription, lifecycle, fresh-start, fallback and neutral-sender behaviour.
- `docs/frd/frd-02-intake-and-source-identity.md`: identifiers only cross the queue; the Worker remains the processing owner.
- `docs/adr/0024-stable-approved-mailbox-identity-and-explicit-baseline.md`: implement stable mailbox identity and remove cursor-carrying Graph-identity adoption.
- `docs/adr/0032-near-real-time-durable-intake-triggering.md`: Web validates/enqueues, Worker reads Graph, and polling is recovery.

No governing document change is planned.

## Dependencies

INTK-043 blocks MAIL-013 because both touch the queue envelope, unified Worker function, poison route, composition and infrastructure. Start implementation from current `origin/dev` only after INTK-043 merges. DELIV-021 remains blocked by MAIL-013 and owns deployment/live proof.

## Implementation

1. **Establish one mailbox identity.** Change inbound estate, poll state, poison, retained message and receipt occurrence identity to `ApprovedMailbox.Id`. Add activation time and cursor-scope fingerprint. Remove the Graph-identity adoption path; a scope change/410 fails closed into explicit fresh-start activation.
2. **Add minimal subscription policy/state.** Add focused Core records/ports and one SQL row per enabled Inbox containing only mailbox ID, Graph subscription ID, resource, expiry, lifecycle state and last maintenance result. Keep clientState in protected configuration.
3. **Implement Graph subscription operations.** Reuse existing Graph authentication/HTTP handling for exact-Inbox basic `created` subscriptions, renewal/reauthorization by one PATCH, and recreate after removal/expiry/wrong scope.
4. **Add the Web protocol boundary.** Map `POST /hooks/microsoft-graph/mail`. Return decoded validation tokens as `200 text/plain`. For a bounded batch, verify clientState and active tenant/subscription/scope, then publish mailbox/subscription identifiers with a bounded lifecycle kind. Return `202` after send and 5xx if a valid wake cannot be queued. Do no Graph read or intake work.
5. **Reuse the unified warm Worker route.** Extend INTK-043's queue envelope and `UnifiedWorkFunction` with mailbox wake handling. Resolve and revalidate the mailbox, then enter the same lease/delta path as fallback polling. Extend the unified poison handler to record the failure; add no mailbox queue or Function.
6. **Make the existing timer recovery-only.** Rename it truthfully, set five minutes, and in the same invocation claim subscription maintenance only when due at six hours before running the estate fallback. Duplicate wake/fallback work remains safe under the existing mailbox lease.
7. **Wire and observe.** Add the secret reference, callback URL, SQL grants, configuration, telemetry and deployment assertions. Preserve the capacity configuration inherited after INTK-043; MAIL-013 neither adds nor removes always-ready capacity.
8. **Verify and prepare review.** Prove protocol deadlines, stable identity/fresh-start, lifecycle repair, retry/poison, duplicate overlap, sender neutrality, ownership, least privilege and exact infrastructure. Run locked restore/build, focused and full tests, deployment-plan validation and the required simplification pass; then write the implementation report and PR. Deployment stays with DELIV-021.

## Acceptance evidence

- Web returns the exact decoded validation token and never performs mailbox/intake work.
- A valid notification is queued and acknowledged within Graph's three-second delivery window in tests; invalid input queues nothing and leaks no secret.
- The queue and Function inventory remains INTK-043's single unified work route.
- Targeted wake and five-minute fallback enter the same mailbox lease/delta implementation.
- Operational mail state uses `ApprovedMailbox.Id`; the old re-key/adoption and dual identity are absent.
- One enabled Inbox has at most one exact-scope subscription; maintenance, missed, removed and reauthorization paths recover through delta.
- The forwarding desk is never introduced as a temporary sender.
- Telemetry separates Graph delivery, callback, queue, delta, durable receipt and downstream processing.
- Evidence states that Microsoft documents message notification latency as under one minute average and up to three minutes; no five-second Exchange-to-Pegasus guarantee is claimed.

## Risks and controls

- **Graph delay or loss:** measure provider time separately; retain lifecycle recovery and five-minute fallback.
- **Duplicate delivery:** stable mailbox ID, existing lease/cursor and receipt idempotency remain authoritative.
- **Anonymous endpoint abuse:** exact route, small body/batch limits, constant-time secret comparison and uniform invalid response.
- **Identity migration:** one target schema and explicit fresh-start; no compatibility path or carried cursor.
- **Concurrent INTK-043 changes:** dependency prevents overlapping implementation; rebase and reuse its final contract.

## Simplification pass

Implementation must confirm that the diff adds no mailbox-only queue/function, generic notification framework, second processor, feature flag, compatibility layer or capacity change. Record actual findings and dispositions here before review.
