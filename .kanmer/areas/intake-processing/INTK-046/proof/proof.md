# Proof — INTK-046: Port Triage, Unidentified, Received and the image-record pages

## What was verified, and where

Verified on merged `dev` at `b92cb9a7` in the primary checkout
`C:/Users/PC/Documents/GitHub/pegasus`, read-only. PR #605
(`task/intk-046-record-pages` → `dev`) merged 2026-08-29T09:13:29Z as
`a01a640b`; `gh pr view 605` reports `"state":"MERGED"`,
`"mergeCommit":{"oid":"a01a640be3e4b6f43706106df4d25bb887812382"}`. All
seven recorded commits (`6dfa674d`, `702833ae`, `fc24dc65`, `d1591f24`,
`72addf22`, `0578835e`, `d39db016`) pass
`git merge-base --is-ancestor <sha> HEAD` against `b92cb9a7`, so every
recorded SHA is reachable on the merge target (rule 17). The merge
changed exactly five files:

```
git diff --stat a01a640b^1 a01a640b
 src/Pegasus.Web/Pages/ImageIntake/Details.cshtml   | 317 ++++++---
 src/Pegasus.Web/Pages/Intake/Details.cshtml        | 824 +++++++++-------
 src/Pegasus.Web/Pages/Triage/Details.cshtml        | 764 +++++++++------
 src/Pegasus.Web/Pages/Unidentified/Details.cshtml  | 201 ++++--
 src/Pegasus.Web/Presentation/OperatorLabels.cs     |  24 +
 5 files changed, 1311 insertions(+), 819 deletions(-)
```

Build and test tiers are cited from the orchestrator's canonical gate
evidence for this SHA, not re-run here.

## Evidence

### The four pages exist on their contract routes

Tier: build/test (source on merged `dev`).

```
Pages/Triage/Details.cshtml:2        @page "/Triage/{id:guid}"
Pages/Unidentified/Details.cshtml:1  @page "/Unidentified/{id:guid}"
Pages/Intake/Details.cshtml:2        @page "/Received/{id:guid}"
Pages/ImageIntake/Details.cshtml:2   @page "/VehicleImages/{id:guid}"
```

D1 is satisfied for this lane: the image *record* page survives on
`/VehicleImages/{id}`. Deleting the `/VehicleImages` *list* is
UIIMP-009's, not this ticket's.

### Named production callers for all four routes

Tier: registration + reachable caller (source on merged `dev`).

The four pages are not test-only or orphan routes. Every one is
navigated to from merged production code:

```
git grep -n '"/Triage/\|"/Unidentified/\|"/VehicleImages/\|"/Received/' -- src/Pegasus.Web
Pages/Cases/Index.cshtml.cs:552   $"/VehicleImages/{item.Id:D}",
Pages/Cases/Index.cshtml.cs:569   $"/Triage/{item.Id:D}",
Pages/Cases/Index.cshtml.cs:586   $"/Unidentified/{row.Id:D}",
Pages/Cases/Index.cshtml.cs:623   $"/Received/{item.Id:D}",
Pages/Index.cshtml.cs:61          NeedsAttentionKind.Mail => "/Unidentified/Details",
Pages/Index.cshtml.cs:62          NeedsAttentionKind.Triage => "/Triage/Details",
Presentation/UploadOutcome.cs:211,247,268,281,305,331
```

Those `Cases/Index` strings become `row.DetailHref`, rendered as the row
link at `Pages/Cases/Index.cshtml:126`
(`<a class="row-button" href="@row.DetailHref" …>`). Three further
callers are Razor links:

```
Pages/Cases/Create.cshtml:67    asp-page="/Intake/Details"
Pages/Cases/Details.cshtml:377  asp-page="/ImageIntake/Details"
Pages/Search/Index.cshtml:130   asp-page="/ImageIntake/Details"
```

### No page model changed — the ported markup posts pre-existing handlers

Tier: build/test.

```
git diff --stat a01a640b^1 a01a640b -- \
  src/Pegasus.Web/Pages/{Triage,Unidentified,Intake,ImageIntake}/Details.cshtml.cs
(empty)
```

