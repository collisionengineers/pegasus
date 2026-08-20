# Plan

Estimated diff: about 15 production lines and 45–70 focused test lines across three existing files.

1. In `ResolveDeletedItemsFolderAsync`, reject a non-object successful root with the existing `InvalidDataException`.
2. In `ReadFolderMessagesAsync`, reject a non-object root or missing/non-array `value`, and reject any present next-link that is not a valid absolute URI; retain existing exact host/path validation.
3. Extend the existing Graph invalid-response theory for every exact shape and add authenticated Web evidence for envelope and next-link categories.
4. Run Release build, focused Graph/Web tests, diff check, and the four-lens pass; update shared PIR/inventory.

Reuse: existing `InvalidDataException`, outer unavailable mapping, Graph HTTP fake, authenticated Web host, fixed bounds, and exact URI validator. No catch broadening or abstraction.

## Simplification pass — 2026-08-20

- **Reuse:** Reused the existing `InvalidDataException` unavailable path, exact URI validator, fake-HTTP Graph test, authenticated Web host, and approved-mailbox estate.
- **Simplification:** Added direct shape guards at the two existing parse sites. The outer catch remains unchanged; no response wrapper, general JSON validator, retry, or exception hierarchy was added.
- **Efficiency:** Invalid envelopes stop before message enumeration or MIME reads. Valid request counts, global ordering, and the fixed 100-message bound are unchanged.
- **Altitude:** Provider response validation remains inside `GraphMailClient`; Deleted source policy and Web presentation remain unchanged. The test-only credential/handler are the external-boundary fakes required by the real authenticated caller. No unapplied findings.
