---
name: kanmer-setup
description: Set up or reconcile Kanmer in a project — apply version steps, migrate an older board, refresh the AGENTS.md operating instructions, and ingest whatever the repo already records as work (GitHub issues, plan documents, or commit history). Re-run it after every Kanmer update. Use when the user asks to "set up kanmer", "onboard this project", "start using the board here", "upgrade kanmer", or when a project has no .kanmer folder yet and the user wants work tracked.
---

# Setting up Kanmer in a project

**Setup is reconciliation, not a first-time action** (ADR-0010). Every run brings
reality into Kanmer and is safe to repeat — which is why "run setup after
updating Kanmer" is the standing instruction, not a one-off.

There are no modes to detect. There is one loop, and each step is a no-op when
there is nothing to do.

## 1. Orient

`get_status` (it never creates `.kanmer/`). Report what you found before
changing anything: whether a board exists, its format, and its counts.
`rootSource` on that answer says *how* the server found its root — `flag`, `env`,
`cwd`, `cwd-worktree`, `ancestor`, `ancestor-worktree` or `init`. Read it: a
board found by discovery is not necessarily the one the user meant.

In a Git repo set up through the GUI the board lives in its own worktree,
`.worktrees/kanmer`, and MCP is already rooted there. Never create, switch or
push that board branch yourself.

### If the server would not start at all

A server started without `--root` in a repo that has **no board anywhere** now
**fails to boot** rather than starting against an empty one (ADR-0012). You will
see, on stderr or in the host's server log:

```
Error: no Kanmer board found. Tried:
  …\proj\.kanmer
  …\proj\.worktrees\*\.kanmer
 Pass --root <board>, set KANMER_ROOT,
 or pass --init to create one here.
```

That is the one thing setup cannot work around from inside a tool call — there
is no session to call a tool in. **Onboarding a board-less repo therefore means
re-registering the server with the `--init` opt-in** (or `KANMER_INIT=1`, or an
explicit `--root <repo>`), then re-running setup. `--init` does not create
anything by itself; it re-permits the lazy creation on the **first write**, which
is what step 6's greenfield path relies on. Tell the user what to change and
why — do not hand-create a `.kanmer/` folder to route around it, because a board
built by hand is exactly the drift this skill exists to reconcile.

A board found by discovery, or a root given by `--root`/`KANMER_ROOT`, needs no
flag: the opt-in matters only when nothing was found.

## 2. Apply version steps

If the installed Kanmer is newer than the board was last reconciled against,
apply whatever that version requires. This is the step that makes the run
repeatable; without it, "setup" would be something you did once and then drifted
away from.

## 3. Migrate if the board predates the current format

`migrate_board dry_run: true` first, always — show the preview (stage mapping,
tickets with no matching stage, documents relocating, fields being dropped) and
let the user decide. Then apply.

Safe to call unconditionally: an already-current board reports nothing to do.

## 4. Refresh the AGENTS.md operating instructions

Run the script that owns the managed block (see below). It only ever rewrites
between the markers, so this is idempotent and safe on a repo with its own
`AGENTS.md` content.

### Reconcile the user-owned guide skeleton

The managed block is necessary but not the repository's whole contributor
guide. Assess the **user-owned** portion of `AGENTS.md` as a separate,
non-destructive reconciliation:

- **No `AGENTS.md`:** copy the canonical
  `plugins/kanmer/skills/kanmer-docs/assets/agents-template.md` into the target
  repository first. It is deliberately incomplete: preserve its TODO markers
  and then run the existing managed-block writer, which prepends the Kanmer
  block and handles any `CLAUDE.md` pointer as usual. Do not copy the managed
  block into the template or create another writer.
- **Existing `AGENTS.md`:** do not replace, complete, reformat, or otherwise
  edit its human-authored prose. Ignore the marker-delimited Kanmer block, then
  look case-insensitively at Markdown headings of any depth for **Commands**,
  **Architecture map**, **Conventions**, **Gotchas**, and **Verification**.
  Report the exact labels that are absent; when none are absent, report that no
  user-owned skeleton action was needed. Heading style and the quality of
  present prose are the repository owner's decision.
- **Malformed markers:** stop when the existing writer refuses them. Never
  work around that stop condition by copying, moving, or guessing at the
  managed span.