This is the load-bearing fact for the whole ticket: the handlers were
not written by this lane, so "the control posts a real handler" reduces
to a name match against code that already shipped.

### Triage: 12 posted action names, 12 handler cases, exact match

Tier: build/test.

`Pages/Triage/Details.cshtml.cs:97` declares
`OnPostActionAsync`, whose switch carries twelve `case` labels
(lines 128–216). Extracting the `actionName` values the ported markup
posts and the switch's cases and sorting both gives identical sets:

```
assign  await_information  cancel  complete  link_case  link_response
record_finding  reopen  supersede_finding  unassign  unlink_case
unlink_response
```

No posted name is unhandled; no handler case is unreachable from the
page.

### Every `data-dialog-open` target resolves to a rendered dialog

Tier: build/test.

Triage draws eight dialog triggers. Each has a matching dialog and,
critically, the *same* render condition, so no trigger can reference a
dialog that is not on the page:

| Trigger | Line | Trigger condition | Dialog | Line |
| --- | --- | --- | --- | --- |
| `triage-assign-dialog` | 77, 84 | `mutable` | `_ReasonDialog` | 426, 436 |
| `triage-unassign-dialog` | 88 | `mutable && AssigneeId is not null` | `_ReasonDialog` | 444 |
| `triage-await-dialog` | 186 | `State is Open or FindingRecorded` | `_ReasonDialog` | 454 |
| `triage-complete-dialog` | 205 | `canComplete` (attribute nulled otherwise) | `_ReasonDialog` | 462 |
| `triage-cancel-dialog` | 211 | `mutable` | `_ReasonDialog` | 473 |
| `triage-reopen-dialog` | 220 | `!mutable` | `_ReasonDialog` | 529 |
| `triage-link-case-dialog` | 267 | `LinkedCaseId is null` | inline `data-dialog` | 484 |
| `triage-unlink-case-dialog` | 274 | `LinkedCaseId is not null` | `_ReasonDialog` | 513 |

The two case-link rows deserve a note, because the trigger and the
dialog test *different* fields — trigger on
`CaseAssociationUnavailableReason` (`Details.cshtml:255`), dialog on
`CaseAssociationUnavailableCaseId` (`:481`, `:513`). They are equivalent:
`Details.cshtml.cs` assigns the pair together at every one of the four
sites that touch either (`:88-89`, `:316-317`, `:325-327`, `:331-335`),
so one is null exactly when the other is.

Both dialog mechanisms in `wwwroot/js/site.js` were checked, because the
inline dialogs carry `data-dialog` but no `id`. The native-`<dialog>`
binder at `site.js:98-99` skips them on its
`typeof dialog.showModal !== 'function'` guard; the div-backdrop binder
at `site.js:787` keys on `data-dialog` and binds their triggers at
`site.js:873`. So the inline `triage-link-case-dialog` and
`unidentified-resolve-dialog` are wired, not orphans.

### The other three pages

Tier: build/test.

Unidentified: one trigger (`Details.cshtml:84`), one dialog (`:123`),
both inside `@if (isOpen)`; the dialog's form is
`asp-page-handler="Resolve"` (`:131`) against
`OnPostResolveAsync` (`Details.cshtml.cs:93`).

ImageIntake: one trigger (`Details.cshtml:56`), one `_ReasonDialog`
(`:207`), both inside
`@if (Model.Detail.State == ImageInitiatedCaseState.AwaitingInstruction)`;
`DialogActionUrl = Url.Page("/ImageIntake/Details", "Close", …)` against
`OnPostCloseAsync` (`Details.cshtml.cs:48`). Back link is
`asp-page="/Cases/Index" asp-route-tab="not_ready"` (`:16`), as the
ticket specifies.

Received: ten `asp-page-handler` values in the markup, ten
`OnPost…Async` handlers on the model, exact match —
`Block`, `ClaimCaseLease`, `CorrectDraft`, `DismissSuggestion`,
`LinkCase`, `OpenTriage`, `Reevaluate`, `RegisterImageIntake`,
`RetryAllocation`, `ReverseCaseLink`.

