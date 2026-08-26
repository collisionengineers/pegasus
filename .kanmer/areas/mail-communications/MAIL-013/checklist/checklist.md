# Checklist

- [x] Rebase after INTK-043 and confirm its unified queue/function/poison contract is the one reused.
- [x] Replace Graph-keyed inbound operational state with stable `ApprovedMailbox.Id`, activation time and cursor-scope fingerprint; remove adoption/dual identity.
- [x] Add the minimal subscription Core policy, SQL state, migration and least-privilege grants without storing clientState.
- [x] Add exact-Inbox basic `created` subscription create, PATCH renew/reauthorize and recreate operations.
- [x] Add the bounded Graph Web endpoint and publish mailbox/subscription lifecycle wakes through the unified `intake-work` queue.
- [x] Dispatch mailbox wakes and poison through the existing unified Worker functions into the shared lease/delta route.
- [x] Convert the existing Inbox timer to five-minute recovery with six-hour due maintenance; add no timer Function.
- [x] Add secret/configuration/IaC/telemetry/smoke assertions without changing capacity.
- [x] Add focused protocol, identity, lifecycle, retry, overlap, sender, composition and grant tests.
- [x] Run locked restore/build, Core, Architecture, focused changed-area tests and deployment-plan validation; record that the diagnostic full integration run was bounded.
- [x] Run and record the simplification pass; write the implementation report and PR to `dev`.
- [x] Address [[PR-067]] by preserving retained evidence and restrictive dependants through exact stable-ID migration.
- [x] Address [[PR-068]] by enforcing and proving the complete Graph webhook contract.
