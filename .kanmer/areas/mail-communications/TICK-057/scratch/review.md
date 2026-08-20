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

## Independent re-review — 2026-08-21 — PR #491 at 4a13def9

Reviewer: independent of the implementation.

### Changes since the needs-changes verdict

- [[PR-053]] removes the two prohibited read-only explanatory blocks and the now-unused active-label helper. The labelled native selector and its selected option remain the sole active-view presentation.
- [[PR-054]] composes the existing folder and queue parsers into one private page-boundary helper used by GET, reload, and all six exact-message POST handlers: prepare Link, prepare Unlink, final Link, final Unlink, classification correction, and folder move.
- Authenticated tests exercise unknown queue and Deleted Items plus queue across all six POST paths, proving no classification/history, provider-move, association or lease side effect. Rejected final association submissions retain their previously prepared lease, while valid queue success and uncertain move recovery preserve the queue key.

### Prior findings rechecked

- Core remains the sole destination-policy owner; Infrastructure translates its descriptor and filters the current classification before SQL count/paging.
- Unidentified replaces only the broad abstention and remains distinct from Triage.
- No schema, migration, destination store, policy copy, write framework, external call or deployment change was added.
- The 13-file PIR inventory matches the branch diff, and the original plus correction four-lens simplification records have no unapplied finding.

### CI

Replacement run 32427631319 for exact head 4a13def9 passed: changes, documentation, local-development-scripts, reference-data, unit, browser, SQL shards 1–3, and SQL integration coverage. Infrastructure was correctly skipped.

### Comments and disposition

- PR-053: fixed in PR; blocker resolved.
- PR-054: fixed in PR; blocker resolved.
- No new blocking or non-blocking finding.

### Verdict

**Pass.** Exact head 4a13def9 is approved for merge to dev. After merge, move TICK-057, PR-053 and PR-054 exactly one stage from Review to Verifying; verification remains a later merged-main gate.
