---
kind: review-attestation
pr: "641"
head_sha: "c6842a8c3a36fe806a3103d067fef207d22651d3"
verdict: pass
reviewer: "claude-code/20260901T215000Z-claude-controller/reviewer-a1"
independent: true
plan_hash: "74a5ddf0a5ef2ece"
ticket_updated: "2026-09-02T01:51:24.337Z"
board_sha: "dd005fa6bbfaf001fde7de5fc766dff8a977d7bd"
expected_reviewers:
  - "claude-code/20260901T215000Z-claude-controller/reviewer-a1"
threads_snapshot:
  - source: github
    id: "PRRT_kwDOThBrk86eEwcz"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-001
  - source: github
    id: "PRRT_kwDOThBrk86eEwc4"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-002
  - source: github
    id: "PRRT_kwDOThBrk86eEwc7"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-003
  - source: github
    id: "IC_kwDOThBrk88AAAABR2XTyg"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-010
findings:
  - id: F-001
    severity: minor
    summary: "A sparse delta entry that also omits parentFolderId is rejected by GraphMailClient.ReadDeltaAsync's exact-folder check before it can reach the new skip, so that shape would still stall the cursor."
    disposition: accepted-risk
    reason: "Out of this ticket's scope and unobserved: the 2026-09-01 incident produced only InvalidDataException, never UnauthorizedAccessException, so parentFolderId was present in every observed payload. The exact-folder assertion is a security boundary and must not be loosened on speculation. Parked in open-questions with a named reopen trigger, and dispositioned publicly by the owner on the thread."
  - id: F-002
    severity: major
    summary: "The first commit skipped a present-but-unparseable receivedDateTime as if it were absent, which would advance the cursor over a corrupt Graph response and discard the message permanently."
    disposition: fixed
    reason: "Fixed in c6842a8c by GraphDeltaItem.ReceivedDateTimePresent (raw TryGetProperty, independent of parse success) and pinned by InboxThrowsOnAPresentButUnparseableReceivedDateTimeRatherThanSkipping. Verified at the head: GraphApprovedSources.cs lines 637-649."
  - id: F-003
    severity: minor
    summary: "A message moved into the approved Inbox could arrive sparse and be silently skipped, because nothing here proves the item was already retained."
    disposition: accepted-risk
    reason: "Unobserved shape; Graph re-emits a full representation for a genuinely new resource, and the alternative (a supplemental hydration call) is exactly the MIME fetch this ticket forbids and a reliability/cost design decision, not a mechanical correction. Parked in open-questions with a reopen trigger and dispositioned publicly by the owner on the thread. Strictly better than the pre-fix behaviour, which stalled the whole mailbox."
  - id: F-004
    severity: note
    summary: "The PR body's Test plan names only the first of the two new tests, and its recorded local test-command result line predates commit c6842a8c."
    disposition: accepted-risk
    reason: "Deliberate: the plan required every body line except the footer to stay byte-identical, and the post-implementation report states the constraint. No false green is asserted, because the authoritative evidence is CI run 33525322197 at this exact head, not the body text."
  - id: F-005
    severity: note
    summary: "Both commit trailers still read Kanmer: MAIL-029, and the branch keeps its mail-029 slug."
    disposition: accepted-risk
    reason: "Correcting them would need a history rewrite the repository rules forbid and would discard a fully green check run. The board prs/commits fields plus the PR title and body footer, both now MAIL-033, carry the correction; verification reads the PR body footer on the merge commit."
  - id: F-006
    severity: minor
    summary: "ParseItem probes receivedDateTime twice - once through OptionalInstant/OptionalString and once for the new presence flag - encoding a tri-state (absent / unparseable / valid) as two record members."
    disposition: rejected-with-reason
    reason: "Confirmed independently and agreed with the implementer's simplification disposition: this is a zero-diff adoption where any code change is a deviation, the second probe is one dictionary lookup on an already-materialised JsonElement, and the shared helpers have six other callers the current form leaves untouched. Not ticket-worthy on its own."
  - id: F-007
    severity: minor
    summary: "An explicit JSON receivedDateTime null is classified present-but-unparseable, because TryGetProperty is true for a JSON null, so such a payload throws instead of being skipped and would stall the cursor exactly as before."
    disposition: accepted-risk
    reason: "Verified at GraphApprovedSources.cs line 565 and lines 641-649. receivedDateTime is non-nullable in the Graph message schema and a sparse delta representation omits rather than nulls; the incident payloads are no evidence either way. Widening the skip on speculation would weaken the negative test. Parked in open-questions by the implementer with a named reopen trigger (a mailbox stalling on a literal null)."
  - id: F-008
    severity: note
    summary: "GraphDeltaItem now ends in two adjacent positional bools (Removed, ReceivedDateTimePresent), which a future construction site could transpose without a compiler error."
    disposition: accepted-risk
    reason: "ParseItem is the only construction site in src and tests at this head (git grep finds one match, the declaration itself). Trivial risk, and no change is warranted on a zero-diff adoption."
  - id: F-009
    severity: note
    summary: "The three Codex review threads remain unresolved on GitHub."
    disposition: accepted-risk
    reason: "Each already carries a public owner disposition on the thread itself, so the record survives outside the board; all three are outdated against the reviewed head, having been filed on the superseded commit 712bfcf3. mergeStateStatus is CLEAN, so conversation resolution is not a required gate on this repository, and this dispatch grants the reviewer no PR write beyond the merge command."
  - id: F-010
    severity: note
    summary: "Codex posted an automated review-summary comment on the PR."
    disposition: rejected-with-reason
    reason: "A status summary with no actionable content; Codex is not an expected reviewer and its presence or absence gates nothing."