### No unbound control anywhere in the four pages

Tier: build/test.

```
grep -n '<button type="button"' <each page> \
  | grep -v 'data-dialog-open\|data-dialog-close'
Triage/Details.cshtml:203     (the gated Complete — see below)
Unidentified/Details.cshtml   (none)
Intake/Details.cshtml         (none)
ImageIntake/Details.cshtml    (none)
```

Every other control is a `type="submit"` inside a posting form, an `<a>`
with `asp-page`/`href`, or a `data-dialog-close`.

### The contested Complete control — what it actually does

Tier: build/test. **The ticket's own description of this control is
inaccurate; the shipped behaviour is not.**

`Pages/Triage/Details.cshtml:202-209`:

```razor
<span class="gated" data-condition="@(canComplete ? null : "Available once a finding is recorded")">
    <button type="button"
            class="btn @(canComplete ? string.Empty : "is-disabled")"
            data-dialog-open="@(canComplete ? "triage-complete-dialog" : null)"
            disabled="@(canComplete ? null : "disabled")">
```

and `:462`:

```razor
@if (canComplete)
{
    <partial name="Shared/_ReasonDialog" … ["actionName"] = "complete" … />
}
```

So, precisely:

- **`canComplete` true** — the button opens `triage-complete-dialog`,
  whose form posts `actionName=complete` to `OnPostActionAsync`
  (`Details.cshtml.cs:197`). A real handler, wired.
- **`canComplete` false** — the button is `disabled`, carries no
  `data-dialog-open`, and the dialog is not rendered. It posts
  **nothing**.

