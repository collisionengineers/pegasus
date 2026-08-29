# Plan — ENG-028

Implemented by `gpt-5.6-terra` (xhigh). The ticket body already carried the
contract; this records the approach taken and its evidence.

## What this ticket is for

Two Done tickets were reversed to `verifying` under D20 because their
capabilities had no production caller. **This ticket is the caller for both:**
[[ENG-026]] built named estimates with per-estimate VAT and a Current estimate,
and nothing edited them; [[ENG-025]] built the Assessment shell with its editor
and Send-to-Claude dialog split out to here.

## Rule 14 — the five callers that unblock ENG-026

| Capability | Rendered control | Handler | Core port |
| --- | --- | --- | --- |
| New / save estimate | `Index.cshtml:199` → form `:430` | `Index.cshtml.cs:666` | `ISaveEstimate` |
| Import estimate | trigger `Index.cshtml:205` | `Index.cshtml.cs:1092` | `ISaveEstimate` |
| Duplicate | form `Index.cshtml:399` | `Index.cshtml.cs:756` | `IDuplicateEstimate` |
| Use estimate (set Current) | form `Index.cshtml:411` | `Index.cshtml.cs:841` | `ISetCurrentEstimate` |
| Delete | trigger `Index.cshtml:392` → form `:623` | `Index.cshtml.cs:794` | `IDiscardEstimate` |

**ENG-026's named estimates now have operator entry points.** Its re-audit can
run once this merges (D15).

## Send to Claude is wired but NOT claimed as delivered

The handler exists at `Index.cshtml.cs:602` with a real caller. But
`docs/operations.md` records **`Features:SendToAi` as OFF in production**
(DevelopmentOffline only; production activation additionally needs a non-preview
transport decision, ADR-0031).

Under D21 that is the "capability behind a gate that is CLOSED in the deployed
estate" row: **not delivered.** The lane said so itself rather than counting it,
which is the correct call. Opening that gate is a D26 release-time decision, not
this lane's.

## The D7 seams are untouched

Glass's and Audatex remain drawn, disabled, and wrapped with a non-empty
`data-condition` at `Index.cshtml:220,223` — verified by the orchestrator. They
belong to [[ENG-030]] and are settled by operator decision **D23**: draw the
button, never claim the capability.

## Dialog triggers keep static targets

Import, delete and Send to Claude each have a query-string static target with
`data-dialog-open` as the enhancement, matching the shape [[PLAT-027]] adopted in
the same session. [[TICK-223]] records the rule; the two must not diverge, and
now do not.

## Reused, not rebuilt

ENG-026's ports; Core `EstimateTotals` and `EstimateOperations`; the already
registered JSON parser; existing CSS; the existing dialog convention. **No Core
change, no new abstraction, no money policy in the browser.**

## Verification

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` | exit 0, 0 warnings, 0 errors, **0 `CS####`** |
| `dotnet test … --filter "FullyQualifiedName~AssessmentEstimateImportWebTests"` | **9 passed, 0 failed, 0 skipped** |

## Assertion integrity — 35 removed lines, examined line by line

A raw diff shows **35 removed `Assert.` lines**, which is exactly the shape that
must never be waved through. The orchestrator examined all of them:

- **Total assertions in the file rose 74 → 90.**
- **Every retained test kept or gained** assertions: 28→29, 5→5, 5→5, 6→6, 4→4.
- Two whole tests were removed (see `files/files.md`), and four added.
- The three import refusals that still apply under the named-estimate model
  survive intact with their `Assert.Empty`: edit-mode never entered, rejected
  parse, non-Engineer.

The removed `AnExistingDraftRefusesASecondImport` encoded "one draft at a time" —
the single-draft rule the named-estimate model exists to replace. **That is a
deliberate behaviour change, not a weakened assertion** — but it is the judgement
a reviewer should test rather than inherit, and `research/research.md` flags it
as this lane's least-safe assumption.

## Simplification pass — 2026-08-29

- **Reuse** — ENG-026's ports, Core totals and line operations, the registered
  JSON parser, existing CSS and dialog convention.
- **Simplification** — the obsolete single-estimate acceptance UI is replaced by
  one named-estimate path rather than the two coexisting.
- **Efficiency** — one estimate list per render; no browser-side money policy.
- **Altitude** — page handlers validate and route only; Core keeps estimate
  policy and AI-job ownership.

No unapplied findings.

## Commits

- `7242dfba` — feat(assessment): wire named estimate editor
- `e29ee083` — test(assessment): prove named estimate callers

