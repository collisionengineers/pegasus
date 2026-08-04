# Alteration plan — E-mail activity drill-down (was Email operations)

Exact current copy strings are quoted with file references in `../review.md`; this plan
masks the banned vocabulary word as `[banned]` where an old string contains it.

## Review summary

The current page is an orphaned developer console: a "Bounded approved-mailbox Received
and Sent processing outcomes." lede, cards headed "Received · Failed", links labelled by
mechanism ("Open [banned] receipt", "Open [banned] queue"), raw failure codes, machine
timestamps, and a retry button with no designed result state. Its default appearance is
two empty apology panels. Under the new IA it becomes the Dashboard's **E-mail activity**
drill-down: one dense table per direction, human copy, destination-labelled links, and a
retry that confirms before it fires and shows what it did.

## Changes

1. **Relocate and retitle.** Page becomes a Dashboard drill-down reached from the
   E-mail activity section. Nav highlights **Dashboard**. Eyebrow and lede removed.
   h1: "Email" → **"E-mail activity"**. "Back to Operations" → **"Back to Dashboard"**.
2. **Cards become tables.** The per-item `article` + `<dl>` stacks become one table per
   section (Received, Sent): columns Mailbox · Status · Last activity · Where it went ·
   Action. 40px-class rows; five failures are five rows, not five cards.
3. **Section headings stay plain.** "RECEIVED"/"SENT" uppercase micro-labels → sentence-case
   `<h2>` "Received" and "Sent"; the per-item heading "Received · Failed" is deleted —
   direction is the section, state is a chip in the row.
4. **Links labelled by destination.**
   - "Open [banned] receipt" → **"Open in Inbox"**
   - "Open [banned] queue" → **"Open Inbox"**
   - "Open Triage" → **"Open queue item"** (Queues is the new surface name)
   - "Open Case X" → **"Open case X"** (kept, lowercase "case")
   - Sent fallback "Not linked" → muted **"—"** (an em dash, not a sentence).
5. **State chips.** `StateLabel` values render as chips: Pending = amber, Failed = red,
   Succeeded = green, Unknown = neutral. Never colour-only; the word is always present.
6. **Retry gets a designed confirmation.** "Retry Received processing" → small **"Retry"**
   action per failed row. First click swaps the action cell to an inline confirm —
   "Retry processing for this mailbox?" with **Retry** / **Cancel**. After posting, the row
   shows a green **"Retry scheduled"** chip and the action disappears (state matches
   the existing replay-safe handler; "already scheduled" renders the same chip).
7. **Failure copy becomes human.** The raw `FailureCode` block → one plain sentence from
   a failure-label map, shown as a second line under the mailbox ("The last message from
   this mailbox could not be processed."). Codes never reach the page.
8. **Timestamps.** `u`-format UTC ("2026-08-04 15:24:11Z") → local **"04 Aug 2026 16:24"**,
   keeping the `<time datetime>` ISO attribute.
9. **Empty states.** "No Received processing outcomes are recorded." → **"Nothing has
   been received recently."**; "No Sent processing outcomes are recorded." →
   **"Nothing has been sent recently."** One line, no panel-in-panel.
10. **Truncation notice.** "Showing the latest N Received outcomes." → **"Showing the
    latest N items."** once, under the table it applies to.
11. **Mailbox fallback.** "Not recorded" → muted **"Mailbox not recorded"** styled as
    secondary text, not masquerading as a mailbox name.
12. **Status messages** (post-retry TempData) keep their current plain wording — they are
    already human — but render in the compact corner status pattern, not a full-width card.

## Dependencies (backend needs, plan only)

- A failure-code → operator-sentence label map (Web-side, pattern already exists for
  state labels in `Email.cshtml.cs:94`).
- Dashboard E-mail activity section needs a real entry card (counts: received today /
  failed) so this drill-down is discoverable — counts are new Core queries flagged in the
  root standards doc, not assumed here.
- No Core changes for retry: the existing `RetryMailboxProcessing` handler, expected
  failure-code/due-time guards, and replay behaviour are reused as-is.
- Route stays `/Operations/Email` or moves under Dashboard; either way the old URL
  redirects.

## Open questions

1. Do operators ever need Sent retries, or is Sent retry an administrator action that
   should not render for the User role?
2. Should Received and Sent merge into one table with a Direction column once both are
   chips? (Two tables kept in this plan because the fallback links differ.)
3. The capped list cannot answer "what failed last week" — is a date filter or "Show
   older" needed, or is the cap genuinely sufficient for the office's volume?
