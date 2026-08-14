# Kanmer MCP tool reference

Kept in sync with `packages/mcp-server/src/index.ts` — run
`node scripts/check-plugin-sync.mjs` after changing either side.

## Read tools

| Tool | Purpose | Key params |
|---|---|---|
| `get_status` | Orientation — call first, every session. Project root, whether `.kanmer/` exists (never creates it), format version, board `source`, per-stage/per-type counts, archived/taken counts, warning count. | — |
| `list_board` | Board config: stages (statuses), areas (each with its ticket id `prefix`), priorities, legacy id prefixes. The `source` field is `"file"` for a real board.yml, `"default"` when the project has no board yet and you're seeing the synthesized default. | — |
| `list_items` | Item summaries (see fields below; no body). Archived excluded by default; with `include_archived: true`, archived and active items are returned together and distinguished by the summary's `archived` field. Normally a plain array; if any `.kanmer` files are malformed or misnamed it returns `{ items, warnings }` instead — surface those warnings to the user rather than ignoring them. | `type?`, `status?`, `area?`, `label?`, `include_archived?`, `updated_since?`, `sort?` (`id`/`updated_desc`), `limit?` |
| `get_item` | Full frontmatter + Markdown body of one item; for tickets also `docs` presence and `checklist` progress. | `id` |
| `get_ticket_doc` | Read one pipeline document from a ticket's folder. `content: null` when not written yet. Also returns `version` — a token for the document's current bytes; pass it back as `set_ticket_doc`'s `expected_version` to be rejected instead of overwriting a concurrent edit. | `id`, `doc` (a **per-area configured** doc id — see `get_doc_gates`; or a scratch file as `scratch-<slug>`) |
| `search_items` | Full-text search over id, title, body, labels, assignee. | `query`, `type?` |
| `get_links` | Forward links + backlinks for an item, with titles, plus the typed dependency edges: `blocks` (stored on the blocker) and `blockedBy` (derived, never stored). | `id` |
| `get_activity` | The change log: one `{ts, id, op, field, from, to, actor}` entry per mutation, oldest-first. This is what makes "X moved to review yesterday" a fact instead of an inference. Derived convenience — safe to delete, never truth. | `id?`, `since?`, `limit?` |
| `get_doc_gates` | Inspect the document model. With `id`: the ticket's resolved doc types, which exist, its area/status, and the per-area gate rules — self-check before `move_item` instead of failing into a gate. Without `id`: the board's default + per-area doc types and gates, the governing-doc path globs, and whether deployment tracking is on. | `id?` |

## Write tools

