# Kanmer MCP tool reference

Kept in sync with `packages/mcp-server/src/index.ts` — run
`node scripts/check-plugin-sync.mjs` after changing either side.

## Read tools

| Tool | Purpose | Key params |
|---|---|---|
| `get_status` | Orientation — call first, every session. Answers **which board** and **which server**. Board: `projectRoot` and `rootSource` (`flag`/`env`/`cwd`/`cwd-worktree`/`ancestor`/`ancestor-worktree`/`init`), `repoRoot` — what governing-doc `refs` resolve against — and `repoRootSource` (`flag`/`env`/`derived`), whether `.kanmer/` exists (never creates it), format version, board `source`, per-stage/per-type counts, archived/taken counts, warning count. `project` carries canonical `{ boardRoot, format, repoRoot, boardSource, fingerprint }`; use its versioned fingerprint as the optional optimistic project token when `compat.expectedProject` is `"optional"`. Server: a `server` block naming the build that is answering — `version`, the resolved `path`, the runtime `sha256`/`sha256Short` of its bytes, `mtime`, `size`, and `build` (`packaged`/`plugin`/`dev-standalone`/`dev-esm`/`unknown`). Two hosts on the same board can run different builds enforcing different gates; comparing `server.sha256` is how you see it. **The `server` block is absent on servers older than 0.3.3 — that absence is the signal, not an error**; individual fields are `null` if unreadable, and the call never fails over it. Repo: a `repo` block — `{ upToDate, stale: [{ artefact, state, detail, fix }] }` — saying whether this repo's Kanmer artefacts kept up with that build. Checked by content hash, not version string: the AGENTS.md managed block, the installed skills trees and their `.kanmer-skills-version` stamps, `board.yml`, and the provider MCP registrations. `state` is `behind` (act), `compensated` (old file, runtime already papers over it — informational), `unstamped` (no evidence either way) or `unknown` (unreadable); `upToDate` is true iff nothing is `behind`. Board format is not listed — it is the `format` field. Repair is never automatic: run `kanmer-setup`. **Absent on servers older than 0.3.4, and that absence is the signal.** | — |
| `list_board` | Everything needed to orient, resolved: the six fixed `stages`, `areas` (each with its ticket id `prefix` and optional `defaultProfile`), `profiles` and `defaultProfile`, `groupKinds`, `proofTypes`, the `docTypes` vocabulary and `gateExemptFolders`, `boundaries`, and the governing-doc globs. The `source` field is `"file"` for a real board.yml, `"default"` for the synthesized default. | — |
| `get_sources` | Resolve project-declared MCP, plugin, and llms.txt preferences by area/labels. Host observations are explicit; declarations never install, enable, authenticate, or grant authority. | `area?`, `labels?`, `connected_mcp?`, `installed_plugins?` |
| `list_items` | Item summaries (see fields below; no body). Filters combine with AND. `group?` filters by membership — an unknown group id returns nothing rather than erroring, and this, not `get_group`, is how you build a working roster from a group, because summaries carry `profile`/`taken`/`docs` while `get_group`'s derived members carry only id/title/stage. Archived excluded by default; with `include_archived: true`, archived and active items are returned together and distinguished by the summary's `archived` field. Every ticket summary also carries `documentPaths`: the exact type-relative Markdown paths callers can pass to `get_ticket_doc`. Normally a plain array; if any `.kanmer` files are malformed or misnamed it returns `{ items, warnings }` instead — surface those warnings to the user rather than ignoring them. | `type?`, `status?`, `area?`, `label?`, `group?`, `include_archived?`, `updated_since?`, `sort?` (`id`/`updated_desc`), `limit?` |
| `get_item` | Full frontmatter + Markdown body of one item; for tickets also `docs` presence, exact type-relative `documentPaths`, and `checklist` progress. | `id` |
| `get_ticket_doc` | Read one ticket document by **type-relative path**, or an ordered batch. Supply exactly one of legacy `doc` or `docs` (1–25 ids). Single responses remain `{id,doc,exists,content,version}`. Batch responses are `{id,documents:[{doc,exists,content,version}]}` after first-order deduplication. `content: null` is a normal missing document; invalid ids fail the whole call. Each version binds to returned bytes, so a batch is not an atomic snapshot. | `id`, `doc?` or `docs?` — `research`, `research/azure/tokens.md`, `scratch/notes`. A bare type resolves to that folder's index. |
| `search_items` | Full-text search over id, title, body, labels, assignee. | `query`, `type?` |
| `get_links` | Forward links + backlinks for an item, with titles, plus the typed dependency edges: `blocks` (stored on the blocker) and `blockedBy` (derived, never stored). | `id` |
| `get_activity` | The change log: one `{ts, id, op, field, from, to, actor}` entry per mutation, oldest-first. This is what makes "X moved to review yesterday" a fact instead of an inference. Derived convenience — safe to delete, never truth. | `id?`, `since?`, `limit?` |
| `get_doc_gates` | **Call this before any move.** With `id`: the ticket's resolved `profile`, every gated `boundary` with each requirement and whether it is satisfied, non-blocking `warnings`, plus `reachable` stages and `blockedBy` reasons per stage — so you self-check instead of failing into a gate. It also returns per-type counts and exact type-relative `documentPaths`, which are the safe inputs to `get_ticket_doc`. Requirements vary per ticket by profile, so this is the only reliable source; do not assume a fixed pipeline. Without `id`: the board's profiles, boundaries, doc vocabulary, proof types and governing-doc globs. | `id?` |
| `get_group` | A group with its **derived** membership: every ticket naming it, with title and stage, plus per-stage progress. Computed on every read, so it cannot go stale. Read a member ticket's groups before working it — the shared context is part of the ticket's context. | `id` |
| `list_groups` | Every group, optionally by kind. Archived excluded unless asked. | `kind?`, `include_archived?` |
| `get_group_doc` | Read a group's shared context document by relative path. Free-form — a group's context is whatever its work needs. | `id`, `path` |

