# Proof — UIIMP-007: FRD-12, capabilities and boundaries

## What was verified, and where

Verified against merged `dev` at `b92cb9a7`, in the primary checkout
`C:/Users/PC/Documents/GitHub/pegasus`, on 2026-08-29. The ticket's six
commits (`c63f1c20`, `e2f6bee3`, `e65571f4`, `b8b01479`, `8d6d58cf`,
`1ee04b97`) reached `dev` through PR #586, merge commit
`690ca5799fc9ff78d293513b1d5fe50c0d1728d7` ("Merge pull request #586 from
collisionengineers/task/uiimp-007-frd12-capabilities", 2026-08-28).
`git merge-base --is-ancestor 690ca579 b92cb9a7` exits 0, so every claim
below is read off the current merge target.

This is a docs-only ticket: it ships no code, so the "registration" and
"deployed feature" tiers do not apply to its own artefacts. The
document-tree analogue of a production caller is used instead — a
capability row's *Canonical owner* cell resolving to a real file and
heading, `docs/index.md` routing to the document, and (for the one
behavioural decision recorded here, D14) a shipped Razor consumer. Each
claim below names which of those it is.

## Evidence

### FRD-12 exists on merged `dev` and owns the shell, routes and IA

Tier: artefact on merged `dev` (file content, not a runtime tier).

`git cat-file -p origin/dev:docs/frd/frd-12-operator-experience.md` returns
440 lines. Its heading set is the FRD template plus the EPIC-011 §1
surfaces:

```
1   # FRD-12: Operator experience
5   ## Purpose
23  ## Behaviour
25  ### Operator experience
68  ### Shell and routes
109 ### Work Centre
139 ### Cases: queues and filters
188 ### Search
202 ### Case workspace
258 ### Assessment
269 ### Operations
279 ### Administration
292 ### Workspace tabs, command palette and keyboard
315 ### Breakpoints
323 ### Display labels
332 ### Upload
361 ### Dashboard freshness and reconciliation
385 ## States and transitions
397 ## Edge cases and fail-closed behaviour
411 ## Acceptance evidence
423 ## Links
```

The plan's premise that the two pre-existing inbound anchors survive the
rewrite holds: `#operator-experience` (line 25) and
`#dashboard-freshness-and-reconciliation` (line 361) both still exist.

### The document is reachable — `docs/index.md` routes to it

Tier: documentation caller.

```
git grep -n "Integrated Operations Workspace shell contract" origin/dev -- docs/index.md
origin/dev:docs/index.md:23:| What are the UI rules, and the Integrated
  Operations Workspace shell contract? | [Design](design/README.md) —
  visual, component and shell authority; route and page behaviour (Work
  Centre, Cases, Search, Case workspace) is
  [FRD-12](frd/frd-12-operator-experience.md) |
```

### `UI-16` is registered in `docs/capabilities.md` with a canonical owner

Tier: registry row on merged `dev`.

`docs/capabilities.md:167` (table columns `ID | Durable outcome | Horizon |
Target release | Canonical owner | Activation/boundary`, header at line 72):

```
| UI-16 | Integrated Operations Workspace shell: one persistent rail (Work
Centre, Inbox, Upload, Cases, Search, Operations, Administration) with live
counts, … | Now | 0.1.0-alpha.1 | [Shell and routes](frd/frd-12-operator-
experience.md#shell-and-routes) | Allocated 2026-08-28 for the next alpha
release (EPIC-011); replaces the Dashboard/Queues/Cases route set and
removes the `/VehicleImages` list. … |
```

The ID's addition is also recorded in the provenance section,
`docs/capabilities.md:15` — "On 2026-08-28, `UI-16` was added for the
Integrated Operations Workspace shell (EPIC-011)".

### The four bring-forwards are registered at `Now / 0.1.0-alpha.1`

Tier: registry rows on merged `dev`.

| ID | Line | Horizon / target | Canonical owner cell |
| --- | ---: | --- | --- |
| `AI-10` | 273 | Now / 0.1.0-alpha.1 | FRD-11 § AI Job List (AUTO-009) |
| `EXT-09` | 252 | Now / 0.1.0-alpha.1 | FRD-06 § Professional engineering findings and correction |
| `EXT-10` | 253 | Now / 0.1.0-alpha.1 | FRD-07 § External boundary |
| `MI-01` | 275 | Now / 0.1.0-alpha.1 | FRD-06 § Vehicle and engineering evidence |

Each owner target exists: `docs/frd/frd-11-reports-correspondence-and-
reviewed-proposals.md:206` is `### AI Job List`, and the FRD-06/FRD-07
anchors resolve (see the link-script result below). `AI-10`'s
activation note cites ADR-0035, and `docs/adr/0035-ai-job-ledger.md`
exists on `dev`.

### Every anchor pointed into FRD-12 resolves

Tier: mechanical check on merged `dev`.

```
git grep -oh "frd-12-operator-experience\.md#[a-z0-9-]*" origin/dev | sort | uniq -c
      1 …#administration
      1 …#assessment
      1 …#dashboard-freshness-and-reconciliation
      1 …#operations
     34 …#operator-experience
      1 …#shell-and-routes
```

All six slugs appear in the heading list above; none is dangling.

### The boundaries entry exists

Tier: artefact on merged `dev`.

`docs/boundaries.md:23`, written by `e65571f4`:

```
| AI assistance | typed evidence/proposal/review identity; the shared AI job
ledger (in scope under ADR-0035, AUTO-009: Estimate, Unidentified
resolution, Query response and Unidentified-queue pass jobs, `AI-10`) |
direct mutation, approval, business policy | accepted Core proposal port,
representative evaluation, abstention/challenge gates, human approval,
caller proof, and capability-specific capacity measurement |
```

The diff moved "shared AI usage ledger" out of the *excluded* column and
into the *in scope* column with the ADR-0035 citation. The automated-
correspondence row (MAIL-024's) is byte-identical — `git show e65571f4
--stat` reports `docs/boundaries.md | 2 +-`, a single changed line.

### D14 was recorded here, and shipped code consumes it

Tier for the record: artefact. Tier for the consumer: build/test
(compiled Razor on merged `dev`; no deployed-and-exercised evidence).

`docs/frd/frd-12-operator-experience.md:112-118`:

```
Not ready, Review, Held, Unidentified, Blocked — each an exact link to its
Cases tab (`/Cases?tab=…`). Blocked links to `/Cases?tab=unidentified`,
where Blocked intake items are surfaced with their own state chip; there is
no separate Blocked tab. The Unidentified tab count, and the rail Cases sum,
count Unidentified items only; Blocked intake rows are listed in that tab
uncounted, with their own `Blocked intake` chip, so the two meanings stay
distinct.
```

That is D14 as minuted in the EPIC-011 context, and the shipped Work Centre
implements it — `src/Pegasus.Web/Pages/Index.cshtml:53-56` on merged `dev`:

```
<a class="metric" data-value="blocked" asp-page="/Cases/Index"
   asp-route-tab="unidentified">
    <span class="metric-label">…<span>Blocked</span></span>
    <span class="metric-value">@Model.Counts.BlockedIntake</span>
</a>
```

with the comment at `Index.cshtml:34-35` naming D14 explicitly. The paired
route rule is live too: `src/Pegasus.Web/Pages/Unidentified/Index.cshtml.cs:18`
is `RedirectPermanent("/Cases?tab=unidentified")`. Note that the Work Centre
markup is UIIMP-008's delivery, cited here only as evidence that this
ticket's contract is consumed rather than shelf-ware.

### Build and test

Tier: build/test, cited not re-run.

The canonical gate for merged `dev` at `b92cb9a7` is the orchestrator's
run: `dotnet restore --locked-mode` exit 0; `dotnet build --configuration
Release` "Build succeeded. 0 Warning(s), 0 Error(s)"; `dotnet test` with
`Category!=Corpus&Category!=Browser` — ArchitectureTests 100 passed,
Core.Tests 1133 passed, IntegrationTests 1022 passed / 2 pre-existing
skips, 0 failures. This ticket changes no compiled file, so that suite
neither confirms nor could falsify its content; it is recorded for the
state of the merge target.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Allocation summary arithmetic still adds up (233 rows: Now 140 / Next 29 / Later 35 / Not planned 29; 204 planned) | Proven at the merge SHA; the numbers have since moved, and still add up | See below |
| `scripts/Test-DocumentationLinks.ps1` passes (124 files) | Proven; file count now 129 | See below |

**Arithmetic.** Mechanical recount of the `## Capabilities` table at the
ticket's own merge `690ca579` (horizon column, header row excluded):

```
35 Later · 29 Next · 29 Not planned · 140 Now
```

— exactly the four figures the ticket claimed, and the summary table at
`690ca579:docs/capabilities.md:34-37` states the same four.

On current `dev` `b92cb9a7` the same recount gives 233 capability rows and
233 unique IDs, split `142 Now · 27 Next · 35 Later · 29 Not planned`,
and the summary table at `docs/capabilities.md:34-37` states those
figures. The shift of two rows from Next to Now is not this ticket's:
`git log 690ca579..origin/dev -- docs/capabilities.md` names
`dfd981a9 docs(capabilities): bring API-01 and API-04 forward to
0.1.0-alpha.1 (TICK-061)`. The invariant the item asserts still holds on
`dev`: per-target counts sum to 204 planned + 29 unallocated = 233, and the
ordered release sequence table (`docs/capabilities.md:334-346`) matches the
per-target recount target-for-target (142, 5, 19, 3, 5, 5, 0, 10, 5, 4, 3,
3).

**Link script.** Re-run on `b92cb9a7`:

```
pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
All relative Markdown links resolve (129 files checked).
EXIT=0
```

The script is a read-only relative-link resolver, not part of the solution
test suite. 129 rather than 124 files: five tracked Markdown files have been
added to the repository since `690ca579` by other tickets. The assertion —
that every relative link resolves — passes.

## Outstanding

- **FRD-12 § Acceptance evidence is a specification, not a discharged
  claim.** It requires real-browser proof of every rail route and count,
  both redirects, the removed `/VehicleImages` list, the tab limit and
  eviction, the keyboard map, axe accessibility, focus behaviour, and no
  document overflow at 1580 / 1100 / 760px. None of that is proven by this
  ticket and none is claimed here. The layout/overflow walk is
  [[UIIMP-010]]'s; the per-surface behavioural evidence belongs to the
  implementing lane tickets.
- **The four bring-forward rows are allocations, not deliveries.**
  `AI-10`, `EXT-09`, `EXT-10` and `MI-01` now read `Now /
  0.1.0-alpha.1`; that is a roadmap position. Each row says so in its own
  activation cell ("delivery evidence is separate"). This proof asserts the
  registry rows exist and resolve, nothing about the features.
- **The ticket's `refs` frontmatter lists only
  `docs/frd/frd-12-operator-experience.md`**, while the body claims
  ownership of `docs/capabilities.md`, `docs/boundaries.md` and
  `docs/index.md` too. All four files were changed as described; the
  frontmatter is simply narrower than the scope. Recorded, not a defect in
  the shipped work.

Nothing shipped contradicts the ticket's description: every file it claims
to own was changed in the way it describes, and no file outside that set
was touched by its commits (`git log --name-only` over the five content
commits lists exactly `docs/frd/frd-12-operator-experience.md`,
`docs/capabilities.md`, `docs/boundaries.md`, `docs/index.md`).

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.