| Tool | Purpose | Key params |
|---|---|---|
| `create_item` | Create a ticket. Returns the allocated id — tickets born in an area get that area's prefix (e.g. `API-007`), area-less tickets get the fallback prefix. Rejects a `status`/`area`/`priority` id the board doesn't define, and any `links`/`blocks` entry naming a nonexistent item — errors list the valid ids. **Creation is ungated**: a ticket may be created directly in any stage (for importing/backfilling finished work); the document gates apply on `move_item`, not creation. Link governing docs with `refs` (each must exist) or set `docs_todo`. On format-2 boards `plan`/`research` types are rejected: those live inside ticket folders via `set_ticket_doc`. | `type`, `title`, `status?`, `area?`, `priority?`, `assignee?`, `labels?`, `links?`, `blocks?`, `refs?`, `docs_todo?`, `commits?`, `prs?`, `deployment?`, `body?` |
| `create_items` | Bulk create up to 50 items in one call, sequential. Partial success: per-entry `{ ok, item \| error }` results in order — check them, don't assume all succeeded. | `items` (array of create_item fields) |
| `update_item` | Patch frontmatter and/or the body. Omitted fields are left alone, but a supplied `body` **replaces** the whole body — it is not merged. A patch that changes nothing is a no-op and does **not** bump `updated`. Changing a ticket's `area` moves its folder; the id never changes. `archived: true` hides from board. `type` **cannot** be changed — create a new item and archive the old one instead. Pass `expected_updated` (the `updated` you last read) when rewriting a body: if the item changed since, the call fails with a conflict telling you to re-read, instead of silently overwriting the newer version. Passing `[]` clears an array field (`refs`/`commits`/`prs`); `deployment: ""` clears deployment. | `id`, `title?`, `status?`, `area?`, `priority?`, `assignee?`, `order?`, `labels?`, `links?`, `blocks?`, `refs?`, `docs_todo?`, `commits?`, `prs?`, `deployment?`, `body?`, `archived?`, `expected_updated?` |
| `move_item` | Move an item to a workflow stage. Rejects a status that is not on the board — call `list_board` for valid ids. Enforces the ticket area's **document gates** (e.g. `proof.md` before the final stage, `post-implementation-report` before review) and names every missing document/governing-doc on failure — call `get_doc_gates` to self-check first. Optional `position` places it within the column — `"top"`, `"bottom"`, or `{ after: "API-003" }` — maintaining the manual order the human sees. | `id`, `status`, `position?`, `expected_updated?` |
| `take_ticket` | Take a ticket before working it: records `taken_at` + `branch` (required) + `worktree?`, sets assignee (defaults to your client name), moves to the working stage (default `implementing`). Errors if already taken unless `force`. `action: "release"` clears the taken fields when the work ends. | `id`, `action` (`take`/`release`), `branch`, `worktree?`, `stage?`, `assignee?`, `force?` |
| `set_ticket_doc` | Write one pipeline document into a ticket's folder (plain Markdown, no frontmatter). `doc` is a **per-area configured** doc id (`get_doc_gates`); an unknown id is rejected with the valid ids, and a doc that `requires` others is rejected until they exist. `append: true` adds below existing content — for progress notes. For free-form notes use `append_scratch`. Pass the `version` you last read from `get_ticket_doc` as `expected_version` and a concurrent edit is refused with a conflict; the result carries the new `version`. | `id`, `doc`, `content`, `append?`, `expected_version?` |
| `append_scratch` | Append a free-form working note to a ticket's scratch file (`scratch-<slug>.md`). Never gated or validated against the doc types — the agent's running notepad. Read it back with `get_ticket_doc(doc: "scratch-<slug>")`. Successive appends are separated by a blank line. | `id`, `slug?` (default `notes`), `content` |
| `link_doc` | Maintain a ticket's `refs[]` — repo-relative paths to governing docs (PRD/FRD/ADR) in the repo's own `/docs/`. `add` validates the path exists under the project root; `remove` drops it. Distinct from `link_items` (item↔item); this is item↔repo-file. A linked governing doc satisfies the leave-backlog gate. | `id`, `path`, `action` (`add`/`remove`) |
| `link_items` | Add/remove a structured relation source → target. `rel: "relates"` (default) writes `links[]`; `rel: "blocks"` writes `blocks[]` — source blocks target, and blocked-by derives from it. `add` requires the target to exist; `remove` works even on dangling links so they can be cleaned. | `source_id`, `target_id`, `action` (`add`/`remove`), `rel?` (`relates`/`blocks`) |
| `migrate_board` | Bring the board fully current: run the v1→v2 migration if needed, then backfill the 7-stage default (alias-aware, additive — never renames/reorders existing stages, never touches item files). `dry_run: true` previews what would move and which stages would be added. | `dry_run?` |
| `add_column` | Add a stage, area or priority to the board. `color` is a hex string like `#5b8cff`; areas may set `prefix` (2–6 uppercase alphanumerics) for the ids of tickets born there. Rejects an id that already exists. | `id`, `name`, `kind` (`status`/`area`/`priority`), `color?`, `prefix?` |
| `update_column` | Rename/recolour a column, or pin an area's `prefix`. The column id itself is immutable. | `kind`, `id`, `name?`, `color?`, `prefix?` |
| `reorder_columns` | Reorder stages/areas/priorities; `order` must be a permutation of the existing ids. The **first** status is where new items land; the **last** is the proof-gated final stage. | `kind`, `order` |

## Destructive

| Tool | Purpose | Key params |
|---|---|---|
| `delete_item` | Permanently delete an item — for tickets this removes the **whole folder**, pipeline documents and attachments included. Cannot be undone; prefer archiving. Frontmatter `links[]` in other items pointing at the deleted id are cleaned automatically (`cleanedLinks`); body `[[wiki]]` mentions are prose, left in place (`bodyReferencesRemain`). | `id` |
| `remove_column` | Remove a stage/area/priority. Refuses while items still use it unless `migrate_to` names another column of the same kind — then every matching item is rewritten first (area migrations move ticket folders; migrating into the final stage still requires proof.md). | `kind`, `id`, `migrate_to?` |

## What a `list_items` summary contains

