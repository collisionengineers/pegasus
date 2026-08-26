# Open questions — MAIL-013

None blocking implementation.

Settled decisions:

- INTK-043 merges first and MAIL-013 reuses its unified warm queue/function/poison route.
- ADR-0024 stable mailbox identity is implemented in this ticket because Graph wake depends on it; the obsolete Graph-keyed path is removed.
- The existing Inbox timer becomes five-minute recovery and also runs due six-hour subscription maintenance; no second timer Function is added.
- MAIL-013 makes no capacity change.
- Graph delivery and Pegasus processing latency are reported separately; five seconds is not presented as a Microsoft Graph guarantee.
