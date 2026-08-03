# Report renderer integration — master plan

Transient task plan for the `report-renderer-integration` claim
(`task/report-renderer-integration`, taken 2026-08-03). This file is the
canonical plan for the claim; the sibling `report-renderer-integration-*.md`
files in this directory are its supporting drafts and are deleted with it by
the post-merge maintenance push.

## Scope of this task

This task produces **planning documents only**. It changes no project, no
solution, no runtime code and no capability status. Nothing in this task
activates a report capability, proves a caller, or retires the workspace.
The retirement of `workspaces/report-renderer/` is the *outcome these plans
describe*, not something this task performs.

Stated plainly, because the routing rules make the distinction load-bearing:
relocating source is not integration, integration is not activation, and
activation is not acceptance
([evidence tiers](../operations.md#required-evidence-tiers)).

## Deviation from the temp-plan contract

The [temp-plan contract](README.md) specifies one `<task-slug>.md` per
claimed task. This claim carries six distinct planning subjects, each large
enough that a single file would be unreviewable. On operator direction the
plan is split across sibling files sharing the `report-renderer-integration`
slug prefix, so that the orphan rule and the post-merge deletion still work
on the whole set. The supporting files are listed under
[Plan set](#plan-set).

## Capability inventory

Every capability ID that governs the renderer, its integration, or what
consumes it. Bands and targets are as recorded in
[capabilities](../capabilities.md); this plan does not change them.

### Primary — what the renderer would eventually activate

| ID | Band | Target | Outcome |
| --- | --- | --- | --- |
| RPT-01 | Later | 1.1.0 | Deterministic renderer validates accepted data, computes each figure once, applies the fixed Collision Engineers design |
| RPT-02 | Later | 1.1.0 | Assessment rendering covers four outcome variants, emits the fee note plus itemised repair-specification breakdown |
| RPT-03 | Later | 1.1.0 | Audit rendering preserves conservative and maximised specifications and records their uplift |
| RPT-04 | Later | 1.1.0 | Diminution rendering uses accepted original-case data plus the Engineer-entered percentage |
| RPT-05 | Later | 1.1.0 | Addenda render from accepted case data plus a versioned amendment |
| EXT-08 | Later | 1.1.0 | Activate deterministic report generation from accepted Core-owned data through the approved renderer contract |

### Blocking prerequisites

[Requirements](../requirements.md) fixes the order: accepted `CASE-31`,
`ENG-01` and `ENG-02` data and workflow **precede** `EXT-08` and
`RPT-01`–`RPT-05`. None of the three exists.

| ID | Band | Target | Outcome |
| --- | --- | --- | --- |
| CASE-31 | Later | 1.0.0 | One accepted structured case/engineering record is the source for every deterministic report, fee note, addendum, query document, invoice input, and statistic |
| ENG-01 | Later | 1.0.0 | One canonical repair specification with route provenance |
| ENG-02 | Later | 1.0.0 | Engineer-owned final value/deductions, outcome, salvage category/value, roadworthiness drive derived figures without retyping |

### MCP

| ID | Band | Target | State |
| --- | --- | --- | --- |
| MCP-01 | Now | 0.1.0-alpha.1 | Implemented, composition-gated off by default |
| MCP-02 | Now | 0.1.0-alpha.1 | Implemented (case search, get, edit-lease begin/end) |
| MCP-03 | Now | 0.1.0-alpha.1 | Implemented (queue list, durable intake submission) |
| MCP-04 | Now | 0.1.0-alpha.1 | Implemented (lease-guarded add, download, export) |
| MCP-05 | Next | 0.3.0 | Allocation only |

### Downstream consumers of accepted report events

| ID | Band | Target | Relationship |
| --- | --- | --- | --- |
| MAIL-17 | Later | 1.2.0 | Idempotent report/fee-note send; consumes accepted rendering |
| EXT-11 | Later | 1.2.0 | Versioned fee/invoice and Engineer cost inputs |
| MI-02 | Later | 1.2.0 | Per-principal report counts feeding invoice generation |
| MI-03 | Later | 1.2.0 | Turnaround measures |
| CASE-23 | Next | 0.4.0 | Post-report query and dispute work |
| MAIL-12 | Later | 0.5.0 | Staff compose/reply/forward/send |
| UI-15 | Later | 1.0.0 | Engineer workbench arrangement, includes a report section |

### Already accepted, and distinct from rendering

These exist today and must not be conflated with report generation. A Box
report PDF, a generated artifact, or a file upload proves neither sending nor
external receipt.

MAIL-14, MAIL-15, MAIL-16 (report-sent evidence), DOC-02, DOC-03, DOC-07
(custody, versions, export), CASE-21 (`First sent to Engineer` proxy),
CASE-24, CASE-30, EXT-03 (EVA handoff), CASE-01, UI-04.

### Renderer templates with no capability ID

The imported renderer ships twelve template identifiers. Five of them map to
no Pegasus capability at all:

| Template ID | Mapped capability |
| --- | --- |
| `total-loss-report` | RPT-02 |
| `repairable-contract-repair-report` | RPT-02 |
| `fee-note` | RPT-02 |
| `expert-report` | RPT-02 (generic base for eight presets) |
| `addendum-report` | RPT-05 |
| `diminution-rebuttal` | RPT-04 |
| `market-valuation-evidence` | none |
| `advert-evidence-pack` | none |
| `blank-letterhead` | none |
| `roadworthy-criminal-report` | none |
| `part-35-response` | none |
| `response-letter` | none |

RPT-03 (Audit rendering, conservative and maximised specifications) has **no
corresponding renderer template**. This is a two-way gap in both directions
and is carried into the open questions.

## Evidence state at the start of this task

Recorded so no later reader mistakes any of it for progress.

| Claim | State |
| --- | --- |
| Renderer source is present in the repository | True — non-caller source import |
| A Pegasus caller invokes the renderer | False |
| `Pegasus.slnx` references the renderer | False, and architecture-tested to stay false |
| A Core render contract exists | False |
| Report wording is accepted | False — an open decision blocks four named items |
| CASE-31 / ENG-01 / ENG-02 exist | False |
| Any RPT-* or EXT-08 capability is activated | False |

The renderer's asset coupling is the one place where the boundary is already
crossed: the templates, document stylesheet, brand logo, engineer signatures
and desktop icon assets are tracked under `design/`, not in the workspace,
and the workspace project files reach four levels up to embed them. The
workspace therefore does not build standalone today.

## Plan set

| File | Subject |
| --- | --- |
| `report-renderer-integration-seam.md` | The architectural seam and target area; the draft ADR |
| `report-renderer-integration-runtime-uplift.md` | .NET 8 to the repository target framework |
| `report-renderer-integration-desktop-removal.md` | Removal of the WinUI 3 desktop application and its assets |
| `report-renderer-integration-mcp.md` | Renderer MCP tools onto the existing Pegasus ingress, retiring the `.mcpb` bundle |
| `report-renderer-integration-templates.md` | The `rendererref1` blueprints and report templates |
| `report-renderer-integration-docs-migration.md` | Workspace documentation and ADRs folded into canonical files |
| `report-renderer-integration-open-questions.md` | Consolidated operator questions and unresolved conflicts |

## Sequencing across the plan set

The supporting plans are not independent. This is the order their work must
land in, and the reason for each edge.

1. **Desktop removal** — removes the only Windows-only target framework and
   the Windows App SDK dependency, and is the precondition for a
   platform-neutral build.
2. **Runtime uplift** — cannot sensibly precede desktop removal, because the
   WinUI project would have to be uplifted only to be deleted.
3. **Seam and placement** — the architectural decision and its ADR. Nothing
   moves into `src/` before this is accepted.
4. **MCP consolidation** — depends on the seam, because the tool class in the
   composition root must reach the renderer through a Core-owned port.
5. **Documentation migration** — lands with, not after, each code move; the
   authority rule requires a losing document to be fixed in the same commit.
6. **Templates and blueprints** — the largest body of work, gated on the
   report-wording open decision and on CASE-31/ENG-01/ENG-02.

## Verification

Each supporting plan carries its own verification section mapped to the
[evidence tiers](../operations.md#required-evidence-tiers). For this master
plan the verification is documentary only:

- every capability ID named here resolves to a row in
  [capabilities](../capabilities.md) with the band and target stated;
- no statement in this plan set asserts a caller, deployment, activation or
  acceptance that does not exist;
- the plan set contains no relative link to a file that does not yet exist,
  as the [temp-plan contract](README.md) requires.

## Non-goals and stop conditions

Non-goals for this task: any change to `Pegasus.slnx`, any project reference
to workspace source, any deployment unit, any capability band or target, any
report wording, any provider selection, and any deletion of workspace source.

Stop conditions — work halts and returns to the operator if any of these is
reached:

- a plan would require inventing report wording blocked by the open decision;
- a plan would create a second business-policy owner alongside
  `Pegasus.Core`;
- a plan would require a new top-level project, store, runtime, migration
  stream or deployment unit without an accepted ADR proving the existing
  boundary cannot carry it;
- a plan would change the meaning of an operator statement in
  [operator notes](../operator-notes.md).

## Open questions

Consolidated in `report-renderer-integration-open-questions.md` once the
supporting plans have reported. The two that shape everything else:

1. Which lineage is the accepted report design — the operator-approved
   July 2026 `DESIGN_SPEC` template lock, or the imported C# renderer's
   twelve-template catalogue? They do not share a data contract.
2. Does the operator intend the integration to proceed now, ahead of
   CASE-31/ENG-01/ENG-02, as source relocation without activation; or to wait
   until the data prerequisites exist?
