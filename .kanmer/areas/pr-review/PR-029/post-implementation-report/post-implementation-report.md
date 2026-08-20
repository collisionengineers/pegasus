# Post-implementation report

The existing retained detail query accepts the normalized active term and reuses its current match mapper to populate `Summary.Matches`. Both GET and correction reload use one outside-scope predicate that now includes a nonmatching active search. Authenticated Web evidence opens matching/nonmatching members of one retained thread, preserves `search=needle`, and renders the existing outside-view state only for the nonmatching member.

Commit: `7932d683782669e112f3d996c6914323e8ba72d4`; PR: https://github.com/collisionengineers/pegasus/pull/469. Files: `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, `Message.cshtml.cs`, `MailWorkspaceWebTests.cs`. No second membership service or query owner.

## Verification

- Release solution build passed with 0 warnings/errors.
- Core retained-mail class: 27/27 passed.
- Focused Graph/Web/SQL blocker slice: 27/27 passed.
- Complete `MailWorkspaceWebTests` plus `RetainedMailPersistenceTests`: 38/38 passed.
- Exact normalized-body SQL rerun: 1/1 passed.
- `git diff --check`: passed.

No external/cloud/mailbox write, deployment, backfill, merge, or self-review occurred.
