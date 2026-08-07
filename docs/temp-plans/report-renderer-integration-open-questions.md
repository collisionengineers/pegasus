# Report renderer integration — consolidated open questions

Draft supporting document for the `report-renderer-integration` task. It
consolidates every unresolved question raised across the six planning documents,
deduplicated and ordered by what each one blocks. Nothing here is a decision.

Decisions already taken by the operator are recorded in the "Settled" section at
the end so they are not re-asked.

## How to read this

| Band | Meaning |
| --- | --- |
| **Blocking** | A plan cannot proceed past design without an answer. Proceeding under any assumption risks work that must be thrown away |
| **High** | Answerable later, but the answer changes the shape of the implementation |
| **Medium** | Answerable during implementation; a sensible default exists and is stated |

Each question names the plan that raised it and what it blocks.

## Blocking — all four answered 2026-08-03

The four blocking questions were put to the operator on 2026-08-03 and answered.
Their original text is retained below for the reasoning; each now carries its
decision and the consequences that follow. The consequences are the live work.

### B1. Which renderer lineage is the accepted design?

> **DECIDED 2026-08-03: the C# `CollisionRenderer` is authoritative.**
> `DESIGN_SPEC.md` is superseded evidence, not a requirement.
>
> Consequences, stated plainly because they are not all comfortable:
>
> - `docs/reference/rendererref1/` keeps exactly the role
>   `docs/reference/README.md:3-14` already assigns it — supplied evidence, not
>   a requirement, implementation proof or authorization. Nothing is imported
>   from it. The four sample PDFs remain immutable visual reference.
> - The templates plan's gap analysis stops being a work list and becomes a
>   **record of what the C# renderer does not do**. No
>   `AssessmentReportDocument`, no `assessment_report.scriban`, no four-preset
>   family, no composed-narrative engine is built from `DESIGN_SPEC`.
> - **The RPT capability outcomes do not change, and the C# renderer as it
>   stands does not satisfy them.** It performs no arithmetic at all beyond a
>   fee-note sum in `HtmlComposer`; two template IDs cover three or four
>   outcomes; there is no conservative/maximised specification pair for RPT-03;
>   there is no versioned amendment identity for RPT-05. So RPT-01's "computes
>   each figure once" and RPT-02's "four outcome variants" still need an
>   implementation — this decision removes `DESIGN_SPEC` as their
>   *specification*, not the work itself.
> - That specification must therefore come later, from accepted `CASE-31` /
>   `ENG-01` / `ENG-02` data plus operator decisions. It is not written by this
>   task and cannot be.
> - **Net effect on Stage 1: templates are untouched.** The four `.scriban`
>   bodies and `report.css` move as-is. This materially shrinks Stage 1 and
>   removes the whole templates work stream from the near-term critical path.
> - Questions H5, H6, M5, M8, M9 and M10 below were all raised by the templates
>   plan against `DESIGN_SPEC`. They are **deferred, not answered** — they
>   return when the RPT specification is written.

### B1 (original text)

*Raised by: templates. Blocks: every template decision, and the value of the
`rendererref1` reference set.*

`docs/reference/rendererref1/DESIGN_SPEC.md` is an operator-approved template
lock ("Approved by Andrew, 21/07/2026") describing a **Python** generator,
`ce_report_generator.py`, which is not in this repository. Its
`report_data_schema.json` is a domain model with computed figures and negative
constraints forbidding derived values from being supplied.

The imported C# `CollisionRenderer` is the accepted successor stack — the
workspace's own ADR-0003 rejected Python, and `report.css` records that the
stylesheet was "ported verbatim from the proven WeasyPrint renderer". But the C#
renderer inherited the **stylesheet** and not the **job schema**: its
`Models/Documents.cs` is a generic content-block model where the caller types
every paragraph.

These are not the same artefact, and the second cannot be reached by configuring
the first.

**Question.** Is `DESIGN_SPEC` the accepted design for Pegasus report output, to
be re-implemented on the C# family — or is it superseded evidence?

**If unanswered:** the templates plan cannot write a template body, and roughly
half its content is contingent.

