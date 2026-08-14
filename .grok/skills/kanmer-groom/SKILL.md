---
name: kanmer-groom
description: Groom and triage a Kanmer board by actually fixing it — dedupe near-duplicate tickets, fill in missing areas and priorities, split oversized tickets, archive dead ones, chase stale taken tickets, and repair off-board statuses. Use when the user says "groom the backlog", "tidy the board", "triage", "clean up the tickets". DO NOT USE FOR reporting problems without changing anything (kanmer-report).
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
- **Missing fields** — no `area` on a board that has areas; no priority
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
  problems the tools can repair via `move_item` / `update_item`.

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

## 4. Report

What changed, one line per item, plus what you deliberately left alone and
why. Grooming that can't be audited is indistinguishable from damage.