| `get_execution_packet` | Read-only weak-agent entry point: returns one bounded implementation packet or a normal `ready:false, code:GATE_BLOCKED` refusal. Refusal precedence is non-ticket/legacy → spike → unmet leave-preparing requirements → unresolved questions → incomplete/unsafe taken location → occupied by another actor; `missing` contains exact raw requirements (or `[]` for occupancy/location). A later MCP client can deliberately resume an occupied ticket only by supplying both exact recorded values in `resume`; a missing or mismatched value remains refused. A taken ticket needs both branch and worktree, and its worktree may not be the board or another active ticket's recorded worktree. A ready packet includes project identity, ticket/body/taken details, ordered full group contexts, profile-resolved gates, fixed `plan`/`checklist`/`files` index documents with versions, sorted extra Markdown paths/versions, an ATX stop condition, and command hint. `ticket.taken` means validate and reuse that worktree/branch — do not create or take it again. Chore tickets need only their resolved plan; same-actor occupancy may continue only when its taken location is complete and safe. The call never takes, moves, writes, dispatches or creates a worktree. | `id`, `resume?` (`branch`, `worktree`) |
| `dispatch_task` | Mutating, policy-bound start of exactly one named core task for one existing ticket. Disabled by default; requires operator `KANMER_DISPATCH_*` enablement, provider/task allowlists, project fingerprint when advertised, ticket feasibility/occupancy checks and either successful host elicitation or explicit `preapproved` policy. Caller supplies only `ticket_id`, shared `provider`, shared `task` and optional bounded `timeout_ms`; no command, args, prompt, cwd, environment or log path. Refusals are normal `{ok:false,code,reason}` results and create no child/log. | `ticket_id`, `provider`, `task`, `timeout_ms?`, `expected_project?` |
| `list_dispatches` | Read-only active plus bounded recent lifecycle metadata for the configured project. Includes policy-disabled state and sanitized `dispatchId`, project/ticket/provider/task/requester/state/timestamps/exit/reason/recordingError only; raw tail, command, environment and local log path never cross MCP. | `ticket_id?`, `state?`, `include_recent?` |
| `cancel_dispatch` | Mutating project/policy-bound cancellation of one active opaque dispatch id. The server resolves the child and safely kills descendants; callers cannot supply a pid or process field. Records the cancelling actor and bounded reason, and returns sanitized status. | `dispatch_id`, `reason?`, `expected_project?` |