### B2. What does "replacement for the `.mcpb` style of implementation" mean?

*Raised by: MCP. Blocks: the whole MCP consolidation.*

Three readings, implying different tasks:

- **(a)** Delete the stdio host and `.mcpb` now, and expose render on Pegasus
  `/mcp`. This is what the MCP plan assumes.
- **(b)** Build the Pegasus `/mcp` render tools first, keep the `.mcpb` working
  until parity is demonstrated, then delete.
- **(c)** Narrower — replace only the transport and packaging while leaving the
  seven-tool shape intact.

This matters operationally. `render_valuation_outputs` and
`open_valuation_output` exist to serve a valuation connector or skill that lives
**outside this repository** and consumes the renderer's `{artifacts, validation}`
envelope. Deleting the stdio host today breaks that workflow, and Pegasus cannot
replace it: Pegasus renders for a Case, and the valuation connector has no Case.

**Recommendation if you have no strong view:** (b). It is the only reading that
does not break an external workflow on the day the host is deleted.

> **DECIDED 2026-08-03: (b) — parity first, then delete.**
>
> Consequences:
>
> - The `.mcpb` stdio host stays working until the Pegasus `/mcp` render tools
>   demonstrate parity. The MCP plan's section 6 deletion inventory becomes a
>   **second-phase** action, not a first-phase one.
> - "Parity" needs a definition before it can gate anything. It cannot mean
>   seven-tool parity, because four of the seven are dropped on ADR-0011 and
>   security grounds (`install_browser`, `render_health`, `open_valuation_output`,
>   `render_valuation_outputs`). Proposed definition, for operator confirmation:
>   *the three retained tools — template list, validate, render — produce
>   byte-identical PDFs for the same payload through the Pegasus port as through
>   the stdio host, on the same machine and browser build.*
> - The external valuation connector keeps working throughout. Its eventual
>   fate is **not** resolved by this decision and stays open as M7a below:
>   Pegasus renders for a Case and the connector has no Case, so the two-artifact
>   valuation contract has no Pegasus home.
> - The workspace therefore **cannot be fully retired in Stage 1** while the
>   stdio host must keep running. Either the workspace survives in reduced form
>   until parity, or the host is republished from the relocated engine. This is
>   a direct conflict with the docs-migration plan's single-deletion commit and
>   is raised as B6 below.

### B3. Relocation now, or wait for the data prerequisites?

*Raised by: seam, templates, docs-migration. Blocks: the sequencing of everything.*

`docs/requirements.md:53-56` sequences accepted `CASE-31`, `ENG-01` and `ENG-02`
data and workflow **ahead of** `EXT-08` and `RPT-01`–`RPT-05`. None of the three
exists. `EXT-08` and all five `RPT-*` are `Later / 1.1.0`; the current release is
`0.1.0-alpha.1`, and `NOW.md`'s path is the QDOS cutover, which explicitly notes
"EVA keeps engineering and reports".

So the integration can relocate source and land a contract, but it cannot
activate a report capability.

**Question.** Do you want Stage 1 (relocate source, land the Core contract, no
caller, no capability advanced) executed now — or should the whole integration
wait until CASE-31/ENG-01/ENG-02 exist?

**Note:** Stage 1 has real standalone value — it retires a parallel build,
dependency surface and ADR store — but it also puts unaccepted report wording and
provenance-sensitive signature images into the production assembly (see B4, H2).

> **DECIDED 2026-08-03: Stage 1 now, no activation.**
>
> Consequences:
>
> - The seam plan's Stage 1 is authorised: relocate source, land both Core ports,
>   register the fail-closed renderer and the preview composer, edit the
>   architecture tests, extend the CI build-path pattern, file the ADR.
> - **No capability identifier advances.** The Stage A capability-note wording in
>   the docs-migration plan is the wording to use, and review must reject any PR
>   that claims more.
> - Combined with B1, Stage 1 is smaller than first planned: no template work at
>   all, and the four `.scriban` bodies move unchanged.
> - H2 (unaccepted wording and signature images in the binary) is **not**
>   resolved by this decision and still needs an answer before the asset move.

### B6. New — "parity first" conflicts with retiring the workspace in Stage 1

