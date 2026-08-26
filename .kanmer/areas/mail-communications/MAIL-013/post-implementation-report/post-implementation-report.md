# Post-implementation report

## Delivered

MAIL-013 uses Microsoft Graph change notifications to wake approved Inbox intake. Web returns Graph validation tokens as plain text; for notification batches it validates clientState, tenant, active subscription, exact mailbox resource, and either `created` or one of the three supported lifecycle events before placing stable identifiers on INTK-043's existing `intake-work` queue. Unknown, malformed, wrongly scoped and oversized input queues nothing. A valid queue failure returns 5xx.

The unified Worker resolves the stable mailbox ID and enters the same lease/delta/intake path used by recovery. Lifecycle `missed`, `subscriptionRemoved` and `reauthorizationRequired` schedule that targeted path and update subscription lifecycle state after the delta pass.

`ApprovedMailbox.Id` is the operational identity for poll, poison and retained-mail state. The migration maps existing rows only through an exact saved Graph mailbox identity and fails closed when no exact mapping exists. It preserves retained messages, attachments, restrictive folder-move dependants, poll state and poison evidence; it performs no hidden reset or evidence deletion. Activation and scope fingerprint still force the explicit fresh-start boundary before the preserved cursor can be used.

One exact-Inbox basic `created` subscription is maintained per active mailbox. The existing timer is a five-minute bounded recovery pass with six-hour subscription maintenance. No queue, Function, deployment unit, feature flag or capacity was added.

## Verification

- `dotnet restore Pegasus.slnx --locked-mode` — passed.
- `dotnet build Pegasus.slnx -c Release --no-restore` — passed, 0 warnings/errors.
- Core tests — 1001/1001 passed.
- Architecture tests — 100/100 passed.
- Review-correction integration matrix — 81/81 passed, covering the complete webhook contract, Graph subscription provider, retained persistence, populated stable-ID migration, schema and exact runtime grants.
- Graph webhook contract — 12/12 passed.
- Populated migration and exact subscription grants — 2/2 passed.
- `az bicep build --file infra/main.bicep --stdout` — passed.
- `pwsh scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — passed.
- `git diff --check` — passed (line-ending notices only).

Earlier broad changed-area verification passed 102/102. Full IntegrationTests was used diagnostically and bounded; no completed final full-suite run is claimed.

## Review corrections

- [[PR-067]]: removed hidden deletion/reset, preserved retained evidence and restrictive dependants, and added populated migration plus grant proof.
- [[PR-068]]: enforced the full Graph webhook trust boundary and added invalid, lifecycle, batch and queue-failure coverage.

Independent simplification findings remain applied and recorded in the plan. Deployment and live notification evidence remain owned by [[DELIV-021]].
