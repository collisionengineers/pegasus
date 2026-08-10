# Report-renderer integration — the skill packages and the renderer as one product surface

Draft supporting plan for the `report-renderer-integration` task
(`task/report-renderer-integration`, taken 2026-08-03). It is a sibling of the
[master plan](report-renderer-integration.md), the
[seam plan](report-renderer-integration-seam.md) and the
[open questions](report-renderer-integration-open-questions.md), shares their
slug prefix, and is deleted with them by the post-merge maintenance push. It
changes no code, no capability status and no wording. It answers one question:
what is the real relationship between the imported AI skill packages under
`workspaces/ai-centre/skills/` and the twelve renderer template identifiers, now
that the renderer is being integrated into the monolith and the skills are not.

## Protected external source — read before anything else

The packages `ce-cost-defence`, `ce-house-style`, `collision-engineers-design`,
`diminution-rebuttal`, `diminution-report`, `manufacturer-methods-evidence`,
`roadworthy-report`, `salvage-categorisation`, `total-loss-assessment`,
`vehicle-assessment` and `vehicle-history-check` are **protected external
source** under the repository root `AGENTS.md`.

They may be **read**. They must **never** be modified, deleted, renamed,
regenerated or normalized without prompt-specific user authorization naming the
exact package and the exact operation.

Every finding below is a read-only observation. **No step in this plan edits,
moves, reformats, lints, re-links, corrects or "aligns" a file inside any of
those eleven directories** — including their `SKILL.md`, their `references/`,
their `scripts/`, their `assets/` and their `agents/` files. Where this plan
finds a contradiction between a package and a canonical Pegasus file, the
resolution is always to record the authority relationship **in the canonical
Pegasus file**, never to change the package.

Two consequences that are easy to get wrong and are therefore stated explicitly:

- The packages contain broken and unresolvable references — a `vehicle-valuation`
  package that was never imported, a `get_template_sample` tool the imported
  renderer does not expose, and a `dvsa-mot` connector with no Pegasus
  equivalent. **These are not defects to fix.** They are source provenance and
  they stay exactly as imported.
- The packages contain colour values, wording and layout claims that conflict
  with `docs/design.md`. **The conflict is resolved by documenting the losing
  source in a canonical file, not by correcting the package.**

`workspaces/ai-centre/skills/README.md` is the Pegasus-authored package index,
not one of the eleven protected packages; it already carries a Pegasus-authored
"Known import gap" note. This plan treats it as editable and flags the point in
open question S12 in case the operator reads it otherwise.

## Scope

This plan is documentary. It produces no project, no contract file, no test and
no capability movement. It exists so that the renderer integration lands with the
skills relationship understood rather than discovered later, and so that the
strong resemblance between the two halves does not quietly become a technical
dependency or a transfer of authority.

## 1. Is the pairing real?

**Yes — and it is more explicit than the observation supposed. The renderer is
not merely a plausible layout engine for skill output; six of the eleven packages
name the renderer by connector name and by exact `templateId` string as their
only permitted render path.** But the pairing is not uniform, and two of the
proposed pairings are refuted outright.

### The direct evidence

`workspaces/ai-centre/skills/diminution-rebuttal/SKILL.md:34` is the clearest
single statement:

> **Render formal reports and addenda with the `collisionrenderer` connector**
> (`templateId: diminution-rebuttal`). Build the camelCase payload per
> `references/structure.md`, fetch the current shape with `get_template_sample`,
> check it with `validate`, then `render`. […] If the connector is unavailable in
> the session, present the validated payload JSON and say the render needs the
> connector — **do not build the document any other way.**

`diminution-rebuttal/references/structure.md:66-84` then specifies the payload
field-for-field: `meta.ourRef` / `meta.yourRef` / `meta.date`, `title`,
`salutation`, `intro[]`, `sections[] = { heading, blocks }` with block types
`paragraph`, `bullets`, `datatable`, `keyvalue`, `evidencetable`, `valuebox`,
`mediarow`, and `signature`.

That block-type list is a verbatim match for the renderer's own document model.
`workspaces/report-renderer/src/CollisionRenderer.Core/Models/Documents.cs:216`
reads:

```csharp
/// <summary>paragraph | bullets | datatable | keyvalue | evidencetable | valuebox | mediarow.</summary>
public string Type { get; init; } = "paragraph";
```

and the surrounding records are `DocumentMeta`, `SignatureBlock` and
`ExpertReportDocument`. The skill is not describing a document; it is describing
the renderer's `ExpertReportDocument` deserialisation target.

`vehicle-assessment/references/assessment-output-structure.md:72-113` does the
same job for the assessment family, with a pack-section-to-payload-field mapping
table, and closes with the same instruction: *"This is the only render path for
the branded document. If the connector is unavailable, present the validated
payload JSON and stop — never route the branded pack through another renderer,
HTML, or DOCX path."*

### What each package actually produces

