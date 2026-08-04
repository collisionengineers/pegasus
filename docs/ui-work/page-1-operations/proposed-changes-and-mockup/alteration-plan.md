# Page 1 — Operations → Dashboard: alteration plan

Source: `src/Pegasus.Web/Pages/Index.cshtml`. Operator notes: `../page1.md`.
Screenshots reviewed: `page1.png`, `refresh-and-last-updated.png`, `staged-intake.png`,
`local-dashboard.png`. Governing standards: `../../ui-standards-and-review.md` (§2 vocabulary,
§4 presentation rules).

## Review

### Aesthetics

The page is cluttered: seven tiles in the first strip, five in the second, two oversized
workspace cards, and two bottom panels — fourteen containers before any real information.
The dominant visual note is grey "Unavailable" pills: nine tiles and both workspace cards are
hardcoded to the literal string `Unavailable` (`Pages/Index.cshtml:7-11`,
`const string Absent = "Unavailable"`), so a first-run user sees a wall of failure chrome on a
healthy system. The lede — *"Every current queue for the office, with the exact filter behind
each count."* — narrates the page instead of being it. The freshness banner is a full-width
container carrying a redundant "Current" badge for what is, in content terms, one timestamp
and one button. The "Staged intake artifacts" panel prints a `staging/…` blob path, a 64-char
hash and `10378983 bytes` directly onto the operator's home screen.

### Practicality

An operator opening this page wants three answers: what is in my case queues, what is happening
with e-mail, and what moved today/this week. The current grouping ("Case and intake queues")
mixes case-stage counts with e-mail counts in one strip, so none of the three questions is
answerable at a glance. The two workspace cards exist only to say *"No dashboard aggregate
exists for this route."* — a tile explaining why it has no number is a tile that should not
exist (standards §1.1). "Not ready" and "Held" show "Unavailable" where the honest answer is a
number (operator: "Not ready and held showing as unavailable instead of 0"). There is nothing
personal on the page: an Engineer signing in sees office totals but not their own assigned
reports or outstanding queries.

### Performance, design and good practice

- The "Review" tile is mislabelled: it renders `@Model.Counts.DraftReady`, the DraftReady
  **e-mail** count (`asp-route-decision="draft_ready"` links to the e-mail list), not the count
  of cases in the Review stage. A case-queue tile backed by an e-mail query is a correctness
  bug, not a styling issue.
- Nine tiles ship without backing Core queries. Shipping placeholder chrome ("Unavailable")
  instead of omitting the tile violates standards §4.2 ("Zero is zero… a tile whose query does
  not exist is not shipped") and §4.9 ("Disabled ≠ visible").
- The staged-artifacts panel leaks storage internals (blob keys, dispositions, byte counts)
  and its copy — *"Bounded inventory from the latest refresh: 0 pending · 1 failed ·
  0 orphaned · 0 unmatched"* — is reconciliation diagnostics narrated at the operator.
- The development-only "Local acceptance boundary" banner narrates test scope at whoever runs
  the app locally.

## Changes

1. **Rename the page.** Nav item and `<h1>`: "Operations" → **"Dashboard"**. `<title>` follows.
2. **Remove the lede.** Delete *"Every current queue for the office, with the exact filter
   behind each count."* — no replacement (standards §4.1).
3. **Restructure into three sections plus a role-scoped fourth:**
   - **Active cases** (was part of "Case and intake queues"): Not ready · Review · Held. Each
     tile links to the matching Queues tab.
   - **E-mail activity** (operator's name): Received today · Queries outstanding · Needs
     sorting. Each tile links to the matching Inbox filter.
   - **Today and this week**: New cases today · Sent to Engineer today · Sent to Engineer this
     week · Reports sent today · Reports sent this week.
   - **To do** (Engineer accounts only): the signed-in Engineer's assigned reports and
     outstanding e-mail queries, each row linking to its case. Absent for non-Engineer roles —
     absent, not disabled (standards §4.9).
4. **Every count renders a number.** `0` renders as `0`. The "Unavailable" placeholder pill is
   removed everywhere on this page. Genuine runtime failure of a real query uses the designed
   failure state with last-good timestamp, not a placeholder.
5. **Fix the Review tile's backing query.** It must count cases in the Review stage, not
   DraftReady e-mail receipts (see Dependencies).
6. **Remove the "Staged intake artifacts" panel** entirely. If the reconciliation inventory is
   worth keeping it belongs on a system-health/administration surface, never the dashboard.
7. **Remove the "Requests: Box and Pegasus" card.** Box File Requests are superseded
   (operator note); Pegasus request totals do not belong on the dashboard.
8. **Remove the "Email: Received and Sent" card** and its copy *"Mailbox outcomes and owned
   retries. No dashboard aggregate exists for this route."* The combined received+sent count is
   explicitly unwanted; the E-mail activity section covers the need. The Email operations
   drill-down page gains an honest entry elsewhere (standards §3.1).
9. **Shrink refresh/last-updated to a compact corner element**: small text
   "Updated 4 Aug 2026 10:43" plus an icon refresh button, top-right of the content area. The
   full-width banner and the "Current" badge are removed (standards §4.11).
10. **Drop the "Triage" and "Due case work" tiles from the first strip.** Pre-assignment work
    lives in Queues; scheduled chase work either joins "To do"/case surfaces or is not a
    headline metric. "Due case work" list stays only if its query is real, retitled and
    demoted below the metric sections.
11. **Remove the development-only "Local acceptance boundary" section** from the operator
    surface (keep the behaviour flag out of the page; local-only banners are not part of the
    designed screen).
12. **Uppercase section labels reduced to one per section** and a single H1 (standards §4.7);
    tiles keep icon + label + number, whole tile is the link.

## Dependencies

Plan only — these must exist before the tiles ship (a tile without its query is omitted, not
faked):

- **New Core count queries**: cases in Not ready, cases in Review, cases in Held (the current
  page hardcodes these to "Unavailable"; `Pages/Index.cshtml:7-11`). The current "Review" tile
  reuses the DraftReady e-mail count and must be rewired to the new case-stage count.
- **New Core count queries for the time section**: new cases today; sent to Engineer today /
  this week; reports sent today / this week.
- **E-mail activity counts**: received today; queries outstanding; needs sorting. "Needs
  sorting" exists today; "received today" and "queries outstanding" need composed queries.
- **Engineer To do query**: assigned reports and outstanding queries for the signed-in
  Engineer, plus role detection to scope the section.
- Freshness element: reuse the existing loaded-at timestamp; the stale/failed chip states from
  the design contract apply when a real query fails.

## Open questions

- Should the "Due case work" list (currently a real query, `Model.DueWork`) survive on the
  Dashboard below the metric sections, or move behind the case surfaces? The mockups omit it;
  restoring it is additive.
- Destination of the Email operations drill-down (standards proposes Dashboard drill-down with
  honest entry): needs an operator decision on where its entry point lives once the card is
  gone.
