# Plan — wake approved mailbox intake through Graph notifications

## Outcome

Replace 15-second Inbox polling as the ordinary trigger with a Microsoft Graph basic-notification wake, while preserving one Worker-owned mailbox lease/delta/intake route. Keep the existing Inbox timer at five minutes solely for recovery. Reuse the current warm Web app, SQL database, Azure Storage Queue transport, Flex Worker and managed identities; add no runtime and no always-ready capacity.

Microsoft documents Outlook message notification delivery at under one minute average and up to three minutes. The implementation must make Pegasus's own callback and processing fast and observable, but must not claim Graph can guarantee five-second end-to-end delivery.

## Ordered implementation

1. **Create the targeted Core seam.** In `MailboxIntake.cs`, expose one approved-mailbox execution path that performs the same actor validation, mailbox validation, approval re-check, SQL claim, delta read, retention, cursor advancement and recoverable release as the existing estate poll. Refactor only enough for both estate fallback and targeted wake to call the same private implementation.

2. **Add the minimal subscription model and persistence.** Add one SQL row per `ApprovedMailbox.Id` with subscription id, exact resource/scope fingerprint, expiry, lifecycle/maintenance state and last result. Add focused Core ports/use cases and an EF store using the existing mailbox entity/model/concurrency conventions. Give Web lookup-only access and Worker lifecycle-write access. Do not persist clientState.

3. **Add the Graph subscription adapter.** Reuse `GraphApprovedSources` authentication and HTTP conventions. Create basic `changeType: created` subscriptions on the exact approved Inbox with the same notification/lifecycle URL; renew within 48 hours using PATCH; use one PATCH to reauthorize and renew; recreate missing, removed, expired, lifecycle-URL-missing or wrong-scope subscriptions. Map missed/removed gaps to the existing delta recovery path.

4. **Add the shared mailbox-wake queue and Web callback.** Extend INTK-042's Infrastructure queue transport with one canonical identifier-only mailbox-wake message. Map `POST /hooks/microsoft-graph/mail` on Web. Return URL-decoded validation tokens as 200/text/plain. For bounded notification batches, validate clientState in constant time plus tenant, active subscription, exact scope and enabled mailbox; enqueue valid wakes; return 202 after send. Ignore invalid items without queueing or detailed errors; return 5xx when a valid wake cannot be published so Graph retries. Do no Graph read or intake work inline.

5. **Add Worker callers without another business path.** Add the `mailbox-wake` queue trigger, explicit poison trigger and six-hour subscription-maintenance timer in `MailboxFunctions.cs`. The wake trigger resolves and revalidates the mailbox, then calls the targeted Core seam. Change `ApprovedInboxPollSchedule` to `0 */5 * * * *`; it remains the same estate-wide recovery use case.

6. **Wire existing Azure resources and least privilege.** Update Bicep/configuration for the queue, Key Vault-backed clientState, exact callback URL, schedules, function census and Web-send/Worker-consume permissions. Preserve deployed Web `minReplicas: 1`, `maxReplicas: 1`; preserve Worker Flex scale-to-zero with no always-ready entry. Add no live subscription or deployment in this implementation ticket.

7. **Prove protocol, security, durability and ownership.** Add focused tests for the ten-second validation protocol contract, sub-three-second design boundary, bounded batches, secret/scope rejection, queue-send failure, one subscription per Inbox, renew/reauthorize/recreate, lifecycle delta recovery, duplicate delivery, poison handling, disabled mailboxes, fallback overlap, neutral sender and host dependency direction. Extend deployment-plan/smoke assertions without logging secrets.

8. **Verify and prepare review.** Run locked restore, Release build, focused tests, full tests and deployment-plan validation. Run the required simplification lenses over only this branch's diff, remove duplicate or speculative machinery, record dispositions in this plan, then write the implementation report, commit, push and open the PR to `dev`. Deployment and measured production proof stay with `DELIV-021`.

## Acceptance evidence

- Graph validation responds exactly with decoded text/plain token.
- Valid callbacks durably enqueue and acknowledge within the Graph three-second delivery window under test; invalid callbacks queue nothing and disclose nothing.
- Web never reads Graph mail, advances cursors or runs intake.
- Worker wake and five-minute fallback enter one lease/delta path and remain idempotent under overlap/retry.
- Each enabled approved Inbox has at most one exact-scope active subscription; lifecycle recovery cannot lose the delta gap.
- ClientState never appears in SQL, queue bodies, telemetry, responses or proof.
- Sender remains neutral until MAIL-009's effective sender is established.
- Web remains one warm replica; Worker remains zero always-ready.
- Telemetry separates Graph delivery time from Pegasus callback, queue, delta and processing time.
- No claim is made that Microsoft Graph guarantees five-second Exchange-to-Pegasus delivery.

## Dependencies and sequencing

INTK-040 and INTK-042 are already merged into the current `origin/dev` baseline. MAIL-013 is not blocked by another implementation ticket. It continues to block `DELIV-021`, which owns approved deployment, live Graph subscription creation, latency/recovery/cost observation, and current-state documentation.

## Simplification constraints

Reuse the current mailbox lease/delta implementation, shared Queue sender pattern, EF mailbox model, Web endpoint convention, Worker poison convention and managed identities. Add only the external-boundary ports required by Graph/SQL/Queue. Do not add a notification framework, dispatcher hierarchy, second cursor owner, second intake owner, new store/runtime, compatibility path, feature flag, rich-notification encryption, or speculative always-ready capacity.