*Raised by: the B2 and B3 decisions together. Blocks: the deletion commit.*

B3 authorises Stage 1, whose stated outcome is that
`workspaces/report-renderer/` is deleted. B2 requires the `.mcpb` stdio host to
keep working until parity is demonstrated. The stdio host is built from
`CollisionRenderer.Mcp`, which references `CollisionRenderer.Core` — both inside
the workspace being deleted.

The two decisions cannot both be executed in one change set. Three ways out:

| Option | Effect |
| --- | --- |
| **(i) Reduced workspace survives to parity** | Stage 1 relocates the engine into `src/` and deletes only `.Cli`, `.Api`, `.Gui`. `CollisionRenderer.Core` + `.Mcp` remain in `workspaces/` until parity, then go. Cost: the engine exists in two places for the duration, which is exactly the duplicate-implementation stop condition |
| **(ii) Republish the host from the relocated engine** | The stdio host is rebuilt as a thin console project consuming the relocated Infrastructure adapter. Cost: a fifth project, needing the ADR proof that the existing boundary cannot carry it — and it would be a Windows-only, unauthenticated stdio host inside the product tree |
| **(iii) Freeze the existing `.mcpb` artefact** | Build the bundle once from the current commit, hand it to whoever runs the valuation connector, and delete the workspace. The connector keeps working on a frozen binary; no source survives. Cost: the frozen bundle receives no fixes |

**Recommendation: (iii).** It is the only option that neither duplicates the
engine nor adds a project. It also matches what the bundle already is — a
manually built, unversioned, Windows-only artefact that no workflow produces.

**Question.** Which option? This is now the gating question for the deletion
commit.

### B4. Are the Scriban security advisories re-accepted for a production assembly?

*Raised by: seam, docs-migration, uplift. Blocks: the code move.*

The workspace suppresses `NU1901`–`NU1904` in its own `Directory.Build.props`
under workspace ADR-0010, on three stated conditions: templates are first-party
embedded artefacts; end users never author or compile runtime templates; payload
text is HTML-encoded and passed as values.

The root `Directory.Build.props` sets `TreatWarningsAsErrors=true` with **no**
`NoWarn`. Moving the projects under `src/` makes those advisories build errors.

Three options, one rejected outright:

| Option | Verdict |
| --- | --- |
| Add the four codes to the root `NoWarn` | **Rejected by both plans.** It would silence package advisories for the eight existing adapter package families too, and hide the next real advisory |
| Check first whether a Scriban release without the advisories exists and upgrade | **Do this first** |
| Scope the suppression to the single Scriban `PackageReference`, with the rationale restated in the new ADR | **Recommended** if the upgrade is not available |

`NOTICE.md:87` already says the acceptance "must be revisited before release" if
"a new trust boundary is introduced". Moving the engine into a multi-tenant web
application **is** a new trust boundary description, so revisiting it is required
by the decision's own terms — this is not optional diligence.