When the missing-file path was used, make the documentation debt visible **after
a board exists** (so normal setup ingestion/greenfield work has completed):
search for `Source: AGENTS.md skeleton created by kanmer-setup`. If no matching
ticket exists, create one backlog ticket titled `Complete the AGENTS.md
contributor guide`, with that exact source marker in its body, an explanation
that the template TODOs need repository facts, and `docs_todo: true`. Use the
configured `docs` area when it exists; otherwise omit the area rather than
assuming a board layout. If the marker is already present, report that existing
ticket instead of creating a duplicate. A present partial guide gets only its
missing-section report — it does not create or mutate a documentation ticket.

## 5. Ingest what the repo already records

Something in the repo is already the record of intended work. Find it and put
it on the board — **one source, in this order**, not all three. Mining commits
on a repo that has issues just produces two tickets for the same work.

### 5a. GitHub issues, if the repo uses them

Each open issue becomes a ticket in the right area, its body carrying a
`Source: <issue url>` line.

Then GitHub stops being a source of truth, which means closing the issues. **That
is a destructive action outside this repo, affecting other people.** Follow this
exactly:

1. **List** every issue that will be closed — number and title, all of them.
2. **Wait** for explicit confirmation. Not an assumption, not "proceeding unless
   you object".
3. **Close** each with a comment: `migrated to Kanmer (<ID>)`.
4. **Report** what was closed.

There is no shortcut for a small number of issues.

### 5b. Plan documents, if there are no issues

Mine **per item, not per document**. A plan with twelve numbered items becomes
twelve tickets, because the items are what reveal the board's areas and become
the template for how future work is written.

Work already finished becomes a ticket created **directly in Done** with
`profile: "custom"` and an empty `requires` map. Creation is ungated, so this
needs no exemption — and `custom` with no requirements is the only honest
profile for work that finished before the board existed. Any other profile would
leave it permanently owing documents nobody will ever write.

Plan prose lands in the ticket's `plan/`; anything that reads as verification
seeds `proof/`.

**Preview before creating anything**: "N documents → M items → K tickets, in
areas A/B/C". A plan directory can imply a hundred tickets, and the user should
see that number before it exists.

### 5c. Commit history, if there is neither

Cluster by scope or tag and propose the clusters. Coarser and less reliable than
the other two — say so, and expect to throw some away.

### Idempotency is mandatory

Before creating anything from any source, search for its marker
(`search_items` for the `Source:` line, or the plan item's title). Already
present → skip it and say you did.

This is what makes the loop re-runnable. A setup that duplicates its own output
on the second run is not reconciliation.

## 6. A board with nothing to ingest: the greenfield interview

A genuinely fresh project has no issues, no plans and no history to mine. Then,
and only then, build the board from a brief.

Use the Kanmer manual's greenfield chapter (`docs/manual/greenfield.md` in the
Kanmer repository, or the in-app manual) to choose the appropriate initial depth
and keep the first horizon bounded. This skill is copied into other
repositories, so it never links into the Kanmer tree. It is a
planning aid, not an alternative to this brief-first interview or its explicit
confirmation before board creation.

This is the path that needs the `--init` opt-in when the server was started
without a root (see step 1): the board is created lazily by the first write, and
without the opt-in the server will not have started.

0. **Require a brief.** If the user hasn't given one, ask for a paragraph or two:
   what they're building and for whom. Don't invent a product; a fresh repo with
   no brief means stop and ask.
1. **Annotate the brief → `docs/product/vision.md`** — the durable statement of
   what and why.
2. **Split into governing docs** via `kanmer-docs`: each product span becomes a
   **PRD**, each PRD's behaviour an **FRD** (with acceptance criteria), each
   cross-cutting decision an **ADR**. Unresolved questions go to
   `docs/product/open-questions.md` — surfaced, not guessed.
3. **Materialise the `/docs/` tree** + `docs/contributing/doc-structure.md`.
4. **Propose, then create.** In one message the user confirms:
   - **Areas** (3–6) from the FRDs/ADRs, with hex colours and 2–6 letter
     prefixes. Watch for prefix collisions.
   - **Default profiles** per area — which documents that area's tickets will
     owe. Stages are **fixed** in format 3 and are not up for discussion; the
     profile is the thing that varies.
   - **The backlog with counts** — "N PRDs → M FRDs → K tickets".

   Only then: `add_column` each area, then one `create_items` call — one ticket
   per FRD acceptance criterion / ADR consequence, each with `refs` to the doc it
   implements (or `docs_todo` where a doc is still owed). Leave `status` unset.