| Package | Primary output | Render path it names | Renderer template ID it names | Shaped for a renderer template? |
| --- | --- | --- | --- | --- |
| `vehicle-assessment` | A validated `assessment_payload.json` estimate plus a chat pack; two PDFs | `collisionrenderer` for the branded PDF; a frozen local Python generator for the Audatex/EVA PDF | `expert-report`, `total-loss-report`, `repairable-contract-repair-report` | **Yes**, with a full field mapping |
| `diminution-rebuttal` | A validated `diminution_intake.json`, then a camelCase document payload | `collisionrenderer` only | `diminution-rebuttal` | **Yes**, exact name match, full field spec |
| `diminution-report` | Report prose in one of three output modes | `collisionrenderer` only | `expert-report` | **Yes** (`references/report-structure.md:77`) |
| `vehicle-history-check` | Structured government-record facts and a plain summary | `collisionrenderer`, only if a formal document is asked for | `expert-report` | **Partly** — normally chat output, not a document |
| `ce-house-style` | A pass/fail lint verdict and corrected prose; no document | none — it is a checker | none, but its `references/document-tone-notes.md` is sectioned by **eight renderer template names** | **Yes, at the wording layer** |
| `collision-engineers-design` | Design tokens, fonts, letterhead spec, HTML/JSX specimen kits | none — it is a spec | none, but `references/document-letterhead.md:122` names `collisionrenderer` as *"the production PDF path […] There is no other renderer."* | **Yes, at the layout layer** |
| `ce-cost-defence` | A branded `.docx` court report | **its own** Node `scripts/build_report.js` | none | **No** — a second, parallel document toolchain |
| `roadworthy-report` | An HS (Hackney Solutions) `.docx` | **its own** `scripts/render_roadworthy.py` over a third-party DOCX template | none | **No** |
| `total-loss-assessment` | An Audatex-format PDF for EVA import | **its own** frozen `scripts/audatex_gen_v4.py` | none — and it forbids the renderer | **No, explicitly** |
| `salvage-categorisation` | A category recommendation with evidence for and against; no document | none | none | **No** — decision support |
| `manufacturer-methods-evidence` | Paraphrased method pointers, or "no maintained pointer found" | none | none | **No** — lookup |

### Corrections to the proposed pairings

Two rows of the observation's table are **refuted by the source**:

- **`roadworthy-report` ↔ `roadworthy-criminal-report` is false.** The renderer
  describes `roadworthy-criminal-report` as *"Safety, compliance or
  criminal-matter report with defect findings"* (`TemplateCatalog.cs:117-124`).
  The skill produces an *HS taxi and private-hire licensing* report from a
  third-party DOCX template, and `collision-engineers-design/SKILL.md:35`
  explicitly instructs *"third-party HS template; do **not** apply CE styling"*.
  They share a word, not a document. No skill in the imported set produces
  `roadworthy-criminal-report`.
- **`total-loss-assessment` ↔ `total-loss-report` is false.**
  `total-loss-assessment/SKILL.md:76` states it directly: *"Do not route this
  output through the `collisionrenderer` connector: its `total-loss-report`
  template is the CE-branded expert report, a different document."* The skill's
  output is the Audatex-format EVA-import PDF. In Pegasus terms its nearest
  relative is `EXT-03` (the deterministic EVA handoff), not `RPT-02`.

Three rows are **weaker than stated**: `salvage-categorisation`,
`manufacturer-methods-evidence` and `ce-cost-defence` produce no renderer payload
at all. The first two produce reasoning that would become *input to*
`ENG-01`/`ENG-02` data if they were ever activated; the third produces a finished
document through an entirely separate generator.

Six rows are **stronger than stated** — `diminution-rebuttal`,
`diminution-report`, `vehicle-assessment`, `vehicle-history-check`,
`ce-house-style` and `collision-engineers-design` are not merely adjacent to the
renderer; they were authored against it.

### The strongest structural evidence: the missing half

`workspaces/ai-centre/skills/README.md:40-44` records that
`vehicle-history-check` names a `vehicle-valuation` package as a consumer, but no
such package exists in the imported set.

The renderer contains that package's other half. `CollisionRenderer.Mcp/` ships a
whole `Valuation/` subsystem — `ValuationOutputsRenderer.cs`,
`ValuationPayloadMapper.cs` — and a dedicated `render_valuation_outputs` MCP tool
that maps a snake_case valuation payload onto the `market-valuation-evidence` and
`advert-evidence-pack` templates. Those two templates are precisely the two the
master plan lists as mapping to no Pegasus capability.

A renderer carrying a bespoke payload mapper for a skill that was not imported,
and a skill index recording that skill as an import gap, is not a coincidence of
naming. **They were one system, split by the import boundary.**

### One drift signal worth recording

Six packages instruct the agent to *"fetch the current shape with
`get_template_sample`"*. The imported renderer's MCP tool inventory
(`CollisionRenderer.Mcp/Tools/`) is `render_health`, `list_templates`,
`validate`, `render`, `install_browser`, `render_valuation_outputs` and
`open_valuation_output`. There is no `get_template_sample`; the nearest
equivalent is the CLI's `forms starter`, backed by
`AuthoringCatalog.GetStarterJson`. The two halves were imported from the same
upstream commit but were not in lockstep. This matters only as a caution: the
skill text is not a specification of the renderer, and must never be treated as
one.

## 2. Where the seam sits

### The decisive fact about the renderer

`design/assets/report-renderer/templates/expert_report.scriban` is 55 lines and
contains exactly one piece of literal English — the words `Image slot`. A grep
for "statement of truth", "Category N" and "roadworthy and fit" across every
`.scriban` body and across `CollisionRenderer.Core` returns nothing.

**The renderer owns page furniture and nothing else. Every word that appears in
an issued report arrives in the payload.**

That single fact locates the seam. The question "who owns report wording" is not
a question about the renderer at all — it is a question about *who composes the
payload*. In the skills world the answer is: the document skill drafts it and
`ce-house-style` polices it. In Pegasus that answer is unavailable, because
`docs/requirements.md:1033` forbids *"no model, skill, prompt, or external source
issuing an accepted case, engineering, economic, legal, or report outcome"*, and
root `AGENTS.md` forbids a skill from becoming an application policy owner.

It cannot be the skill. It cannot be the renderer. **Therefore Core.**

### What the Core contract looks like

The [seam plan](report-renderer-integration-seam.md) has already drafted the
render half. `src/Pegasus.Core/Reports/ReportContracts.cs` defines `ReportKind`,
`ReportPayload`, `ReportComputedFigures`, `RenderReportRequest`,
`RenderedReportArtifact`, `IReportRenderer` and `ReportWordingAcceptance`, with
the doc comment on `IReportRenderer` already reading *"The renderer decides no
business outcome, writes no case state, computes no figure."*