The execution packet's three fixed document keys are always present with `{exists, content, version}`; absent docs are normal `exists:false` entries. Extra docs expose only `{path, version}` and exclude those index paths. The stop-condition fallback is `Stop at the checklist; do not merge; do not start another ticket.` and the commands fallback is `Use only the commands named in the plan/checklist, record exact exit codes, and stop on a failure.` Refusals are normal JSON results, not MCP `isError` failures, so a weak agent can stop on `ready:false` without treating the board as broken.

## Write tools

Every mutating tool accepts `expected_project?` at its **top-level call boundary**. Read `get_status.project.fingerprint` first and send that value to fail closed before actor attribution, initialization, elicitation, or store mutation if a client is pointed at another project. It is never nested inside `create_item` fields or individual `create_items.items[]` entries. A mismatch retains plain `Error: …` text and adds `structuredContent.error.code: "WRONG_PROJECT"`; stale revision conflicts and document-gate refusals retain their legacy `Conflict: …` / `Error: …` text and add `REVISION_CONFLICT` / `GATE_BLOCKED`. Other errors remain unclassified.

| Tool | Purpose | Key params |
|---|---|---|
| `create_item` | Create a ticket. Returns the allocated id — tickets born in an area get that area's prefix (e.g. `API-007`), area-less ones the fallback prefix. Rejects an unknown `status`, `area`, `profile` or `groups` entry, and any `links`/`blocks` naming a nonexistent item — errors list the valid ids. **Creation is ungated**: a ticket may be created directly in any stage, which is what makes importing and backfilling finished work possible; gates apply on `move_item`. Pick a `profile` that matches the nature of the work (see below) — that is what decides how much evidence this ticket will owe. Link governing docs with `refs` (each must exist) or set `docs_todo`. | `type`, `title`, `status?`, `area?`, `profile?`, `requires?`, `groups?`, `assignee?`, `labels?`, `links?`, `blocks?`, `refs?`, `docs_todo?`, `commits?`, `prs?`, `deployment?`, `body?` |
| `create_items` | Bulk create up to 50 items in one call, sequential. Partial success: per-entry `{ ok, item \| error }` results in order — check them, don't assume all succeeded. | `items` (array of create_item fields) |
| `update_item` | Patch frontmatter and/or the body. Omitted fields are left alone, but a supplied `body` **replaces** the whole body — it is not merged. A patch that changes nothing is a no-op and does **not** bump `updated`. Changing a ticket's `area` moves its folder; the id never changes. `archived: true` hides from board. `type` **cannot** be changed — create a new item and archive the old one instead. Pass `expected_updated` (the `updated` you last read) when rewriting a body: if the item changed since, the call fails with a conflict telling you to re-read, instead of silently overwriting the newer version. Passing `[]` clears an array field (`refs`/`commits`/`prs`); `deployment: ""` clears deployment. Changing `profile` re-evaluates the gates immediately — a move blocked a moment ago may now be allowed. `groups` is how membership is set; there is no add/remove tool. | `id`, `title?`, `status?`, `area?`, `profile?`, `requires?`, `groups?`, `assignee?`, `order?`, `labels?`, `links?`, `blocks?`, `refs?`, `docs_todo?`, `commits?`, `prs?`, `deployment?`, `body?`, `archived?`, `expected_updated?` |
| `move_item` | Move an item to one of the six fixed stages. Enforces the ticket's **profile gates** and names the unmet requirement and boundary on failure — call `get_doc_gates` to self-check first. **A single move may cross at most one gated boundary**: writing every document and jumping straight to `done` is refused even though nothing is missing, because the pipeline is meant to be walked rather than satisfied at the end. Move one stage at a time; the refusal names the next one. Optional `position` places it within the column — `"top"`, `"bottom"`, or `{ after: "API-003" }` — maintaining the manual order the human sees. | `id`, `status`, `position?`, `expected_updated?` |
| `take_ticket` | Take a ticket before working it: records `taken_at` + `branch` (required) + `worktree?`, sets assignee (defaults to your client name), moves to the working stage (default `implementing`). Errors if already taken unless `force`. `action: "release"` clears the taken fields when the work ends. | `id`, `action` (`take`/`release`), `branch`, `worktree?`, `stage?`, `assignee?`, `force?` |
| `set_ticket_doc` | Write one pipeline document into a ticket's folder as plain Markdown, preserving frontmatter bytes when a SHA-bound record uses it. `doc` is a **per-area configured** doc id (`get_doc_gates`); an unknown id is rejected with the valid ids, and a doc that `requires` others is rejected until they exist. `append: true` adds below existing content — for running notes only. For frontmatter records use a whole-file write with append omitted/false; for free-form notes use `append_scratch`. Pass the `version` you last read from `get_ticket_doc` as `expected_version` and a concurrent edit is refused with a conflict; the result carries the new `version`. | `id`, `doc`, `content`, `append?`, `expected_version?` |
| `set_sources` | Replace the complete project-declared source preference list in board.yml. Explicit configuration only: no installation, enabling, authentication, trust grant, or network fetch. | `sources`, `expected_project?` |
| `fetch_source` | Fetch one applicable declared HTTPS llms.txt using the bounded same-origin depth-1 policy (32 direct pages, 2 MiB aggregate, 10-second timeout, 24-hour cache/validators). Writes only `.kanmer/data/sources` cache data and returns failures instead of hiding them. | `source_id`, `area?`, `labels?`, `force?`, `expected_project?` |
| `append_scratch` | Append a free-form running note to a ticket's scratch file (`scratch/<slug>.md`). Never gated or validated against the doc types — the agent's running notepad. Read it back with `get_ticket_doc(doc: "scratch/<slug>")`. Use whole-file `set_ticket_doc(doc: "scratch/review")` for frontmatter-backed SHA-bound records; successive note appends are separated by a blank line. | `id`, `slug?` (default `notes`), `content` |
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
 (which pipeline document types exist — `null` for legacy-layout items), `documentPaths`
 (the exact type-relative Markdown paths readable through `get_ticket_doc`, including Markdown in gate-exempt folders; readable does not mean gate-satisfying, and `null` means legacy-layout), `checklist`
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

