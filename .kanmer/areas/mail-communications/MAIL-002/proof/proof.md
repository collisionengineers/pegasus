# Proof — MAIL-002

Type: visual + command-log. Released in **release 14** (`d91fd7d7…`, PR #451), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Live (signed-in browser pass): `/Administration/Mailboxes` shows Address / worded Route scope / Approved chip / Polling in London time — **no mailbox or folder identifiers anywhere**, for any role; "Add an approved address" takes an email address alone; the identifier inputs, Version column, and narration are gone. Absence pinned by `ApprovedMailboxAdministrationWebTests`.
- Backend: `IResolveApprovedMailboxIdentity` port with `GraphApprovedMailboxResolver` (fail-closed null → "The address could not be found in the mail system."); `Graph__BaseUri` supplied to the Web container (bicep) and required by the Production host — verified in the deployed release.
- Open production limb (operator action): the Web managed identity still needs Graph `User.Read.All` + `Mail.Read` application roles with admin consent before a live add resolves; until then adds fail closed with the on-page message. Flagged in the release report.
- Full transcript: DELIV-013 scratch.