Three properties of that draft do the load-bearing work here, and they should be
recognised as the skills-surface contract rather than re-invented:

1. **`ReportPayload` is opaque JSON plus a schema version and a hash.** Nothing
   in the port lets a caller hand the renderer prose directly; the payload is a
   Core artefact with an identity that is recorded on the issued
   `RenderedReportArtifact` as `PayloadSchemaVersion` and `PayloadSha256`.
2. **`ReportComputedFigures` carries already-computed literal strings.** Core
   computes each figure once (`RPT-01`) and the renderer performs no arithmetic.
   A figure produced by a model would have to be inserted into a `ReportFigure`
   by a Core caller, which makes the insertion an auditable Core act rather than
   a rendering detail.
3. **`ReportWordingAcceptance.Unaccepted` fails every render closed** until a
   wording set is accepted by key and version. This is the same gate the
   `docs/open-decisions.md` "Report wording" row demands.

What the skills surface adds is a **fourth** property the seam plan does not yet
need but Stage 3 will: a type-level firewall between a *proposal* and a
*payload*.

The recommendation, for a later stage and explicitly **not** for this task:

- A proposal is its own Core record — carrying proposal identity, proposal
  version, the immutable case and evidence revision it was grounded on, the
  producing worker's identity, and the model/provider identity — and it has **no
  conversion to `ReportPayload`**.
- The only route from a proposal to a payload is a Core composition function
  that requires an acceptance record naming the authorised human (`ActionActor`),
  the acceptance time, the exact proposal version accepted, and any amendment the
  human made. An amendment is a new proposal version, not an edit.
- `RenderReportRequest` remains constructible only from accepted case data plus
  an accepted narrative. There is deliberately no overload, no optional parameter
  and no "draft mode" that skips the acceptance record.

This is the same shape `docs/requirements.md` already fixes for AI work:
*"Durable AI proposal work has stable request, lease, evidence,
proposal-version, and human-disposition identities […] no AI caller mutates,
approves, or sends autonomously."* The contribution here is only that the
firewall should be expressed in the **type system**, so that the rule survives a
future contributor who has not read the requirement.

### Relationship to `IReportRenderer`

`IReportRenderer` is unchanged by any of this and must stay unchanged. It is the
*last* step. The proposal contract sits several layers above it and never touches
it. Concretely:

```text
AI-09 work request → worker lease → proposal (Core record, versioned)
  → named-Engineer review, amend, accept or reject (Core, recorded in history)
  → Core payload composition (wording gate + figure policy)
  → IReportRenderer.RenderAsync
  → RenderedReportArtifact → document custody → MAIL-17 send
```

Every arrow but the last two is blocked today. The renderer integration's job is
to make the last two real and to make sure the earlier arrows cannot be
short-circuited.

`IReportPreviewComposer`, the retained HTML preview, deserves a specific warning
here. Its doc comment already says its output *"is never evidence of anything"*,
and risk R13 in the seam plan flags it as an injection surface. It is also the
single most tempting place for a proposal to leak into a visible document without
passing the gate. The architecture test that keeps the composer browser-free
should be joined, at the stage a proposal contract exists, by one asserting the
composer cannot accept a proposal type.

## 3. AI-08 specifically

`docs/capabilities.md:268`:

> **AI-08** | Intended Microsoft Foundry candidate proposes a case-grounded query
> response in approved house style/letterhead; a named Engineer reviews, amends
> if needed, and approves it before sending | Later | 1.3.0 | [Targeted sending
> and reviewed AI proposals] | Allocation only; Foundry remains subject to
> evaluation, and the proposal cannot mutate accepted case truth or send
> autonomously.

AI-08 is the capability that joins the two halves, because it is the only row in
the register that names *both* an AI proposal and a letterhead document.

### What activating it requires, end to end

| # | Prerequisite | Owner | State today |
| --- | --- | --- | --- |
| 1 | `CASE-31` accepted structured case/engineering record | Core | Not built (`Later / 1.0.0`) |
| 2 | `ENG-01` canonical repair specification | Core | Not built (`Later / 1.0.0`) |
| 3 | `ENG-02` Engineer-owned value, outcome, salvage, roadworthiness | Core | Not built (`Later / 1.0.0`) |
| 4 | The "Report wording" open decision closed — salvage Categories N/A/B/N-A, recovery and storage, the final statement of truth, named qualifications | Operator | Open |
| 5 | A Core-owned house-style/letterhead acceptance, by key and version, that is not a skill package | Core | Does not exist |
| 6 | Renderer Stage 1: Core render contract landed | Seam plan | Planned, this task |
| 7 | Renderer Stage 2: a real caller, persisted report issues, `EXT-08` / `RPT-01` in part | Seam plan | Blocked on 1–3 |
| 8 | Renderer Stage 3: deployed browser and font provisioning, determinism, recovery, operator acceptance | Seam plan | Blocked on risks R1–R3 |
| 9 | `AI-09` Core work-request, lease, proposal-version and human-disposition contract | Core | Allocation only (`Later / 1.3.0`) |
| 10 | The "Send-to-AI transport experiment" resolved for a Foundry model and transport | Operator | Open |
| 11 | `CASE-23` post-report query and dispute lifecycle — AI-08 responds to a *query*, so the query must have a state | Core | Open decision (`Next / 0.4.0`) |
| 12 | `UI-15` Engineer workbench re-entering the full design approval route | `docs/design.md` | Routeless review markup only |
| 13 | `MAIL-17` idempotent report/fee-note send | Core + adapter | Allocation only (`Later / 1.2.0`) |
| 14 | Evidence tiers 5, 7, 9 and 12 plus operator acceptance | Engineering classification; operator acceptance remains operator-owned | None |

Fourteen prerequisites, of which two are operator decisions that no plan can
take, three are unbuilt Core data capabilities, and one is an entire deployment
question the seam plan has three unresolved routes for.