Exactly these fields, always all present: `id`, `type`, `title`, `status`,
`area`, `priority`, `assignee`, `labels`, `order` (number or `null`), `blocked`
(true when a live blocker exists), `refs` (governing-doc paths or `null`),
`deployment` (deployment status or `null`), `created`, `updated`, `archived`,
`taken` (`{ taken_at, branch, worktree }` or `null` when not taken), `docs`
(which pipeline documents exist — `null` for legacy-layout items), `checklist`
(`{ checked, total }` or `null`). `links`, `commits`, `prs` and the Markdown
body are **not** included and require `get_item` — a `null`/absent relation in a
summary means "not reported here", not "no links".

## Field semantics

- `status` — the single workflow dimension; a column on the human's board.
  Default stages on a fresh board: backlog → researching → planning →
  implementing → review → verifying → done. Call `list_board` for the ids that
  are actually configured — older or customised boards commonly differ, and
  writes to an id the board doesn't define are rejected. Transitions are subject
  to the area's document gates (see `get_doc_gates`).
- `area` — colour-coded grouping (e.g. UI, API); clusters cards within stage
  columns. A board can legitimately have **no** areas defined (`areas: []`), in
  which case leave the field off items.
- `priority` — id into the board's configurable priority list.
- `assignee` — free-text; the only person field, so it doubles as "who is this
  waiting on" when an item is in review.
- `links` — array of item ids; combined with `[[ID]]` body wiki-links into a
  backlink graph. Links are one-directional; backlinks are derived, not stored.
- `blocks` — array of item ids this item blocks. Stored only on the blocker;
  `blockedBy` is derived by `get_links` and never written. A summary's
  `blocked` flag is true while at least one blocker is live (not done,
  not archived).
- `refs` — repo-relative POSIX paths to governing docs (PRD/FRD/ADR) in the
  repo's own `/docs/`. Maintained with `link_doc`; each must exist. A ref whose
  kind matches the leave-backlog gate satisfies it. `docs_todo: true` is the
  escape when the doc is still to be created.
- `commits` / `prs` — SHAs and PR references (number or URL) tying the ticket to
  its code. Emitted only when non-empty; skills populate them at execute/closeout.
- `deployment` — a flat string, only when the board declares `deployment`
  environments: `n/a` (not deployable) \| `not-deployed` \| an environment id.
  Pass `""` to `update_item` to clear it.
- `order` — optional fractional sort key giving the human's manual order within
  a stage column. Let `move_item`'s `position` compute it (`"top"`,
  `"bottom"`, `{ after: "API-003" }`) rather than setting numbers by hand;
  items with no `order` sort after those that have one.
- `taken_at` / `branch` / `worktree` — written by `take_ticket` and cleared by
  its `release` action: when the work started, the git branch, and the worktree
  if one is used. Their presence is what "an agent has this ticket" means.
- `archived` — true hides the item from the board without deleting it.
- `created` / `updated` — ISO-8601 timestamps in the item's frontmatter, stamped
  by the tools on every write. Compare `updated` against today's date to judge
  staleness; it is frontmatter, not a filesystem mtime, so it only moves when an
  item actually changes.
- Bodies are Markdown; `[[ID]]` references render as clickable links in the GUI.

## Item types

Format-2 boards store **tickets only**. A ticket is a folder:

    .kanmer/areas/<area|_none>/<ID>/<ID>.md      ← the ticket itself
                                   research.md impact.md plan.md
                                   checklist.md proof.md

| Type | Where it lives | Id prefix | Use for |
|---|---|---|---|
| `ticket` | `areas/<area\|_none>/<ID>/<ID>.md` | the area's `prefix`, else `idPrefixes.ticket` (`TICK`) | A unit of work that appears on the board |
| `plan` | **retired** — use `set_ticket_doc(doc: "plan")` | `PLAN` (legacy ids only) | Format-1 boards only |
| `research` | **retired** — use `set_ticket_doc(doc: "research")` | `RES` (legacy ids only) | Format-1 boards only |

`create_item` with `type: "plan"` or `"research"` is **rejected on format-2 boards** — those
live inside a ticket folder as documents. Unmigrated format-1 boards still accept them.
Call `get_status` to see which format a board uses.

The pipeline documents in a ticket's folder are **per-area configurable** (`board.docs`);
the five above are the default set. Call `get_doc_gates` for a ticket's actual doc types and
gates. Creation is **ungated** — a ticket may be created in any stage — but `move_item` enforces
the area's document gates on every transition.
