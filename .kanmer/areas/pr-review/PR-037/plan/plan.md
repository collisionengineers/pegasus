# Plan

Estimated diff: about 15 production lines and 45–70 focused test lines across three existing files.

1. In `ResolveDeletedItemsFolderAsync`, reject a non-object successful root with the existing `InvalidDataException`.
2. In `ReadFolderMessagesAsync`, reject a non-object root or missing/non-array `value`, and reject any present next-link that is not a valid absolute URI; retain existing exact host/path validation.
3. Extend the existing Graph invalid-response theory for every exact shape and add authenticated Web evidence for envelope and next-link categories.
4. Run Release build, focused Graph/Web tests, diff check, and the four-lens pass; update shared PIR/inventory.

Reuse: existing `InvalidDataException`, outer unavailable mapping, Graph HTTP fake, authenticated Web host, fixed bounds, and exact URI validator. No catch broadening or abstraction.
