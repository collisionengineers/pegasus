# Kanmer MCP tool reference

Kept in sync with `packages/mcp-server/src/index.ts` — run
`node scripts/check-plugin-sync.mjs` after changing either side.

## Read tools

| Tool | Purpose | Key params |
|---|---|---|
| `get_status` | Orientation — call first, every session. Answers **which board** and **which server**. Board: `projectRoot` and `rootSource` (`flag`/`env`/`cwd`/`cwd-worktree`/`ancestor`/`ancestor-worktree`/`init`), `repoRoot` — what governing-doc `refs` resolve against — and `repoRootSource` (`flag`/`env`/`derived`), whether `.kanmer/` exists (never creates it), format version, board `source`, per-stage/per-type counts, archived/taken counts, warning count. Server: a `server` block naming the build that is answering — `version`, the resolved `path`, the runtime `sha256`/`sha256Short` of its bytes, `mtime`, `size`, and `build` (`packaged`/`plugin`/`dev-standalone`/`dev-esm`/`unknown`). Two hosts on the same board can run different builds enforcing different gates; comparing `server.sha256` is how you see it. **The `server` block is absent on servers older than 0.3.3 — that absence is the signal, not an error**; individual fields are `null` if unreadable, and the call never fails over it. Repo: a `repo` block — `{ upToDate, stale: [{ artefact, state, detail, fix }] }` — saying whether this repo's Kanmer artefacts kept up with that build. Checked by content hash, not version string: the AGENTS.md managed block, the installed skills trees and their `.kanmer-skills-version` stamps, `board.yml`, and the provider MCP registrations. `state` is `behind` (act), `compensated` (old file, runtime already papers over it — informational), `unstamped` (no evidence either way) or `unknown` (unreadable); `upToDate` is true iff nothing is `behind`. Board format is not listed — it is the `format` field. Repair is never automatic: run `kanmer-setup`. **Absent on servers older than 0.3.4, and that absence is the signal.** | — |
| `list_board` | Everything needed to orient, resolved: the six fixed `stages`, `areas` (each with its ticket id `prefix` and optional `defaultProfile`), `profiles` and `defaultProfile`, `groupKinds`, `proofTypes`, the `docTypes` vocabulary and `gateExemptFolders`, `boundaries`, and the governing-doc globs. The `source` field is `"file"` for a real board.yml, `"default"` for the synthesized default. | — |
| `list_items` | Item summaries (see fields below; no body). Filters combine with AND. `group?` filters by membership — an unknown group id returns nothing rather than erroring, and this, not `get_group`, is how you build a working roster from a group, because summaries carry `profile`/`taken`/`docs` while `get_group`'s derived members carry only id/title/stage. Archived excluded by default; with `include_archived: true`, archived and active items are returned together and distinguished by the summary's `archived` field. Normally a plain array; if any `.kanmer` files are malformed or misnamed it returns `{ items, warnings }` instead — surface those warnings to the user rather than ignoring them. | `type?`, `status?`, `area?`, `label?`, `group?`, `include_archived?`, `updated_since?`, `sort?` (`id`/`updated_desc`), `limit?` |
| `get_item` | Full frontmatter + Markdown body of one item; for tickets also `docs` presence and `checklist` progress. | `id` |
| `get_ticket_doc` | Read one ticket document by **type-relative path**. `content: null` when not written yet. Also returns `version` — a token for the document's current bytes; pass it back as `set_ticket_doc`'s `expected_version` to be rejected instead of overwriting a concurrent edit. | `id`, `doc` — `research`, `research/azure/tokens.md`, `scratch/notes`. A bare type resolves to that folder's index. |
| `search_items` | Full-text search over id, title, body, labels, assignee. | `query`, `type?` |
| `get_links` | Forward links + backlinks for an item, with titles, plus the typed dependency edges: `blocks` (stored on the blocker) and `blockedBy` (derived, never stored). | `id` |
| `get_activity` | The change log: one `{ts, id, op, field, from, to, actor}` entry per mutation, oldest-first. This is what makes "X moved to review yesterday" a fact instead of an inference. Derived convenience — safe to delete, never truth. | `id?`, `since?`, `limit?` |
| `get_doc_gates` | **Call this before any move.** With `id`: the ticket's resolved `profile`, every gated `boundary` with each requirement and whether it is satisfied, non-blocking `warnings`, plus `reachable` stages and `blockedBy` reasons per stage — so you self-check instead of failing into a gate. Requirements vary per ticket by profile, so this is the only reliable source; do not assume a fixed pipeline. Without `id`: the board's profiles, boundaries, doc vocabulary, proof types and governing-doc globs. | `id?` |
| `get_group` | A group with its **derived** membership: every ticket naming it, with title and stage, plus per-stage progress. Computed on every read, so it cannot go stale. Read a member ticket's groups before working it — the shared context is part of the ticket's context. | `id` |
| `list_groups` | Every group, optionally by kind. Archived excluded unless asked. | `kind?`, `include_archived?` |
| `get_group_doc` | Read a group's shared context document by relative path. Free-form — a group's context is whatever its work needs. | `id`, `path` |

