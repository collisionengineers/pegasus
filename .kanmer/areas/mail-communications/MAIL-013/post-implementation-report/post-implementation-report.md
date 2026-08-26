# Post-implementation report

## Delivered

MAIL-013 now uses Microsoft Graph change notifications to wake approved Inbox intake. Web validates the Graph protocol and client state, resolves the active subscription, and writes a bounded mailbox wake onto INTK-043's existing `intake-work` queue. The existing unified Worker function resolves the stable mailbox ID and enters the same lease/delta/intake path used by recovery.

The approved mailbox row is now the single operational identity. Poll, poison and retained-mail state use `ApprovedMailbox.Id`; activation and scope fingerprint establish an explicit fresh baseline. The obsolete configured-mailbox fallback and Graph-key adoption path were removed.

One exact-Inbox basic `created` subscription is maintained per active mailbox. Renewal/recreation and lifecycle wakes are handled through the same Worker. The old ordinary poll timer is a five-minute bounded recovery pass; subscription candidates are checked at six-hour intervals. No queue, Function, deployment unit, feature flag or capacity was added.

IaC and release scripts now require the protected client-state secret, callback URL, renamed recovery Function and exact SQL grants.

## Verification

- `dotnet restore Pegasus.slnx --locked-mode` — passed.
- `dotnet build Pegasus.slnx -c Release --no-restore` — passed, 0 warnings/errors.
- Core tests — 1001/1001 passed.
- Architecture tests — 100/100 passed.
- Changed-area integration matrix — 102/102 passed, covering Graph/webhook, mailbox estate, persistence/migration, host configuration and affected UI/automation fixtures.
- Focused stable-identity/Graph/persistence suites — 39/39, 29/29, 3/3 and 74/74 passed during implementation.
- `az bicep build --file infra/main.bicep --stdout` — passed.
- `pwsh scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — passed.
- `git diff --check` — passed (line-ending notices only).

Independent simplification findings were applied and recorded in the plan.

## Review focus

Confirm the anonymous endpoint returns validation tokens verbatim but enqueues only bounded, clientState-authenticated notifications for an active exact-scope subscription. Confirm Worker wake and recovery both use `PollApprovedInbox`'s stable-ID lease/delta route, and confirm the migration deliberately discards obsolete pre-release operational cursor/poison/retained rows rather than carrying the removed identity model.

Deployment and live notification evidence remain owned by [[DELIV-021]].
