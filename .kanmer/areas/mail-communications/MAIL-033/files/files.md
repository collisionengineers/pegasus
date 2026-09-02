# Files — MAIL-033

Surveyed 2026-09-02 against PR #641 head `c6842a8c3a36fe806a3103d067fef207d22651d3`
(branch `task/mail-029-graph-received-datetime`, base `dev`, 2 ahead / 0 behind
`origin/dev`). This is an adoption: both files below are **already changed on that
branch**; the expected further change is nil.

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | `GraphApprovedInboxSource.ReadAsync` (line ~603): the sparse-item skip now sits immediately after the existing `item.Removed` skip in the same page loop, and `item.ReceivedAtUtc.Value` replaces the inline `?? throw`. `GraphDeltaItem` (line ~1108) gains `ReceivedDateTimePresent`, set in `ParseItem` (line ~565) from raw `TryGetProperty("receivedDateTime", out _)`, so a present-but-unparseable value still throws `InvalidDataException`. What could break: the page cursor arithmetic (`processed`, `consumed`, `pageCursor`) is untouched, so a mistake here would either double-advance or stall the cursor. +19 / −3. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Two added facts on the existing `ProductionGraphSourceTests` class (no traits, so they run in the `Category!=Corpus&Category!=Browser` integration shard): `InboxSkipsASparseDeltaItemMissingReceivedDateTimeWithoutFetchingMime` (item excluded, no `/$value` MIME request, cursor still the delta path) and `InboxThrowsOnAPresentButUnparseableReceivedDateTimeRatherThanSkipping` (the negative case). +53 / −0. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | The only production consumer. `PollOneAsync` (line ~409) calls `inboxSource.ReadAsync`, then `AdvanceAsync(message.NextCursor)` per handled message and `CompleteAsync(page.NextCursor)` **after** the loop — this is why "the delta link is the only cursor owner and persists only after the page is handled consistently" holds without a code change. `ValidatePage` (line ~661) rejects a blank `page.NextCursor` but accepts an **empty** `Messages` list, which is exactly what a skipped sparse page returns. The activation-time branch (line ~420) already advances the cursor for a message that is deliberately not retained — the precedent the skip mirrors. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` lines 264–295 (`GraphMailClient.ReadDeltaAsync`) | Every non-removed delta item must match `approvedFolderId` exactly or `UnauthorizedAccessException` is thrown. A sparse entry that also omitted `parentFolderId` would stall here instead — a sibling risk this ticket does not own (see Out of scope). |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` lines 397–435 (`ReadFolderMessagesAsync`) | The Deleted Items read uses a full `$select` on a message list, not a delta, so a null received time there really is a fault. Its unconditional `InvalidDataException` is correct and must stay. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` lines 933–990 (`GraphApprovedSentSource.ReadAsync`) | Shares `ParseItem` and the identical cursor arithmetic but never reads `ReceivedAtUtc`, so the new record member is inert there. Confirms the change adds no failure path to Sent polling. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` lines 284–345 | The governing text: per-mailbox durable cursor, "one mailbox's failure or backlog never affects another", the Worker as sole owner of "the mailbox lease, cursor/delta read", "maintain a durable cursor/checkpoint and idempotent occurrence processing", and mail before activation "advances the cursor but is not retained". |
| `AGENTS.md` (Commands, ~line 155) and `docs/runbook.md` lines 306–331 | Canonical restore/build/test and the focused shard forms; the integration pair is a complement, so the new tests belong to `Category!=Corpus&Category!=Browser`. |
| EPIC-011 `context.md` §1.3 and D22 | Mail freshness is a fixed 15 minutes with no backfill: a stalled cursor is a visible freshness/health defect, which is the operator-facing meaning of the incident. |

## Ripple effects

- No further repository file. No Razor page changed, so no `docs/design/test-ui/` snapshot or catalogue regeneration.
- PR #641 metadata: title and the trailing `Kanmer:` footer line still say MAIL-029.
- Board: MAIL-033 needs `commits` and `prs: ["641"]`; the plan needs its dated `## Simplification pass` block; a `post-implementation-report` is the `enter-review` gate.
- Commit message bodies keep `Kanmer: MAIL-029` — history is not rewritten (no rebase, no amend); the PR body and title carry the correction.

## Out of scope

- `src/Pegasus.Core/Intake/MailboxIntake.cs` — the consumer already behaves correctly; touching it would be scope expansion.
- The Deleted Items `receivedDateTime` throw (line ~431) and `GraphApprovedSentSource` — deliberately unchanged.
- A sparse delta entry that omits `parentFolderId` (would raise `UnauthorizedAccessException` in `ReadDeltaAsync`) and a moved-in message arriving sparse and being skipped — both unconfirmed sibling risks, parked in `open-questions`, not fixed here.
- MAIL-029's real subject (missing Inbox attachment columns) — a different ticket that keeps its meaning.
- Rewriting the two existing commits to change their `Kanmer:` trailers.
