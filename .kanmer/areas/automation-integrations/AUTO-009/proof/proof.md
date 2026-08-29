# Proof — AUTO-009: FRD-10/FRD-11 and ADR-0035, AI job ledger, automation.jobs scope and per-estimate VAT

## What was verified, and where

Verified against merged `dev` at `b92cb9a7b8bf7727b452aa397d9df04084da1270`
(`b92cb9a7`, "Merge pull request #612"), the primary checkout
`C:/Users/PC/Documents/GitHub/pegasus`, on 2026-08-29. The ticket's single
commit `879fbba8` ("docs(ai): FRD-10/FRD-11 and ADR-0035 for the AI job ledger
and automation.jobs scope (AUTO-009)", 2026-08-28 09:14:42 +0100) merged
through PR #585 as merge commit `2b02609f25484fcff800030dd78936db9b09a5fb`.
Both are reachable from `origin/dev`:

```
git merge-base --is-ancestor 879fbba8 origin/dev   -> YES
git merge-base --is-ancestor 2b02609f origin/dev   -> YES
```

The shipped diff is exactly the four files the ticket claims to own, and
nothing else:

```
git show --stat --format="" 879fbba8
 docs/adr/0035-ai-job-ledger.md                                      | 121 +++
 docs/adr/README.md                                                  |   1 +
 docs/frd/frd-10-mcp-automation-and-actor-boundary.md                |  35 +++
 docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md    |  78 +++
 4 files changed, 235 insertions(+)
```

## A note on evidence tiers for a docs-only ticket

The repository's three tiers — registration, green build/test, deployed and
exercised — describe code. A Markdown governing document has no DI
registration and does not execute, so no claim below is offered at the
deployed tier. Where a shipped document is load-bearing, the strongest
honest evidence is **downstream consumption on merged `dev`**: other merged
artefacts that cite the section by name and were built to it. That is the
tier used, and it is named as such each time. It is not a deployed-feature
claim.

## Evidence

### ADR-0035 exists on merged `dev`, with AGENTS.md frontmatter and one decision

Tier: merged source, read directly at `b92cb9a7`.

`docs/adr/0035-ai-job-ledger.md`, 121 lines. Frontmatter occupies lines 1–10
and carries all eight fields AGENTS.md requires:

```
docs/adr/0035-ai-job-ledger.md:1-10
---
id: ADR-0035
status: accepted
date: 2026-08-28
supersedes: []
superseded_by: []
related_capabilities: [AI-10, AI-09, MCP-06, MCP-01]
related_frd: [frd-10, frd-11]
tags: [ai, mcp, automation, ledger]
---
```

The body follows the AGENTS.md template in order, and Status is stated first:

```
grep -n '^#' docs/adr/0035-ai-job-ledger.md
12:# ADR-0035: AI job ledger
14:## Status
21:## Context
46:## Decision
78:## Consequences
97:## Options considered
113:## Links
```

H1 at line 12 (below frontmatter) matches the established ADR shape —
`docs/adr/0034-…md:12` and `docs/adr/0031-…md:12` are identical.

One decision, not a bundle: `## Decision` opens "Pegasus keeps a **durable,
pull-based AI job ledger** — the `AiJobs` store — owned by `Pegasus.Core`"
(`docs/adr/0035-ai-job-ledger.md:48`), and its six numbered clauses are all
properties of that one store. `supersedes: []` is correct: the ADR supersedes
a `docs/boundaries.md` exclusion, not another ADR, and `supersedes` is an
ADR-to-ADR field.

### The ADR index row exists

Tier: merged source.

```
grep -n "0035" docs/adr/README.md
45:| [0035](0035-ai-job-ledger.md) | AI job ledger | FRD-10, FRD-11 |
```

The row sits in the `## Current architecture decisions (status: accepted)`
table (header at line 18), which is how this index expresses `accepted`.

### The ADR's cross-document links resolve, including their anchors

Tier: build/test — a repository gate, run below — plus a manual anchor check
the gate does not perform.

The two FRD anchors the ADR cites both exist as headings:

```
grep -n "^#.*AI Job List" docs/frd/frd-11-…-proposals.md
206:### AI Job List

grep -n "^#.*AI job and estimate tools" docs/frd/frd-10-…-boundary.md
57:## AI job and estimate tools
```

The four ADR files it links (`0011`, `0026`, `0027`, `0031`) and
`../boundaries.md` all exist.

### FRD-11 carries the AI-10 catalogue with D5, and the D9 VAT rule

Tier: merged source.

`docs/frd/frd-11-…-proposals.md:206` — `### AI Job List`. It names the four
kinds as a closed Core list (Estimate, Unidentified resolution, Query
response, Unidentified-queue pass), the seven states (`Queued` → `Taken` →
`Draft ready` → `Completed`, with `Failed`, `Cancelled`, `Expired`), started-by,
lease expiry, and the Operations/Administration surfaces. D5 is honoured
explicitly — the Unidentified-queue pass row reads "An external scheduler
through the Actor `create` tool — Pegasus runs no timer".

`docs/frd/frd-11-…-proposals.md:266` — `### Estimate VAT on the rendered
report`, D9: "the Current estimate's VAT percentage replaces the built-in
repairer-VAT-registered rule; that rule applies only when no Current estimate
exists", with the seven-row figure table.

### FRD-10 carries the seven `pegasus_ai_job_*` tools under `automation.jobs`

Tier: merged source.

`docs/frd/frd-10-…-boundary.md:57` — `## AI job and estimate tools`, with a
nine-row tool table: `pegasus_ai_job_list/create/take/progress/complete/fail/
release` under `automation.jobs`, plus `pegasus_estimate_save` and
`pegasus_estimate_list` under `automation.assessment`. It states that
`automation.jobs` is a new scope requiring its own consent description (D6).

### Downstream consumption — the documents are load-bearing on merged `dev`

Tier: downstream consumption on merged `dev`. This proves other merged work
was written to these sections; it is **not** a claim that any AI job has run
in a deployed environment.

The `automation.jobs` scope, the seven tools and the consent description all
exist in shipped code, added later the same day by **AUTO-011** (`a54dcce9`,
2026-08-28 09:49, a descendant of `879fbba8`, ancestor of `origin/dev`):

```
src/Pegasus.Web/Mcp/AutomationMcp.cs:34
    public const string JobsScope = "automation.jobs";

src/Pegasus.Web/Pages/Connect/Authorize.cshtml.cs:35
    [AutomationMcp.JobsScope] = "List, take, progress, complete, fail and
    release AI jobs; create Unidentified-queue passes."
```

All seven tool names in `src/Pegasus.Web/Mcp/AiJobMcpTools.cs` match the FRD-10
table exactly (`…_list:50`, `…_create:85`, `…_take:130`, `…_progress:159`,
`…_complete:189`, `…_fail:234`, `…_release:264`), and that file's header cites
the document by name (`AiJobMcpTools.cs:35` — "The AI job ledger tools
(ADR-0035, FRD-10 § AI job and estimate tools)").

The D9 VAT rule is implemented against the FRD-11 section, by **ENG-026**
(`bcee2ae2`), with a named production caller:

```
src/Pegasus.Core/Reports/AssessmentReportRendering.cs:88-92 (doc comment)
/// figures, including <see cref="Vat"/>, come from
/// <c>Pegasus.Core.Assessment.EstimateTotals</c> through
/// <see cref="VatOverride"/> (FRD-11 § Estimate VAT on the rendered report);
/// the built-in repairer-VAT-registered rule below applies only when no
/// Current estimate exists.

src/Pegasus.Core/Reports/AssessmentReportRendering.cs:105-107
    public decimal Vat => VatOverride ?? decimal.Round(
        (RepairerVatRegistered ? Subtotal : Parts + PaintMaterials) * 0.20m,
        2, MidpointRounding.AwayFromZero);

src/Pegasus.Core/Reports/AssessmentReportProjection.cs:97  (the caller)
            VatOverride: totals.Vat);
```

Eleven code sites across `Pegasus.Core` and `Pegasus.Web` cite an
`FRD-11 §` section written by this ticket (`grep -rn "FRD-11 §" src/
--include=*.cs --include=*.cshtml | wc -l -> 11`), and ADR-0035 is cited in
seven files including the ledger store, the EF entity, the connector tools and
the `20260828084644_GrantAiJobs` migration — the shape ADR-0035's Consequences
section required ("Wave-3 implementation carries the `AiJobs` migration, its
grants, the Core ledger, and the Actor tools together").

The ADR's supersession claim was carried through by **UIIMP-007**
(`e65571f4`); `docs/boundaries.md:23` now reads "the shared AI job ledger (in
scope under ADR-0035, AUTO-009…)", and `docs/capabilities.md:273` records
"[FRD-11] § AI Job List (AUTO-009)" as AI-10's canonical owner.

### Solution build and test

Tier: green build/test. Not re-run here — cited from the canonical gate
evidence for merged `dev` at `b92cb9a7` (orchestrator run, 2026-08-29):
`dotnet restore --locked-mode` exit 0; `dotnet build --configuration Release`
succeeded with 0 warnings and 0 errors; `dotnet test --filter
'Category!=Corpus&Category!=Browser'` — ArchitectureTests 100 passed,
Core.Tests 1133 passed, IntegrationTests 1022 passed / 2 pre-existing skips,
0 failures. This ticket changed no code, so the suite is context, not
evidence of its behaviour.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| ADR has frontmatter per AGENTS.md and one decision. | Proven | `docs/adr/0035-ai-job-ledger.md:1-10` (eight frontmatter fields), headings at 12/14/21/46/78/97/113 matching the Status·Context·Decision·Consequences·Options·Links template, single decision at line 48. |
| `scripts/Test-DocumentationLinks.ps1` passes. | Proven | `pwsh` run on `b92cb9a7`: "All relative Markdown links resolve (129 files checked)." `EXITCODE=0`. The script checks path existence only, not anchors (its own header says so); the two cross-document anchors were checked by hand — see above. |

One additional repository gate was run because the ticket adds a new Markdown
file: `scripts/Test-MarkdownPlacement.ps1 -Base 2b02609f^ -Head 2b02609f` →
"Markdown placement passed for 2b02609f^..2b02609f." `EXITCODE=0`.

## Outstanding

- **FRD-10's `automation.mail` sentence is now stale on merged `dev`.**
  `docs/frd/frd-10-…-boundary.md:87` states "The existing `automation.mail`
  scope is granted today without a consent description; it must carry one
  before any connector is consented to it." That was a verified fact when
  `879fbba8` was written (the ticket plan records the read-only check). It
  stopped being true 35 minutes later: AUTO-011's `a54dcce9` added
  `[AutomationMcp.MailScope] = "List and read retained mail and correct a
  message's classification."` at `src/Pegasus.Web/Pages/Connect/
  Authorize.cshtml.cs:34`. The FRD's *requirement* is satisfied; only the
  "granted today without" clause needs deleting. This is doc currency, not a
  defect in what AUTO-009 shipped, and the fix belongs to whichever ticket
  next edits FRD-10 — it is **not** in AUTO-009's scope and was not silently
  corrected here.

- **FRD-11's Operations panel clause has no shipped surface.** The section
  requires an AI Job List panel and a `Send Unidentified to AI` control on
  `/operations`; `src/Pegasus.Web/Pages/Operations/Index.cshtml` renders only
  Service health (line 51), Attention required (101) and Active upload links
  (144) — no AI or job markup anywhere in the page or its model. Owned by
  **PLAT-049** ("Operations: AI Job List, Service health and Send Unidentified
  to AI"), which is in `review`, not merged. AUTO-009's plan put code
  explicitly out of scope, so this is not a gap in this ticket.

- **No browser or layout evidence is claimed.** This ticket rendered nothing;
  the 1580/1100/760 walk is UIIMP-010's and is not asserted here.

- **Observation, pre-existing, no owner asserted.** AGENTS.md specifies the
  ADR index as `ID | Title | Status | Superseded-by | Owner capability`;
  `docs/adr/README.md` has long used `ADR | Title | Related FRD` with a
  separate superseded table. AUTO-009 correctly matched the existing table
  rather than introducing a second shape. Flagged only so the divergence is
  recorded somewhere; it predates this ticket by many rows.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.
