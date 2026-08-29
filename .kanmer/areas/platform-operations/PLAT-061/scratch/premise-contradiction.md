## 2026-08-29 — settle the Razor premise BEFORE working this ticket

This ticket's scope rests on a claim about Razor that **two places in the
repository contradict each other about**. Establish which is true first; the
answer decides whether this ticket is a real defect, a smaller one, or none.

### The two claims

**[[CASE-027]]'s finding** (in its plan and post-implementation report): Razor
omits a plain HTML attribute whose expression is `false`, but does **not** omit
one whose expression is `null`. On that reading,
`data-condition="@(cond ? null : "…")"` leaves the attribute present and empty,
and `.gated::after`'s unguarded `content: attr(data-condition)` paints an empty
pill on every **enabled** gated control.

**`src/Pegasus.Web/Pages/Shared/_Layout.cshtml:159-160`**, in a comment that
predates this session:

> Razor omits an attribute whose value is null, so a page that is not a record
> renders a plain main.

The layout relies on that behaviour for `data-workspace-record`,
`data-workspace-href` and `data-workspace-label`.

Both cannot be right as written.

### Evidence gathered so far — suggestive, not decisive

- **The committed Test UI snapshots contain zero `data-condition=""`**
  (`grep -rho 'data-condition=""' docs/design/test-ui/pages/` → 0), while
  non-empty ones such as `data-condition="Available in Review"` do appear. That
  leans toward the layout comment being right.
- **But the snapshot set does not cover every state.** It may simply contain no
  capture of a gated control in its *enabled* state, which is exactly the case
  in question. So this is not proof.
- CASE-027 states its test `NoWorkspaceGateEverRendersAnEmptyCondition`
  (`tests/Pegasus.IntegrationTests/CaseVehicleWebTests.cs:205`) "is what caught
  it" — implying it failed against the pre-fix markup. **That would be decisive
  if confirmed, and it has not been confirmed independently.**
- CASE-027's own reviewer narrowed the affected sites: of the four originally
  claimed, the three in `Pages/Cases/Assessment/Index.cshtml` are **not** the
  same idiom — each renders only inside a branch where its condition is
  non-null. It judged `Pages/Triage/Details.cshtml:202` the one genuine
  remaining case.

### The decisive experiment, which costs minutes

Revert only the gate hunk in `Pages/Cases/Details.cshtml` to its `origin/dev`
form — `data-condition="@(Model.CanOpenAssessment ? null : "Available after the
current Review export")"` — and run:

```
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj \
  --configuration Release --no-build \
  --filter "FullyQualifiedName~NoWorkspaceGateEverRendersAnEmptyCondition"
```

- **Fails** → CASE-027 is right, Razor keeps the empty attribute, and this
  ticket's `[data-condition]` guard on `.gated::after` is a real fix.
- **Passes** → the layout comment is right, Razor omits it, and this ticket
  should be rescoped or closed as not-a-defect. In that case CASE-027's plan
  needs its premise corrected, and its gate restructuring — while harmless —
  was not fixing what it thought.

**Do not work this ticket from either claim without running that.** Facts are
checked, not argued: this is exactly a premise about how the framework behaves,
and the read-only check is cheap.

### If it turns out to be real

The one-selector root fix named by CASE-027 is a `[data-condition]` guard on
`.gated::after` in `src/Pegasus.Web/wwwroot/css/site.css` — PLAT-029's file,
which [[PLAT-027]] has already edited once this session under a declared D19
case-2 exception, so the ownership precedent exists.