> **DECIDED 2026-08-03: check for a clean release first. The check was run and a
> clean release exists.**
>
> Measured on 2026-08-03 with
> `dotnet list package --vulnerable --include-transitive`:
>
> | Version | Result |
> | --- | --- |
> | **Scriban 5.12.1** (the current pin) | **14 advisories: 1 Critical, 9 High, 4 Moderate** |
> | **Scriban 7.2.6** (current stable, `net10.0`) | **No vulnerable packages** |
>
> The Critical is `GHSA-5wr9-m6jw-xx44`, CVSS 9.1, patched in **7.0.0**: a
> sandbox escape where `TemplateContext` caches type accessors by `Type` only,
> built from the *then-current* `MemberFilter`, so a reused context with a
> tightened filter keeps exposing previously hidden members.
>
> **That advisory is not theoretical for this code.** `HtmlComposer` caches
> parsed templates in a `ConcurrentDictionary` and reuses composition state
> across renders. Whether it reuses a `TemplateContext` across renders with
> differing member exposure must be read before anyone argues the advisory is
> inapplicable.
>
> Consequences:
>
> - **The suppression question disappears.** No `NoWarn` is added anywhere —
>   not root, not project-scoped, not item-scoped. Root
>   `TreatWarningsAsErrors=true` applies unmodified.
> - Workspace ADR-0010's disposition changes from **promote** to **retire as
>   obsolete** in the docs-migration plan's ADR table. There is no advisory
>   acceptance left to carry forward. The new root ADR records that the
>   acceptance was resolved by upgrade, not inherited.
> - The seam plan's risk R8 is closed, and its stop condition 1 and open
>   question 1 are struck.
> - **New work appears, and it is not free.** 5.12.1 → 7.2.6 crosses two major
>   versions. There will be breaking changes affecting `Templating/HtmlComposer.cs`
>   and possibly the `.scriban` bodies. The uplift plan listed a Scriban upgrade
>   as an explicit non-goal; that non-goal is now reversed, and the upgrade needs
>   its own render-parity evidence — the rasterised before/after comparison
>   described in the uplift plan's verification section, run against the 5.12.1
>   baseline.
> - **Sequencing:** do the Scriban upgrade and prove parity **before** the code
>   move, while the workspace still has its own relaxed build settings and its
>   own visual-regression script. Doing it after the move means debugging a
>   two-major-version template-engine upgrade under
>   `TreatWarningsAsErrors=true` in a tree that has just changed shape.

### B5. Where do the third-party licence conclusions live?

*Raised by: docs-migration. Blocks: the documentation fold.*

The workspace `NOTICE.md` carries licence tables for PDFsharp, Scriban,
Microsoft.Playwright, Chromium, .NET, xUnit, Liberation and DejaVu, plus the
brand-asset ownership statements. **There is no root `NOTICE` file, and
`docs/index.md:3-5` forbids creating one.** Licence evidence is also an explicit
activation condition in the workspace register.

**Question.** Do the surviving licence conclusions become a "Third-party
components" table under `docs/architecture.md`, or do you authorise a root notice
file as an exception to the one-file-per-question rule?

Two of the conclusions currently read "No conclusion stated in the retained
notice" (PDFsharp, ModelContextProtocol) and need verification regardless.

## Deferred by the B1 decision

These were raised by the templates plan against `DESIGN_SPEC`. With the C#
renderer authoritative they are **not answered and not live**. They return
verbatim when the RPT specification is eventually written against accepted
`CASE-31` / `ENG-01` / `ENG-02` data.

- **H5** the four blocked report wordings, plus the missing Category S text
- **H6** the vehicle history check provider
- **M5** report kinds and superseded template IDs
- **M8** the design tokens `report.css` lacks
- **M9** the six schema and spec contradictions
- **M10** sample-data handling

One of them keeps a live edge and is restated here rather than deferred:
`docs/open-decisions.md:222` "Report wording" remains an **open decision in the
canonical documentation** regardless of which lineage is authoritative. Deferring
these questions does not close that entry, and the wording gate in the Core
contract still defaults closed.

## High

### H1. Where does rendering execute in production?

*Raised by: seam, MCP.*

Two facts discovered during planning make this urgent rather than theoretical:

- **There is no Web Dockerfile.** `Pegasus.Web.csproj` sets no
  `ContainerBaseImage`, and `scripts/Build-ReleaseArtifacts.ps1` builds the
  deployed image with `dotnet publish /t:PublishContainer`. The base is the
  default `aspnet:10.0`, which has neither Chromium nor Liberation/DejaVu fonts,
  and `PublishContainer` cannot run `apt-get`.
- **ADR-0015 allocates Web 0.5 vCPU / 1 GiB, min 0 / max 1 replica.** A Chromium
  page render does not fit comfortably beside ASP.NET Core, EF Core and
  OpenIddict, and there is no second replica to absorb a stall.

Worker is ruled out: `azure.yaml` deploys it as `host: function`, published as
`worker.zip`, which has no route to install a browser.

**Options:** (i) set `ContainerBaseImage` to a Playwright image, accepting a
jammy/noble base and a much larger CVE surface for the whole Web app;
(ii) introduce a Dockerfile and abandon SDK container publish, amending
ADR-0015's build route; (iii) deploy rendering as a separate service behind the
same unchanged Core port.