## Write tools

| Tool | Purpose | Key params |
|---|---|---|
| `create_item` | Create a ticket. Returns the allocated id — tickets born in an area get that area's prefix (e.g. `API-007`), area-less ones the fallback prefix. Rejects an unknown `status`, `area`, `profile` or `groups` entry, and any `links`/`blocks` naming a nonexistent item — errors list the valid ids. **Creation is ungated**: a ticket may be created directly in any stage, which is what makes importing and backfilling finished work possible; gates apply on `move_item`. Pick a `profile` that matches the nature of the work (see below) — that is what decides how much evidence this ticket will owe. Link governing docs with `refs` (each must exist) or set `docs_todo`. | `type`, `title`, `status?`, `area?`, `profile?`, `requires?`, `groups?`, `assignee?`, `labels?`, `links?`, `blocks?`, `refs?`, `docs_todo?`, `commits?`, `prs?`, `deployment?`, `body?` |
| `create_items` | Bulk create up to 50 items in one call, sequential. Partial success: per-entry `{ ok, item \| error }` results in order — check them, don't assume all succeeded. | `items` (array of create_item fields) |
| `update_item` | Patch frontmatter and/or the body. Omitted fields are left alone, but a supplied `body` **replaces** the whole body — it is not merged. A patch that changes nothing is a no-op and does **not** bump `updated`. Changing a ticket's `area` moves its folder; the id never changes. `archived: true` hides from board. `type` **cannot** be changed — create a new item and archive the old one instead. Pass `expected_updated` (the `updated` you last read) when rewriting a body: if the item changed since, the call fails with a conflict telling you to re-read, instead of silently overwriting the newer version. Passing `[]` clears an array field (`refs`/`commits`/`prs`); `deployment: ""` clears deployment. Changing `profile` re-evaluates the gates immediately — a move blocked a moment ago may now be allowed. `groups` is how membership is set; there is no add/remove tool. | `id`, `title?`, `status?`, `area?`, `profile?`, `requires?`, `groups?`, `assignee?`, `order?`, `labels?`, `links?`, `blocks?`, `refs?`, `docs_todo?`, `commits?`, `prs?`, `deployment?`, `body?`, `archived?`, `expected_updated?` |
| `move_item` | Move an item to one of the six fixed stages. Enforces the ticket's **profile gates** and names the unmet requirement and boundary on failure — call `get_doc_gates` to self-check first. **A single move may cross at most one gated boundary**: writing every document and jumping straight to `done` is refused even though nothing is missing, because the pipeline is meant to be walked rather than satisfied at the end. Move one stage at a time; the refusal names the next one. Optional `position` places it within the column — `"top"`, `"bottom"`, or `{ after: "API-003" }` — maintaining the manual order the human sees. | `id`, `status`, `position?`, `expected_updated?` |
| `take_ticket` | Take a ticket before working it: records `taken_at` + `branch` (required) + `worktree?`, sets assignee (defaults to your client name), moves to the working stage (default `implementing`). Errors if already taken unless `force`. `action: "release"` clears the taken fields when the work ends. | `id`, `action` (`take`/`release`), `branch`, `worktree?`, `stage?`, `assignee?`, `force?` |
| `set_ticket_doc` | Write one pipeline document into a ticket's folder (plain Markdown, no frontmatter). `doc` is a **per-area configured** doc id (`get_doc_gates`); an unknown id is rejected with the valid ids, and a doc that `requires` others is rejected until they exist. `append: true` adds below existing content — for progress notes. For free-form notes use `append_scratch`. Pass the `version` you last read from `get_ticket_doc` as `expected_version` and a concurrent edit is refused with a conflict; the result carries the new `version`. | `id`, `doc`, `content`, `append?`, `expected_version?` |
| `append_scratch` | Append a free-form working note to a ticket's scratch file (`scratch-<slug>.md`). Never gated or validated against the doc types — the agent's running notepad. Read it back with `get_ticket_doc(doc: "scratch-<slug>")`. Successive appends are separated by a blank line. | `id`, `slug?` (default `notes`), `content` |
| `link_doc` | Maintain a ticket's `refs[]` — repo-relative paths to governing docs (PRD/FRD/ADR) in the repo's own `/docs/`. `add` validates the path exists under the project root; `remove` drops it. Distinct from `link_items` (item↔item); this is item↔repo-file. A linked governing doc satisfies the leave-backlog gate. | `id`, `path`, `action` (`add`/`remove`) |
| `link_items` | Add/remove a structured relation source → target. `rel: "relates"` (default) writes `links[]`; `rel: "blocks"` writes `blocks[]` — source blocks target, and blocked-by derives from it. `add` requires the target to exist; `remove` works even on dangling links so they can be cleaned. | `source_id`, `target_id`, `action` (`add`/`remove`), `rel?` (`relates`/`blocks`) |
| `migrate_board` | Bring the board fully current: run the v1→v2 migration if needed, then backfill the 7-stage default (alias-aware, additive — never renames/reorders existing stages, never touches item files). `dry_run: true` previews what would move and which stages would be added. | `dry_run?` |
| `add_column` | Add an **area** — the only configurable column. Stages are fixed and priority no longer exists, so neither can be added. `color` is a hex string; `prefix` (2–6 uppercase alphanumerics) sets the ids of tickets born there. Rejects an id that already exists. | `id`, `name`, `kind` (`area`), `color?`, `prefix?` |
| `update_column` | Rename/recolour a column, or pin an area's `prefix`. The column id itself is immutable. | `kind`, `id`, `name?`, `color?`, `prefix?` |
| `reorder_columns` | Reorder areas; `order` must be a permutation of the existing ids. Stages cannot be reordered — they are constants. | `kind` (`area`), `order` |
| `create_group` | Create an `epic` (these ship together) or a `horizon` (this is what matters now). Returns the allocated id. Add members with `update_item(groups: [...])`. | `kind`, `title`, `body?` |
| `update_group` | Patch a group's own fields: `title`, `body`, `archived`. Omitted fields are left alone; a supplied `body` **replaces** the whole body. A patch that changes nothing does **not** bump `updated`. `archived: true` retires the group — it drops out of `list_groups` unless `include_archived`, stays readable, and its **members are untouched**; this is the retirement path, there is no delete. `kind` **cannot** be changed — the id prefix is allocated from it — and membership is not patchable here either: it lives on tickets via `update_item(groups: [...])`. Pass `expected_updated` (the `updated` you last read) to be rejected with a conflict instead of overwriting a concurrent edit. | `id`, `title?`, `body?`, `archived?`, `expected_updated?` |
| `set_group_doc` | Write shared context into a group's folder — the decision or constraint every member sits under, rather than repeating it per ticket. Cannot write the group's own `<ID>.md`; use `update_group` for that. | `id`, `path`, `content` |

