# Post-implementation report

Retained search now admits root body matches only through the retained message's displayed `BodyPlainText`; receipt search documents contribute only named attachment-content matches. A root-only canonical wrapper phrase is proved not to return a row, while existing visible body, filename, and attachment-content matches remain green.

Shared PR: https://github.com/collisionengineers/pegasus/pull/469
Implementation commit: `c0fa9a9905f2808ec1e2eb03e42dbe29cfde7ae4`

Files: `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` (SQL admission alignment), `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` (negative root-only proof). Verification: Release build succeeded with zero warnings/errors; focused remaining-blocker slice passed 25/25. No schema, store, parser, or backfill change.

## Final review-blocker follow-up — 2026-08-20

The existing receipt root projection is now route-aware and normalized once with `StaffForwardBodyCleaner`; an attached original is selected by its existing effective-sender source label. Retained SQL body admission, MessageBody evidence, and detail display use that same root. Historical receipts without a root retain cleaned display fallback but are not reconstructed into searchable data. Commit `7932d683782669e112f3d996c6914323e8ba72d4`; PR #469. Files: Core projection/process, retained contract/store, Core and persistence tests. The SQL test proves raw wrapper-only text cannot match while normalized visible text does and equals detail.

## Verification

- Release solution build passed with 0 warnings/errors.
- Core retained-mail class: 27/27 passed.
- Focused Graph/Web/SQL blocker slice: 27/27 passed.
- Complete `MailWorkspaceWebTests` plus `RetainedMailPersistenceTests`: 38/38 passed.
- Exact normalized-body SQL rerun: 1/1 passed.
- `git diff --check`: passed.

No external/cloud/mailbox write, deployment, backfill, merge, or self-review occurred.
