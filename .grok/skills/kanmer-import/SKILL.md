---
name: kanmer-import
description: Bring external work onto a Kanmer board — turn GitHub issues into tickets in the right area, each linked back to its source, idempotently (re-running updates instead of duplicating). Use when the user says "import the GitHub issues", "put this issue on the board", "sync the board with the issue tracker". DO NOT USE FOR first-time board setup (kanmer-setup) or PR review feedback — that is kanmer-review's job now.
---

# Importing external work onto the board

The board is only trustworthy if it shows *all* the work — including the
work that arrives in GitHub instead of in conversation. Importing is a
mapping job with one hard rule: **idempotent**. Every imported ticket
carries its source URL in the body, and the import checks for that URL
before creating anything, so running twice changes nothing the second time.

## GitHub issues

1. **Fetch**: `gh issue list --state open --json
   number,title,body,labels,url` (add `--label`/`--search` filters when the
   user scopes the import).
2. **Dedupe**: for each issue, `search_items` for its URL (and for close
   title matches — a human may have filed the same work by hand). Found →
   update that ticket if the issue changed materially; skip otherwise.
3. **Map**: pick the `area` from the issue's labels and the files/components
   it names, against `list_board`'s areas; issue labels worth keeping become
   ticket labels, plus `gh-import`. Priority only when the issue clearly
   signals one.
4. **Create** via `kanmer-tickets` conventions (`create_items` for bulk,
   check per-entry results): title stays imperative (rewrite the issue title
   if it's a complaint, not an instruction), body carries the What/Why
   distilled from the issue plus a `Source: <url>` line. Leave `status` unset,
   and set **`docs_todo: true`** — an imported ticket has no governing doc yet,
   so the flag keeps the leave-Backlog gate from stranding it until
   `kanmer-docs` links or writes one. Issues referencing each other become
   `links` / `rel: "blocks"`.

## PR review feedback → kanmer-review

Import no longer handles PR comments. Turning PR review feedback into blocking
tickets is `kanmer-review`'s job (its `pr-comments` / `pr-comment-disposition`
docs) — one owner, so the same feedback can't be double-filed.

## Report

Created / updated / skipped-as-duplicate, one line each with `[[ID]]` ↔
source URL. If the user wants the reverse link, add a "Tracked on the
Kanmer board as <ID>" comment to the issue (`gh issue comment`) — ask, don't
spam their tracker unprompted.
