## Independent review — 2026-08-20 — PR #491 at 4b851ded

Reviewer: independent of the implementation.

### Changes

- `MailOperationalDestinationPolicy.cs` adds one Core-owned aggregate query descriptor and makes category mapping consume that descriptor.
- `RetainedMail.cs` adds mutually exclusive destination/detail list scope and current classification/destination row projection.
- `EfRetainedMailboxMessageStore.cs` translates the Core descriptor against the current persisted classification before SQL count/paging and maps current classification for page rows.
- `Mail/Index.cshtml(.cs)` adds canonical aggregate/detail parsing and one native selector, preserves the key through list navigation, and shows row classification/destination.
- `Mail/Message.cshtml(.cs)` carries queue context through list return, sections and existing action routes and detects a current message outside the originating queue.
- Core, SQL and authenticated Web tests cover policy agreement, scope validation, distinct Unidentified/Triage, current correction, SQL totals/pages and GET list/detail context.
- `docs/capabilities.md` records the implemented UI-14 slice; `docs/design/README.md` replaces only one broad intake use of the superseded phrase while retaining Triage-specific uses.

The PIR's 13-path inventory exactly matches the PR diff and its no-schema/no-store/no-write-framework claims hold.

### Comments and disposition

1. **Blocking — repository UI simplicity rule.** The new `field-hint` (“Current view”) and queue-specific `empty-state` paragraph are explanatory copy/panels in a read-only view. The selected native option already presents the active value. Disposition: filed as [[PR-053]], which blocks [[TICK-057]].
2. **Blocking — invalid action context is not fail-closed before mutation.** GET and reload parse/reject unknown queue keys and Deleted Items plus queue, but successful exact-message POST handlers invoke classification, folder-move and Case-association/lease operations before the parser runs. A forged invalid context can mutate and only fail on redirect. Disposition: filed as [[PR-054]], which blocks [[TICK-057]].
3. **Non-blocking/pass — ownership and query shape.** The descriptor is justified by the Core→Infrastructure boundary; there is one policy owner, no destination column/migration/store, and EF composes the current decision predicate before `CountAsync`/`Skip`/`Take`. Disposition: no change.
4. **Non-blocking/pass — terminology.** Unidentified replaces only the broad abstention; Triage remains a separate classification/workflow/view. Remaining “Needs sorting” occurrences reviewed in the changed documentation are Triage/intake-specific or unrelated allocations, not a UI-14 taxonomy copy. Disposition: no change.
5. **Non-blocking/pass — report, plan and simplification.** The plan is proportional, names existing seams, records all four lenses with applied findings, and the implementation stays within its declared read-model/UI scope. Disposition: no change.

### Verdict

**Needs changes.** Do not merge at 4b851ded. Re-review after PR-053 and PR-054 land on PR #491 and replacement full CI is green.

CI state at needs-changes handoff: on run 32425757561 for reviewed head 4b851ded, changes, documentation, local-development-scripts, reference-data and unit were green; infrastructure was skipped; three SQL shards and browser were still running. The run was not treated as merge evidence because PR-053/PR-054 require a replacement head and replacement full CI.