Stage 1 does not need this answered — it registers a fail-closed renderer
precisely so the question stays open — but Stage 2 cannot start without it.

### H2. Unaccepted wording and signature images in the production binary

*Raised by: seam.*

Relocating the four `.scriban` bodies and `report.css` embeds report prose that
`docs/open-decisions.md:222` records as an **open decision**. Relocating the three
engineer signature PNGs makes provenance-sensitive document assets
(`docs/requirements.md:949`) into production assembly content.

The seam plan gates rendering behind `ReportWordingAcceptance.Unaccepted`, so
nothing can be produced — but the bytes still ship.

**Question.** Acceptable behind a closed gate, or must the asset move wait for
Stage 2?

### H3. Under which capability ID does an MCP render tool sit?

*Raised by: MCP.*

`MCP-01`–`04` are `Now / 0.1.0-alpha.1`. `MCP-05` is `Next / 0.3.0` and scoped to
the classified-email workspace. `RPT-01`/`EXT-08` are `Later / 1.1.0`. There is
**no allocated capability** under which an MCP render tool is `Now` work.

A new ID or a re-allocation appears to be required. Alternatively — and the MCP
plan raises this against itself — the honest answer may be that the Automation
Actor has no business need to render at all, in which case the task shrinks to:
retire the `.mcpb`, land the Core port and adapter, and expose only
`list_templates` and `validate` on MCP, leaving `render` to RPT-01 and the Web
caller.

### H4. Does the preview get an allocated capability and a design route?

*Raised by: seam (following your 2026-08-03 decision).*

You directed that the HTML preview be kept and separated from the GUI. The plans
now retain `PreviewComposer` and give it a Core port. But:

- **no capability ID allocates a report preview surface**, and
- `design/README.md:48` requires every deferred UI capability to re-enter
  specification, alternatives, independent review, explicit approval, visual
  generation and manual visual review before implementation, with no
  `0.1.0-alpha.1` control, navigation, workflow or placeholder.

So the composer can be kept and integrated at library and port level now; a
staff-facing preview screen needs an allocated ID and the full design route
first.

**Question.** Allocate a preview capability ID in `docs/capabilities.md` now so
the design route can start, or leave the preview as a library capability with no
surface until the Engineer workbench (UI-15) absorbs it?

### H5. The four blocked report wordings — and a fifth that was assumed settled

*Raised by: templates.*

`docs/open-decisions.md:222` names four: salvage paragraphs for Categories N, A,
B and N/A; the recovery and storage paragraph; the final statement of truth; and
qualifications for E Mawdsley and N O'Reilly. `DESIGN_SPEC`'s own "Open wording
placeholders" list is identical.

**A fifth item is worse than open.** `DESIGN_SPEC` describes the Category S
salvage wording as "confirmed" but **does not reproduce the text anywhere**. So
no salvage wording of any category exists in this repository. The templates plan
counts seven of fifteen composed narrative elements as having no accepted text.

**Question.** Can the accepted wording set be supplied, and does it include
Category S?

### H6. Vehicle history check provider

*Raised by: templates.*

`DESIGN_SPEC` names Experian AutoCheck as the source and marks the check
Mandatory. Pegasus has `EXT-01`/`EXT-02` (DVLA/DVSA + MOT) and no Experian
adapter; `docs/open-decisions.md` leaves each global check's provider and
unavailable contract open.

**Question.** Which provider, and what does the report print when the check is
unavailable? No plan will select one.

### H7. Does this task write the Automation Actor contract ADR?

*Raised by: MCP.*

`NOW.md` queues two MCP follow-ups. The second — promote the settled Automation
Actor identity/authentication/tool-inventory contract to an ADR — exists to
freeze the tool inventory and scope set. This task would change both, adding a
fourth scope and three tools.

Writing the ADR first makes it stale within one task; writing it after leaves the
contract unowned meanwhile. **The MCP plan recommends this task absorbs it.**

The first follow-up (tier-5 evidence from a real external client) runs beside
this work but is *enlarged* by it: evidence recorded against a 9-tool inventory
must be re-run or supplemented for a 12-tool one. **Recommendation:** sequence
the external-client run after this task and exercise all 12 tools in one session.