Read every path in `documentPaths` before starting. It is the authoritative
inventory for a ticket folder; a bare document type still addresses only that
type's conventional index file.

Scratch is a folder like the rest, but its files are addressed by **slug**:
`append_scratch <id> review "…"` writes `scratch/review.md`, and you read it back
as `get_ticket_doc(doc: "scratch/review")`. The MCP doc path is the same
type-relative path with its `.md` suffix omitted.

| Type | Where it lives | Id prefix | Use for |
|---|---|---|---|
| `ticket` | `areas/<area\|_none>/<ID>/<ID>.md` | the area's `prefix`, else `idPrefixes.ticket` (`TICK`) | A unit of work that appears on the board |
| `plan` | **retired** — use `set_ticket_doc(doc: "plan")` | `PLAN` (legacy ids only) | Format-1 boards only |
| `research` | **retired** — use `set_ticket_doc(doc: "research")` | `RES` (legacy ids only) | Format-1 boards only |

`create_item` with `type: "plan"` or `"research"` is **rejected** — those live
inside a ticket folder as documents. Unmigrated format-1 boards still accept
them; call `get_status` to see which format a board uses.

## SHA-bound record schemas

These records are advisory in this horizon. The ordinary document gates remain
existence-based: a `FAIL` or `INCONCLUSIVE` proof file still satisfies the
structural proof gate, while the review/verify skills and future gate consumers
must stop rather than treat failing evidence as a pass. The records are plain
Markdown with YAML frontmatter and are parsed with `gray-matter`; do not extract
SHA fields with a regular expression.