## 7. Report

What changed, what was skipped and why, and what is still owed — tickets with
`docs_todo`, historical tickets that may want a real profile later, issues left
open. Finish with `get_status`.

If the board already existed and had real history, most steps will have been
no-ops. Say that rather than nothing: "already current" is a useful answer.

Day-to-day ticket work is `kanmer-tickets` and the phase skills, not this one.

## The AGENTS.md operating instructions (step 4)

Step 4 makes sure the target repo's `AGENTS.md` **begins** with
this managed block — it's how any agent that opens the repo learns the board
exists and how to behave on it:

```markdown
<!-- kanmer:instructions:start — managed by kanmer-setup; edits inside will be overwritten -->
# Kanmer operating instructions

This repo's work is tracked on a Kanmer board in `.kanmer/`. In a Git repo set up
through the GUI the board lives in its own worktree, `.worktrees/kanmer`, on the
board branch, and MCP is already rooted there — never create, switch or push that
branch yourself. Your own ticket worktree is a separate thing, recorded by
`take_ticket`.

The board branch convention is the repository variable `KANMER_BOARD_BRANCH`,
falling back to `kanmer-board` when it is unset. A branch rename is an
administrator handoff: retarget branch protection and required checks, update
the repository variable, and only then reconcile the board worktree and remove
old refs. Agents must not mutate protected refs, branch protection, or repository
variables; stop and report when the observed branch and configured convention
disagree.

- Start every session with `get_status`, then `list_board` / `list_items` to find your ticket.
- **Which documents a ticket needs depends on its profile, not on a fixed pipeline.** Call `get_doc_gates <id>` before every move. Not `board.yml` — requirements are injected at resolve time, so its `profiles:` block is not the effective set.
- Stages: backlog → preparing → implementing → review → verifying → done. **A move crosses at most one gated boundary**, so walk the stages one at a time; a jump is refused even when every document exists.
- **Gates constrain `move_item` and nothing else** — creation in any stage is ungated, and `gh pr merge` is outside the engine, so an unmet gate never stops a merge.
- An unticked `- [ ]` in `open-questions/` blocks a move: tick it, or move it below the literal `## Parked (explicitly deferred)` with a reason.
- Read the whole ticket folder before starting — documents are folders (`research/`, `plan/`, …), so there may be several files per type. If the ticket is in a group, read the group's `context.md` too: the constraint binding the batch is written once, there.
- Work each fresh ticket on its own branch and worktree: worktree `.worktrees/<id>`, branch `<id>-<slug>`; `take_ticket` records both and moves the stage. A resumed execution packet is available only in `implementing` and must validate/reuse the exact recorded branch and **worktree root** — never create a second worktree or take the ticket again. It must not name the board, shared source checkout, another active ticket's worktree, or any child of those; its checked-out branch and Git common directory must match the record and source repository. Pause by retaining that taken record; never release a paused ticket while its worktree/branch remains a resume target.
- Write pipeline documents with `set_ticket_doc`. Running notes go to `append_scratch` — scratch is the notepad and is never gated, and neither is anything under `reference/` or `assets/`.
- Proof is written on merged `main`, after review and the merge, not before.
- Archive, don't delete. Reference other items with [[ID]] wiki-links.
- Skills run in this order: kanmer-tickets → -research → -plan → -execute → -review → -verify → -closeout. How far a ticket walks it depends on its profile, so ask `get_doc_gates` rather than assuming every step. Off to the side: -auto (drives that order over many tickets), -docs (governing docs), -groom (fix the board), -report (read-only), -setup (reconcile after a Kanmer update).
- Each skill ends by naming what comes next — read that line before improvising a hand-off.

The local MCP convention is `KANMER_BOARD_BRANCH` in each project-scoped
provider registration or exported local runtime, falling back to the default
board branch when unset. GUI Connect writes the saved board-branch setting into local
registrations. Hosted Actions should mirror the same value in the repository
variable, but Actions variables are not inherited by local processes.
When a native runtime supervisor launches Kanmer through an operator-private
wrapper, that wrapper must export both `KANMER_PROVIDER_CWD` and
`KANMER_BOARD_BRANCH` before invoking the stable launcher.
The GUI's OpenAI tunnel controls manage the same long-lived native runtime
alias through `tunnel-client runtimes connect/status/stop/rm`. Application quit
does not stop that runtime; readiness requires structured non-stale status, and
local removal must confirm the alias is stopped before deleting its metadata.

