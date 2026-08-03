# Report renderer integration — capability coverage matrix

Draft supporting document for the `report-renderer-integration` task. It answers
one question: with **all** report-renderer-related features in scope, including
`Later` ones, what does the plan set cover and what does it not?

Bands and targets are as recorded in `docs/capabilities.md` and are not changed
by any plan in this set.

## Coverage states

| State | Meaning |
| --- | --- |
| **Covered** | A plan specifies the work in enough detail to implement and verify it |
| **Partial** | A plan covers part of the outcome; the remainder is named and unowned or deferred |
| **Route only** | A plan describes what activation would require, without specifying the implementation |
| **Prerequisite** | Not renderer work. Gates renderer work, and the renderer's demands on it are specified |
| **Adjacent** | Already accepted or separately owned; the plan set states the boundary but does not plan it |
| **Not covered** | In scope by the operator's widening, and no plan addresses it |

## 1. Renderer capabilities

| ID | Band / target | State | Owning plan |
| --- | --- | --- | --- |
| RPT-01 | Later 1.1.0 | **Partial** | Determinism, artifact identity, hash and the wording gate are in the seam plan. "Computes each figure once" is in the rendering-capabilities plan |
| RPT-02 | Later 1.1.0 | **Covered** | rendering-capabilities |
| RPT-03 | Later 1.1.0 | **Covered** | rendering-capabilities — including the fact that no renderer template exists for it |
| RPT-04 | Later 1.1.0 | **Covered** | rendering-capabilities |
| RPT-05 | Later 1.1.0 | **Partial** | Issue identity and the no-overwrite rule are in the seam plan; data reuse is in rendering-capabilities; the correction lifecycle is blocked on the CASE-23 open decision and is deliberately unowned |
| EXT-08 | Later 1.1.0 | **Route only** | seam — Stage 2/3. Activation is described, not specified; it cannot be until the prerequisites exist |

## 2. Prerequisites the renderer depends on

These are not renderer work. The rendering-capabilities plan specifies what the
renderer will demand of each, which is the useful contribution this task can make
to them.

| ID | Band / target | State |
| --- | --- | --- |
| CASE-31 | Later 1.0.0 | **Prerequisite** — one accepted structured case/engineering record as the source for every deterministic report |
| ENG-01 | Later 1.0.0 | **Prerequisite** — canonical repair specification with route provenance |
| ENG-02 | Later 1.0.0 | **Prerequisite** — Engineer-owned values, outcome, salvage category/value, roadworthiness |

`docs/requirements.md:53-56` sequences all three ahead of `EXT-08` and
`RPT-01`–`RPT-05`. None exists.

## 3. Consumers of a rendered report

| ID | Band / target | State | Owning plan |
| --- | --- | --- | --- |
| MAIL-17 | Later 1.2.0 | **Covered** | consumers |
| EXT-11 | Later 1.2.0 | **Covered** | consumers — the fee-note boundary |
| MI-02 | Later 1.2.0 | **Covered** | consumers |
| MI-03 | Later 1.2.0 | **Covered** | consumers |
| CASE-23 | Next 0.4.0 | **Partial** | consumers — bounded by its own open decision; no states or transitions are invented |
| CASE-24 | Now 0.1.0-alpha.1 | **Adjacent** | consumers states the boundary |
| MAIL-12 | Later 0.5.0 | **Route only** | consumers |
| UI-15 | Later 1.0.0 | **Route only** | consumers, deferring to the in-flight `task/ui-alpha-design-pass` and the `design/README.md` design route |
| CASE-22 | Later 1.0.0 | **Route only** | consumers — replacing EVA report-preparation is the renderer's eventual purpose |

## 4. Report-sent evidence and the EVA handoff

Already accepted and implemented for reports produced outside Pegasus. The
consumers plan owns the join between a *generated* artifact and this evidence,
and keeps the four claims distinct: artifact generated, artifact filed, report
sent, external receipt.

| ID | Band / target | State |
| --- | --- | --- |
| MAIL-14 | Now 0.1.0-alpha.1 | **Adjacent** |
| MAIL-15 | Now 0.1.0-alpha.1 | **Adjacent** |
| MAIL-16 | Now 0.1.0-alpha.1 | **Adjacent** |
| CASE-21 | Now 0.1.0-alpha.1 | **Adjacent** — the `First sent to Engineer` proxy is not a report |
| CASE-30 | Now 0.1.0-alpha.1 | **Adjacent** |
| EXT-03 | Now 0.1.0-alpha.1 | **Adjacent** — the EVA bundle is rigorously distinct from a rendered report |

## 5. Custody

| ID | Band / target | State | Owning plan |
| --- | --- | --- | --- |
| DOC-02 | Now 0.1.0-alpha.1 | **Covered** | consumers — a rendered artifact becomes a document version |
| DOC-03 | Now 0.1.0-alpha.1 | **Covered** | consumers |
| DOC-07 | Now 0.1.0-alpha.1 | **Adjacent** | consumers states the boundary |