### Review attestation

The physical path is `scratch/review.md`, addressed through MCP as
`scratch/review`. Write or replace the whole file with
`set_ticket_doc(doc: "scratch/review")`; do not use `append_scratch` for this
record. Read the current document version first and pass it as
`expected_version` on replacement so a concurrent review cannot be clobbered.

The frontmatter is exactly:

```yaml
kind: review-attestation
pr: "123"
head_sha: "<full reviewed PR head SHA>"
verdict: pass
reviewer: "reviewer-id"
independent: true
plan_hash: "<get_ticket_doc(doc: \"plan\").version>"
ticket_updated: "<ticket updated timestamp read for review>"
findings: []
```

`kind` is the literal `review-attestation`. `pr` is a non-empty string (a PR
number or URL). `head_sha` is the full reviewed commit id, normally 40 lowercase
hex characters. `verdict` is exactly `pass` or `needs-changes`; `reviewer` is a
non-empty stable identity; `independent` is boolean; `plan_hash` is exactly the
content-version returned by `get_ticket_doc(doc: "plan")`, not a separately
computed hash; `ticket_updated` is the ticket timestamp read for that review;
and `findings` is an ordered array.

Each finding is an ordered mapping with these keys and enums:

```yaml
- id: F-001
  severity: blocker # blocker | major | minor | note
  summary: "Non-empty finding summary"
  disposition: open # open | fixed | rejected-with-reason | accepted-risk | deferred-to-ticket
  reason: "Required for rejected-with-reason or accepted-risk"
  ticket: "MCP-025" # required for deferred-to-ticket
```

`id` is a stable `F-###`-style string and `summary` is non-empty. `reason` is
required for `rejected-with-reason` and `accepted-risk`, and optional otherwise.
`ticket` is required for `deferred-to-ticket`, and optional otherwise. The body
holds the human-readable change coverage, acceptance checks, finding details,
dispositions, and residual risk; frontmatter is the machine-facing authority.

### Proof record

The physical path is `proof/proof.md`, addressed through MCP as `proof`. Replace
it with whole-file `set_ticket_doc(doc: "proof")`, using the current document
version as `expected_version` when rewriting. Its frontmatter is exactly:

```yaml
kind: proof-record
merged_sha: "<full merge commit SHA>"
environment: "Windows 11 / Node 20 / local merged worktree"
verified_at: "<ISO-8601 timestamp>"
result: PASS
attempts: []
```

`kind` is the literal `proof-record`; `merged_sha`, `environment`, and
`verified_at` are non-empty strings; and top-level `result` is exactly one of
`PASS | FAIL | INCONCLUSIVE | NOT_APPLICABLE | WAIVED_BY_OPERATOR`.
`attempts` is chronological history. Each attempt contains:

```yaml
- attempted_at: "<ISO-8601 timestamp>"
  command: "<exact command or manual check>"
  cwd: "<repo-root-relative or injected path>"
  exit_code: 0 # integer, or null for manual/inconclusive checks
  result: PASS # PASS | FAIL | INCONCLUSIVE | NOT_APPLICABLE
  summary: "<observed output/result synopsis>"
```

An attempt's `result` is exactly `PASS | FAIL | INCONCLUSIVE | NOT_APPLICABLE`.
Failed and inconclusive attempts are retained in order when a later attempt
passes; a successful rewrite must not erase that history. `WAIVED_BY_OPERATOR`
is a top-level disposition only and requires the operator identity and reason in
the Markdown body; it is not a normal attempt result.

Which documents a ticket owes comes from its **profile**, not from its area and
not from a fixed pipeline — call `get_doc_gates` for the ticket's actual types
and boundaries. Creation is **ungated**, which is what makes importing and
backfilling finished work possible; `move_item` is where the gates apply.
