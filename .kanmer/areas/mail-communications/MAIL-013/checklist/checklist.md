# Checklist

- [ ] Rebase after INTK-043 and confirm its unified queue/function/poison contract is the one reused.
- [ ] Replace Graph-keyed inbound operational state with stable `ApprovedMailbox.Id`, activation time and cursor-scope fingerprint; remove adoption/dual identity.
- [ ] Add the minimal subscription Core policy, SQL state, migration and least-privilege grants without storing clientState.
- [ ] Add exact-Inbox basic `created` subscription create, PATCH renew/reauthorize and recreate operations.
- [ ] Add the bounded Graph Web endpoint and publish mailbox/subscription lifecycle wakes through the unified `intake-work` queue.
- [ ] Dispatch mailbox wakes and poison through the existing unified Worker functions into the shared lease/delta route.
- [ ] Convert the existing Inbox timer to five-minute recovery with six-hour due maintenance; add no timer Function.
- [ ] Add secret/configuration/IaC/telemetry/smoke assertions without changing capacity.
- [ ] Add focused protocol, identity, lifecycle, retry, overlap, sender, composition and grant tests.
- [ ] Run locked restore/build, focused/full tests and deployment-plan validation.
- [ ] Run and record the simplification pass; write the implementation report and PR to `dev`.
