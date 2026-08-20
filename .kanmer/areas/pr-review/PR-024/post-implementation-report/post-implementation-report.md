# Post-implementation report

Retained search now admits root body matches only through the retained message's displayed `BodyPlainText`; receipt search documents contribute only named attachment-content matches. A root-only canonical wrapper phrase is proved not to return a row, while existing visible body, filename, and attachment-content matches remain green.

Shared PR: https://github.com/collisionengineers/pegasus/pull/469
Implementation commit: `c0fa9a99a3f9a1b1082591a32e84687a44076210`

Files: `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` (SQL admission alignment), `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` (negative root-only proof). Verification: Release build succeeded with zero warnings/errors; focused remaining-blocker slice passed 25/25. No schema, store, parser, or backfill change.