## Cross-model review and remediation — 2026-08-29

A Claude-family reviewer (this lane was Codex-built) returned `REQUEST_CHANGES`
with one blocker. Its central judgement went **in the lane's favour**: the
deleted refusal test was justified, with three citations from ENG-026's own
merged code proving several drafts may coexist, and `IDuplicateEstimate` could
not exist otherwise. Rule 14 is satisfied at all five caller chains.

The blocker was something nobody had looked at, and it took **three passes** to
settle. Each intermediate state looked correct.

### Pass 1 — the defect · the editor destroyed imported evidence

`ReadEditorPost` hard-coded `null` for `GuideCode`, `Betterment`, `Status`,
`EvidenceLabel` and `Justification`, and `false` for `Unpriced`. Verified: the
page model mentioned **zero** of those five fields.
`EfRepairSpecificationStore.SaveEstimateAsync` does
`RemoveRange(entity.Lines); entity.Lines.Clear();` and re-adds from the request.

So the ticket's own primary journey — import an Audatex PDF, adjust one labour
hour, save — destroyed the guide code, betterment, provisional status, evidence
label and To-be-confirmed flag on **every line**, while the estimate's provenance
continued asserting those lines came from that document. Operator-visible: the
read-only view renders Code and Betterment columns the editor does not have.

### Pass 2 — the fix · carry the fields forward by persisted line id

The editor now posts each line's existing `CaseEstimateLineRecord.Id` and the
handler carries the unrendered fields forward from the matching line. **Matching
is by stable persisted id, not position** — so removed and reordered rows are
safe, a new row has no id and no earlier evidence to preserve, and a foreign id
matches nothing in the *selected* estimate's dictionary and so cannot borrow
another line's evidence.

Proven against the pre-fix code: `expected guide code "12 34 567", actual null`.

### Pass 3 — the fix's own defect · `Unpriced` was preserved too well

Caught by the orchestrator reviewing the remediation. `Unpriced` was carried
forward **unconditionally**, but `AssessmentPolicy.cs:488` refuses a line that is
both marked To be confirmed and priced:

> "A line marked To be confirmed cannot also carry a price."

`linePartPounds` (`Index.cshtml:506`) is a real editor input, so an operator
pricing an imported to-be-confirmed line would produce exactly that combination.
The refusal is reached in production through `EstimatePolicy.ValidateSave`, which
routes `request.Lines` through
`AssessmentPolicy.NormalizeRepairSpecificationLines` (`Estimates.cs:187`).

**Before the remediation this worked** (while destroying the flag); after it, the
save would have been refused. A regression introduced by the fix.

Now `Unpriced = previous.Unpriced && line.Price is null` — carried forward only
while the line still has no price. The other five fields are unaffected.

New test `PricingAnImportedUnpricedLineClearsItsToBeConfirmedFlag`:

```
pre-fix:   Assert.False() Failure — Expected: False, Actual: True
post-fix:  AssessmentEstimateImportWebTests — 11 passed, 0 failed
```

**Scope of that evidence, stated so it is not read as more than it is:** the
suite's `RecordingStores` is a fake `ISaveEstimate` and does not run Core policy,
so the test demonstrates the invalid combination being *constructed* — the
precondition — not the refusal itself. The refusal is established by the call
chain above, not by this test.

### Medium — raw internals in operator copy · **FIXED**

All four new handlers now use `MutationRefusalMessage` (`:743`, `:806`, `:851`,
`:893`), which the file's own doc comment says exists because "version and lease
conflicts carry internals". The import handler already used it; the four new ones
did not. Without this an Engineer would see text like *"Case '3f2b8c1a-…' is at
version 9, not expected version 7."*

### Smaller items

- Qty input `min="0"` → `min="1"`, matching Core's refusal (`Index.cshtml:503`).
- The duplicated seven-row totals `<dl>` replaced by a shared
  `RenderEstimateTotals` helper (`:681`), following the file's existing
  `RenderSpecificationLines` convention.
- `files/files.md` correction, reported rather than self-edited: the two lines
  are new switch arms on the shared top-level
  `OperatorLabels.RepairSpecificationRoute`, **not** "two lines appended inside
  this lane's own nested class".

### Process note — the worktree moved mid-remediation

The remediating agent reported that `origin/dev` advanced under it during work
(`1d7d50d1`). That was the orchestrator merging `dev` into the worktree while the
remediation was running — the same error made on CASE-027's worktree during its
review. **Do not touch a worktree with a review or remediation in flight.** The
agent rebuilt and re-ran after the merge, so its numbers stand.