## Medium

### M1. Renderer strictness — now, or at relocation?

*Raised by: uplift.* Estimated blast radius under root strictness is **low
hundreds of diagnostics** across 9,143 lines, concentrated in `CA1707` (136
guaranteed hits from underscore-named test methods alone), `CA1062` and
`CA1031`. Recommendation: mirror `document-extraction` — local strictness with a
scoped `NoWarn`, in its own commit, not bundled with the runtime change.

### M2. Does the MCP host survive the uplift?

*Raised by: uplift.* If the MCP plan deletes `CollisionRenderer.Mcp`, three
uplift items disappear (the `Microsoft.Extensions.Hosting` bump, the
`build-mcpb.ps1` path fix, and the single-file native-probing verification).
Confirm before starting the uplift.

### M3. Is the renderer container a maintained artefact at all?

*Raised by: uplift.* The Dockerfile references
`mcr.microsoft.com/playwright/dotnet:v1.61.0-jammy`, and **that tag does not
exist** — jammy publication stops at `v1.59.0`. The container build is already
broken today, before any uplift, which means nobody has built it recently.

Repair it (switch to `v1.61.0-noble`, which is SDK-10-based and therefore also
the uplift enabler), or delete the Dockerfile?

### M4. Lock files for the renderer workspace?

*Raised by: uplift.* Adding six `packages.lock.json` files aligns the renderer
with the other two .NET workspaces and lets CI use `--locked-mode`, at the cost of
six new tracked files. Note: the absent `--locked-mode` on the renderer CI lane
is **correct today**, not a defect — the workspace has no lock files.

### M5. Report kinds and superseded template IDs

*Raised by: seam, templates.* The renderer ships twelve template IDs, of which
eight share one body. Five map to **no Pegasus capability at all**
(`market-valuation-evidence`, `advert-evidence-pack`, `blank-letterhead`,
`roadworthy-criminal-report`, `part-35-response`), and `RPT-03` (Audit rendering,
conservative vs maximised specifications) has **no renderer template**.

Separately: `total-loss-report` and `repairable-contract-repair-report` would be
superseded by the new assessment family. Retired, repointed, or kept alongside?

### M6. Density in the issued-artifact contract

*Raised by: seam.* Auto-fit re-renders at Normal → Compact → UltraCompact and the
chosen density changes the bytes. The seam plan recommends issued reports render
at a policy-fixed density, with auto-fit retained as engine behaviour but not part
of the issued contract. Confirm.

### M7. Custody role for a rendered artifact

*Raised by: MCP.* The natural fit is `DocumentSemanticRole.EngineerReport` with
an automation source — but "the Automation Actor produced an Engineer report" may
be a claim you do not want the data model to make.

### M8. Design tokens the spec needs and `report.css` lacks

*Raised by: templates.* `#EFEFEF` total-row grey is **absent** from `report.css`
and is close enough to three existing greys (`#f5f5f5`, `#f2f2f2`, `#f4f4f3`) to
be mistaken for them. Document red `#C80A32` is used throughout `report.css` but
is recorded nowhere in `design/README.md` — which lists "document red" among
tokens explicitly excluded from the Web palette. Status badges and the four figure
tiles are new visual components with no CSS at all.

Approve the additions, or reject?

### M9. Schema and spec contradictions to resolve

*Raised by: templates.* Six, each needing a one-line answer:

1. **VIN rule.** `DESIGN_SPEC` asserts a 17-char VIN check in one place and says
   there is no VIN format rule in two others (because bicycles and trailers may
   have none). Which governs?
2. **Recovery and storage charges.** Do they enter the subtotal, the repair
   total, the settlement, or none? The spec's formula excludes them; the schema
   files them under `costs`.
3. **Enum display forms.** `below_average` → "below average"?
   `moderate_to_heavy` → ? `right_rear` → "right rear"? `wheel` → "wheel(s)"?
   And the literal slash in "collision/impact damage".
4. **Negative settlement.** When salvage exceeds the engineer's value the
   settlement is negative. What do the red tile and the settlement sentence say?
