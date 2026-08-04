# Alteration plan — E-mail activity drill-down (was Email operations) — SUPERSEDED

> **This page is merged into Inbox.** Operator decision 2026-08-04: pages 2 and 6 overlap and
> become one surface. The live plan is
> [`../../page-2-intake/proposed-changes-and-mockup/alteration-plan.md`](../../page-2-intake/proposed-changes-and-mockup/alteration-plan.md),
> with the merged wireframe and mockups alongside it. The review findings behind this plan are
> still valid and are retained in [`../review.md`](../review.md); they have been folded into the
> merged plan's Review section.

## Why it merged

Pages 2 and 6 answer one operator question — *what came in, and did it work?* — on two screens
that never reference each other. Page 6 was orphaned: its only route in was a Dashboard card
that itself read "Unavailable", so an operator could not discover the one screen that would tell
them this morning's instruction never arrived. Splitting *arrival* from *did arrival work* is
what made it an orphan.

## Where each feature went

Nothing was dropped. In the merged Inbox:

| Page 6 feature | Where it lives now |
|---|---|
| Received / Sent sections | Direction tabs at the top of Inbox (merged plan, change 3) |
| Mailbox identity, with "Mailbox not recorded" fallback | A Mailbox column on every row (change 5) |
| Status chips (Pending / Failed / Succeeded / Unknown) | Merged into one state scale with the business states; "Succeeded" retires — a succeeded item is described by what it became (change 6) |
| Failure sentence from a label map; no raw `FailureCode` | Second line under the subject on Failed rows (change 7) |
| Retry with inline confirm, then "Retry scheduled" | Row action on Failed rows; same replay-safe handler and `expectedFailureCode`/`expectedDueAtUtc` guards (change 7) |
| Destination-labelled links ("Open case 26001") | Kept throughout both tabs (changes 9, 13) |
| Local timestamps keeping the ISO `<time datetime>` | Applied to both tabs (change 11) |
| Truncation notice | Replaced by pagination, which also fixes page 6's inability to answer "what failed last week" (change 12) |
| Empty states in business language | Per filter and per tab (changes 8, 9) |
| Cards → tables, one direction parameter instead of two copy-paste blocks | The merge itself delivers this (change 3) |

The one new affordance the merge creates is a **Failed filter chip** on the Received tab: the
question page 6 existed to answer, now one click from a list the operator already has open.

Page 6's three open questions carry over as merged-plan open questions 3–5.

## Route

`/Operations/Email` redirects to Inbox with the Received tab and the Failed filter applied, so
existing links land on what they were pointing at.