### What the renderer integration must have in place first

Narrowed to what this task's plan set actually controls, AI-08 needs the renderer
integration to deliver exactly four things, all of them Stage 1 or Stage 2 work
already in the seam plan:

1. **A Core-owned `ReportKind` set that is Core's, not the skills'.** AI-08's
   "letterhead" output corresponds to the renderer's `blank-letterhead` template
   (`TemplateCatalog.cs:67`, and the special case at `Validators.cs:91`) or to
   `response-letter`. Both currently map to **no Pegasus capability ID**. Either
   AI-08 needs an RPT row of its own, or the two templates need capability
   coverage. This is open question S6.
2. **`ReportWordingAcceptance` defaulting closed and provably unbypassable.** The
   seam plan's verification already tests this at tier 2. It is the single guard
   that stops an AI-drafted phrase from becoming an issued report while the
   wording decision is open.
3. **`ReportComputedFigures` as the only route for a number.** AI-08 proposes a
   *query response*; query responses cite figures. The figure must be a
   Core-computed literal, not something the proposal carries.
4. **Deterministic issued-artifact identity.** `RenderedReportArtifact` records
   `PayloadSha256`, `TemplateSha256`, `RendererKey` and `RendererVersion`. When a
   proposal-derived report is later challenged, the ability to reproduce the exact
   bytes from the accepted payload is what separates "an Engineer approved this"
   from "a model wrote something once".

Everything else AI-08 needs is outside this task.

## 4. What must not happen

Ten failure modes, each with its mechanism and its guard. The mechanism matters
more than the label — every one of these is reachable by an ordinary,
well-intentioned implementation step.

**F1 — A skill's payload reaches an issued report without human acceptance.**
*Mechanism:* the packages instruct an agent to build a camelCase payload and call
`validate` then `render`. If a Pegasus MCP tool ever exposes a `render` that
accepts a caller-supplied payload, that exact sequence works end to end with no
human in it. *Guard:* no Pegasus render tool accepts a caller-supplied payload;
`RenderReportRequest` is constructible only from accepted case data, and an
issued artifact requires an issue identity that only Core allocates. ADR-0011
already restricts MCP to the Automation Actor's approved inventory of *ordinary
operational Core use cases*; issuing a report is not one.

**F2 — A skill becomes the owner of report wording the open decision has left
open.** *Mechanism:* `ce-house-style/references/document-tone-notes.md` supplies
fixed text for exactly the slots `docs/open-decisions.md` leaves unresolved — a
fixed legal-status line for roadworthy reports, a required visual-examination
caveat line, and named-engineer qualification and AQP-number formats. Adopting
that text, even as a "starting point", closes an operator decision by import.
*Guard:* the wording decision is closed by the operator in
`docs/open-decisions.md` and recorded as an accepted key and version in
`ReportWordingAcceptance`. A protected package is evidence the operator may
consult; it is never the acceptance.

**F3 — A prompt or model determines a figure.** *Mechanism:* the skills compute
extensively — `audatex_gen_v4.build_pdf` returns `totals.grand_inc_vat`, the
assessment skill computes PAV ratios and threshold comparisons, the rebuttal
skill reasons about multiplier bands. If any of that arithmetic is carried across
as "the calculation the renderer needs", Core stops being the single computing
owner and `RPT-01` ("computes each figure once") becomes false. *Guard:*
`ReportComputedFigures` carries pre-computed literal strings with a `PolicyKey`
and `PolicyVersion`; the renderer performs no arithmetic, no rounding and no
currency conversion; a model-originated number can only enter as a proposal that
a human accepts, and its acceptance is recorded.

**F4 — The house-style package becomes the design authority in place of
`docs/design.md`.** *Mechanism:* `collision-engineers-design/SKILL.md:43-46`
asserts *"These are the single source of truth — callers must not re-define their
own font or colour stack."* That sentence is true inside the package's own world
and false inside Pegasus. *Guard:* section 5 below.

**F5 — Skill `templateId` strings become the Pegasus report taxonomy.**
*Mechanism:* the path of least resistance is to keep `diminution-rebuttal` as a
string because six packages already say it. That makes an external package the de
facto namer of a Core policy enum. *Guard:* the seam plan's `ReportKind` enum is
already a closed Core-owned set whose doc comment reads *"The set is policy: it
names what may be produced […] Adding a member is a requirements change, not a
template change."* `ReportArtifactSchema.TemplateKey(kind)` is the only mapping
from Core policy to a template asset, and it may diverge from the skill strings
freely.

**F6 — `ce-cost-defence` becomes a second render path.** *Mechanism:* it ships a
complete, tested, deterministic branded-document generator — Node, `docx`, its own
logo asset, its own fixed footer, its own signatory default and its own brand red
`#C8102E`. It produces a document structurally almost identical to the renderer's
`expert-report` (ref block, FAO The Court, red-ruled headings, summary table,
sections, conclusion, CPR 35.6 line, statement of truth, signature). It is the
most plausible candidate for accidental adoption because it *works*. *Guard:*
`workspaces/ai-centre/README.md` already states *"Deterministic assembly belongs
to `workspaces/report-renderer` […] Do not create a duplicate renderer or a
model-calling report path here."* That statement must survive the renderer's
relocation into `src/` — see step S0 below.

**F7 — Skill-referenced tool names drive the Pegasus MCP inventory.**
*Mechanism:* the packages name `get_template_sample`, a tool the imported
renderer does not have. Adding it to satisfy the skill text would expand the
approved MCP inventory to serve an unactivated external consumer. *Guard:*
ADR-0011 fixes the inventory as approved ordinary operational Core use cases; the
[MCP plan](report-renderer-integration-mcp.md) owns the actual tool set.