5. **Matter line.** The composed prefix "Road Traffic Accident" is hard-coded for
   every report. Pegasus supports case types that are not RTAs. Fixed, or
   case-type-driven?
6. **VAT-row label, registered mode.** The not-registered label is fixed as
   `VAT (20% — parts & paint only)`. The registered-mode label is unspecified.

### M10. Sample-data handling

*Raised by: templates.* The four `sample_job_*.json` files carry a claimant name,
a principal's postal address, a registration and a VIN, and are committed under
`docs/reference/`. Confirm they are retained as-is and that no derivative —
fixture, starter, or baseline raster — may carry their values.

### M11. Provenance home after retirement

*Raised by: docs-migration.* The workspace provenance row records six facts that
cannot be reconstructed once the tree is deleted (upstream repo, branch, commit,
source path, file count, byte count, SHA-256). Recommendation: freeze them
verbatim in a provenance clause of the promoted root ADR. Confirm, rather than a
retired-imports subsection in `workspaces/README.md`.

### M12. Template schema documentation

*Raised by: docs-migration.* `TEMPLATES.md` L86-399 is roughly 300 lines of
hand-maintained payload schema. The plan retires it in favour of code plus
validators as the authority, on the "code plus passing tests beat any document
about current state" rule. Confirm, or name the canonical owner that should carry
it.

### M13. Small operational leftovers

*Raised by: docs-migration, uplift, desktop removal.*

- Does `visual-regression.ps1` migrate to `scripts/`, or die with the workspace?
  Determines whether `docs/operations.md:52`'s `poppler-utils` row is repointed or
  deleted.
- Does any Pegasus image install `fonts-liberation` and `fonts-dejavu-core`?
  `docs/operations.md:53`'s current justification disappears with the workspace,
  and the row must not name an image that does not install them.
- MSIX identity `71B58B04-E006-42EA-9C51-D1DB853DDB3A` is deleted from source. Any
  external registration — Store, MDM, package feed, code-signing binding,
  installed machines — needing separate retirement outside this repository?
- Simplify the renderer solution's configuration platforms to `Any CPU` once the
  GUI is gone, or leave the vestigial entries for a smaller diff?
- Accept the Ubuntu 24.04 container output as a new baseline, or require a one-off
  comparison against `v1.59.0-jammy` before switching? There is no valid "before"
  image, because the referenced tag never existed.

## Settled

Recorded so these are not re-asked.

| Decision | Date | Source |
| --- | --- | --- |
| The C# `CollisionRenderer` is the authoritative design; `DESIGN_SPEC.md` is superseded evidence (B1) | 2026-08-03 | Operator, this task |
| `.mcpb` replacement means parity first, then delete (B2) | 2026-08-03 | Operator, this task |
| Stage 1 proceeds now, advancing no capability (B3) | 2026-08-03 | Operator, this task |
| Scriban is upgraded rather than suppressed; 7.2.6 is clean, 5.12.1 carries 14 advisories including one Critical (B4) | 2026-08-03 | Operator decision plus measured evidence, this task |
| The HTML preview is wanted. `PreviewComposer` is retained and separated from the GUI, not deleted with it | 2026-08-03 | Operator, this task |
| The desktop/UI elements of the renderer are removed | 2026-08-03 | Operator, this task; pre-authorised by `design/README.md:237` |
| The renderer workspace is retired and its documents integrated into the main repository | 2026-08-03 | Operator, this task |
| Report policy stays in `Pegasus.Core`; it does not move into Infrastructure or the renderer | 2026-07-27 | `docs/adr/0009` |
| A local MCPB/stdio bridge is rejected for Pegasus | — | `docs/adr/0004:105` |
| MCP is one named vendor-neutral Automation Actor with no configuration, credential, cloud, release or deletion authority | — | `docs/adr/0011` |
| Inspection mode is a provider-determined database setting; the exact literal is `Image Based Assessment`; "IBA" is not staff-facing | 2026-08-03 | `docs/adr/0018` |
| Renderer GUI package assets are removed when the GUI is decommissioned during Pegasus integration | — | `design/README.md:237` |
