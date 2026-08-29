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
