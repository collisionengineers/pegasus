# Kanmer MCP tool reference

Kept in sync with `packages/mcp-server/src/index.ts` — run
`node scripts/check-plugin-sync.mjs` after changing either side.

## Read tools

| Tool | Purpose | Key params |
|---|---|---|
| `get_status` | Orientation — call first, every session. Answers **which board** and **which server**. Board: `projectRoot` and `rootSource` (`flag`/`env`/`cwd`/`cwd-worktree`/`ancestor`/`ancestor-worktree`/`init`), `repoRoot` — what governing-doc `refs` resolve against — and `repoRootSource` (`flag`/`env`/`derived`), whether `.kanmer/` exists (never creates it), format version, board `source`, per-stage/per-type counts, archived/taken counts, warning count. `project` carries canonical `{ boardRoot, format, repoRoot, boardSource, fingerprint }`; use its versioned fingerprint as the optional optimistic project token when `compat.expectedProject` is `"optional"`. Server: a `server` block naming the build that is answering — `version`, the resolved `path`, the runtime `sha256`/`sha256Short` of its bytes, `mtime`, `size`, and `build` (`packaged`/`plugin`/`dev-standalone`/`dev-esm`/`unknown`). Two hosts on the same board can run different builds enforcing different gates; comparing `server.sha256` is how you see it. **The `server` block is absent on servers older than 0.3.3 — that absence is the signal, not an error**; individual fields are `null` if unreadable, and the call never fails over it. Repo: a `repo` block — `{ upToDate, stale: [{ artefact, state, detail, fix }] }` — saying whether this repo's Kanmer artefacts kept up with that build. Checked by content hash, not version string: the AGENTS.md managed block, the installed skills trees and their `.kanmer-skills-version` stamps, `board.yml`, and the provider MCP registrations. `state` is `behind` (act), `compensated` (old file, runtime already papers over it — informational), `unstamped` (no evidence either way) or `unknown` (unreadable); `upToDate` is true iff nothing is `behind`. Board format is not listed — it is the `format` field. Repair is never automatic: run `kanmer-setup`. **Absent on servers older than 0.3.4, and that absence is the signal.** Delivery: `delivery` is the project's fully resolved Git delivery policy (FRD-031) — `integrationBranch`, `releaseBranch`, `releaseCandidatePattern`, `hotfixBackport` — plus `source` (`board` when board.yml declares one, `default` for the shipped main-only policy). Read `source` rather than assuming: `default` on a project that believes it declared a policy means the block is gone from board.yml. `release` is the release-serialization read side (FRD-031, CORE-132): `channels[]` carries the current lease/candidate summary; ordered `attempts[]` retains every current and terminal attempt with outcome, failure reason, verification, retry schedule, PRs, tickets, tag, artifacts and predecessor/successor; `attemptCount`, `pendingTransactions[]` and `unreadable` make interrupted or corrupt state explicit. An empty `channels` array is what "the release-channel lease is clear" looks like; write through `release_channel`. Board sync: `boardSync` is `{ remoteBranch, localSha, remoteSha, ahead, behind }` against the last-fetched `origin/<branch>` ref, or `null` without a Git board or remote ref; `ahead > 0` means unpushed board commits the CI merge gate cannot see — confirm the board is pushed before treating a gate result as current. | — |
| `list_board` | Everything needed to orient, resolved: the six fixed `stages`, `areas` (each with its ticket id `prefix` and optional `defaultProfile`), `profiles` and `defaultProfile`, `groupKinds`, `proofTypes`, the `docTypes` vocabulary and `gateExemptFolders`, `boundaries`, and the governing-doc globs. The `source` field is `"file"` for a real board.yml, `"default"` for the synthesized default. | — |
| `list_projects` | Observe every **named** project endpoint in the operator-owned endpoint registry (FRD-029, MCP-054) — read-only, never a way to reach another board. One MCP process stays bound to exactly one project; the registry is a JSON file (`{ schema: 1, endpoints: { <name>: { boardRoot, repoRoot?, boardBranch?, policy? } } }`) whose location is fixed when the process is spawned (`KANMER_ENDPOINT_REGISTRY`, else `~/.kanmer/endpoints.json`). No request can supply a path; the only input is a registry **name** filter. Returns `registry` (`path`, `source` `env`/`default`, `exists`, `error`), `bound` (this process's `project_id`/`board_id`/`identity`/`fingerprint` and the registry `endpoint` name that is this project, or `null`), `endpoints[]` and `missing[]`. Each endpoint: `health` (`ok` / `unassigned` — legacy board without `project.json` / `missing-board` / `invalid` — malformed entry, see `problems` / `error`), `bound`, `project`, `location` (machine-local evidence with its own `fingerprint` digest), `boardSync`, `policy` (operator-declared label, echoed), `format`, `boardSource`, `ticketCount`, `controllers[]` (`{ controller, tickets }`) and `workspaces[]` (`{ ticket, branch, worktree, controller, claim: live/expired, expiresAt }`) from taken tickets, `problems[]`. Cross-project operations are observational only: to mutate another project, connect to **its** endpoint and pass **its** `project_id` as `expected_project` — sending it here is refused with `WRONG_PROJECT`. A missing registry is empty, not an error. Available when `get_status.compat.endpointRegistry` is `"optional"`. | `name?` |
| `get_sources` | Resolve project-declared MCP, plugin, and llms.txt preferences by area/labels. Host observations are explicit; declarations never install, enable, authenticate, or grant authority. | `area?`, `labels?`, `connected_mcp?`, `installed_plugins?` |
| `list_items` | Item summaries (see fields below; no body). Filters combine with AND. `group?` filters by membership — an unknown group id returns nothing rather than erroring, and this, not `get_group`, is how you build a working roster from a group, because summaries carry `profile`/`taken`/`docs` while `get_group`'s derived members carry only id/title/stage. Archived excluded by default; with `include_archived: true`, archived and active items are returned together and distinguished by the summary's `archived` field. Every ticket summary also carries `documentPaths` and a `batch` summary (`{ id, controller, frozenAt, state, members, workspace, branch }` or `null`). Active and releasing manifests are projected onto every immutable-roster member until manifest unlink, even after ticket-local fields clear, so fresh closeout can discover every active or archived member and the shared Git path after an interruption. Normally a plain array; if any `.kanmer` files are malformed or misnamed it returns `{ items, warnings }` instead — surface those warnings to the user rather than ignoring them. `profile?` filters on the ticket's explicit profile, which is how a roster excludes quick captures (`profile: "capture"` lists only them; any other value excludes them). | `type?`, `status?`, `area?`, `label?`, `profile?`, `group?`, `include_archived?`, `updated_since?`, `sort?` (`id`/`updated_desc`), `limit?` |
| `get_item` | Full frontmatter + Markdown body of one item; for tickets also `docs` presence, exact type-relative `documentPaths`, and `checklist` progress. | `id` |
| `get_ticket_doc` | Read one ticket document by **type-relative path**, or an ordered batch. Supply exactly one of legacy `doc` or `docs` (1–25 ids). Single responses remain `{id,doc,exists,content,version}`. Batch responses are `{id,documents:[{doc,exists,content,version}]}` after first-order deduplication. `content: null` is a normal missing document; invalid ids fail the whole call. Each version binds to returned bytes, so a batch is not an atomic snapshot. | `id`, `doc?` or `docs?` — `research`, `research/azure/tokens.md`, `scratch/notes`. A bare type resolves to that folder's index. |
| `search_items` | Full-text search over id, title, body, labels, assignee — so a quick capture is found by the words of its observation, which is stored as the body. Returns summaries for matching non-archived items and projects batch metadata onto those matches through manifest unlink. It does not include archived results and is never a complete batch-roster census; closeout uses `list_items include_archived: true`. | `query`, `type?`, `profile?` |
| `get_links` | Forward links + backlinks for an item, with titles, plus the typed dependency edges: `blocks` (stored on the blocker) and `blockedBy` (derived, never stored). | `id` |
| `get_activity` | The change log: one `{ts, id, op, field, from, to, actor}` entry per mutation, oldest-first. This is what makes "X moved to review yesterday" a fact instead of an inference. Derived convenience — safe to delete, never truth. | `id?`, `since?`, `limit?` |
| `get_doc_gates` | **Call this before any move.** With `id`: the ticket's resolved `profile`, every gated `boundary` with each requirement and whether it is satisfied, non-blocking `warnings`, plus `reachable` stages and `blockedBy` reasons per stage — so you self-check instead of failing into a gate. It also returns per-type counts and exact type-relative `documentPaths`, which are the safe inputs to `get_ticket_doc`. Requirements vary per ticket by profile, so this is the only reliable source; do not assume a fixed pipeline. Without `id`: the board's profiles, boundaries, doc vocabulary, proof types and governing-doc globs. | `id?` |
| `get_group` | A group with its **derived** membership: every ticket naming it, with title and stage, plus per-stage progress. Computed on every read, so it cannot go stale. Read a member ticket's groups before working it — the shared context is part of the ticket's context. | `id` |
| `list_groups` | Every group, optionally by kind. Archived excluded unless asked. | `kind?`, `include_archived?` |
| `get_group_doc` | Read a group's shared context document by relative path. Free-form — a group's context is whatever its work needs. | `id`, `path` |

| `get_execution_packet` | Read-only weak-agent entry point: returns one bounded implementation packet or a normal `ready:false, code:GATE_BLOCKED` refusal. Refusal precedence is non-ticket/legacy → spike → unmet leave-preparing requirements → unresolved questions → incomplete/unsafe taken location → pending/inconsistent batch or batch authority mismatch → occupied isolated ticket → (only with `step`) a plan that cannot be compiled into a bounded step; `missing` contains exact raw requirements (or `[]` for occupancy/location/step compilation). A later MCP client can deliberately resume an occupied isolated ticket only by supplying both exact recorded values in `resume`; a missing or mismatched value remains refused. Batch packets additionally require the same nonempty durable `controller_run` persisted in the manifest, and exact-match it together with the actual MCP request actor; a copied label or exact resume path cannot transfer batch authority. A taken ticket needs both branch and worktree, and its worktree may not be the board, any physical child of a dedicated board worktree, or another active ticket's recorded worktree. A ready packet includes project identity, ticket/body/taken details, `ticket.revision`, and the authorised `ticket.workspace`, ordered de-duplicated group contexts each with a `context.md` `version`, profile-resolved gates, fixed `plan`/`checklist`/`files` index documents with versions, sorted extra Markdown paths/versions, an ATX stop condition, a command hint, and an **advisory** FRD-033 plan `validation` report (`{ok, blockers, advisories, findings[]}`) whose findings never refuse a whole-ticket packet. `ticket.taken` means validate and reuse that worktree/branch — do not create or take it again. For an untaken frozen member it truthfully remains null while `ticket.workspace`, top-level `claim.workspace`, `claim.batch.branch` / `claim.batch.workspace`, and a compiled `step.workspace` expose the immutable shared location to use on the later take; no second member worktree is created. Chore tickets need only their resolved plan; same-actor occupancy may continue only when its taken location is complete and safe. Supplying `step` (a 1-based ordered-step index, or `"next"` for the first step the checklist has not ticked) requires at least one mapped unchecked checklist marker for that selected ordered step, then compiles one bounded **step packet** and makes the structural validation findings blocking: the added `step-packet/2` block carries a full 64-character SHA-256 `packetId`, project + ticket revision/authority/counted-document census + batch + exact branch/worktree/HEAD/dirty-entry baseline + plan path/version + exact checklist snapshot + step identity, `allowedFiles`, `allowedSymbols`, `forbiddenFiles`, preconditions, required/preserved/forbidden behaviour, negative cases, tests, commands, expected output, done condition, deviation stop, a one-step stop condition, and the two evidence layers (`group` and `ticket`) with their content versions. A plan that cannot be compiled refuses normally and carries the same `validation` report; a refusal writes nothing. A later numeric or `"next"` request also supplies the complete exact controller-retained predecessor as `prior_step_packet`; it is issued only after that exact packet reconciles PASS, and a short id, worker-returned packet, reconstruction or numeric skip is refused. The call never takes, moves, writes, dispatches or creates a worktree. | `id`, `resume?` (`branch`, `worktree`), `controller_run?` (required for a batch), `step?` (positive integer or `"next"`), `prior_step_packet?` (complete exact `step-packet/2`) |
| `reconcile_ticket` | Read-only reconciliation inspector for one existing ticket (FRD-028 dry-run half). Collects the fixed board, claim, proof, recorded-workspace, local release-sidecar and PR/required-check facts. With optional `step_packet`, it strictly verifies the complete `step-packet/2` shape before Git, reconstructs its plan/evidence/ticket authority from a bounded stable snapshot, and compares the packet baseline with bounded read-only HEAD/index/worktree evidence; caller-supplied changed paths are never proof. It returns typed PASS/FAIL/INCONCLUSIVE `step` evidence: missing, unreadable, unstable, escaped, symlinked or hard-linked facts and iterative path-match budget exhaustion are inconclusive, while forbidden/undeclared paths, stale authority, contradictory documents or any checklist change beyond the selected unchecked-to-checked marker fail. Free-form symbol names cannot be mechanically proven from Git paths, so any actual change with non-empty `allowedSymbols` adds `STEP_SYMBOL_SCOPE_INCONCLUSIVE`; a forbidden or undeclared path FAIL takes precedence, no-change invents no symbol finding, and empty symbols preserve file-scoped PASS. Marker reconciliation preserves every other raw line body, CRLF/CR/LF terminator and final-newline state exactly. The controller supplies only its exact retained packet — `packetId` is tamper-evident identity, not authentication — and a worker-returned or reconstructed packet is never authority. The optional `step` block is additive: even an invalid or stale packet skips step Git but preserves the ordinary evidence and recommendation. The ordinary inspector returns typed `findings` plus one **advisory** `recommendation` — `MOVE_TO_IMPLEMENTING` / `MOVE_TO_VERIFYING` / `MOVE_TO_DONE` / `ROUTE_VERIFICATION_FAILURE` / `RELEASE_CLEAN_TERMINAL_CLAIM` / `RECOVER_EXPIRED_CLAIM`, with `advisory: true`, the `ticketId` and the document-inclusive `revision` it was computed from — or `null`. **This call itself never mutates anything**: it does not touch the board, Git, workspace, checks or release state. Applying a recommendation is a separate explicit call to `apply_reconciliation` with that `revision`, and an operator/controller may equally act on it through the ordinary tools. A FAIL proof in Verifying routes by its `failure_class`: `implementation` → Implementing, `plan` → Preparing, while `transient`, `inconclusive` and any absent or unrecognised class recommend **nothing** and leave the ticket in Verifying. `evidence.claim.state` is `current` / `expired` / `unclaimed` with `expiresAt`, `reviewRound` and `remediationBudget`; worktree identity is proven by Git common directory. Unavailable GitHub/CI/workspace/release facts are reported as `EVIDENCE_INCONCLUSIVE`, never invented. | `id`, `step_packet?` (complete exact `step-packet/2`) |
| `dispatch_task` | Mutating, policy-bound start of exactly one named core task for one existing ticket. Disabled by default; requires operator `KANMER_DISPATCH_*` enablement, provider/task allowlists, project fingerprint when advertised, ticket feasibility/occupancy checks and either successful host elicitation or explicit `preapproved` policy. Caller supplies only `ticket_id`, shared `provider`, shared `task` and optional bounded `timeout_ms`; no command, args, prompt, cwd, environment or log path. Refusals are normal `{ok:false,code,reason}` results and create no child/log. | `ticket_id`, `provider`, `task`, `timeout_ms?`, `expected_project?` |
| `list_dispatches` | Read-only active plus bounded recent lifecycle metadata for the configured project. Includes policy-disabled state and sanitized `dispatchId`, project/ticket/provider/task/requester/state/timestamps/exit/reason/recordingError only; raw tail, command, environment and local log path never cross MCP. | `ticket_id?`, `state?`, `include_recent?` |
| `cancel_dispatch` | Mutating project/policy-bound cancellation of one active opaque dispatch id. The server resolves the child and safely kills descendants; callers cannot supply a pid or process field. Records the cancelling actor and bounded reason, and returns sanitized status. | `dispatch_id`, `reason?`, `expected_project?` |

The execution packet's three fixed document keys are always present with `{exists, content, version}`; absent docs are normal `exists:false` entries. Extra docs expose only `{path, version}` and exclude those index paths. The stop-condition fallback is `Stop at the checklist; do not merge; do not start another ticket.` and the commands fallback is `Use only the commands named in the plan/checklist, record exact exit codes, and stop on a failure.` Refusals are normal JSON results, not MCP `isError` failures, so a weak agent can stop on `ready:false` without treating the board as broken.

Both packet routes bind the requested ticket record, then complete one canonical
metadata census and preflight every per-file and aggregate byte bound before
opening ticket-document, group-record or context content. Those bytes are read
through identity-bound capped handles; replacement, growth, symlink,
special-file or hard-link evidence refuses. Scratch and reference documents
remain revision-exempt while still consuming inventory and aggregate bounds.
Physical confinement is anchored at the configured project root: a junction at
that root is allowed, but any symlink or junction below it, including `.kanmer`
and ticket, document or group directories, refuses.

Step-packet file declarations use canonical repository-relative POSIX paths:
literals, `*` within one segment, and a whole-segment `**`. Plan declarations
normalize benign backslashes to POSIX; absolute paths, traversal, colon forms
and unsupported pattern syntax are refused. Packet wire paths and observed Git
paths must already be canonical and refuse backslashes.
Expected-file patterns authorize only equal-or-narrower step declarations;
every intersecting forbidden pattern wins. Git-observed filenames retain exact
bytes (including whitespace, Unicode and newlines) and are never declaration-
normalized before classification.
Only an exact level-three `### Step N — <title>` heading is a structured
boundary: declared numbers start at 1 and remain contiguous, while nested or
explanatory headings never become steps. Named checklist authority exists only
when the checkbox label begins with `Step N`; an explanatory prose mention of
`step N` never maps that checkbox to a step.
The bounded Git census covers tracked, staged, unstaged and untracked paths plus
both rename endpoints. Changed-path evidence also includes one bounded complete
union of every path touched by every intervening commit, including paths later
reverted; a non-ancestor baseline or exhausted history is `INCONCLUSIVE`.
That history census validates both old and new modes from every intervening
tree edge; any intervening `120000` symbolic-link or `160000` Git-link mode
refuses even if a later commit restores a regular endpoint.
A packet workspace HEAD is a full 40- or 64-character
Git object ID. Every sample hashes one bounded NUL
`git ls-files -v -s -z` index census, binding flag, mode, object id, stage and
path: assume-unchanged or skip-worktree entries refuse; nonzero stages and
gitlinks refuse without index mutation; census drift is `INCONCLUSIVE`. On
filesystems that expose the owner-executable bit, every clean tracked regular
path must agree with its indexed `100644`/`100755` executable class;
disagreement refuses. A tracked
mode-`120000` path is retained only when its checkout representation and capped
target bytes are identity-bound and its physical target is an indexed tracked
regular file inside the worktree; external, chained-external, dangling,
unreadable, unstable or over-budget links refuse.
Tracked-link target bytes retain a leading UTF-8 BOM.
Ignored or untracked link targets refuse. Execution authority requires exactly
one selected ticket endpoint across v2 areas and legacy v1 storage; duplicates
refuse before either record is opened. Ignored paths and `.git` / common-directory metadata are
outside it and constrained workers must never mutate them; any need or attempt
is a deviation stop recorded as `INCONCLUSIVE`, never an inferred PASS from an
absent path. Symlink, hard-link, containment, encoded-byte, entry-count,
checklist-line and aggregate collection-time limits also fail closed.
Iterative path matching has its own aggregate work budget; exhaustion is
reported as `INCONCLUSIVE`, never converted into authorization or an
undeclared-path failure. Checklist marker reconciliation preserves raw line
bodies, CRLF/CR/LF terminators and final-newline state byte-for-byte. Exact
checklist bytes retain a leading UTF-8 BOM. Compilation and strict verification
derive every marker state from those bytes, require a completed prefix and
unfinished selected step, and refuse any checked successor marker.
Plan-time glob containment and intersection have a separate aggregate proof
context that charges alphabet construction, NFA closure/transitions, caches and
queues; exhaustion reports `PLAN_GLOB_COMPLEXITY` instead of silently
classifying the relationship.

## Write tools

Every mutating tool accepts `expected_project?` at its **top-level call boundary**. Read `get_status.project` first and send its `project_id` (the board's stable logical identity, FRD-029; the same across copies of the board at other paths or machines) — or, for a board still reporting `identity: "unassigned"`, its legacy machine-local `fingerprint` — to fail closed before actor attribution, initialization, elicitation, or store mutation if a client is pointed at another project. It is never nested inside `create_item` fields or individual `create_items.items[]` entries. A mismatch retains plain `Error: …` text and adds `structuredContent.error.code: "WRONG_PROJECT"`; stale revision conflicts and document-gate refusals retain their legacy `Conflict: …` / `Error: …` text and add `REVISION_CONFLICT` / `GATE_BLOCKED`. Other errors remain unclassified, but **every** result — reads, writes and errors alike — carries `structuredContent.project` (`project_id`, `board_id`, `fingerprint`) naming the logical project that answered. A successful call's `structuredContent` also carries the whole payload under `result`, mirroring `content[0].text` exactly, so a client that renders structured content in preference to text shows the result and not just the project stamp; an error result carries `error` there instead and no `result`.

Ticket mutations (`update_item`, `move_item`, `take_ticket`, `set_ticket_doc`, `append_scratch`, `link_doc`, `link_items`) additionally accept `expected_revision?`: the document-inclusive `revision` read from `get_item` (also returned by `set_ticket_doc` and in an execution packet's `ticket.revision`). Unlike `expected_updated`, it changes whenever **any** pipeline document — plan, proof, review record — is rewritten, so a proof written by someone else since your read is a `REVISION_CONFLICT`, not a silent overwrite. Scratch notes and `reference/` inputs are excluded from it. The revision is computed on read and never stored in frontmatter. On `take_ticket` it applies to every action — `take`, `release`, `renew` and `transfer` — and a stale value is refused before anything is written.

`get_status.project` also reports `location` — repo path, board path, machine, board branch, remote origin (with any embedded `user:token@` userinfo stripped) and a versioned `location.fingerprint` digest — as machine-local evidence of where this board is. It is never identity: a missing or changed remote origin is reported there and does not reassign `project_id`. A legacy board without `.kanmer/project.json` receives its identity once, on its first accepted write or `migrate_board`, with the prior fingerprint recorded as the auditable fallback (`origin: "migrated"`, `migratedFrom`, and a `board`/`project_id` activity entry).

| Tool | Purpose | Key params |
|---|---|---|
| `create_item` | Create a ticket. Returns the allocated id — tickets born in an area get that area's prefix (e.g. `API-007`), area-less ones the fallback prefix. Rejects an unknown `status`, `area`, `profile` or `groups` entry, and any `links`/`blocks` naming a nonexistent item — errors list the valid ids. **Creation is ungated**: a ticket may be created directly in any stage, which is what makes importing and backfilling finished work possible; gates apply on `move_item`. Pick a `profile` that matches the nature of the work (see below) — that is what decides how much evidence this ticket will owe. Link governing docs with `refs` (each must exist) or set `docs_todo`. `profile: "capture"` files a quick observation instead: it needs a `title` and a `body` (the observation, refused when blank) plus optional `capture_evidence`, owes no document, and must never be given `docs_todo`. | `type`, `title`, `status?`, `area?`, `profile?`, `requires?`, `groups?`, `assignee?`, `labels?`, `links?`, `blocks?`, `refs?`, `docs_todo?`, `commits?`, `prs?`, `deployment?`, `capture_evidence?`, `capture_actor?`, `body?` |
| `create_items` | Bulk create up to 50 items in one call, sequential. Partial success: per-entry `{ ok, item \| error }` results in order — check them, don't assume all succeeded. | `items` (array of create_item fields) |
| `update_item` | Patch frontmatter and/or the body. Omitted fields are left alone, but a supplied `body` **replaces** the whole body — it is not merged. A patch that changes nothing is a no-op and does **not** bump `updated`. Changing a ticket's `area` moves its folder; the id never changes. `archived: true` hides from board. `type` **cannot** be changed — create a new item and archive the old one instead. Pass `expected_updated` (the `updated` you last read) when rewriting a body: if the item changed since, the call fails with a conflict telling you to re-read, instead of silently overwriting the newer version. Passing `[]` clears an array field (`refs`/`commits`/`prs`); `deployment: ""` clears deployment, and `""` clears any one `delivery_*` field the same way. **Delivery state** (FRD-031) is recorded here and is never a gate input: `delivery_state` is `not-integrated` / `integrated` / `release-candidate` / `released` / `deployed` / `production-verified`; `integrated` and beyond need `delivery_branch` (this project's integration branch, or its release branch for a hotfix) plus a full 40-character `delivery_sha`; `released` and beyond also need `delivery_release_branch` + `delivery_release_tag`; `release-candidate` needs a `delivery_candidate` matching the project's `delivery.releaseCandidatePattern`. The whole record is re-validated on every write, so a two-call sequence is judged exactly like one call. `delivery_backport_required` is **derived**, not settable: delivering on the release branch of a project whose release branch differs from its integration branch records the backport owed, and only a real 40-character `delivery_backport_sha` clears it. Changing `profile` re-evaluates the gates immediately — a move blocked a moment ago may now be allowed. `groups` is how membership is set; there is no add/remove tool. This is also where a quick capture is **promoted**: `capture_disposition` is one of `duplicate` (with `capture_result` naming the ticket it merges into — linked and archived), `already-fixed` / `not-required` (archived), `batch` (with `capture_result` naming the batch **and** a non-capture `profile`), `promoted` (with that `profile`), or `retained` (stays a capture; the only decision that may later be superseded). The disposition, its result, the deciding actor and the timestamp are recorded in one atomic write, and the new profile's gates apply from that decision onward, never retroactively. | `id`, `title?`, `status?`, `area?`, `profile?`, `requires?`, `groups?`, `assignee?`, `order?`, `labels?`, `links?`, `blocks?`, `refs?`, `docs_todo?`, `commits?`, `prs?`, `deployment?`, `delivery_state?`, `delivery_branch?`, `delivery_sha?`, `delivery_candidate?`, `delivery_release_branch?`, `delivery_release_tag?`, `delivery_backport_sha?`, `capture_evidence?`, `capture_disposition?`, `capture_result?`, `body?`, `archived?`, `expected_updated?` |
| `move_item` | Move an item to one of the six fixed stages. Enforces the ticket's **profile gates** and names the unmet requirement and boundary on failure — call `get_doc_gates` to self-check first. **A single move may cross at most one gated boundary**: writing every document and jumping straight to `done` is refused even though nothing is missing, because the pipeline is meant to be walked rather than satisfied at the end. Move one stage at a time; the refusal names the next one. Optional `position` places it within the column — `"top"`, `"bottom"`, or `{ after: "API-003" }` — maintaining the manual order the human sees. **Backward moves need a `reason`** (`BACKWARD_MOVE_NEEDS_REASON`); Review → Implementing additionally needs a `needs-changes` attestation in `scratch/review.md` naming this ticket's PR, or a reason beginning `operator:` (`REVIEW_RETURN_NEEDS_ATTESTATION`), and is refused with `REMEDIATION_BUDGET_EXHAUSTED` once `review_round` reaches `remediation_budget` (default 1). Every backward move is recorded under `## Transitions` in `scratch/execution.md`. | `id`, `status`, `position?`, `expected_updated?`, `reason?` |
| `take_ticket` | Acquire a ticket's **workspace lease** before working it (FRD-030): records `taken_at` + `branch` (required) + `worktree?`, sets assignee (defaults to your client name), stamps `claim_expires_at` (board `claimExpiryMinutes`, default 30) and `claim_controller`, mints the lease record (`lease_id`, `lease_revision`, `lease_workspace`, `lease_phase`, `lease_heartbeat_at`, plus `controller_run`/`worker_run`/`provider` when given) and moves to the working stage (default `implementing`). One live writer per workspace: a worktree or branch recorded on another taken ticket refuses with `WORKSPACE_OCCUPIED` (`force` does not bypass it); an already-taken ticket refuses with `LEASE_LIVE` unless `force`. **Batch mode** is the one deliberate exception: the first member's take passes `batch` (an id), `batch_members` (the complete list of two or more related ticket ids including itself), and a nonempty `controller_run` retained in the controller's durable run record across reconnects/restarts. Under the board write lock, Kanmer writes a hash-bound `pending` declaration manifest before changing any member, rolls every endpoint to its intended bytes, and publishes the compact immutable roster as `active`; closeout changes it to `releasing` until every terminal member is cleared. Pending, active and releasing manifests persist the exact pair of the actual MCP request actor and that durable run id. Declaration, pending recovery, every later member take, batch renew and batch execution packet must exact-match both values; `assignee`, `controller`, copied owner labels and exact workspace paths are never authority. Recovery also repeats the exact declaring ticket, roster, branch, worktree and request facts. Missing/mismatched runs, other actors, partial/extra rosters and per-member transfer are refused (`BATCH_RUN_REQUIRED`, `BATCH_OWNER_MISMATCH`, `BATCH_FROZEN`, or `BATCH_TRANSACTION_*`); unreadable or contradictory state fails closed. Members share one PR/head but each needs its own exact-PR, exact-head independent pass attestation and proof. `release` refuses `BATCH_ACTIVE` until the complete immutable roster is Done or archived, then resumes idempotently through `releasing`; terminal release is deliberately not actor-bound, so a fresh closeout agent may finish it without the implementation actor or `controller_run`. `action: "renew"` is the heartbeat — renew at least every `get_status.leases.heartbeatMinutes` (5) and before long commands. Every modern manifest-backed batch renew supplies its exact `controller_run` and both current `lease_id` and `lease_revision`; it has no no-token compatibility fallback. A non-current `lease_id` is `LEASE_EXPIRED` (the lease was reclaimed — stop), a stale `lease_revision` is `REVISION_CONFLICT`, nothing is written on refusal, and an expired lease nobody reclaimed still renews for its holder. `phase: "running-command"` + `extend_minutes` is the explicit long-command state, bounded by `leaseCommandMaxMinutes` (120). Outside batch mode, a renew naming no lease falls back to the owner check (`CLAIM_NOT_OWNED`) and migrates a legacy claim into a lease. `action: "transfer"` reclaims an **expired isolated** lease for the caller (or `assignee`): it re-reads worktree/branch/PR/commit/proof evidence first, records it with the old and new controller in `scratch/execution.md`, keeps the recorded branch/worktree and any dirty work, and refuses `RECOVERY_REFUSED` for a board or foreign-repository workspace, or one not checked out on the recorded branch; a live lease refuses with `CLAIM_LIVE` unless `reason` begins `operator:`. `action: "release"` clears the claim and lease at closeout; batch ownership is cleared through its recoverable `releasing` manifest. Never `force` to recover a dead controller's ticket — transfer only an isolated expired lease. | `id`, `action` (`take`/`release`/`transfer`/`renew`), `branch`, `worktree?`, `stage?`, `assignee?`, `controller?`, `reason?`, `force?`, `lease_id?`, `lease_revision?`, `phase?`, `extend_minutes?`, `controller_run?` (required for batch take/renew), `worker_run?`, `provider?`, `batch?`, `batch_members?`, `expected_revision?` |
| `set_ticket_doc` | Write one pipeline document into a ticket's folder as plain Markdown, preserving frontmatter bytes when a SHA-bound record uses it. `doc` is a **per-area configured** doc id (`get_doc_gates`); an unknown id is rejected with the valid ids, and a doc that `requires` others is rejected until they exist. `append: true` adds below existing content — for running notes only. For frontmatter records use a whole-file write with append omitted/false; for free-form notes use `append_scratch`. Pass the `version` you last read from `get_ticket_doc` as `expected_version` and a concurrent edit is refused with a conflict; the result carries the new `version`. | `id`, `doc`, `content`, `append?`, `expected_version?` |
| `set_sources` | Replace the complete project-declared source preference list in board.yml. Explicit configuration only: no installation, enabling, authentication, trust grant, or network fetch. | `sources`, `expected_project?` |
| `fetch_source` | Fetch one applicable declared HTTPS llms.txt using the bounded same-origin depth-1 policy (32 direct pages, 2 MiB aggregate, 10-second timeout, 24-hour cache/validators). Writes only `.kanmer/data/sources` cache data and returns failures instead of hiding them. | `source_id`, `area?`, `labels?`, `force?`, `expected_project?` |
| `append_scratch` | Append a free-form running note to a ticket's scratch file (`scratch/<slug>.md`). Never gated or validated against the doc types — the agent's running notepad. Read it back with `get_ticket_doc(doc: "scratch/<slug>")`. Use whole-file `set_ticket_doc(doc: "scratch/review")` for frontmatter-backed SHA-bound records; successive note appends are separated by a blank line. | `id`, `slug?` (default `notes`), `content` |
| `link_doc` | Maintain a ticket's `refs[]` — repo-relative paths to governing docs (PRD/FRD/ADR) in the repo's own `/docs/`. `add` validates the path exists under the project root; `remove` drops it. Distinct from `link_items` (item↔item); this is item↔repo-file. A linked governing doc satisfies the leave-backlog gate. | `id`, `path`, `action` (`add`/`remove`) |
| `link_items` | Add/remove a structured relation source → target. `rel: "relates"` (default) writes `links[]`; `rel: "blocks"` writes `blocks[]` — source blocks target, and blocked-by derives from it. `add` requires the target to exist; `remove` works even on dangling links so they can be cleaned. | `source_id`, `target_id`, `action` (`add`/`remove`), `rel?` (`relates`/`blocks`) |
| `migrate_board` | Bring the board fully current: run the v1→v2 migration if needed, then backfill the 7-stage default (alias-aware, additive — never renames/reorders existing stages, never touches item files). Also performs the one-time identity migration (FRD-029), reported under `identity`. `dry_run: true` previews what would move, which stages would be added and whether an identity `wouldAllocate` — a dry run is read-only and does not initialise the board. | `dry_run?` |
| `add_column` | Add an **area** — the only configurable column. Stages are fixed and priority no longer exists, so neither can be added. `color` is a hex string; `prefix` (2–6 uppercase alphanumerics) sets the ids of tickets born there. Rejects an id that already exists. | `id`, `name`, `kind` (`area`), `color?`, `prefix?` |
| `update_column` | Rename/recolour a column, or pin an area's `prefix`. The column id itself is immutable. | `kind`, `id`, `name?`, `color?`, `prefix?` |
| `reorder_columns` | Reorder areas; `order` must be a permutation of the existing ids. Stages cannot be reordered — they are constants. | `kind` (`area`), `order` |
| `create_group` | Create an `epic` (these ship together) or a `horizon` (this is what matters now). Returns the allocated id. Add members with `update_item(groups: [...])`. | `kind`, `title`, `body?` |
| `update_group` | Patch a group's own fields: `title`, `body`, `archived`. Omitted fields are left alone; a supplied `body` **replaces** the whole body. A patch that changes nothing does **not** bump `updated`. `archived: true` retires the group — it drops out of `list_groups` unless `include_archived`, stays readable, and its **members are untouched**; this is the retirement path, there is no delete. `kind` **cannot** be changed — the id prefix is allocated from it — and membership is not patchable here either: it lives on tickets via `update_item(groups: [...])`. Pass `expected_updated` (the `updated` you last read) to be rejected with a conflict instead of overwriting a concurrent edit. | `id`, `title?`, `body?`, `archived?`, `expected_updated?` |
| `set_group_doc` | Write shared context into a group's folder — the decision or constraint every member sits under, rather than repeating it per ticket. Cannot write the group's own `<ID>.md`; use `update_group` for that. | `id`, `path`, `content` |
| `apply_reconciliation` | Apply the one recovery action `reconcile_ticket` currently recommends, and only while it is still current (FRD-028 acceptance 2-4). **You never supply the action**: this first finishes at most one already-authorised bounded release transaction left by an interrupted writer, then re-collects and re-classifies through the same read-only inspector; `reconcile_ticket` itself remains mutation-free. Git/GitHub and full release-history collection stay outside the write lock; a constant-size release transaction epoch is sampled around that snapshot and rechecked inside the lock. If release evidence changes, the apply retries its bounded collection and proceeds only when the freshly classified evidence is still safe; otherwise `RECONCILIATION_DRIFT` refuses it. This binds release observation, ticket CAS and mutation without scanning retained history in the critical section. `expected_revision` is the `revision` carried by the recommendation you are applying — document-inclusive, so a proof, plan or review record rewritten since is a structured `REVISION_CONFLICT` and no ticket or reconciliation audit record is written; prior release crash recovery may still complete before that refusal. No recommendation at all is the normal `RECONCILIATION_INCONCLUSIVE` refusal, not an error; a ticket that changed while its evidence was being collected is `RECONCILIATION_DRIFT`. The action set is exhaustive and composed only of existing verbs: `MOVE_TO_VERIFYING` (merged Review), `MOVE_TO_DONE` (PASS proof in Verifying), `MOVE_TO_IMPLEMENTING` (closed-unmerged or worker-less Review), `ROUTE_VERIFICATION_FAILURE` (a FAIL proof's `failure_class`), `RELEASE_CLEAN_TERMINAL_CLAIM` (a Done ticket's clean, identity-matched claim — it releases the **claim**, never the worktree) and `RECOVER_EXPIRED_CLAIM` (transfer an expired lease, preserving branch, worktree and any dirty work). **Authority is not widened**: a backward move is judged by the ordinary contract, so Review → Implementing still needs a bound `needs-changes` attestation or a `reason` beginning `operator:` (`REVIEW_RETURN_NEEDS_ATTESTATION` otherwise), and a live lease still refuses with `CLAIM_LIVE`. Each applied action appends exactly one durable line to `## Transitions` in `scratch/execution.md` naming the action, the stage or controller change and the revision. It never deletes a worktree or branch, cleans or force-pushes a workspace, bypasses a required check, adds a stage, or mutates the Kanmer board worktree, which is refused as a target in every path. | `id`, `expected_revision`, `reason?`, `controller?` |
| `release_channel` | Serialize releases on one channel (FRD-031). **One renewable lease owns a release channel at a time**, and a release attempt is an immutable-identity record: `attempt_id`, normalized `channel`, `ordinal`, `candidate_id`, `candidate_ref`, `integration_sha`, `release_branch`, `delivery_policy_version`, `created_at`, `owner` and `supersedes` are frozen at mint (`RELEASE_CANDIDATE_IMMUTABLE`), and a terminal attempt is frozen whole (`RELEASE_ATTEMPT_TERMINAL`) so a failed release keeps its exact proof forever. `acquire` binds the exact integration SHA to the delivery-policy version read before Git resolution and refuses policy drift inside the lock; its configured integration branch is resolved through `refs/heads`, while an explicit `integration_ref` is preserved. A durable per-channel head survives successful lease clearing, supplies the only next ordinal without scanning history under the board write lock, and makes a lost highest attempt fail closed instead of freeing its identity. Lock-free snapshots also require the complete canonical ordinal history below the head, so losing an older proof is unavailable rather than neutral. Absent ownership is free only before first mint, while malformed/unreadable ownership fails closed; schema-1 records reject unknown keys, and an active lease, head and attempt must agree on current identity and owner. Every mutation first publishes a constant-size pending epoch, then journals the immutable-attempt, head and mutable-lease endpoints together. Journal admission is a closed union of the six legal transitions, and recovery preflights every CAS before writing, so interruption has an idempotent path without permitting an extra history rewrite or partial commit. Channel case is normalized and Windows device names are refused so filesystem aliases cannot create two owners. `renew` is the heartbeat; `record` also renews expiry and writes progress. A bounded `service_unavailable` schedule freezes whole at exhaustion. `supersede` is remediation/reclaim: authorization uses the real calling actor, not observable owner text; an active incumbent becomes `superseded`, while an already failed terminal attempt stays byte-for-byte failed and the successor records its predecessor. A successor starts with empty evidence. `complete` records the release and clears the lease but retains the high-water head; `fail` retains the lease and exact failure proof. Every action but `acquire` names the current `lease_id` and `lease_revision`, and ordinary `renew`/`record`/`complete`/`fail` calls must also come from the actual owner — public CAS values are not authority. Fields outside the selected action and a supersede missing its lease CAS are refused before Git or mutation, and contradictory unavailable/recovered observations are refused. Read complete current and terminal evidence from `get_status.release`; snapshots fail closed during concurrent or corrupt cross-record state, while causal successor traversal prevents a ticket deliberately dropped from a successor's fresh roster from remaining frozen behind its predecessor. Records live in `.kanmer/releases/`, never in `board.yml`, and release evidence is **never** a gate. | `action` (`acquire`/`renew`/`record`/`supersede`/`complete`/`fail`), `channel?`, `integration_sha?`, `integration_ref?`, `lease_id?`, `lease_revision?`, `reason?`, `release_tag?`, `verification_state?`, `included_prs?`, `included_tickets?`, `artifact_manifest?`, `service_unavailable?`, `service_recovered?` |

Constrained packet evidence uses one lexical, de-duplicated group census in
both whole-ticket and step issuance. Counted ticket documents plus unique group
ids are capped at 256 before any group or context read; missing or conflicting
resolved identity refuses. Board authority is metadata-first and read through
identity-bound capped handles after its complete per-file and aggregate
preflight. The shared path-match budget is charged before raw
path parsing and before every literal or wildcard comparison, so exhaustion is
`INCONCLUSIVE`. Dirty regular-file bytes are read once through one capped handle
whose pre-open, handle-before/after and post-path device, inode, type, mode,
link-count and size facts agree; the handle closes on every result. Every Git
sample hashes the bounded NUL `git ls-files -v -s -z` index census described
above; hidden flags, mode/object/stage drift and unprovable tracked links refuse
without index mutation.

`take_ticket.worktree` is conditionally optional: an isolated branch-only take
may omit it, but a first batch declaration (`batch_members`) must supply a
nonblank shared worktree. Missing or blank declaration worktrees fail before
the WAL or any ticket write as `BATCH_WORKSPACE_INVALID`, surfaced by MCP as a
structured `LEASE_CONFLICT`.

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
`capture` (true for an unpromoted quick capture — the field a roster filters on),
`capture_disposition` (the recorded promotion outcome, or `null`),
`batch` (`{ id, controller, frozenAt, state, members, workspace, branch }` from
the authoritative manifest, or `null`; `state` is `pending`, `active`, or
`releasing`, and `members` is the complete immutable roster).
`list_items include_archived: true` is the sole complete roster census: it
projects an active/releasing manifest onto every roster member until manifest
unlink, including after release clears every ticket-local batch, worktree and
branch field. `search_items` projects batch metadata only for matching
non-archived results and is not a complete roster census. A fresh closeout uses
the list census to capture the complete roster and shared Git path, keeps that
manifest linked through shared Git cleanup, then releases members so final
unlink occurs only after the worktree and branch are gone,
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
  `spike`, `capture`, or `custom` (which reads the ticket's own inline
  `requires` instead of the board's table). This, not the stage, is what decides
  how much evidence the ticket must produce. Changing it re-evaluates the gates
  immediately. `capture` owes nothing and is not deliverable work: it stays in
  Backlog, cannot be taken, and is refused an execution packet
  (`CAPTURE_NOT_PROMOTED`) until `update_item` records a `capture_disposition`.
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
- `claim_expires_at` / `claim_controller` — the bootstrap claim (CORE-121): when the
  claim expires and which durable controller holds it. A legacy claim without
  `claim_expires_at` expires `claimExpiryMinutes` after `taken_at`. `review_round` /
  `remediation_budget` count Review → Implementing returns against their budget.
- `lease_id` / `lease_revision` / `lease_workspace` / `lease_phase` /
  `lease_heartbeat_at` / `lease_controller_run` / `lease_worker_run` /
  `lease_provider` / `lease_reclaimed_from` — the renewable workspace lease
  (FRD-030). `take_ticket` mints them; `renew` names `lease_id` + `lease_revision`
  and bumps the revision; `transfer` mints a new `lease_id` and records the
  previous controller in `lease_reclaimed_from`; `release` clears them. A taken
  ticket without `lease_id` is a legacy claim that receives its lease on its
  first renew or transfer. Board timing: `claimExpiryMinutes` (30),
  `leaseHeartbeatMinutes` (5), `leaseCommandMaxMinutes` (120) — reported by
  `get_status.leases`. A modern manifest-backed batch renew always names both
  current CAS fields and the exact nonempty `controller_run`; the no-token
  owner compatibility path is never a batch path.
- `delivery_state` / `delivery_branch` / `delivery_sha` / `delivery_candidate` /
  `delivery_release_branch` / `delivery_release_tag` / `delivery_backport_required` /
  `delivery_backport_sha` / `delivery_recorded_at` — how far the change actually
  travelled (FRD-031), recorded independently of the workflow stage. A ticket
  reaches Done on acceptance against its **integration** target; its inclusion in
  a production release is recorded here afterwards. Nothing here opens a gate
  (ADR-0005) — a `released` ticket with no proof is still refused entry to Done.
  `delivery_backport_required` and `delivery_recorded_at` are derived by the
  store, never supplied. Policy comes from `get_status.delivery`.
- `lease_batch` / `lease_batch_controller` / `lease_batch_frozen_at` — the
  ticket-side projection of a deliberate batch workspace (FRD-030). The
  authoritative record is the hash-named declaration manifest under
  `.kanmer/batches/transactions/`: `pending` retains enough hashes and take
  intent to roll the complete declaration forward, `active` protects the exact
  immutable roster, and `releasing` makes all-terminal member cleanup
  idempotent. All three manifest states persist both the actual MCP request
  actor and the nonempty durable `controller_run`; declaration, pending
  recovery, later member take, renew and execution-packet access exact-match
  that pair. The manifest worktree is canonical and repository-relative, with
  the branch recorded separately; copying or relocating the repository keeps
  the same authority, while absolute paths are derived only for local
  collision checks. `lease_batch_controller` is the actor projection, while
  `lease_controller_run` is present on a taken member's lease; supplied owner
  labels cannot authorize either. These fields are absent in isolated mode and
  may clear member by member only while the manifest is `releasing`. Summary
  projection remains manifest-backed through final unlink, so those clears do
  not hide `state`, complete `members`, `workspace`, or `branch` from closeout.
  Once the roster is all-terminal, closeout cleans the captured shared Git path
  while this projection remains linked. If cleanup fails it issues no release;
  only after cleanup succeeds does the intentionally owner-unbound release let
  a fresh closeout agent finish the releasing pass and final unlink.
- Plan validation and step packets (FRD-033) read documents; they are not
  frontmatter and nothing is persisted. A finding is `blocker` or `advisory`:
  `PLAN_VAGUE_INSTRUCTION` (a sentence that resolves no exact decision, file,
  caller, error or test) and `PLAN_RISK_EVIDENCE_MISSING` (state, migration,
  service, runtime, public-contract, security or release work the plan cites no
  evidence for) are **always advisory**. `PLAN_STEPS_MISSING`,
  `PLAN_STEP_NOT_FOUND`, `PLAN_STEP_UNSTRUCTURED`, `PLAN_STEP_FIELD_MISSING`,
  `PLAN_STEP_FILE_UNDECLARED`, `PLAN_STEP_FILE_FORBIDDEN`,
  `PLAN_ALLOWED_FILES_MISSING`, `PLAN_ACCEPTANCE_MISSING`,
  `PLAN_STOP_CONDITION_MISSING`, `PLAN_EVIDENCE_STALE`,
  `PLAN_PACKET_BUDGET_EXCEEDED` and
  `PLAN_EVIDENCE_UNRECORDED` become blockers **only** when `step` is supplied.
  A plan pins its evidence with an `Evidence:` line in `## Starting state`;
  compiled packets are `step-packet/2`.
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
and `findings` is an ordered array. Three further keys are optional for the
parser (older attestations omit them) but always written by `kanmer-review`:
`board_sha` (full SHA of the pushed board tip; must be a full hex id when
present), `expected_reviewers` (array of non-empty reviewer identities — the
independent reviewers named for the ticket, never bots) and `threads_snapshot`
(array of the review threads on the head, each mapped to a finding id). A
present but malformed value makes the attestation invalid.

For a batch PR, one independent review may cover the shared diff, but its
evidence is written separately on every ticket in the immutable roster. Read
that complete roster from the authoritative `list_items include_archived: true`
batch projection and require `state: active` before passing review. Each
member-owned record must be an independent `pass` naming the same exact PR and
full head SHA while retaining that member's own plan version, ticket timestamp,
findings and thread mapping. A leader-only or partial-roster attestation cannot
satisfy the protected batch gate; every member later receives its own merged-SHA
proof as well.

A dependency edge between two members of that exact immutable roster orders
work inside the shared PR and is not a protected-gate blocker. External and
dangling blockers remain failures, and singular-ticket behavior is unchanged.

Each finding is an ordered mapping with these keys and enums:

```yaml
- id: F-001
  severity: blocker # blocker | major | minor | note
  summary: "Non-empty finding summary"
  disposition: open # open | fixed | rejected-with-reason | accepted-risk | deferred-to-ticket | obsolete-after-change
  reason: "Required for rejected-with-reason, accepted-risk or obsolete-after-change"
  ticket: "MCP-025" # required for deferred-to-ticket
```

`id` is a stable `F-###`-style string and `summary` is non-empty. `reason` is
required for `rejected-with-reason`, `accepted-risk` and
`obsolete-after-change`, and optional otherwise; for `obsolete-after-change`
that reason names the superseding commit (`superseded by <full-sha>`).
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
the Markdown body; it is not a normal attempt result. A `FAIL` or `INCONCLUSIVE`
record also carries `failure_class: implementation | plan | transient |
inconclusive`, which `kanmer-verify` uses to route the ticket (Implementing,
Preparing, retry, or wait); the parser does not enforce it. A record that
names no class is routed as `inconclusive`, never as a retryable `transient`.

Which documents a ticket owes comes from its **profile**, not from its area and
not from a fixed pipeline — call `get_doc_gates` for the ticket's actual types
and boundaries. Creation is **ungated**, which is what makes importing and
backfilling finished work possible; `move_item` is where the gates apply.
