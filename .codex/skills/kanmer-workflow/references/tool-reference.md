# Kanmer MCP tool reference

Kept in sync with `packages/mcp-server/src/index.ts` — run
`node scripts/check-plugin-sync.mjs` after changing either side.

## Read tools

| Tool | Purpose | Key params |
|---|---|---|
| `list_board` | Board config: stages (statuses), areas, priorities, id prefixes. Call first. | — |
| `list_items` | Item summaries (see fields below; no body). Archived excluded by default. | `type?`, `status?`, `area?`, `label?`, `include_archived?` |
| `get_item` | Full frontmatter + Markdown body of one item. | `id` |
| `search_items` | Full-text search over id, title, body, labels, assignee. | `query`, `type?` |
| `get_links` | Forward links + backlinks for an item, with titles. | `id` |

## Write tools

| Tool | Purpose | Key params |
|---|---|---|
| `create_item` | Create ticket / plan / research. Returns allocated id (e.g. TICK-007). | `type`, `title`, `status?`, `area?`, `priority?`, `assignee?`, `labels?`, `links?`, `body?` |
| `update_item` | Set any frontmatter field and/or the body. Omitted fields are left alone, but a supplied `body` **replaces** the whole body — it is not merged. `archived: true` hides from board. | `id`, plus any create field, `archived?` |
| `move_item` | Move an item to a workflow stage. | `id`, `status` |
| `link_items` | Add/remove a structured relation source → target. | `source_id`, `target_id`, `action` (`add`/`remove`) |
| `add_column` | Add a stage, area or priority to the board. `color` is a hex string like `#5b8cff`. | `id`, `name`, `kind` (`status`/`area`/`priority`), `color?` |

## Destructive

| Tool | Purpose | Key params |
|---|---|---|
| `delete_item` | Permanently delete an item file. Cannot be undone. Prefer archiving. | `id` |

## What a `list_items` summary contains

Every field except the body: `id`, `type`, `title`, `status`, `area`,
`priority`, `assignee`, `labels`, `updated`. Use `get_item` when you need the
body too.

## Field semantics

- `status` — the single workflow dimension; a column on the human's board.
  Default stages: todo → planning → implementing → review → verifying → done.
- `area` — colour-coded grouping (e.g. UI, API); clusters cards within stage
  columns. A board can legitimately have **no** areas defined (`areas: []`), in
  which case leave the field off items.
- `priority` — id into the board's configurable priority list.
- `assignee` — free-text; the only person field, so it doubles as "who is this
  waiting on" when an item is in review.
- `links` — array of item ids; combined with `[[ID]]` body wiki-links into a
  backlink graph. Links are one-directional; backlinks are derived, not stored.
- `archived` — true hides the item from the board without deleting it.
- `created` / `updated` — ISO-8601 timestamps in the item's frontmatter, stamped
  by the tools on every write. Compare `updated` against today's date to judge
  staleness; it is frontmatter, not a filesystem mtime, so it only moves when an
  item actually changes.
- Bodies are Markdown; `[[ID]]` references render as clickable links in the GUI.

## Item types

Each type lives in its own folder with its own id prefix (configurable):

| Type | Folder | Default prefix | Use for |
|---|---|---|---|
| `ticket` | `.kanmer/tickets/` | `TICK` | A unit of work that appears on the board |
| `plan` | `.kanmer/plans/` | `PLAN` | Coordinating several tickets toward one outcome |
| `research` | `.kanmer/research/` | `RES` | Findings that outlive the conversation |
