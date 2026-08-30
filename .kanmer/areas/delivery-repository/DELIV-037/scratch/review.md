## 2026-08-30 — independent verification of PR #637 against the live estate

`gpt-5.6-terra` (high) verified every recorded fact against Azure and production
SQL rather than against the PR. **24 facts independently confirmed**, and it
found **six false statements** across two rounds. All are fixed.

### What was false, and why it matters

A current-state document asserting something untrue is worse than one that says
nothing, and every one of these was written by the agent that also ran the
deploy — recording its own account of its own work.

1. **"the Worker reads it" of `AiJobs`.** It does not. Zero source references
   under `src/Pegasus.Worker`, and `20260828084644_GrantAiJobs` grants Web only.
   The migration's own comment says why: *"Only the Web runtime touches the
   ledger … The Worker runs no AI timer (ADR-0035, EPIC-011 D5), so it gets no
   grant."*
2. **"the standalone `/VehicleImages` list and detail pages were removed".**
   Only the **list** page was. `Pages/ImageIntake/Details.cshtml` is live at the
   deployed SHA with `@page "/VehicleImages/{id:guid}"`, and the four surfaces
   named as replacements are entry points that **link to it**. Worse: the
   original bullet's detail half was **true**, and the first pass replaced it
   with a statement that a live route does not exist.
3. **"removed, not left alongside"** of the triage-list and mail-categories
   pages. The opposite — both survive as deliberate `RedirectPermanent` shims
   that carry the tab through so bookmarks still land on the same work.
4. **"built at release 36 but composed out"** of the Provider API. It did not
   exist at release 36 at all: `git grep ProviderApi -- src/` at `84132d01`
   returns nothing. Routes, scheme, flag and two of the eleven migrations were
   introduced inside this release's own range.
5. **`capabilities.md:227` — "the feature gate is off"**. It is on. This is the
   same class of claim the release falsified, in a file the new `operations.md`
   row *links to* but the first pass did not edit.
6. **`current-architecture.md:108` — "`/Triage` … physical list owner"**.
   `/Triage` is a `RedirectPermanent`; only `/Triage/{id:guid}` is a real page.
   The first pass corrected the adjacent `/VehicleImages` bullet and left this
   one, so **the document contradicted itself** — its own new section says the
   list surfaces became redirects.

### Unverifiable, now made checkable or honestly labelled

- **The range figures were stale**, measured before #609 merged. Remeasured from
  release 36's source: **49 PRs, 405 commits, 434 files, +142,535/−17,804**
  (was 48/379/371/+125,843).
- **The manifest hash rested on the deploying agent's word**, because the
  artifacts had never been copied out of the disposable worktree — a procedural
  step the release skill requires. Now retained at
  `artifacts/releases/release-37-0b3ec847`, and the retained manifest hashes to
  exactly the recorded value, so the figure is checkable.
- **`ValidateOnBuild follows IsDevelopment()`** was written as a repository
  fact. It appears nowhere in `src/` and nothing calls
  `UseDefaultServiceProvider`; it is ASP.NET Core host default behaviour.
  Restated, keeping `Program.cs:242`'s factory registration as the part that
  genuinely is a repository fact.
- **The two smoke poll timestamps** cannot be checked after the fact — telemetry
  is dark for that window and no smoke artifact was kept. Labelled an
  uncorroborated operator self-report; the subscription expiry beside them *is*
  independently checkable and is marked as such.

### Scope — the reviewer was right, and it is recorded here

The `open-decisions.md` **AutomationMcp** correction fixes a contradiction that
has stood since **release 9** and was **not caused by release 37**:
`operations.md:122` said the flag was production-enabled under ADR-0026 while
`open-decisions.md:57` said operations must not imply it. Both halves confirmed
live.

It is factually right and sits in the same sentence being edited for
`ProviderApi`, so the reviewer did not block on it — but it is a nine-release-old
pre-existing defect absorbed into a release ticket without its own board record,
which conduct rule 2 exists to prevent. **Named here rather than split**, on the
grounds that splitting one clause of one sentence into its own ticket would cost
more than it records; noting it is the point.

### Gaps closed in the second round

Row-count baseline (all four new tables **zero rows** against seven `Cases` —
the number that lets a later reader detect first use), where the gate levers
actually live (`infra/modules/platform.bicep`, so they change by edit and
re-provision), and a **rollback position**: release 36's revision and digest
still in the ACR, the eleven migrations explicitly **not** assumed
down-reversible (`NamedEstimates` does a data `UPDATE`, `CaseEditLeaseHolderKind`
alters a lease column), and the release-36 `worker.zip` absent from this
workstation.

The heading `## Operations workspace surfaces added at release 37` also baked a
release number into a living as-built snapshot — when 38 ships a reader could
not tell current state from history, the exact ambiguity this pass exists to
remove. Renamed, with the release attribution moved to `operations.md`.

### Deferred to [[DELIV-038]]

Four findings are real but outside a release-evidence pass: the AI-operations
open decision now contradicts shipped code, the upload-link surface has no
local-vs-live boundary row, nothing records how this deploy differed from
release 36's two failed provisions, and telemetry blindness is recorded as a
circumstance rather than actioned.
