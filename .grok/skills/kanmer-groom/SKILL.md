---
name: kanmer-groom
description: Groom and triage a Kanmer board by actually fixing it — dedupe near-duplicate tickets, fill in missing areas and profiles, split oversized tickets, archive dead ones, chase stale taken tickets, and repair off-board statuses. Use when the user says "groom the backlog", "tidy the board", "triage", "clean up the tickets". DO NOT USE FOR reporting problems without changing anything (kanmer-report).
---

# Grooming a Kanmer board

Standup *flags* board problems; groom *fixes* them. But the board is the
human's shared workspace, so grooming is propose-then-apply: one batch of
findings, their sign-off, then the edits — never a silent reshuffle of
someone else's backlog.

## 1. Scan

`get_status` (warnings count), `list_board`, `list_items include_archived:
true`, and `get_activity` for recent movement. Look for:

- **Near-duplicates** — `search_items` on suspicious title pairs; two
  tickets describing one unit of work.
- **Missing fields** — no `area` on a board that has areas; no `profile`, so
  the ticket silently inherits its area's default when a different one fits
  where the user uses them.
- **Doc-gate debt** — tickets with `docs_todo: true` whose governing PRD/FRD/ADR
  was never linked, or missing a doc a later stage requires (`get_doc_gates`
  names the gap). These silently stall at the next gate; hand them to
  `kanmer-docs` to link/write the doc, or `link_doc` an existing one.
- **Oversized tickets** — bodies describing several units of work, or
  checklists that sprawl past one deliverable.
- **Dead tickets** — untouched for months, superseded, or describing code
  that no longer exists.
- **Stale takes** — `taken` with no activity since (>3 days), branch/
  worktree pointing at work nobody is doing.
- **Off-board statuses** and file warnings from `list_items` — data
  problems the tools can repair via `move_item` / `update_item`. A move crosses
  **at most one gated boundary**, so a ticket parked several stages from where it
  belongs is walked there one stage at a time; a single corrective jump is
  refused even when every document exists. That refusal is the rule working, not
  a broken ticket, and there is no way around it: `update_item status` runs the
  same gate check as `move_item`.

## 2. Propose

One message: each finding, the proposed fix, grouped by kind. The user's
repo, their priorities — especially for archiving and splitting.

## 3. Apply

- **Duplicates**: keep the better ticket, merge anything unique from the
  other into it (body or docs), link them, archive the loser. Don't delete.
- **Splits**: file the new tickets (`kanmer-tickets` conventions), move the
  relevant checklist items over, link with `rel: "blocks"` where order
  matters, shrink the original.
- **Fields**: `update_item` with `expected_updated` — grooming touches many
  items, and a conflict means someone else is editing; re-read, don't
  clobber.
- **Stale takes**: ask the owner (or the user) before releasing someone
  else's take; a release without a note strands the worktree — record the
  branch/worktree in the checklist progress notes first, per
  `kanmer-execute`'s pausing rules.
- **Archive** with `archived: true`; `delete_item` only on the user's
  explicit ask.

## Converting grouping labels into groups

A board that has been running a while grows labels doing a group's job —
`v3-phase-4`, `epic-billing`, `q3` — one of the failures groups exist to fix
(PRD-001 problem 2). A label can be filtered on and nothing else: no title, no
shared context, no progress.

**Only convert labels that name a body of work.** A label describing a
*property* of a ticket — `bug`, `blocked`, `security`, `good-first-issue` — is
a label doing a label's job. Leave it. The test is whether the label could
sensibly have a goal and a completion percentage.

1. **Preview, and stop.** For each candidate: the label, the proposed group,
   the ticket count and how many are already done. This writes to every matched
   ticket and `update_item` stamps `updated`, so a wrong run lights up every
   card on the human's board at once. Do not skip the confirmation because the
   mapping looks obvious.
2. **Create the group** — `epic` for a body of work, `horizon` for a
   time-ordered lens — with a `context.md` naming the plan and governing
   documents that bind its members. That shared context is most of the value:
   it is written once and read by every member's agent.
3. **Set `groups` on each ticket.** Membership lives on the ticket (ADR-0001);
   never write a member list into the group. Members and progress are derived.
4. **Leave the labels in place.** Removing them in the same pass makes the run
   irreversible for no gain. Drop them later, once the groups are trusted.
5. **Re-read before each patch.** Build the patch from a fresh `get_item`, not
   from a list captured before the run started. A ticket that gained a group
   earlier in the same pass has a stale `groups` array in that list, and
   patching from it silently erases the membership you just wrote.
6. **Verify against the source.** Each group's derived `complete`/`total` must
   equal a direct count of tickets carrying the label. A mismatch means
   membership did not land — which is exactly how step 5's bug shows itself.

Idempotent by construction: a ticket already naming the group is skipped, and a
no-op `update_item` does not bump `updated`. A second run should report zero
patches.

For horizons, derive the split from something real — the roadmap's ordering,
`blocks` edges, what is actually taken. An invented NOW/NEXT is worse than none,
because a horizon filter that does not match reality teaches people to ignore
it.

## 4. Report

What changed, one line per item, plus what you deliberately left alone and
why. Grooming that can't be audited is indistinguishable from damage.

---

**No successor — control returns to the user.** Grooming changes the board, not
the work: nothing here moves a ticket forward through its pipeline. Tickets this
run made ready go to `kanmer-research` when someone starts them, doc-gate debt
goes to `kanmer-docs`, and a board that needs describing rather than fixing was
`kanmer-report`'s job in the first place.