## Destructive

| Tool | Purpose | Key params |
|---|---|---|
| `delete_item` | Permanently delete an item — for tickets this removes the **whole folder**, pipeline documents and attachments included. Cannot be undone; prefer archiving. Frontmatter `links[]` in other items pointing at the deleted id are cleaned automatically (`cleanedLinks`); body `[[wiki]]` mentions are prose, left in place (`bodyReferencesRemain`). | `id` |
| `remove_column` | Remove an area. Refuses while tickets still use it unless `migrate_to` names another area — then every matching ticket is rewritten first, which moves its folder. | `kind` (`area`), `id`, `migrate_to?` |

## What a `list_items` summary contains

Exactly these fields, always all present: `id`, `type`, `title`, `status`,
`area`, `profile`, `groups` (group ids, or `null`), `assignee`, `labels`,
`order` (number or `null`), `blocked`
(true when a live blocker exists), `refs` (governing-doc paths or `null`),
`deployment` (deployment status or `null`), `created`, `updated`, `archived`,
`taken` (`{ taken_at, branch, worktree }` or `null` when not taken), `docs`
(which pipeline documents exist — `null` for legacy-layout items), `checklist`
(`{ checked, total }` or `null`). `links`, `commits`, `prs` and the Markdown
body are **not** included and require `get_item` — a `null`/absent relation in a
summary means "not reported here", not "no links".