## Agent conduct

**Scope**

1. **Scope is the brief.** “While I’m here” changes are follow-up tickets, not commits.
2. **Never absorb another ticket’s scope.** Link it and let it be worked on its own record.
3. **Release and remediation work ships no new features.**
4. **The ticket precedes the branch.** No board record, no PR.
5. **Stop at the stop condition.** Never merge your own PR or start the next ticket; report deviations instead of redesigning.

**Build**

6. **Greenfield has no legacy.** Unless the brief names users or data, add no fallback, compatibility, or deprecation path; delete what you replace.
7. **Reuse before build.** Name the helper, port, or route you extend; report a genuinely unfit one instead of silently building a parallel copy.
8. **One list per concept.** A second copy in another layer is duplication, even when it is “just strings”.
9. **Paths are relative.** Use repo-root-relative or injected configuration, never machine-specific paths.
10. **Dependencies are approvals.** Add no package unless the brief lists it.
11. **Concurrency results are never discarded.** Retry, defer, or surface them; a swallowed conflict is data loss.
12. **Errors surface.** No catch-all suppression or empty catch.
13. **No fabricated domain data.** Fixtures use the documented estate.

**Prove**

14. **Done means wired.** New code needs a named production caller; registered-but-unreachable or test-only code is not done.
15. **Runtime dependencies ship in the artifact.** Prove the deployed image carries every required browser, font, or package.
16. **A schema change and its permissions ride the same diff.** Include migration, grants, and bootstrap census together.
17. **Recorded commits must be reachable.** Ticket SHAs must exist on the merge target.
18. **Stubs are not done.** Do not present TODOs, placeholders, or mocks as implementation.
19. **Tests prove the claim.** Never weaken or delete an assertion to pass; a failing test stops and is reported.
20. **Verify with exit codes.** Run stated commands and record outputs; INCONCLUSIVE is not PASS, and a later pass does not erase a failure. Done requires PASS; an explicitly disposed terminal non-PASS stays Verifying, is archived, and is released.
21. **No speculative CI or tests.** Delete a gate that gates nothing.

**Conduct**

22. **Review findings get dispositions.** Fix, reject with reason, accept risk, defer to a ticket, or mark obsolete after change, naming the superseding commit; never silence them. Findings from one root cause are one class with one remedy, never one patch per example, and a convention change lands in the same PR as the work that needs it.
23. **Secrets never appear in code, tickets, or proofs.**
24. **A PR that changes commands or conventions updates AGENTS.md in the same PR.**
<!-- kanmer:instructions:end -->
```

**Don't hand-edit — run the script that owns this block:**

```
node <plugin-root>/../../scripts/agents-block.mjs <repo> ["# <project> contributor guide"]
```

It implements all four rules below, is idempotent, and refuses to guess at a
file whose markers are malformed. `scripts/verify-agents-block.mjs` in the
Kanmer repo is its end-to-end test. The block text above is the literal body
the script writes — keep the two in step if you change either.

Hand-edit **only** if the script isn't available (a plugin install without the
repo checked out), in which case the rules are:

- The block is the **very first thing in the file** — above any existing
  content, so it's read before anything else.
- `AGENTS.md` missing → create it with the block plus a stub heading for the
  repo's own content (e.g. `# <project> contributor guide`).
- `AGENTS.md` present → insert the block at the top; **never modify anything
  outside the markers**.
- Block already present → refresh only the content **between the markers**
  (idempotent — running setup twice changes nothing the second time). Leave the
  block where it already sits; moving it would rewrite bytes outside the
  markers.
- If a `CLAUDE.md` exists and doesn't reference `AGENTS.md`, add a one-line
  pointer to it.
- If the markers are malformed (end before start, or only one present), stop
  and tell the user — never guess at a half-marked file.

---

**Hand off to `kanmer-tickets`** — the board now exists and the day-to-day work
of filing and moving tickets is its job, with the phase skills driving each
ticket from there. This skill is re-entrant, not one-time: run it again after
every Kanmer update, and most steps will correctly do nothing.