The ticket's ticked verification item says "the one disabled control
(Complete) posts the same `complete` action in both states", and the
same claim appears in the post-implementation report and in the code
comment at `Details.cshtml:196-199` ("It posts the same `complete`
action either way"). That is **not what the markup does** and the proof
does not repeat it. Recorded as a finding below.

What *is* provable is the narrower defence the ticket also makes, and it
holds:

1. The disabled render is `dev`'s pinned shape, not a lane invention.
   `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs:215-224`
   asserts `Assert.Contains("Available once a finding is recorded", …)`
   under the comment *"Completion keeps its place with its condition
   named, rather than disappearing until it happens to work."* That file
   was last written by `3fee076b` (CASE-024) and
   `git diff --stat a01a640b^1 a01a640b -- tests/` is empty — this lane
   changed no test.
2. Two merged pages gate **non-integration** controls the same way.
   `Pages/Cases/Details.cshtml:269` gates Open Assessment on
   `"Available after the current Review export"` (a state gate), and
   `Pages/Cases/Assessment/Index.cshtml.cs:178-185` supplies
   `ImportCondition` values `"Read-only once Complete"` and
   `"Only an Engineer can import an estimate"` (state and role gates) to
   the `.gated` wrapper at `Assessment/Index.cshtml:203`.
3. `wwwroot/css/site.css:1893`, `:1895`, `:1911`, `:1961` define
   `.gated` as a general design-system rule, not an
   integration-seam-only one.

D7's literal second clause therefore contradicts merged `dev` regardless
of this ticket. UIIMP-012 owns that reconciliation and exists on the
board (backlog, EPIC-011, links INTK-046).

### The three regressions were fixed in the markup, not in the tests

Tier: build/test.

`git diff --stat a01a640b^1 a01a640b -- tests/` is empty, so rule 19 is
satisfied by construction: no assertion was weakened, skipped or
deleted. The three pinned strings are all present on merged `dev`:

- `Details.cshtml:202` — `"Available once a finding is recorded"`
  (pinned at `QdosTriageIntegrationTests.cs:221`).
- `Details.cshtml:104` — `@(correction ? "Post-send correction" : "Determinations")`
  (pinned at `:317`).
- `Details.cshtml:400` — `<h2 id="triage-history-title">Permanent history</h2>`
  (pinned at `:477`).

### The response-evidence either/or fix is real

Tier: build/test.

`Details.cshtml:325` (`ResponseEvidence.Count > 0` → timeline plus the
`unlink_response` form at `:339-353`) and `:356`
(`mutable && ResponseEvidenceCandidates.Count > 0` → the
`link_response` form at `:358-386`) are two independent `@if` blocks,
not exclusive branches. Both render when both collections are populated,
which is the behaviour-preserving fix the plan's simplification pass
records.

### One list per concept, appended not inserted

Tier: build/test.

```
git diff a01a640b^1 a01a640b -- src/Pegasus.Web/Presentation/OperatorLabels.cs
@@ -881,6 +881,30 @@   +24 / -0, one hunk
```

A single `UnidentifiedResolutionTarget(UnidentifiedResolutionTargetKind)`
switch, appended at the end of the class, no existing member reordered —
matching the round-3 correction claimed in the post-implementation
report. The resolve dialog's select is driven by
`Enum.GetValues<UnidentifiedResolutionTargetKind>()`
(`Unidentified/Details.cshtml:141`) over the Core enum at
`src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs:33-40`,
so no destination kind is invented in the view.

### No new asset, no inline style or script

Tier: build/test.

```
git diff --name-only a01a640b^1 a01a640b -- '*.css' '*.js'
(empty)

grep -n '<main\|<script\| style="\|aria-pressed\|aria-current' <each of the four pages>
(no match in any of the four)
```

### Build and test tiers

Tier: build/test, cited from the orchestrator's gate evidence for
`b92cb9a7` — not re-run here.

```
dotnet restore ./Pegasus.slnx --locked-mode                  -> exit 0
dotnet build ./Pegasus.slnx -c Release --no-restore          -> 0 Warning(s), 0 Error(s)
dotnet test  ./Pegasus.slnx -c Release --no-build \
  --filter 'Category!=Corpus&Category!=Browser'
  Pegasus.ArchitectureTests   Passed:  100 / 100
  Pegasus.Core.Tests          Passed: 1133 / 1133
  Pegasus.IntegrationTests    Passed: 1022 / 1024 (2 pre-existing skips)
```

The four ported routes are exercised inside that integration run through
the real page pipeline:
`QdosTriageIntegrationTests.cs`, `TriageEvidenceImagesWebTests.cs` and
`ShellAndStatusPageWebTests.cs` request `/Triage/…`;
`QdosIntakeWebTests.cs` and `TestUiFocusedRenderTests.cs` request
`/Unidentified/…`; twelve classes including `ImageIntakeWebTests.cs`,
`MultiFormatIntakeWebTests.cs` and `OperatorJourneyTests.cs` request
`/Received/…`; `ImageIntakeWebTests.cs` and `ImageViewingWebTests.cs`
request `/VehicleImages/…`.

This is tier 2. It is a green test against the real production route and
handler, in-process. It is **not** a deployed, operator-exercised
feature; no deployment of `b92cb9a7` is claimed here.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Every button posts an existing handler; no inert control | **Proven, with a correction to how it is worded** | 12/12 Triage action names match the handler switch; 10/10 Received handlers match; Unidentified→`OnPostResolveAsync`; ImageIntake→`OnPostCloseAsync`; all 10 dialog triggers resolve to a rendered, JS-bound dialog; no unbound `type="button"` except the gated Complete. The disabled Complete does **not** post `complete` — see the finding below |
| No clipped text/overflow at 1580/1100/760 | **Unproven — left unticked** | `Browser/LayoutIntegrityTests.cs:20-26` draws its routes from `Browser/AccessibilityTests.cs:14-46`, and that list contains **no record-detail route at all** — not `/Triage/{id}`, `/Unidentified/{id}`, `/Received/{id}` or `/VehicleImages/{id}`. The existing browser gate cannot have covered these four pages. UIIMP-010 owns the walk |

Selected checklist items independently re-checked on merged `dev`: the
`OperatorLabels` one-list append (one hunk, `+24/-0`), "no new CSS/JS,
no inline styles/scripts" (no `.css`/`.js` in the merge; no `style=` or
`<script` in any of the four pages), "exactly one `<main>`" (zero
`<main>` in all four — the shell's is the only one), "no stray
`aria-current`/`aria-pressed`" (zero of each), and the dated
Simplification pass heading in `plan.md:117-151` with four applied and
three rejected findings, each carrying a reason.

## Findings

1. **The ticket's ticked verification item overstates the disabled
   Complete control.** "The one disabled control (Complete) posts the
   same `complete` action in both states" (ticket body; repeated in the
   post-implementation report and in the code comment at
   `Pages/Triage/Details.cshtml:196-199`) is contradicted by the markup
   eight lines below it: `data-dialog-open` is nulled and
   `triage-complete-dialog` is not rendered when `!canComplete`, so the
   disabled control posts nothing. The **capability is not missing** —
   Complete is fully wired whenever the record's state permits it, and
   the disabled render matches the merged `.gated` convention and `dev`'s
   own pinned assertion. This is a record defect, not a shipped one. It
   belongs with UIIMP-012, which already owns the D7 wording.