**F8 — A skill's fallback instruction is read as a Pegasus outcome.**
*Mechanism:* six packages say "if the connector is unavailable, present the
validated payload JSON and stop". That is correct chat-surface behaviour and
wrong Pegasus behaviour. *Guard:* the Core taxonomy already distinguishes
`RendererUnavailable` from `PayloadRejected`, `AcceptedDataIncomplete` and
`TechnicalFailure`; an unavailable renderer is never presented as a validation
failure and never as a success.

**F9 — `roadworthy-report` is conflated with `roadworthy-criminal-report`.**
*Mechanism:* the names. *Guard:* section 1 records the refutation; the two
documents have different subjects, different templates, different renderers and
different styling instructions.

**F10 — `total-loss-assessment` is conflated with `total-loss-report`, or the
Audatex/EVA output is treated as a Pegasus report.** *Mechanism:* the names,
again, plus the fact that both are PDFs. *Guard:* `EXT-03` already owns the EVA
handoff and defines it as *"the exact ordered 13-key JSON, every eligible
custody-confirmed Case-vehicle image, and a SHA-256 manifest"* — not a PDF.
Nothing in the renderer integration touches `EXT-03`.

## 5. The house-style collision

### The sources, and what each actually says

There are not three sources for the house style. There are **four**, and they
disagree on the most basic fact — the red.

| Source | Nature | Document/print red | Status claim it makes |
| --- | --- | --- | --- |
| `docs/design.md` | Canonical Pegasus design authority | Not listed; *"Excluded marketing tokens include […] document red and brand-font declarations"* | *"This file is the durable authority for Pegasus visual design […] approved assets, component and pattern boundaries, and source-to-runtime mappings"* |
| `design/assets/report-renderer/templates/report.css` | Tracked implementation, embedded by the renderer | `#c80a32` | Header comment: *"COLLISION ENGINEERS — canonical print stylesheet (A4)"* |
| `collision-engineers-design` (protected) | Imported design-system package | `#C80A32` (`colors_and_type.css:91`, `references/document-letterhead.md:13`) | *"Canonical tokens to hand back […] These are the single source of truth — callers must not re-define their own font or colour stack"* |
| `ce-cost-defence` (protected) | Imported document generator | `#C8102E` (`references/brand.md:17`) | *"Fixed details — never vary between reports"*; *"Never edit the generator to change styling"* |

`ce-house-style` is a fifth participant but does not claim the visual system — it
claims the *voice*, and it defers layout to `collision-engineers-design`. Its
overlap is with the wording decision (F2), not with the design authority.

Three observations sharpen the picture:

- `collision-engineers-design` and `report.css` **agree** on `#C80A32`. That is
  not a conflict; it is one design source and its implementation, correctly
  aligned.
- `ce-cost-defence` is the genuine outlier at `#C8102E`, and it is outside the
  renderer entirely. The two protected packages disagree with each other.
- `docs/design.md` is authoritative but **silent** on the document register. It
  excludes document red from the *application* token table — correctly, since the
  internal command centre must not use it — and its "Web and renderer boundary"
  table delegates report templates and the document stylesheet to the renderer.
  The delegation is real; the value is unrecorded.

### Which is authoritative

**`docs/design.md` is the root design authority.** This is already settled and
does not need re-deciding here; it is asserted by the file itself and already
accepted by `workspaces/ai-centre/README.md`, whose authority table reads *"UI and
application state | [Root design] is the durable visual authority […] Do not
duplicate tokens, components, accessibility rules, layouts, UI policy, or
application state here"* and *"Documents | […] durable visual and letterhead
authority belongs to root `design/`."*

What follows for the other three:

- **`report.css` is the implementation of the document register under that
  authority, not a second authority.** Its "canonical print stylesheet" header
  comment is an inherited upstream claim. It stays byte-unchanged in the move
  (operator decision 2 makes the C# renderer the authoritative design), but
  `docs/design.md` should record the document register it implements so the
  file is governed rather than merely tolerated.
- **`collision-engineers-design` is source evidence for the document register and
  is not authority.** It is protected; it stays exactly as imported; its "single
  source of truth" sentence is scoped to its own package world and is recorded as
  such in a canonical file.
- **`ce-cost-defence`'s `#C8102E` and its DOCX chrome are source evidence for a
  document Pegasus does not produce.** Protected, unchanged, non-authoritative,
  and specifically named as not-a-render-path so F6 cannot happen by accident.

### How the authority relationship is documented

Three canonical-file edits, none of which touches a protected package. All are
documentation and all belong with the seam plan's Stage 1, which already rewrites
`docs/design.md`'s renderer boundary table when the renderer moves into `src/`.

1. **`docs/design.md` — add a "Document and print register" subsection under
   "Tokens".** It records `#C80A32` as an approved **document-only** token, names
   `design/assets/report-renderer/templates/report.css` as its sole runtime
   consumer, states that it is deliberately absent from `:root` and from every
   application surface, and reaffirms that the application red remains `#DB0816`.
   The file already carries exactly this pattern for the `Send to Claude` control
   ("Reviewed divergence"), so the shape is established and needs no new
   convention.
2. **`docs/design.md` — extend the "Web and renderer boundary" table.** Its
   final row today reads *"Imported renderer, prompt, model, skill and AI material
   | Source evidence only unless a separate accepted contract provides a real
   Pegasus caller."* Split it so `collision-engineers-design` and `ce-house-style`
   are named individually as **source evidence, not design or wording
   authority**, and so `ce-cost-defence`'s generator is named as **not a Pegasus
   render path**. Naming beats a general clause, because a general clause does not
   survive a search for "collision-engineers-design".
3. **`workspaces/ai-centre/skills/README.md` — add an authority sentence below the
   package-status table.** The table's "Authority boundary" column already says
   *"Never Pegasus product policy, case mutation, approval, legal/engineering
   authority, caller, or deployment"* for every row. Add one line recording that
   `docs/design.md` is the durable visual and letterhead authority and
   `Pegasus.Core` the wording authority, so a reader who arrives at the packages
   first is routed correctly. The README is Pegasus-authored and already carries
   Pegasus-authored notes; see open question S12.

There is also a **removal risk** to catch. `workspaces/ai-centre/README.md`
currently anchors the documents rule to `workspaces/report-renderer`. When that
workspace is deleted, that sentence becomes a dangling reference. It must be
updated in the same commit as the deletion — the authority rule in root
`AGENTS.md` requires the losing document to be fixed in the same change — and it
must be updated to point at the Core contract, not simply deleted.

## 6. Coverage assessment

### Skill packages

| Package | Renderer template? | Capability ID? | Notes |
| --- | --- | --- | --- |
| `vehicle-assessment` | **Yes** — `expert-report`, `total-loss-report`, `repairable-contract-repair-report` | Partly — `ENG-01`, `ENG-02`, `RPT-02` (data and rendering, not the skill) | The broadest package; also drives the Audatex generator |
| `diminution-rebuttal` | **Yes** — `diminution-rebuttal`, addenda | `RPT-04`, `RPT-05` | Exact template-name match |
| `diminution-report` | **Yes** — `expert-report` | `RPT-04` | Claimant-side opinion |
| `vehicle-history-check` | **Yes**, conditionally — `expert-report` | `EXT-01`, `EXT-02`, plus the mandatory vehicle-history/risk global check | Normally chat output; the document is optional |
| `ce-house-style` | **No**, but keyed to eight template names | `AI-08` names "approved house style/letterhead" | A lint and a wording source; the wording overlap is F2 |
| `collision-engineers-design` | **No**, but names the renderer as the only production path | **None** — `docs/design.md` owns this | The design half |
| `ce-cost-defence` | **No** — its own Node/`docx` generator | `EXT-09` covers the *data* (original-versus-assessed comparison, savings), not the document | A second, complete document toolchain |
| `salvage-categorisation` | **No** — decision support, no document | `ENG-02` (salvage category/value) | Would inform accepted data, never issue it |
| `manufacturer-methods-evidence` | **No** — pointers only | `ENG-01` (repair specification route provenance) | Explicitly refuses to reproduce OEM procedure text |
| `roadworthy-report` | **No** — third-party HS DOCX, its own Python renderer | **None** | `roadworthy-criminal-report` is a different document |
| `total-loss-assessment` | **No** — explicitly forbidden; frozen Audatex generator | Nearest is `EXT-03` (EVA handoff), and that is a JSON bundle, not a PDF | Deliberately unbranded |

Summary: **four** packages have a renderer template. **Seven** have a capability
ID they touch, none of which they own. **Two** — `collision-engineers-design` and
`roadworthy-report` — have neither a Pegasus capability nor a renderer template,
for opposite reasons: one is above the capability layer, the other is outside the
product.

### Renderer templates

| Template ID | Skill that produces it | Capability ID |
| --- | --- | --- |
| `expert-report` | `diminution-report`, `vehicle-assessment`, `vehicle-history-check` | `RPT-02` (generic base) |
| `total-loss-report` | `vehicle-assessment` | `RPT-02` |
| `repairable-contract-repair-report` | `vehicle-assessment` | `RPT-02` |
| `diminution-rebuttal` | `diminution-rebuttal` | `RPT-04` |
| `addendum-report` | `diminution-rebuttal` (addenda) | `RPT-05` |
| `fee-note` | **None** — `ce-house-style` supplies its tone only | `RPT-02`, `EXT-11` |
| `market-valuation-evidence` | **None imported** — the `vehicle-valuation` gap | **None for rendering**; `EXT-10` is the data capability |
| `advert-evidence-pack` | **None imported** — same gap | **None for rendering**; `EXT-10` |
| `blank-letterhead` | **None** — but it is the surface `AI-08` implies | **None** |
| `part-35-response` | **None** — `diminution-rebuttal` handles Part 35 through its own template; `ce-cost-defence` is court-facing but uses its own generator | **None** |
| `response-letter` | **None** | **None** |
| `roadworthy-criminal-report` | **None** — `roadworthy-report` is a different document | **None** |

**Templates with a skill but no capability: none.** Every template a skill
produces has at least one RPT row. That is a genuinely reassuring result — the
skills did not invent document types Pegasus has not allocated.

**Templates with neither a skill nor a capability: five** — `blank-letterhead`,
`part-35-response`, `response-letter`, `roadworthy-criminal-report`, and (for
rendering purposes) `market-valuation-evidence` plus `advert-evidence-pack`,
whose only capability `EXT-10` governs the valuation evidence data rather than
its rendering.

`RPT-03` (Audit rendering, conservative and maximised specifications) has **no
template and no skill**. The master plan already records the template half of this
gap; the skills half confirms it. Nothing in the imported set produces the Audit
uplift document.

## 7. Staged route

Stages are numbered to interleave with the seam plan's Stage 1/2/3 rather than to
replace them.

### S0 — Documentation only (this task)

Record the pairing, record the authority, and make the boundaries findable by
name.

- The three canonical edits in section 5.
- Update `workspaces/ai-centre/README.md`'s "Documents" authority row so it
  survives the deletion of `workspaces/report-renderer/`, pointing at the Core
  render contract instead.
- Confirm the `workspaces/README.md` register row for `ai-centre/skills/` is
  unchanged and still correct: *"Application-facing agent skills — not
  repository-development workflow"*, requiring *"a separate application-skill
  integration contract, agent caller, evaluation, deployment, and human-approval
  evidence."* It is, and nothing here weakens it.

**Advances:** no capability. **Does not advance:** `EXT-08`, any `RPT-*`, any
`AI-*`, any `ENG-*`, `CASE-31`.

### S1 — Core render contract (seam plan Stage 1)

The seam plan's work, unchanged. The skills-surface addition is three
architecture assertions, all tier 1:

- no `src/Pegasus.*` project references, embeds or reads any path under
  `workspaces/ai-centre/skills/`;
- `ReportKind` is a closed Core enum whose members are Core names, and
  `ReportArtifactSchema.TemplateKey` is the only mapping to a template asset;
- no second document generator is introduced anywhere in `src/`.

**Advances:** no capability. **Does not advance:** everything.

### S2 — Report wording and the accepted-data prerequisites

Operator closes the `docs/open-decisions.md` "Report wording" row; `CASE-31`,
`ENG-01` and `ENG-02` are built and accepted. `ReportWordingAcceptance` becomes
populatable by key and version. Core payload composition and the figure policy
land.

**Advances:** `CASE-31`, `ENG-01`, `ENG-02` on their own terms. **Does not
advance:** any `AI-*`.

### S3 — The deterministic report caller (seam plan Stages 2 and 3)

A Web caller, persisted report issues, the browser and font provisioning
decision, determinism proof, recovery, operator acceptance.

**Advances:** `EXT-08` and `RPT-01`, then `RPT-02`, `RPT-04`, `RPT-05` as their
wording and data allow. **Does not advance:** `RPT-03` (no template, no data),
any `AI-*`, `MAIL-17`.

### S4 — The AI proposal contract, no transport

`AI-09`'s Core work-request, lease, evidence-binding, proposal-version and
human-disposition contract, with the type-level firewall from section 2. No
provider, no model, no transport, no `Send to AI` route.

**Advances:** `AI-09` in part. **Does not advance:** `AI-08`, `AI-07`,
`AI-01`–`AI-06`.

### S5 — AI-08

Only after S2, S3 and S4, and only after the operator resolves the "Send-to-AI
transport experiment" for a Foundry model and transport, and `CASE-23` has a
lifecycle, and `UI-15` has re-entered design approval, and `MAIL-17` exists.

**Advances:** `AI-08`. **Does not advance:** `AI-01`–`AI-07`, `AI-09` beyond what
S4 established, `MI-*`.

Nothing in S0 through S5 activates, invokes, deploys, packages or references a
skill package. If an application-facing skill caller is ever wanted, it enters
through the separate contract the `workspaces/README.md` register already
requires, and it is not this task's route.

## 8. Verification

Mapped to the required evidence tiers in `docs/engineering.md`.

| Check | Tier | Stage | What it proves |
| --- | --- | --- | --- |
| Every capability ID cited here resolves to a row in `docs/capabilities.md` with the stated band and target | Documentary | S0 | No invented capability |
| No relative link in this plan points at a file that does not exist | Documentary | S0 | The temp-plan contract |
| `git diff --stat` for the S0 commit touches zero paths under the eleven protected package directories | Documentary | S0 | The protected-source constraint held |
| `docs/design.md`, `workspaces/ai-centre/README.md` and `workspaces/ai-centre/skills/README.md` each name the authoritative source and the non-authoritative ones, by package name | Documentary | S0 | F4 and F6 are findable by search |
| No `src/Pegasus.*` project file, embedded resource, content item or source file references a path under `workspaces/ai-centre/skills/` | 1 | S1 | The skills are not a dependency |
| `ReportKind` is a closed enum; `ReportArtifactSchema.TemplateKey` is exhaustive; adding a member breaks the build | 1 | S1 | F5 — Core names its own report taxonomy |
| Exactly one `IReportRenderer` production implementation plus the fail-closed one; exactly one preview composer; no other document generator in `src/` | 1 | S1 | F6 — no second render path |
| `ReportWordingAcceptance.Unaccepted` yields `RendererUnavailable` with no artifact; a wrong key or wrong version yields the same | 2 | S1 | F2 — the wording gate cannot be bypassed |
| `ReportComputedFigures` is the only route by which a number reaches the renderer; the adapter performs no arithmetic, rounding or currency conversion | 2 + 3 | S1 | F3 — Core computes once |
| A proposal type has no conversion, cast, constructor or helper producing a `ReportPayload` or a `RenderReportRequest` | 1 + 2 | S4 | F1 — the type-level firewall |
| Composing a payload from a proposal without an acceptance record naming an `ActionActor` fails; an amendment produces a new proposal version, never an edit | 2 | S4 | The human disposition is structural, not procedural |
| The preview composer refuses a proposal type and remains browser-free | 1 + 2 | S4 | F1 via the preview surface |
| The authorised staff accept/amend/reject action reaches Core through a real route; the actor, caller, time, proposal version and disposition appear in permanent action history | 5 | S4 | Proposals remain proposals |
| Operator review of a proposal, its provenance and its rejection, without any surface implying the proposal was issued, delivered or accepted | 7 | S5 | The evidence-state discipline in `docs/design.md` |
| Prompt-injection and untrusted-content handling on any path where case evidence becomes model input; redaction and bounded failure metrics | 9 | S5 | The proposal path is a security boundary |
| Full path — case evidence, work request, lease, proposal, human acceptance, payload composition, render, custody, send — through real callers with safe replay | 12 | S5 | Registration or mock-only paths do not satisfy this tier |

Honestly unproved at the end of S0: everything except the documentary rows. S0
produces documentary evidence only, and no tier-1 artefact of its own.

## 9. Non-goals

- Modifying, deleting, renaming, regenerating, normalizing, linting, reformatting
  or re-linking **any** file inside the eleven protected packages.
- Fixing the packages' unresolvable references — the missing `vehicle-valuation`
  package, `get_template_sample`, the `dvsa-mot` connector.
- Reconciling `#C8102E`, `#C80A32` and `#DB0816` by changing a value anywhere.
  The authority is documented; the values stay as they are.
- Building, invoking, packaging, deploying or referencing any skill package from
  Pegasus.
- Adding a skill caller, an agent harness, a model API, a transport or a provider
  selection. ADR-0009 is explicit: *"Pegasus embeds no Claude-specific transport
  and activates no direct model API in this decision."*
- Adopting any wording from `ce-house-style` into a canonical file, a template, a
  test fixture or a plan.
- Adding a capability ID, changing a band or changing a target.
- Creating a proposal contract, a work-request table or an `AI-*` seam in this
  task. Section 2's recommendation is design input for S4, not S0 work.
- Copying `ce-cost-defence`'s generator, its schema or its chrome into `src/`.
- Treating the skills' `templateId` strings as the Pegasus report taxonomy.

## 10. Stop conditions

Work halts and returns to the operator if any of these is reached.

1. A step would edit, move, rename or regenerate a file inside a protected
   package — including a change that appears purely cosmetic, and including a
   change made incidentally by a formatter, a lint pass or a link-fixer.
2. A step would require inventing report wording, a qualification, a
   statement-of-truth line, a salvage-category phrase or a caveat line that the
   `docs/open-decisions.md` "Report wording" row leaves open.
3. A step would make a skill, prompt, model, package or workspace the owner of a
   Pegasus policy, wording, figure, design token or capability.
4. A step would create a second business-policy owner alongside `Pegasus.Core`,
   or a second document render path alongside the Core render contract.
5. A step would give an AI caller a route to an issued artifact, a case mutation,
   an approval or a send without a recorded human disposition.
6. A step would add a Pegasus MCP tool that exists to satisfy skill text rather
   than an approved ordinary operational Core use case.
7. A step would change the meaning of an operator statement in
   `docs/operator-notes.md`.

## 11. Open questions

Prefixed `S` to avoid colliding with the `B`/`H`/`M` series in
[open questions](report-renderer-integration-open-questions.md), into which these
should be consolidated.

**S1.** The skills and the renderer were imported from the same upstream commit
but are not in lockstep — the packages call a `get_template_sample` tool the
renderer does not expose. Is the imported renderer the current upstream renderer,
or is there a newer one the skills were written against? This changes whether the
twelve-template catalogue is the whole surface.

**S2.** `CollisionRenderer.Mcp/Valuation/` contains a complete payload mapper and
a `render_valuation_outputs` tool for a `vehicle-valuation` skill that was never
imported. Should that skill be imported as further source evidence, or should the
valuation subsystem be treated as orphaned and dropped during the move? The
[seam plan](report-renderer-integration-seam.md) currently plans a file-by-file
relocation of `CollisionRenderer.Core` and does not resolve the `.Mcp` valuation
code.

**S3.** `market-valuation-evidence` and `advert-evidence-pack` have no RPT
capability, but `EXT-10` ("Versioned vehicle-valuation evidence, explicit Engineer
acceptance/adjustments/rationale, and revaluation history", `Later / 1.0.0`)
governs the data they would render. Is `EXT-10` intended to carry their rendering
too, or do they need RPT rows?

**S4.** `RPT-03` has no renderer template and no skill package. Was the Audit
conservative/maximised uplift document ever produced by any predecessor tool, or
is it specified but never built?

**S5.** `roadworthy-criminal-report` has no producing skill and no capability, and
is a different document from the HS taxi report the `roadworthy-report` package
produces. Does Collision Engineers issue a roadworthy/criminal report today, and
if so through what? If not, should the template be retired rather than carried
into `src/`?

**S6.** `AI-08` names an "approved house style/letterhead" output. The nearest
templates — `blank-letterhead` and `response-letter` — have no capability ID. Does
`AI-08` need its own RPT row, or does one of those templates need capability
coverage, or does `AI-08`'s output route through `expert-report`?

**S7.** `ce-house-style/references/document-tone-notes.md` supplies fixed wording
for exactly the four items the "Report wording" open decision leaves open. Is that
text the operator's own accepted wording arriving through an import, or is it
drafted text the operator has never accepted? The answer decides whether closing
the wording decision is a review or an authoring exercise. **No plan should assume
either.**

**S8.** `ce-cost-defence` produces a court-addressed cost-defence document through
a separate Node toolchain, with a divergent brand red and a hard-coded default
signatory. Is that document a Collision Engineers product Pegasus is eventually
meant to issue? If yes it needs a capability, a template and a wording decision;
if no, it should be recorded as out of product scope so it is not mistaken for a
gap.

**S9.** `total-loss-assessment` and `vehicle-assessment` both carry a frozen
`audatex_gen_v4.py` and both produce an Audatex-format PDF for EVA import.
`EXT-03` defines the accepted EVA handoff as a JSON bundle plus images plus a
manifest, with no PDF. Is the Audatex PDF a thing Pegasus is ever meant to
produce, or is it purely a predecessor workflow that the accepted `EXT-03` handoff
replaces?

**S10.** The packages' render instructions are written for an agent session with a
connector. If an application-facing skill caller is ever built, does it call
Pegasus, or does Pegasus call it? The workspaces register requires "a separate
application-skill integration contract, agent caller, evaluation, deployment, and
human-approval evidence" but does not fix the direction, and the direction
determines whether ADR-0011's Automation Actor boundary or the `AI-09`
work-request contract applies.

**S11.** `docs/design.md` currently excludes document red from its token table
while the tracked `report.css` uses `#c80a32`. Adding a document-register
subsection records the divergence, but is the document register **approved**
design, or is it inherited implementation awaiting review like the `.scriban`
wording? Operator decision 2 made the C# renderer the authoritative design for the
*templates*; it did not obviously extend to the token values.

**S12.** Is `workspaces/ai-centre/skills/README.md` editable, or does the operator
consider the Pegasus-authored package index protected alongside the packages? It
already carries Pegasus-authored content, so this plan treats it as editable — but
the S0 step that adds an authority sentence to it should not proceed if the
operator reads the protection more broadly.