---

# Review attestation — MAIL-033, PR #641 at c6842a8c

Independent review by `claude-code/20260901T215000Z-claude-controller/reviewer-a1`. The
implementer of record is `claude-code/20260901T215000Z-claude-controller/implementer-a1`
(the ticket's `assignee`); the identities differ, so `independent: true` is truthful. No
repository line, PR branch or proof document was written by this review.

## What was reviewed

`gh pr diff 641` at head `c6842a8c3a36fe806a3103d067fef207d22651d3` — +72 / -3 across
`src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` and
`tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs`. Base `dev`,
`mergeStateStatus: CLEAN`. Also read at that head, from the shared object store rather than
from a checkout: `GraphApprovedInboxSource.ReadAsync`, `GraphMailClient.ReadDeltaAsync`,
`ParseItem`, `GraphDeltaItem`, and `MailboxIntake.PollOneAsync` / `ValidatePage`.

Board inputs: `plan`@`74a5ddf0a5ef2ece`, `files`@`c4e8f725d16b0765`,
`checklist`@`40bd81aaaa7ea1f6`, `post-implementation-report`@`e04eede6877ed0f9`,
`open-questions`@`568172e0a56fb947`, EPIC-011 `context.md`, and
`docs/frd/frd-08-email-mailbox-and-background-processing.md` lines 284-345 on `origin/dev`.

## The three review questions

**Did the plan miss anything the ticket implies?** No material gap. The ticket's four
Verification claims each map to a plan step with a named piece of evidence, the plan
correctly classifies the work as a zero-line adoption and declares any code change a
reportable deviation, and it routes the "rerun the cancelled SQL integration shard" clause
to the test runner and to CI rather than to the implementer. Two small omissions, both
recorded above and neither blocking: the plan never asked anyone to reconcile the PR body's
Test plan with the second commit (F-004), and it never named the three open Codex threads as
work owed by anyone (F-009).

**Did the implementation miss anything in the plan?** No. Steps 1-9 all landed, and the
plan's behavioural claims were re-derived here rather than taken on trust. The one deviation
is honest and recorded: `gh pr edit` failed for a missing `read:project` scope, so the
identical two-field edit went through `gh api -X PATCH .../pulls/641` (ASSUMPTION 1 in
`open-questions`). Confirmed at the head: the title now ends `(MAIL-033)` and the body's
trailing footer reads `Kanmer: MAIL-033`. The board carries both commit SHAs and
`prs: ["641"]`. The tree was clean and nothing was pushed, so the head the checks ran against
is the head being merged.

**Was the simplification pass honest?** Yes. It ran over the real diff at this exact head,
covered all four lenses, applied nothing, and gave each unapplied finding a reason rather than
a silence. Spot-checked independently: F1's duplicate `receivedDateTime` probe is real
(`OptionalInstant` at line 558 reaches `OptionalString`'s `TryGetProperty`, and the new
positional argument probes again at line 565) — carried here as F-006. F2 is genuinely a wash.
The efficiency claim holds: a skipped entry no longer performs its MIME `GET .../$value`, so
the change is net-negative work. Most tellingly, the pass volunteered F3 — a correctness
observation against its own diff, that an explicit JSON `null` throws rather than skipping —
and escalated it to `open-questions` instead of burying it. That is the behaviour the honesty
question exists to detect. It is carried here as F-007.

## Acceptance checks, verified at the head

1. **Sparse entry skipped, no MIME fetch.** The `continue` sits at `GraphApprovedSources.cs`
   line 648, `client.ReadMimeAsync` at line 651, so the skip precedes every MIME fetch.
   `InboxSkipsASparseDeltaItemMissingReceivedDateTimeWithoutFetchingMime` asserts the absence
   of a request path ending `/$value`, not merely the absence of a throw.
2. **Cursor advances exactly once and replay is idempotent.** `processed++` runs at the top of
   the loop, before both the `Removed` skip and the new one, and the page cursor is computed
   from `consumed = cursor.SkipCount + available.Length` — independent of how many entries were
   skipped, so a skip is cursor-neutral. `MailboxIntake.PollOneAsync` persists
   `page.NextCursor` through `pollStore.CompleteAsync` after the page loop, and `ValidatePage`
   rejects a blank cursor but accepts an empty `Messages` list, which is exactly what a
   fully-skipped page returns.
3. **Ordinary, removal and change behaviour retained.** The `Removed` skip, the exact-folder
   `UnauthorizedAccessException` in `ReadDeltaAsync`, the Deleted Items `InvalidDataException`
   and `GraphApprovedSentSource` all lie outside the diff. The skip runs strictly after
   `ReadDeltaAsync`'s folder validation, so no authorization boundary is bypassed or reordered,
   and the new exception message carries no mailbox identifier or other secret.
4. **Required checks green at this head.** GitHub Actions run `33525322197`: `unit`, `browser`,
   `sql-integration (1)(2)(3)`, `sql-integration-coverage`, `test-ui`, `changes`,
   `documentation`, `local-development-scripts` and `reference-data` all SUCCESS;
   `infrastructure` SKIPPED (path-skipped — no infrastructure file in the diff). No check is
   pending, red or absent.

**Governing docs.** FRD-08 lines 284-345 were read directly and carry verbatim the sentences
the plan cites: the per-mailbox durable cursor "so one mailbox's failure or backlog never
affects another", the Worker as "the sole owner of the mailbox lease, cursor/delta read", the
requirement to "maintain a durable cursor/checkpoint and idempotent occurrence processing",
and the precedent that mail received before activation "advances the cursor but is not
retained". The pre-fix throw violated the first three; the skip follows the fourth. EPIC-011
D22 (fixed 15-minute mail freshness, no backfill) makes a stalled cursor an operator-visible
freshness and health defect. No document change and no ADR is owed: tolerating Microsoft's
documented delta contract is conformance to an external contract, not a Pegasus design choice.

**Local rail evidence.** `1-restore`, `2-build`, `3-core-tests` (1185 passed) and
`4-architecture-tests` (100 passed) PASS; `5-sql-integration` INCONCLUSIVE — 710 failures, all
on SQL Server LocalDB error 52, a missing workstation prerequisite rather than a code signal.
`ProductionGraphSourceTests` appears nowhere among those failures, and 395 of that project's
facts passed locally, which is where the two new SQL-free facts sit. The authoritative
evidence for that lane remains the green CI `sql-integration` shards at this exact head.

## Residual risk

Three unconfirmed Graph payload shapes remain tolerated rather than handled: a sparse entry
that also omits `parentFolderId` (F-001), a message moved into the Inbox arriving sparse
(F-003), and an explicit `"receivedDateTime": null` (F-007). Each is parked in
`open-questions` with a concrete reopen trigger, and each is strictly better than the pre-fix
behaviour, which stalled the mailbox in all three cases. The MAIL-029 commit trailers and
branch slug remain an accepted, documented mismatch (F-005). No blocker and no open major
finding: F-002, the only major, was fixed in `c6842a8c` and is pinned by a test.