2. **A stale line citation in the ticket record.** The
   post-implementation report and UIIMP-012 both cite
   `Pages/Cases/Assessment/Index.cshtml:765` as the role-gate precedent.
   On merged `dev` that file is 596 lines; its `.gated` wrappers are at
   `:203`, `:210`, `:213`, `:225`, `:250`, and the role-gate wording
   lives at `Assessment/Index.cshtml.cs:178-185`. The claim holds; the
   line number does not.
3. **Two §1.6 divergences, both disposed in the record, neither hidden.**
   The resolve dialog ships a free-text "Destination identifier" input
   where §1.6 says "case picker" (rejected in `plan.md:143-145` — no
   existing case-search port backs a picker), and "Create Case from
   accepted instruction" is not offered as a destination because no
   `UnidentifiedResolutionTargetKind` backs it (reasoned in the
   `OperatorLabels.cs` doc comment). Recording them here so they are not
   read as delivered.
4. **PLAT-061 confirmed on merged `dev`.** `site.css` matches `gated` at
   only `:1893`, `:1895`, `:1911`, `:1961` — there is no
   `[data-condition]` guard, so `content: attr(data-condition)` resolves
   empty while the pseudo-element keeps its padding and `--band`
   background. Hovering an **enabled** gated control paints an empty
   pill. Reported, not fixed: `site.css` is PLAT-029's file.

## Outstanding

- **Layout at 1580/1100/760 — UIIMP-010.** Not merely unrun: the
  existing tooling does not reach these pages. `LayoutIntegrityTests`
  parameterises over `AccessibilityTests.AuthenticatedRouteList`, which
  holds only static routes (`/`, `/Inbox`, `/Cases`, `/Search`,
  `/Administration/*`, …). A record-detail route needs a seeded id, so
  covering these four pages requires more than adding strings to that
  list. UIIMP-010 should be told this before it starts.
- **UIIMP-012** (backlog, EPIC-011) — §1.5 "Notes panel" vs the shipped
  `Permanent history` heading, and D7's disabled-control clause vs the
  merged `.gated` convention. Both confirmed real here: §1.5 names the
  panel "Notes"; `Pages/Triage/Details.cshtml:400` renders "Permanent
  history"; `origin/dev` said the same before the port
  (`a01a640b^1:…/Triage/Details.cshtml:348`);
  `QdosTriageIntegrationTests.cs:477` pins it; and
  `docs/frd/frd-03-triage.md:43` owns the term. Finding 1 above should be
  folded into this ticket.
- **PLAT-061** (backlog, EPIC-011) — the `.gated::after` guard, cited
  above with exact lines.
- **Superseded findings' recorded values are visible nowhere in the UI**
  (carried forward from the post-implementation report). Core retains
  them; §1.5 does not ask for them. Still has no ticket.
- **Not claimed: deployment.** No environment has been shown running
  `b92cb9a7`. Every claim above is tier 1 (registration/caller) or tier 2
  (green build and test). Tier 3 is out of scope for this proof.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has
not been promoted; the exact-SHA `dev` → `main` promotion happens at
wave 5.
