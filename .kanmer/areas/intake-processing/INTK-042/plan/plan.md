# Plan — publish committed intake and custody work immediately

## Chosen approach

Keep SQL work rows as the outbox and reuse one Core claim/enqueue/mark protocol for both the immediate attempt and the one-minute recovery sweep. The caller asks that protocol to publish after the committing use case has returned; a queue failure is recorded and left recoverable but does not falsely turn a committed receipt/case result into failure.

Move the concrete Azure Queue sender/configuration from Worker into Infrastructure so Web and Worker share the same external-boundary adapters. Web receives sender-only permission/configuration; Worker remains the sole queue-trigger processor. Prefer one shared immediate due-work publisher at the post-commit boundaries over threading transport ids through unrelated business results, unless the refreshed code shows an existing returned id gives a smaller exact claim.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: implement commit-before-publish, immediate best-effort identifier publication, one-minute recovery, truthful acknowledgement, and Worker-only processing.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: preserve asynchronous custody, immutable Case/reference outcomes, and no automatic retry after terminal business failure.
- `docs/adr/0032-near-real-time-durable-intake-triggering.md` after INTK-041 merges: implement the single immediate-plus-recovery trigger architecture; do not retain the five-second timer as a second normal path.
- `docs/current-architecture.md` and `docs/operations.md`: update the as-built source/configuration shape while clearly stating not deployed.

## Ordered steps

1. Wait for INTK-041 and INTK-003 to merge and for INTK-040 overlap to clear; base the ticket worktree on refreshed `origin/dev`.
2. Move/reuse queue client settings and sender adapters in Infrastructure; remove superseded Worker-only copies and compose them in both roots.
3. Extend the existing intake/external dispatch owners with a post-commit immediate attempt that uses their exact claim/enqueue/mark/release protocol.
4. Invoke it after manual/grouped upload receipt, mailbox receipt, and relevant committed custody-producing use cases, without queue calls inside transactions or queue processing in Web.
5. Treat publication failure as recoverable telemetry on an already committed result; leave the row eligible for INTK-003/one-minute recovery and keep operator state truthful.
6. Change the existing dispatch/reconciliation schedules to one-minute recovery and remove timer-first comments/naming where misleading.
7. Add only the Web queue service URI and sender role required by the real caller; retain Worker contributor/processor permissions. Do not deploy.
8. Add Core, EF, Web, mailbox, custody, composition, architecture, and Bicep tests for ordering, failure, duplicates, both intake routes, and least privilege.
9. Update current-state docs as implemented/not deployed; run focused/full Release validation and the required simplification lenses.
10. Report, commit, push, open the PR to `dev`, and move to Review.

## Proof

Tests prove commit precedes publication, both email/manual routes attempt publication without the five-second wait, queue failure keeps the durable success truthful and recoverable, duplicates process once, custody work follows the same pattern, and Web has send-only composition. Merged-dev verification repeats the evidence. Deployment, identity assignment, runtime latency, and cost proof remain DELIV-021.

## Risks and mitigations

- **False failure after commit:** immediate publisher returns an outcome/telemetry fact rather than throwing through the business acknowledgement.
- **Duplicate policy:** both triggers call the same Core dispatcher; adapters only transport identifiers.
- **Over-broad contract changes:** select the smallest post-INTK-040 invocation boundary and avoid forcing work ids through every domain result.
- **Privilege expansion:** Web gets sender-only queue access; no mailbox read or queue processing.
- **Concurrent work:** do not take the ticket until all three named prerequisites are merged/clear.