## Field semantics

- `status` — the single workflow dimension; a column on the human's board. The
  stages are **fixed** and a board cannot change them (ADR-0002): backlog →
  preparing → implementing → review → verifying → done. Writes to any other id
  are rejected. Transitions are subject to the ticket's **profile** gates (see
  `get_doc_gates`) — which boundaries exist varies per profile, so never assume
  a fixed pipeline.
- `area` — colour-coded grouping (e.g. UI, API); clusters cards within stage
  columns, and decides the id prefix of tickets born there. Areas are the only
  configurable column. A board can legitimately have **no** areas defined
  (`areas: []`), in which case leave the field off items.
- `profile` — which requirement set the ticket owes: `feature`, `fix`, `chore`,
  `spike`, or `custom` (which reads the ticket's own inline `requires` instead
  of the board's table). This, not the stage, is what decides how much evidence
  the ticket must produce. Changing it re-evaluates the gates immediately.
- `groups` — ids of the epics/horizons this ticket belongs to. Membership is
  stored on the **ticket** and derived by the group, never the reverse, so a
  group's membership cannot go stale. Set with `update_item(groups: [...])`.
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

Format-3 boards store **tickets only**. A ticket is a folder, and each document
type is a **folder inside it** — so one type can hold several files:

    .kanmer/areas/<area|_none>/<ID>/<ID>.md      ← the ticket itself
                                   research/research.md
                                   files/files.md
                                   open-questions/open-questions.md
                                   plan/plan.md
                                   checklist/checklist.md
                                   post-implementation-report/post-implementation-report.md
                                   proof/proof.md
                                   scratch/<slug>.md       ← never gated
                                   reference/ assets/      ← never gated

Read the **whole folder** before starting: a document type with three files in it
looks identical from `get_item`, which reports only that the type exists.

Scratch is a folder like the rest, but its files are addressed by **slug**:
`append_scratch <id> review "…"` writes `scratch/review.md`, and you read it back
as `get_ticket_doc(doc: "scratch-review")`. The doc id and the path differ — that
is the one place in the layout where they do.

| Type | Where it lives | Id prefix | Use for |
|---|---|---|---|
| `ticket` | `areas/<area\|_none>/<ID>/<ID>.md` | the area's `prefix`, else `idPrefixes.ticket` (`TICK`) | A unit of work that appears on the board |
| `plan` | **retired** — use `set_ticket_doc(doc: "plan")` | `PLAN` (legacy ids only) | Format-1 boards only |
| `research` | **retired** — use `set_ticket_doc(doc: "research")` | `RES` (legacy ids only) | Format-1 boards only |

`create_item` with `type: "plan"` or `"research"` is **rejected** — those live
inside a ticket folder as documents. Unmigrated format-1 boards still accept
them; call `get_status` to see which format a board uses.

Which documents a ticket owes comes from its **profile**, not from its area and
not from a fixed pipeline — call `get_doc_gates` for the ticket's actual types
and boundaries. Creation is **ungated**, which is what makes importing and
backfilling finished work possible; `move_item` is where the gates apply.