## 6. Ingress

| ID | Band / target | State | Owning plan |
| --- | --- | --- | --- |
| MCP-01 | Now 0.1.0-alpha.1 | **Covered** | mcp |
| MCP-02 | Now 0.1.0-alpha.1 | **Adjacent** | unchanged by this work |
| MCP-03 | Now 0.1.0-alpha.1 | **Adjacent** | unchanged |
| MCP-04 | Now 0.1.0-alpha.1 | **Adjacent** | unchanged |
| MCP-05 | Next 0.3.0 | **Adjacent** | scoped to the classified-email workspace, not the renderer |

The mcp plan raises against itself whether the Automation Actor has any business
need to render at all, and notes there is **no allocated capability ID** under
which an MCP render tool is `Now` work. That remains open.

## 7. AI

| ID | Band / target | State | Owning plan |
| --- | --- | --- | --- |
| AI-08 | Later 1.3.0 | **Covered** | skills-surface — the capability that joins the skill packages to the renderer |
| AI-09 | Later 1.3.0 | **Adjacent** | its design route is in-flight under `task/ui-alpha-design-pass` |
| AI-06 | Later 0.6.0 | **Not covered** | — |
| AI-07 | Later 1.3.0 | **Not covered** | — |

## 8. Data sources feeding report content

Each owns a concept the renderer consumes but must never own. The
rendering-capabilities plan defers to them explicitly rather than duplicating.

| ID | Band / target | State |
| --- | --- | --- |
| EXT-09 | Later 1.0.0 | **Adjacent** — versioned estimate lines, approvals, original-versus-assessed comparison, savings |
| EXT-10 | Later 1.0.0 | **Adjacent** — versioned valuation evidence |
| EXT-12 | Later 1.0.0 | **Adjacent** — Audatex/PDF estimate ingestion |
| EXT-13 | Later 1.0.0 | **Adjacent** — valuation-source adapters |
| CASE-05 | Later 0.5.0 | **Adjacent** — diminution cases, the case type RPT-04 renders |

## 9. EVA replacement

The renderer is what eventually replaces EVA's report preparation. These are the
surrounding replacements.

| ID | Band / target | State |
| --- | --- | --- |
| EXT-04 | Later 0.7.0 | **Adjacent** — direct EVA API integration |
| EXT-05 | Later 1.0.0 | **Adjacent** — Engineer assignment |
| EXT-06 | Later 1.0.0 | **Adjacent** — estimating |
| EXT-07 | Later 1.0.0 | **Adjacent** — valuation |

## 10. Templates with no capability identifier

Five of the renderer's twelve template identifiers map to nothing in
`docs/capabilities.md`. The rendering-capabilities plan proposes, for each,
either an allocated identifier or a reasoned retirement.

| Template | Apparent relation |
| --- | --- |
| `market-valuation-evidence` | EXT-10 / EXT-13 valuation evidence |
| `advert-evidence-pack` | EXT-10 / EXT-13 valuation evidence |
| `blank-letterhead` | AI-08 house style / letterhead |
| `roadworthy-criminal-report` | no capability; a `roadworthy-report` skill package exists |
| `part-35-response` | CASE-23 post-report dispute (Civil Procedure Rules Part 35) |

And in the other direction: **RPT-03 has no renderer template at all.**

## Honest summary

Before the scope widening, the plan set covered the **seam** and not the
**features**: RPT-02, RPT-03 and RPT-04 were entirely unowned, RPT-01 and RPT-05
were half-owned, every consumer was unplanned, and the relationship between the
protected AI skill packages and the renderer templates had not been noticed.

With the rendering-capabilities, skills-surface and consumers plans added, the
renderer-owned capabilities are covered and the consumer chain is specified to the
point where its blocking prerequisites are visible.

What remains genuinely **not covered**, and is stated rather than hidden:

- **AI-06 and AI-07.** Neither is renderer-adjacent on inspection; they are named
  here only so the exclusion is deliberate.
- **The RPT specification's own inputs.** With `DESIGN_SPEC` superseded, the
  specification for RPT-02–05 must come from accepted CASE-31/ENG-01/ENG-02 data
  plus operator decisions. The rendering-capabilities plan states the field-level
  contract the renderer needs; it cannot write the specification itself.
- **Report wording.** `docs/open-decisions.md:222` stays open. No plan invents
  any, and the Core wording gate defaults closed.
- **The CASE-23 lifecycle.** An open decision. The consumers plan bounds itself
  by it rather than inventing states.
- **Where rendering executes in production.** Open question H1; there is no Web
  Dockerfile and the deployed base image has neither Chromium nor the fonts.

None of these is a planning omission. Each is a decision that has not been taken
or a capability that does not exist yet.
