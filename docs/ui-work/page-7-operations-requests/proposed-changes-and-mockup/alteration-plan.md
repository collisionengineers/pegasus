# Alteration plan — Upload links and external work (was Request operations)

Exact current copy strings are quoted with file references in `../review.md`.

## Review summary

Today's page narrates its own storage model ("Bounded Box, Pegasus upload-link and
durable external-work outcomes."), leads with a superseded Box section, prints raw byte
counts and a "Limits version" integer, and forces operators to hand-operate a
concurrency state machine to revoke a link ("Enter edit mode to revoke" → "Recover edit
mode to revoke" → "Revocation is unavailable until the current edit mode expires or is
released."). The redesign makes it a Dashboard drill-down with two plain sections —
Upload links and External work — where revoking is one button plus a reason, sizes are
in MB, and the concurrency claim happens automatically behind the post.

## Changes

1. **Drop the Box file requests section.** Box File Requests are superseded per the
   operator's statement. **Flagged as a business decision to confirm** — see Open
   questions for what happens to historical Box records. Nothing Box-related renders on
   this page in either mockup.
2. **Relocate and retitle.** Dashboard drill-down; nav highlights **Dashboard**.
   Eyebrow and lede deleted. h1: "Requests" → **"Upload links"**, with "External work"
   as the page's second section. "Back to Operations" → **"Back to Dashboard"**.
3. **Cards become a table.** Upload links render as rows: Case · Principal · Status ·
   Used · Expires · Action. The eight-row `<dl>` per card disappears.
4. **Sizes in MB, limits humanised.**
   - "Byte limit 2516582 / 26214400" → **"2.4 MB of 25 MB"** (one decimal).
   - "File limit 3 / 10" → **"3 of 10 files"** (combined into one "Used" cell:
     "2.4 MB of 25 MB · 3 of 10 files").
   - "limit version unavailable" fallback → muted **"—"**.
   - **"Limits version" row deleted** — internal integer, never shown.
5. **All edit-mode vocabulary removed.** The "Edit mode" `<dl>` row, "Enter edit mode to
   revoke", "Recover edit mode to revoke", "Renew edit mode", "Leave edit mode", and
   "Revocation is unavailable until the current edit mode expires or is released." are
   all deleted. Replacement per row: one **"Revoke link"** action.
6. **Revoke is one step with a designed confirmation.** Clicking "Revoke link" expands an
   inline confirm: reason field (required) + **"Revoke link"** primary + **"Cancel"**.
   One post performs the whole operation server-side (claim, revoke, release). If the
   case is genuinely being edited by someone else at that moment, the designed failure
   state reads: **"This link's case is open for editing by someone else. Try again in a
   few minutes."** — a result of attempting, never a precondition the operator manages.
7. **State chips.** "Active" = navy chip, "Pending" = amber, "Expired"/"Revoked" =
   neutral, "Failed" = red, "Completed" = green, "Exhausted" → relabelled **"Limit
   reached"** (amber), "Unknown external" → **"Unknown"** (neutral).
8. **External work becomes a table.** Rows: Case · Work · Status · Attempts · Last
   activity · Action. "Work kind" raw value → human label map (e.g. "Report delivery").
   Attempts shown as "3 attempts". Failed rows get a plain-sentence failure line and a
   small **"Retry"** action with the same inline confirm pattern as page 6.
9. **Failure codes never render.** Raw `FailureCode` tokens → operator sentences from a
   label map; the recorded human `FailureReason` is shown when present.
10. **Empty states.** One muted line each: **"No upload links have been issued."** /
    **"No external work is recorded."**
11. **Timestamps** move from `u` format to local **"04 Aug 2026 16:24"** with proper
    `<time datetime>` attributes.
12. **Truncation notice** → **"Showing the latest 50 items."**

## Dependencies (backend needs, plan only)

- A composite revoke command: one Web handler that claims the case edit token, revokes
  the link, and releases — surfacing "someone else is editing" only as a post-attempt
  result. Core already exposes the constituent operations; the composition is new
  (Web/application layer, no policy change).
- Label maps for external-work kinds and failure codes (Web-side, same pattern as
  existing state label maps in `Requests.cshtml.cs`).
- MB formatting helper (bytes → one-decimal MB) shared with the Inbox/Upload surfaces.
- Dashboard entry card for this drill-down (real counts — new Core queries, flagged not
  assumed).
- Business confirmation that Box file requests are retired (change 1).

## Open questions

1. **Box records**: when the section is dropped, do historical Box file requests need a
   read-only home (e.g. case history) or can they disappear from the UI entirely?
2. Should revoked/expired links age off this view after a period, or is the capped
   latest-N list sufficient?
3. Is external-work retry an operator action or administrator-only? (Currently any staff
   role can post it.)
4. Does anyone need the link URL itself resurfacing here (copy button), or is issuing
   handled entirely from the case workspace?
