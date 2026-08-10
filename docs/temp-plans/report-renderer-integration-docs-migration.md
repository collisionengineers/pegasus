# Report renderer documentation migration and workspace retirement

This is a draft supporting plan for the `report-renderer-integration` task. It
plans only the documentation migration and the retirement of
`workspaces/report-renderer/`. It moves no code, accepts no contract, and
activates no capability. Every line reference was read in the working tree at
the time of writing and should be re-checked against the branch before editing,
because line numbers move.

## Operator decisions, 2026-08-03

Three decisions amend this plan.

**Scriban is upgraded, not suppressed.** The check this plan called for was run:
Scriban 5.12.1 carries **14 advisories, one Critical** (`GHSA-5wr9-m6jw-xx44`,
CVSS 9.1, patched in 7.0.0); Scriban 7.2.6 reports **no vulnerable packages**.
Consequently:

- In section 2's ADR disposition table, **workspace ADR-0010 changes from
  *promote* to *retire as obsolete*.** There is no advisory acceptance left to
  carry forward; the promoted root ADR records that it was resolved by upgrade.
- The whole `NoWarn NU1901–NU1904` call-out becomes a **historical explanation
  of why no suppression is carried**, not a live decision. Option B is not taken.
  Root `TreatWarningsAsErrors=true` applies unmodified.
- **Stop condition S2 is closed.** Open question 1 is struck.

**The C# renderer is the authoritative design.** `DESIGN_SPEC.md` is superseded
evidence. This does not change any edit in section 3 —
`reference/README.md:22` is reworded exactly as planned, and the fourteen
files under `reference/rendererref1/` stay — but it does mean the reworded
description must not imply the folder specifies anything. "Evidence only" is the
operative phrase and is already in the proposed wording.

**Stage 1 proceeds now.** The Stage A capability-note wording in section 5 is the
wording to use.

**One conflict this plan cannot resolve.** Section 7 sequences a single deletion
commit that removes `workspaces/report-renderer/` wholesale. The parity-first MCP
decision requires the `.mcpb` stdio host — built from `CollisionRenderer.Mcp` and
`CollisionRenderer.Core`, both inside that tree — to keep working until parity is
demonstrated. **Commit 4 is therefore blocked** until open question B6 in the
consolidated questions document is answered. Commits 1, 2, 3 and 5 are unaffected.

## Verification status of the working assumptions

Confirmed by reading the files:

| Assumption | Verified? | Note |
| --- | --- | --- |
| Workspace `README.md` is 79 lines, 12 template IDs | Yes | 6 relative links inside it |
| `NOTICE.md` is 100 lines | Yes | licence tables, brand, PII, security notice |
| `docs/ARCHITECTURE.md` is 180 lines | Close (assumed 179) | content matches |
| `docs/DEVELOPMENT.md` is 312 lines | Yes | |
| `docs/TEMPLATES.md` is 529 lines | Yes | |
| Eleven workspace ADRs, all Accepted, index 67 lines | Yes (assumed 66) | ADR-0008 is "Accepted, partially superseded" |
| `docs/operations.md:66` wrongly states `net10.0-windows` for the GUI | **Yes — confirmed error** | `CollisionRenderer.Gui.csproj:4` is `net8.0-windows10.0.19041.0`; `scripts/email-eval-desktop` genuinely is `net10.0-windows`, so the row collapses two different frameworks into one wrong one |
| `docs/operations.md:378` renderer line lacks `--locked-mode` | Yes, but it is **not** a defect | the renderer workspace has no `packages.lock.json` anywhere, so `--locked-mode` would fail. This becomes a real gap only when the projects join `src/` |
| `docs/capabilities.md` RPT-01..05 all carry renderer-source notes | **No** | only `RPT-01` (line 263) says "Imported renderer source is non-caller evidence until separately activated". `RPT-02`..`RPT-05` (264-267) say "Allocation only; …" and never mention the workspace. `EXT-08` (line 248) says "Imported renderer source is not activation" |
| `CONTEXT.md` carries renderer/report terms | Partly | report-domain glossary entries, but **no** renderer, workspace or `report-renderer` term. **No edit required** |
| `docs/index.md:21` needs editing | **No** | the row stays true while `document-extraction/` and `ai-centre/` remain |
| `workspaces/AGENTS.md:5,8,10` need editing | Mostly **no** | none of those lines names the renderer workspace |

Found by `git grep`, and not previously listed:

- `workspaces/ai-centre/README.md:58,109,144` and
  `workspaces/ai-centre/docs/architecture.md:29,50,109` name
  `workspaces/report-renderer` as the owner of deterministic document assembly.
  These are retained-workspace documents, are not protected skill packages, and
  **must be repointed in the same commit as the deletion**.
- `workspaces/ai-centre/skills/**` contains roughly twenty `collisionrenderer`
  connector references across ten protected packages. These are protected
  external source under root `AGENTS.md`. **Do not touch them.** They are also
  excluded from the link checker except for their `README.md`.
- `docs/adr/0009-adopt-pegasus-monorepo-workspaces.md:34` names
  `report-renderer/`. That body is immutable; it is a dated historical record of
  what was imported and stays exactly as written.
- `design/assets/report-renderer/` lives under `design/`, **not** under
  `workspaces/`, and is therefore unaffected by deleting the workspace.
- The workspace `global.json` already pins SDK `10.0.300`; only the target
  frameworks are `net8.0`. Workspace ADR-0003's title overstates what is left of
  that decision.

## Routing rule this plan obeys

`docs/index.md:3-5`: one file per question; edit canonical files in place; new
Markdown files only as ADRs or transient plans. Authority order
(`docs/index.md:24-32`): `operator-notes.md` > `requirements.md` >
`capabilities.md` > ADRs > `architecture.md` and `operations.md` >
`engineering.md` and `docs/design.md`. Code plus passing tests beat any
document about current state. On conflict, fix the losing document in the same
commit you notice it.

Consequence: the five workspace documents and eleven workspace ADRs cannot be
"moved". Every statement is either folded into an existing canonical owner,
promoted into one new root ADR, or retired. No new non-ADR Markdown file is
created anywhere.

## 1. Statement-level disposition of the five workspace documents

Classes: **PT** product truth, **DEC** durable decision, **BP** build procedure,
**OBS** obsolete on retirement.

### `README.md` (79 lines)

| Source section | Class | Target | Verdict |
| --- | --- | --- | --- |
| L1-3 "one Core engine serves CLI, Windows GUI, HTTP API and MCP host" | PT + OBS | `docs/architecture.md` implementation map and the new renderer subsection | Fold the engine statement; the four-host list is only true of the surviving hosts |
| L5 workspace-boundary blockquote | OBS | capability notes and `docs/requirements.md:893-896` | Retire the sentence, re-assert the non-activation claim in the capability notes |
| L9-18 five shipped and two test projects | PT | `docs/architecture.md` components and implementation map | Fold, rewritten against post-move project names. Do not carry names that no longer exist |
| L19 "Core converts typed JSON to HTML with Scriban and the shared design assets, then renders A4 PDF through Chromium; PDFsharp only appends existing PDF evidence, it is not the layout engine" | PT + DEC | `docs/architecture.md` renderer subsection; engine choice is a clause of the new root ADR | Fold verbatim in meaning. The single most load-bearing sentence in the workspace |
| L21-37 the exact 12 template IDs | PT | `docs/architecture.md` renderer subsection | Fold as a list. Not `capabilities.md` — the roadmap owns capability IDs, not template IDs |
| L38 "Core also owns blank drafts, starter drafts, form definitions and attachment policies. Hosts must not invent template-specific rendering or validation rules" | DEC | New root ADR clause; restated as current state in `docs/architecture.md` | Promote |
| L40-56 quick start | BP | none | Retire. `docs/operations.md` already owns build and test commands |
| L58-64 documentation link list | OBS | none | Retire |
| L66 "Generated PDFs, screenshots, extracted reference text and test artefacts belong under ignored `artifacts/`. Do not commit customer or case data" | PT policy | already owned by root `AGENTS.md` and `docs/operations.md` corpus safety | Verify the owners say this; retire the copy. If a gap is found, fix the owner in the same commit |

### `NOTICE.md` (100 lines)

| Source section | Class | Target | Verdict |
| --- | --- | --- | --- |
| L1-6 preamble, "source-only and non-caller", "does not infer a licence" | OBS + policy | capability notes; the caveat travels with the licence table | Retire framing, keep the caveat |
| L9-19 direct package licence table | PT, licence evidence | **No canonical owner exists** — see stop condition S1 | Interim: fold surviving rows into a "Third-party components" table under `docs/architecture.md` source-and-generated-material roles. Rows for packages that do not survive are dropped |
| L21-31 runtime/test/container component licences (Chromium BSD-3-Clause, .NET MIT, xUnit Apache-2.0, Liberation OFL, DejaVu GPL-with-exception) | PT, licence evidence | same | Fold rows that remain true |
| L33-42 brand assets: master logo path, signatures, "never redraw", "payload-supplied signatures are case data", "build-time embedding transfers no ownership", Tw Cen MT/Futura not shipped, Arial or metric substitute | PT + DEC, design authority | `docs/design.md` logo section and the Web/renderer boundary table | Fold. `docs/design.md` already states most of this; add only the genuinely additional statements |
| L44-54 design and source provenance | DEC | `docs/design.md` Web and renderer boundary | Fold; mostly a confirmation of an existing rule |
| L56-67 private reference material and personal data: the four local folders, "real customer reports … names, vehicle registrations, claim details", git-ignored, never committed, inventories under `artifacts/`, "documentation may describe reference families but must not reproduce sensitive filenames or case facts" | **Operator/business statement** | `docs/operations.md` corpus safety → safety rules, plus `reference/README.md` handling rules | Preserve **verbatim in meaning**. Note carefully: the fourth folder in that list, `report-renderer/`, is the *prior Python renderer* source folder named in workspace ADR-0009, **not** this workspace. Do not let the retirement delete a rule about a different thing |
| L69-77 generated documents and attachments; do not commit; do not use customer payloads as tests or starters; synthetic fixtures only | **Operator/business statement** | `docs/operations.md` safety rules | Preserve verbatim in meaning |
| L76-77 `%LOCALAPPDATA%` MCP artefacts; API multipart temporary files | PT, host-specific | none unless those hosts survive | Retire with the hosts |
| L79-87 Scriban advisory acceptance, its three conditions, and "must be revisited if runtime template authoring, unencoded values, dynamic compilation or a new trust boundary is introduced" | **DEC, security** | new root ADR | Promote. The highest-risk statement in the whole migration |
| L89-96 `CR_API_TOKEN*` bearer authentication | DEC, contradicted | none | Retire with the `.Api` host. Pegasus authentication is owned by root ADR-0004 and ADR-0011 |
| L98-100 "no inferred grants or conclusions … obtain the exact resolved dependency inventory before external distribution" | policy | with the licence table | Fold |

### `docs/ARCHITECTURE.md` (180 lines)

| Source section | Class | Target | Verdict |
| --- | --- | --- | --- |
| L3 design-authority blockquote | DEC | `docs/design.md` already says it | Retire (duplicate) |
| L5 "one shared engine, thin hosts"; Core owns models, catalogues, forms, attachment policy, validation, HTML composition, density fitting and PDF production | DEC | new root ADR clause; current-state restatement in `docs/architecture.md` | Promote |
| L7-9 workspace boundary | OBS | capability notes | Retire |
| L11-29 project graph table and direct package versions | PT | `docs/architecture.md` components; third-party table | Fold, rewritten for surviving projects only |
| L31-54 composition and the nine principal contracts | PT | `docs/architecture.md` renderer subsection, at contract-name granularity only | Fold names and seam meaning. **Do not** copy signatures into docs — code plus passing tests are the authority for shape |
| L55-68 the eight-step pipeline, "errors stop the render, warnings continue", "retain clean multi-page output and add a warning rather than clipping", SHA-256 of output | PT, determinism rules | `docs/architecture.md` renderer subsection | Fold. This is the render contract in prose and must land somewhere canonical |
| L68 "templates are first-party embedded artefacts; payload text is HTML-encoded; end-user text is not compiled as Scriban" | DEC, security | new root ADR (the Scriban acceptance depends on it) | Promote |
| L70-84 HTML/page furniture: A4 margins, running footer rule and strapline, VAT-number swap for fee notes, `thead` header groups, `break-inside: avoid`, print backgrounds, reserved footer margin, image-format limit, PDFsharp append | PT + design invariant | design invariants → `docs/design.md`; the mechanism → `docs/architecture.md` | Split as shown |
| L85-95 density fitting | DEC + PT | new root ADR clause; behaviour in `docs/architecture.md` | Promote and fold |
| L97-114 exact catalogue table and the seven block types | PT | `docs/architecture.md` renderer subsection, one canonical copy shared with the README's 12 IDs | Fold once, not twice |
| L116-151 host surfaces and parity, the API route table, the JSON render shape, the stricter API attachment policy, the seven MCP tools | PT, host-specific | only for surviving hosts | Retire with the hosts. The one statement that survives is "parity means capabilities come from Core, not that every medium has identical controls" → `docs/architecture.md` |
| L153-164 API authentication variables | DEC, contradicted | none | Retire |
| L166-168 container topology | BP + PT | `docs/operations.md` only if a Pegasus image actually gains Chromium and fonts | Conditional; the seam plan owns it |
| L170-180 current limits: Chromium required, no runtime user templates, auto-fit is a ladder, dense content may exceed target, page counting is not a forensic parser, PDFsharp does not replace layout, local-path capability is not remote filesystem access, **no tagged-PDF / PDF-UA claim**, reference folders are not build inputs | PT, honest-limit statements | `docs/architecture.md` evidence qualifications or the renderer subsection | Fold all of them. These are anti-overclaim statements and are exactly the kind this repository preserves |

### `docs/DEVELOPMENT.md` (312 lines)

| Source section | Class | Target | Verdict |
| --- | --- | --- | --- |
| L7-16 prerequisites table | BP | `docs/operations.md` already owns the equivalents | Retire, except: confirm `docs/operations.md` records that a real render needs the pinned Chromium |
| L11 "use the pinned SDK rather than documenting an evergreen version" | BP rule | already the repository habit | Retire |
| L18 package versions that matter to browser/runtime matching | PT | third-party table | Fold the Playwright-version/image-tag pairing rule only |
| L20-35 restore, `dotnet --info` diagnosis | BP | none | Retire |
| L38-66 build sections including the six-project cross-platform list and the GUI RID build | BP | none | Retire. The per-project list exists only because the workspace has a Windows-only project in its own solution |
| L68 "Scriban advisory warnings NU1901–NU1904 are intentionally handled by repository policy. Do not remove or broaden the suppression without reviewing ADR-0010" | **DEC, security** | new root ADR + the project file that carries the suppression | Promote |
| L70-78 install Chromium; do not run the installer inside the container image | BP | `docs/operations.md` if a Pegasus lane needs a browser install beyond the existing step | Fold only the non-duplicate rule |
| L80-95 test commands; "tests and documentation must not record evergreen test totals" | BP + repo rule | second half already matches `docs/operations.md` | Retire commands; verify the rule exists |
| L97-126 CLI command table, density values, `SuggestedFileName` default | PT (contract) + BP | only if a CLI surface survives | Conditional on the seam plan. If no CLI survives, retire |
| L128-186 run the API, render JSON shape, four `CR_API_TOKEN*` configurations, bearer header | BP + DEC, contradicted | none | Retire with the `.Api` host. `CR_API_TOKEN*` must appear nowhere after retirement |
| L188-201 multipart check and caveats | PT, host-specific | none | Retire with the host |
| L203-219 run the GUI | PT, host-specific | none | Retire with the host |
| L221-231 run the MCP host, seven tools, `%LOCALAPPDATA%` artefacts, "keep stdout reserved for the MCP protocol" | PT, host-specific | none | Retire. Pegasus MCP is `/mcp` in `Pegasus.Web` and is a different thing |
| L233-256 container build/run; repository-root build context "required by linked design assets" | BP | the build-context fact matters if any Pegasus image embeds the design assets | Retire the commands; carry the constraint only if an image needs it |
| L258-268 host-parity checks and "byte identity should not be promised across differing Chromium/font environments; contract and visual parity are the portable guarantees" | **PT, determinism boundary** | `docs/architecture.md` evidence qualifications | Fold. A real anti-overclaim rule about PDF determinism that must not be lost |
| L270-290 troubleshooting: missing/mismatched Chromium; "do not copy a random system Chromium into the expected revision directory"; Linux font widths; `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` | BP, operationally real | `docs/operations.md` failure/observability rules and the Linux capability table | Fold the two genuine operational facts |
| L292-294 GUI build fails on non-Windows | BP | none | Retire |
| L296-309 401 diagnosis; multipart rejection checklist | BP, host-specific | none | Retire with the hosts |
| L311-312 keep generated and private material under ignored `artifacts/`; never commit customer reports | operator/business | `docs/operations.md` safety rules | Preserve in meaning (duplicate of `NOTICE.md`; fold once) |

### `docs/TEMPLATES.md` (529 lines)

| Source section | Class | Target | Verdict |
| --- | --- | --- | --- |
| L8-13 "**the letterhead and footer are not part of any template body**"; a `.scriban` body never draws the logo, ref block, page numbers or strapline | **DEC, design invariant** | `docs/design.md` Web and renderer boundary | Promote into design authority. The load-bearing house-style rule |
| L16-31 three-part build; JSON options; templates/stylesheet/logo/signatures embedded so output is identical from CLI, desktop or Linux container | PT + DEC | `docs/architecture.md` renderer subsection; ADR clause for the templating decision | Fold. The asset path under `design/` is already canonical and survives unchanged |
| L33-51 shared-shell guarantees: ref-block fallbacks, footer rule and strapline text, the fee-note VAT swap, centred UPPERCASE title, red-ruled section headings | **Design invariant, brand-visible** | `docs/design.md` | Fold. Includes the exact strapline string, which is operator-visible brand copy |
| L55-67 payload conventions; **text is HTML-encoded by the composer, payload cannot inject markup**; shared `meta` | PT + security | `docs/architecture.md` renderer subsection; the encoding clause also anchors the Scriban ADR | Fold |
| L71-85 the four base template families table | PT | `docs/architecture.md`, folded into the single catalogue list | Fold at family granularity |
| L86-270 the four per-template payload shapes with JSON examples and per-template notes | PT, but **schema detail** | none — the code and its validators are the authority | **Retire as documentation.** Under "code plus passing tests beat any document about current state" a 180-line hand-maintained schema mirror is a liability. Preserve only: bundled signature keys are a fixed set (→ `docs/design.md`), and the firm-only sign-off convention if the operator confirms it is business copy |
| L274-292 content-block catalogue: exactly seven block types and the validator rejects any other | PT | `docs/architecture.md` renderer subsection, once | Fold once |
| L293-399 per-block JSON examples; the `mediarow` Core-versus-API image policy split | PT, schema detail + host-specific | none | Retire. The Core/API split dies with the `.Api` host |
| L403-416 robustness: A4 `@page`, repeating `thead`, `break-inside: avoid`, "provided it uses the existing table and block CSS classes rather than inventing its own" | Design invariant + PT | `docs/design.md` (CSS-class rule) and `docs/architecture.md` (paging) | Fold. Drop the two sample-document measurements — dated evidence, not rules |
| L420-513 "add a new template" five-step recipe with code snippets and the `.csproj` embedded-resource note | BP, path-keyed | none as written | **Retire as written** — every path changes on the move. If the operator wants the recipe kept it is a rewrite against post-move paths in `docs/engineering.md`, and a separate task. The one durable statement is "adding a template is no engine change" → ADR clause |
| L514-529 verify commands | BP | none | Retire |

## 2. Disposition of the eleven workspace ADRs

`docs/adr/README.md:3-18` states that published root ADR bodies "are normally
immutable" and that "an explicit direct user instruction may authorize an
in-place amendment". **Folding a workspace decision *into* an existing root ADR
body is therefore not available** unless the operator explicitly authorises that
amendment for the named ADR. Reviewed navigation, status and supersession
metadata may be maintained in `docs/adr/README.md` without changing meaning, so
index rows are always editable. The four available verdicts are: promote to a new
root ADR, fold into `architecture.md` / `operations.md` / `docs/design.md`
prose, retire as obsolete, or (rarely, on explicit instruction) amend a root body.

`docs/adr/README.md:44` records root ADR-0010's carve-out: "`docs/adr/` is the
sole root durable-decision store; existing source roles and **workspace-local
decisions remain unchanged**." That carve-out exists because the renderer is a
workspace with its own decision store. **When the workspace is retired the
carve-out has nothing to attach to for this workspace**, so every one of these
eleven decisions must reach a verdict. Root ADR-0010's body is not amended: two
workspaces retain local decision stores, so the clause remains true as written.

Recommendation: promote as **one** new root ADR with numbered clauses, filed at
the next free number. The repository already uses clause-level ADRs (root
ADR-0013). File the Scriban advisory acceptance as its own clause **with its own
explicit consequence for `Directory.Build.props`**, because it is the one
decision that changes repository-wide build policy.

| WS ADR | Subject | Contradiction risk | Verdict | Target |
| --- | --- | --- | --- | --- |
| 0001 | Rendering engine: headless Chromium via Playwright | None known | **Promote** | New root ADR clause 1, restated with Pegasus consequences: a Playwright/Chromium dependency in the application build, CI browser lane, and image |
| 0002 | Modular shared Core, thin CLI/GUI/API clients | **Yes** — the seam plan retires the `.Api` host, and "thin clients" is stated over three surfaces that will not all survive | **Promote, narrowed** | New root ADR clause 2, narrowed to "one engine owns document models, catalogues, forms, attachment policy, validation, composition, density and PDF production; every surface is a caller of that engine and owns no render rule". The surviving-host list is current state and belongs in `docs/architecture.md` |
| 0003 | Unified .NET 8 stack | **Yes** — contradicted by the .NET 10 uplift; `global.json` already pins SDK 10.0.300 | **Retire as obsolete** | Record in the new ADR's retired-decisions clause. Its surviving content ("one language and runtime, no split codebase") is already root ADR-0002's modular-monolith decision |
| 0004 | Templating: Scriban bodies + C# letterhead shell + embedded brand CSS | None; the `.scriban` assets already live under `design/` | **Promote** | New root ADR clause 3, including "templates are first-party embedded artefacts; end users never author or compile runtime templates; payload values are HTML-encoded and passed as values" — the precondition the advisory acceptance rests on |
| 0005 | Reuse the brand CSS design system | None | **Fold into `docs/design.md`** | A root ADR restating it would create a second design source. Fold the specific tokens it names into `docs/design.md` only where that file does not already state them |
| 0006 | Page furniture via Chromium header/footer + paged-media CSS | None | **Split** | Design invariants (footer composition, strapline, page numbering, repeating headers, unbreakable blocks) → `docs/design.md`. The mechanism choice → new root ADR clause 4 |
| 0007 | Density auto-fit | None | **Promote** | New root ADR clause 5. Current behaviour → `docs/architecture.md`. Note the seam plan proposes that issued artifacts render at a fixed density, so the clause must say auto-fit is engine behaviour, not issued-artifact contract |
| 0008 | Cloud portability: ASP.NET Core API + Playwright Docker image | **Yes** — if the `.Api` host is retired the decision has no subject; its bearer access control is already superseded by WS-0011 | **Retire as obsolete for the host and container; keep one clause** | The surviving statement is "the engine carries no Windows-only dependency, so it runs on the Linux runtime" → new root ADR clause 2. Pegasus hosting is owned by root ADR-0015 and ADR-0002 |
| 0009 | Local reference material is git-ignored, never committed | None; already this repository's rule | **Fold into prose** | `docs/operations.md` safety rules and `reference/README.md` handling rules. Verify both carry the substance; add only genuine gaps. No root ADR |
| 0010 | Accept/suppress Scriban advisories NU1901–NU1904 | **Yes** — collides with root `TreatWarningsAsErrors=true` | **Promote as its own clause, with an explicit build-policy consequence, and stop for the operator** | See below |
| 0011 | Multi-token and SHA-256 API authentication | **Yes** — contradicted by Pegasus's OpenIddict `/mcp` model, root ADR-0004 and root ADR-0011. Environment-variable bearer tokens are not a Pegasus authentication mechanism | **Retire as obsolete** | Record in the retired-decisions clause that renderer bearer-token authentication does not enter Pegasus and that `CR_API_TOKEN`, `CR_API_TOKENS`, `CR_API_TOKEN_SHA256` and `CR_API_TOKEN_SHA256S` must appear nowhere in the repository after retirement |

### The `NoWarn NU1901–NU1904` decision, called out separately

This is a real decision, not a formatting detail. A suppressed security advisory
becoming inherited repository-wide policy would be a material change to Pegasus's
security posture, made silently, as a side effect of a file move.

Mechanical facts:

- Root `Directory.Build.props` sets `TreatWarningsAsErrors=true` and declares no
  `NoWarn`.
- Workspace props sets `TreatWarningsAsErrors=false` and
  `NoWarn=$(NoWarn);CS1591;NU1901;NU1902;NU1903;NU1904`.
- The workspace props file is deleted with the workspace. Any moved project under
  `src/` inherits the root props. NuGet audit warnings NU1901–NU1904 would then
  be **errors**, and the build would go red on the first restore.
- `CS1591` is irrelevant under root props: the root does not set
  `GenerateDocumentationFile`, so CS1591 does not fire. That suppression dies and
  needs no successor.

| Option | Effect | Verdict |
| --- | --- | --- |
| A. Add the four codes to root `NoWarn` | Every Pegasus project stops failing on **any** package advisory, including future advisories on unrelated packages | **Reject.** Converts one package's constrained acceptance into blanket repository policy and would hide the next real advisory |
| B. Scope the suppression to the single project that references Scriban, with a comment citing the promoted root ADR clause | The acceptance stays attached to the package and the rationale; every other project keeps `TreatWarningsAsErrors` on audit findings | **Recommended**, conditional on C being checked first |
| C. Check whether a Scriban release without the advisories exists and upgrade instead of suppressing | Removes the decision entirely | **Do this check first.** The pin is `5.12.1`; the acceptance is dated to that pin and must be re-verified, not inherited |
| D. Replace Scriban | Out of scope; would also invalidate workspace ADR-0004 | Reject for this task |

The acceptance rationale must be **re-affirmed, not inherited**. Its three
conditions were: templates are first-party embedded artefacts; end users do not
author or compile runtime templates; payload text is HTML-encoded and passed as
values. In Pegasus the payload source changes — values come from accepted
Core-owned case data rather than a CLI/API caller's JSON — which does not weaken
the conditions but does change the trust boundary description. `NOTICE.md:87`
already says the acceptance "must be revisited before release" if "a new trust
boundary is introduced". Moving the engine into a multi-tenant web application
**is** a new trust boundary description, so revisiting it is required by the
decision's own terms.

**Stop condition:** do not carry the suppression into `src/` without the operator
choosing between B and C. Record the choice as a clause of the promoted root ADR
with its date.

## 3. Exact edit list for canonical files

`workspaces/` is **edited, not deleted**. `document-extraction/` and `ai-centre/`
remain, so `workspaces/README.md`, `workspaces/AGENTS.md`,
`docs/architecture.md`'s Workspaces section and
`.github/workflows/workspaces.yml` all keep existing with fewer entries.

| File:line | Current text (abbreviated) | Replacement intent |
| --- | --- | --- |
| `workspaces/README.md:16` | the `report-renderer/` integration-status row | **Delete the row.** The register is "the sole register for each workspace's role, current integration status, activation conditions, and owner"; a directory that does not exist has no integration status. This also removes the only relative link into the tree from a retained file |
| `workspaces/README.md:25` | the provenance row: `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer`, 108 files, 604,228 bytes, SHA-256 `a3b9b665b23b08b9dd61276d48b9f3a3c551a005213225e7941d0adf6d504471` | **Delete the row here, after copying every field verbatim into the promoted root ADR's provenance clause.** See section 4 |
| `workspaces/README.md:29-42` | manifest semantics | Unchanged in substance. **Add one sentence**: this register lists only workspaces currently present; a retired import's last recorded provenance is preserved in the decision that accepted its integration, and is not regenerated |
| `workspaces/README.md:70-74` | "Updating a source import requires a reviewed provenance change and regenerated current manifest" | **Add one clause**: retiring an import is such a reviewed provenance change; no manifest is regenerated because no tracked tree remains, and the last import identity is frozen in the accepting decision |
| `workspaces/AGENTS.md:5` | "Treat every child as a non-caller source workspace…" | **No edit required.** It quantifies over "every child"; after retirement the children are `document-extraction/` and `ai-centre/`, for which it stays exactly true |
| `workspaces/AGENTS.md:8` | "Do not execute a skill, model, training job, evaluator, external connector, renderer, or document converter against operational data…" | **Keep the word `renderer`.** Removing it would loosen a live safety rule that still binds `ai-centre/skills/` packages, which call a `collisionrenderer` connector. Optional clarifying clause only; do not weaken the sentence |
| `workspaces/AGENTS.md:10` | "A future application integration needs a named capability, accepted contract and change record, actual caller…" | **No edit required.** Still true for the two remaining workspaces. Note in the PR that the renderer's integration is exactly the event this sentence anticipates |
| `docs/architecture.md:391` | "three independently buildable source workspaces imported from four sources" | "two independently buildable source workspaces imported from three sources" |
| `docs/architecture.md:394` | "- report rendering;" | **Delete the bullet** |
| `docs/architecture.md:397-417` | not-a-caller list; entry conditions | Unchanged. Add nothing here about the renderer — its new home is the renderer subsection |
| `docs/architecture.md` implementation map (L496-512) | responsibility → current source | **Add rows** for the moved render engine, in the same commit as the code move, naming real post-move paths only |
| `docs/architecture.md` source-and-generated-material roles (L518-530) | path/role/qualification table | **Add** a `design/assets/report-renderer/` row and, if the GUI is retired, resolve what happens to `design/assets/report-renderer/gui/**` |
| `docs/architecture.md` integration boundaries (L351-417) | existing `###` subsections | **Add one `###` subsection** as the canonical home for the folded render contract: engine, pipeline, contracts by name, the 12 template IDs, the seven block types, density behaviour, and the limits list |
| `docs/operations.md:52` | `poppler-utils` row citing `workspaces/report-renderer/scripts/visual-regression.ps1` | The cited script dies with the workspace. Either repoint to the new path if the script is migrated to `scripts/`, or **delete the row** — with no consumer, `poppler-utils` is not a Linux capability advantage for this repository |
| `docs/operations.md:53` | `fonts-liberation`/`fonts-dejavu-core` row justified by "the renderer's container image" | After retirement there is no renderer container image. Repoint to whichever image actually installs them **only if that is true**; otherwise restate as a local-render font-metric prerequisite with no container claim. Do not name an image that does not install the fonts |
| `docs/operations.md:66` | "`scripts/email-eval-desktop` and `CollisionRenderer.Gui` — These target `net10.0-windows` with Windows Forms and WinUI 3 respectively." | **Factual error — fix required.** Per `docs/index.md:30-32` this must be fixed in the same commit it is noticed, i.e. the first commit of this work, independently of whether the migration proceeds. When the GUI is retired the row narrows to `scripts/email-eval-desktop` alone |
| `docs/operations.md:378` | the renderer validation command line | **Delete the line.** Three lanes remain. The absent `--locked-mode` was correct: the workspace has no lock files. When the projects join `src/` they must gain them, because every `src/` and `tests/` project has one and the build action and browser cache key depend on them |
| `docs/operations.md:374,383` | "Source workspaces validate independently…"; "These checks prove only the imported source snapshots." | Unchanged; still true for three remaining lanes |
| `docs/operations.md:1052` | deferred-seams row "PDF-engine replacement" | Keep the row; its subject shifts from "choosing an engine" to "replacing the accepted engine". Add one qualification naming headless Chromium via Playwright as the accepted engine. Do not delete the row — the "no parallel permanent engines" rule is still operative |
| `docs/operations.md` safety rules (L756-793) | existing rules | **Add** the preserved `NOTICE.md` operator statements: customer reports and extracted text are never committed; generated PDFs, rasters, screenshots and inventories live under ignored `artifacts/`; customer payloads are never used as tests, starters or examples; synthetic non-identifying fixtures only; documentation may describe reference families but never reproduces sensitive filenames or case facts. Verify each against the existing text first and add only what is genuinely absent |
| `docs/design.md:160-164` | logo consumers naming the workspace Core and Gui | Repoint the first bullet to the post-move engine path; **delete** the GUI bullet. The checksummed Web copy bullet is unaffected |
| `docs/design.md:230-240` | the Web and renderer boundary five-row table | Rewrite all five rows: master-logo consumer becomes the in-application engine; templates/stylesheet consumer path updated with "not Web shell assets" retained; signatures row retained with the updated consumer; **the GUI package-assets row resolves** — its own text says "remove when that GUI is decommissioned during Pegasus integration"; the "Imported renderer, prompt, model, skill and AI material" row narrows to prompt/model/skill/AI material only. Also update L240 so relocation still does not prove the capability, without the word "imported" |
| `docs/design.md:665-669` | source-and-runtime map rows | Same treatment: repoint paths, delete the GUI-assets row, narrow the imported-source row |
| `docs/design.md` (additions) | — | **Add** the folded house-style invariants: letterhead and footer are never drawn by a body template; the exact footer strapline and the fee-note VAT swap; the reference-block fallbacks; the fixed A4 margins; repeated table headers and unbreakable blocks; "use the existing table and block CSS classes rather than inventing new ones"; bundled signature keys are a fixed governed set; body copy uses Arial or a metric-compatible substitute such as Liberation Sans; proprietary fonts are never added without a recorded licence check |
| `docs/requirements.md:893-896` | "Reports are produced from accepted case facts and source-labelled evidence through the approved renderer boundary. Renderer source workspaces remain independent source imports until an accepted integration contract and real application caller exist." | Sentence 1 unchanged. Sentence 2's subject ceases to exist. Replace with wording that keeps the same restriction without naming a workspace: relocating renderer source into the repository does not create the approved renderer boundary; report production remains unactivated until an accepted Core-owned render contract and a real application caller exist. **Requirements outranks architecture** — do not let this sentence become weaker than the capability notes |
| `docs/requirements.md:949-950` | signatures are provenance-sensitive document assets | **No edit required**; true before and after |
| `docs/capabilities.md:248` (`EXT-08`) and `:263` (`RPT-01`) | notes columns | See section 5. `RPT-02`..`RPT-05` carry no renderer-source claim and need **no edit**; do not add one |
| `docs/index.md:21` | "What do the imported source workspaces own? → Workspaces" | **No edit required**; two workspaces remain |
| `.gitattributes:4-5` | `design/assets/report-renderer/**/*.{css,scriban} text eol=lf` | **No edit required by the retirement** — these paths are under `design/`. Edit **only** if the seam plan renames or moves the asset directory, and then in the same commit, or the line-ending normalisation silently stops applying |
| `.github/workflows/workspaces.yml:35-42` | the `Validate report-renderer workspace` step | **Delete the step; keep the workflow and its other three steps.** This step is the only one in that workflow that installs a browser; nothing else there depends on it. The `paths: ["workspaces/**"]` trigger is unchanged, so the deletion commit itself runs the workflow |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs:123-149` | the literal seven-path solution array | Must change **in the same commit as any new project added to `Pegasus.slnx`** — that is the code move's commit, not a documentation commit. The `DoesNotContain(... "workspaces/")` assertion stays and becomes *more* true |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs:151-169` | `ApplicationProjectsDoNotReferenceSourceWorkspaces` | **No change** |
| `reference/README.md:22` | "- `rendererref1/` — report-renderer reference material." | The 14 tracked files stay. Reword so it does not name a retired workspace, e.g. "report-rendering reference material: supplied sample reports, design spec, and signature/logo source. Evidence only" |
| `CONTEXT.md` | — | **No edit required.** Verified: no renderer, workspace or `report-renderer` term |
| `workspaces/ai-centre/README.md:58,109,144` | three references naming `workspaces/report-renderer` | **Required, and not previously listed.** Repoint each. L109's "`report-renderer` package proposal" refers to an upstream AI Centre package proposal, not this workspace — verify before touching it |
| `workspaces/ai-centre/docs/architecture.md:29,50,109` | dependency table row, Mermaid node `Report["workspaces/report-renderer"]`, dependency bullet | **Required, and not previously listed.** The Mermaid node label is a string, so the link checker will not catch it — check by grep, not by CI |
| `workspaces/ai-centre/skills/**` | ≈20 `collisionrenderer` connector references across 10 packages | **Do not touch.** Protected external source per root `AGENTS.md`. They describe an upstream MCP connector, not this repository's tree. Record this exclusion explicitly in the PR |
| `docs/adr/0009-adopt-pegasus-monorepo-workspaces.md:34` | "`report-renderer/`, imported from `collisionrenderer`; and" | **Immutable body — do not edit.** A dated record of what was imported |
| `docs/adr/README.md:43-44` | ADR-0009 and ADR-0010 index rows | Index maintenance is explicitly permitted. Add reviewed status metadata noting that the report-renderer import was retired into the application by the new ADR, without changing either decision's meaning. Add the new ADR's own row |

## 4. `workspaces/README.md` retirement mechanics

Two rows describe the renderer and they retire differently.

**The integration-status row is deleted outright.** The table "is the sole
register for each workspace's role, current integration status, activation
conditions, and owner". Integration status is a property of a present workspace.
Once the directory is gone the row would assert a status for nothing, and its
`[Workspace owner](report-renderer/README.md)` link would be the one dangling
relative link in the repository. There is no "Retired" row: a status register
that accumulates absent entries stops being a register. The non-caller claim the
row carried does not disappear — it moves to the capability notes, where
activation claims are adjudicated.

**The provenance row is import lineage and must not simply vanish.**
`docs/design.md:671` says git history "does not replace caller, deployment or
acceptance evidence", so "it is in the history" is not an acceptable answer for
provenance either. The row records six facts that can never be reconstructed from
the tracked tree once it is deleted: upstream repository, branch, commit, source
path, and the import manifest's file count, byte count and SHA-256.

Recommended mechanism, in preference order:

1. **Copy all six fields verbatim into a provenance clause of the promoted root
   ADR, in the commit that creates that ADR — which must land before or with the
   deletion.** Root ADR bodies carry dated provenance and are immutable, which is
   exactly the durability property wanted. The ADR that accepts the integration is
   the natural place to record what was integrated and from where.
2. Add a one-line pointer in `docs/architecture.md`'s source-and-generated-material
   roles so the lineage is reachable from current-state documentation without
   duplicating the hashes.
3. Amend `workspaces/README.md` with the two sentences listed in the edit table,
   so the register itself explains what happened to retired imports.

Explicitly rejected: keeping a "retired" provenance row in the live register;
relying on git history alone; and creating a new Markdown file to hold retired
provenance (forbidden by `docs/index.md:3-5`).

One consequence to state in the PR: the manifest was computed over the **import**
snapshot, and the register already records that the current tracked tree differs
where post-import documentation corrections were accepted. The frozen hash
therefore proves what arrived, not what is being moved. Do not present it as a
verification of the moved files.

## 5. Capability-note updates

The governing rule is the evidence tiers in `docs/operations.md`. Relocating
source is at best tier 1, which that section says "proves consistency only". It
is not tier 5. Nothing in this migration changes any status column or release
target: `EXT-08` stays `Later / 1.1.0`, `RPT-01`..`RPT-05` stay `Later / 1.1.0`.

**Stage A — relocation only (this task and the code move).**

- `EXT-08` note (line 248), replacing "Imported renderer source is not
  activation; …": *Relocating renderer source into the application tree is not
  activation: a compiled engine with no accepted Core render contract, no
  application caller, and no produced report artifact is tier-1 evidence only.
  Versioning, correction, caller, validation, recovery, and acceptance remain
  required.*
- `RPT-01` note (line 263), replacing "Imported renderer source is non-caller
  evidence until separately activated": *Relocated renderer source is non-caller
  evidence. A green build or an engine unit test proves the engine, not that
  accepted Core-owned data reaches it; RPT-01 stays unactivated until a real
  caller is exercised.*
- `RPT-02`..`RPT-05`: **no change.** They never claimed renderer-source status
  and adding a note now would imply progress that has not occurred.

**Stage B — activation (a separate, later task).** Only when a Core-owned render
contract is accepted, a real application caller is exercised at tier 5, and an
artifact with version identity and hash is persisted. The Stage B wording must
name what was actually exercised rather than saying "activated". Do not pre-write
it as accepted, and do not write it in the same PR as Stage A.

Wording discipline to hold in review: never write "the renderer is now part of
Pegasus" as if it were a capability claim; write what is true — the source is in
the tree, it builds, nothing calls it.

## 6. Documentation-link-integrity plan

`.github/workflows/ci.yml` job `documentation` runs
`./scripts/Test-DocumentationLinks.ps1` on `windows-latest` on **every** pull
request, with no path filter — it is the one lane that always runs. It stays on
Windows deliberately, because `Test-Path` is case-insensitive there.

What the script actually does (verified):

- Enumerates tracked `*.md` via `git ls-files`, excluding
  `node_modules|corpus|artifacts|.git|.claude|.agents|.codex`,
  `docs/temp-plans/` except its `README.md`, and `workspaces/ai-centre/skills/`
  except its `README.md`.
- Matches `[text](target)`; skips `http:`/`https:`/`mailto:`/`#` targets; strips
  any `#anchor`; resolves the remaining path relative to the containing file and
  fails if it does not exist.
- **Anchors are never validated.** A link to a real file with a dead `#anchor`
  passes. Bare backticked paths in prose are never validated either.

Links that break when the tree is deleted:

| Link | Where | Effect |
| --- | --- | --- |
| `[Workspace owner](report-renderer/README.md)` | `workspaces/README.md:16` | **The only inbound break.** Fixed by deleting the row in the same commit |
| 6 links in the workspace `README.md`, 1 in its `docs/ARCHITECTURE.md`, 1 in its `docs/DEVELOPMENT.md`, 11 in its `docs/adr/README.md` (19 total) | inside the deleted tree | No effect — the containing files are deleted, so the script never enumerates them |

Everything else that mentions the renderer does so in backticks, not as a
Markdown link: `.gitattributes`, the workspaces workflow, `docs/design.md`,
`docs/operations.md`, `reference/README.md`, `docs/adr/0009`, all six
`ai-centre` document references, and the Mermaid node label. **CI will not catch
any of them.** They must be caught by the grep commands in section 8.

Keeping the job green:

1. Delete `workspaces/README.md:16` in the same commit as the tree deletion.
2. Create the promoted root ADR file **before or in the same commit** as the
   first index row and any `architecture.md` link to it. A documentation-only
   commit that links a not-yet-created ADR fails the lane on every subsequent PR
   until fixed.
3. Add no new relative link to any path the same commit does not create.
4. Check anchors by hand, because CI cannot.
5. This plan file itself contains **no relative Markdown links**, per the
   temp-plan contract, even though `docs/temp-plans/` is excluded from the
   checker.

## 7. Ordering

**Commit 1 — independent, land first.** The `docs/operations.md:66` factual
correction. The authority rule requires fixing a losing document in the commit
that notices the conflict, and this plan notices it. It must not be bundled with
the migration, because it is true whether or not the migration proceeds.

**Commit 2 — the promoted root ADR.** Creates the ADR with the section 2 clauses,
the retired-decisions clause, the frozen provenance clause from section 4, and
the `NoWarn` decision (blocked on the operator answer). Adds its index row in the
same commit so no link dangles. **Must precede Commit 4.**

**Commit 3 — with the code move (owned by the seam plan, listed for sequencing).**
`Pegasus.slnx` project list, the `DependencyDirectionTests` literal path array,
per-project `packages.lock.json` files, the scoped Scriban `NoWarn` (or the
Scriban upgrade), and the `docs/architecture.md` implementation-map and
components rows. Code and its architecture rows land together or
`architecture.md` describes a tree that does not exist.

**Commit 4 — the deletion.** `git rm -r workspaces/report-renderer` together
with, in the same commit and no other: `workspaces/README.md` rows 16 and 25 plus
the two added sentences; the workspaces workflow step deletion;
`docs/operations.md:378`; `docs/architecture.md:391,394`;
`docs/design.md:162-163,230-240,665-669`; `reference/README.md:22`; the
six `ai-centre` document repoints. Splitting any of these from the deletion
leaves either a dangling link (CI red) or a document asserting a tree that is
gone (authority-rule violation).

**Commit 5 — the folds.** The renderer subsection in `docs/architecture.md`, the
design invariants in `docs/design.md`, the preserved operator statements in
`docs/operations.md` safety rules, the third-party component table, the
`docs/requirements.md:893-896` rewrite, and the Stage A capability notes.
Requirements outranks architecture, so if the rewrite and the folded prose
disagree, the requirements sentence wins and the architecture prose is corrected
in the same commit. This may merge into Commit 4 if the reviewer prefers one
atomic change; it must not precede it.

**Waits — not in this task.** Stage B capability wording; any
`operator-notes.md` change; any `.gitattributes` edit; the fate of
`design/assets/report-renderer/gui/**`; the `poppler-utils` row's final form.

## 8. Verification plan

This task can reach tier 1 only. Say so in the PR; claim nothing else.

**Tier 1 — static/build/architecture.**

- `dotnet build --configuration Release` — proves the moved projects compile
  under root `Directory.Build.props` with `TreatWarningsAsErrors=true`, which is
  the concrete test of the `NoWarn` decision.
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release`
  — both workspace guards must pass with the updated solution list.
- `./scripts/Test-DocumentationLinks.ps1` — zero broken links; the file count
  drops by the 17 deleted Markdown files.

**No-dangling-reference greps.** Run each from the repository root.

```
git grep -n -I -i "report-renderer" -- . ":(exclude)design/assets/report-renderer/**"
git grep -n -I -i "CollisionRenderer" -- . ":(exclude)workspaces/ai-centre/skills/**"
git grep -n -I -i "collisionrenderer" -- . ":(exclude)workspaces/ai-centre/skills/**"
git grep -n -I "CR_API_TOKEN" -- .
git grep -n -I "CollisionRenderer.sln" -- .
git ls-files workspaces/report-renderer | wc -l
```

Expected:

| Command | Expected |
| --- | --- |
| `report-renderer` grep | Only `.gitattributes:4-5` and any updated `docs/design.md` rows that legitimately name `design/assets/report-renderer/`. Zero hits under `workspaces/`, `.github/`, `docs/` (except the immutable `docs/adr/0009:34`), `scripts/`, `src/`, `tests/`, `infra/` |
| `CollisionRenderer` / `collisionrenderer` greps | Zero outside `workspaces/ai-centre/skills/**` and `docs/adr/0009:34` |
| `CR_API_TOKEN` grep | **Zero.** Any hit means the retired authentication decision leaked into the monolith |
| `CollisionRenderer.sln` grep | Zero |
| `git ls-files workspaces/report-renderer` | `0` |

Because `.gitattributes`, workflow YAML, PowerShell scripts and Mermaid labels
are invisible to the link checker, run the greps explicitly against them:

```
git grep -n -I -i "report-renderer\|CollisionRenderer" -- .gitattributes .github scripts infra azure.yaml
git grep -n -I -i "report-renderer" -- design docs tests src workspaces
```

**CI evidence.** The `documentation` lane runs on every PR and must be green. The
`source-workspaces` workflow triggers on `workspaces/**`, so the deletion commit
itself runs it; it must be green with exactly three remaining steps.

**Manual checks CI cannot do.**

- Read `docs/operator-notes.md` for any statement this migration would
  contradict. Verified at planning time: it contains no renderer or template
  statement, so nothing there changes. Re-verify before merge and **stop for the
  user** if that changes.
- Confirm every preserved operator/business statement from `NOTICE.md` L56-77
  appears in its new owner with unchanged meaning, by side-by-side reading, not
  by keyword search.
- Confirm the `#integration-status-register` and any other anchor targets still
  resolve.

**Not proved by this task:** tiers 2-12. No Core render contract, no caller, no
persisted artifact, no browser or corpus evidence, no deployment, no operator
acceptance.

## 9. Non-goals, stop conditions, and open questions

### Non-goals

- Moving, renaming or rewriting any renderer source file. The seam plan owns that.
- Defining or accepting the Core-owned render contract.
- Activating `EXT-08` or any `RPT-*` capability, or changing any status column or
  release target.
- Deciding the .NET 10 uplift or whether the `.Api`, `.Cli`, `.Gui` or `.Mcp`
  hosts survive. This plan records what each outcome implies; it does not choose.
- Editing any published root ADR body, including `docs/adr/0009:34` and root
  ADR-0010.
- Editing anything under `workspaces/ai-centre/skills/`.
- Changing the meaning of any `docs/operator-notes.md` statement.
- Creating any new Markdown file other than the one promoted root ADR and this
  transient plan.
- Deleting `workspaces/README.md`, `workspaces/AGENTS.md`,
  `docs/architecture.md`'s Workspaces section, or
  `.github/workflows/workspaces.yml` — all four are **edited**.

### Stop conditions

- **S1.** The `NOTICE.md` third-party licence tables have **no canonical owner**
  in this repository. There is no root `NOTICE`, and `docs/index.md:3-5` forbids
  creating one. Do not silently drop licence conclusions, and do not create a
  root notice file without explicit authorisation.
- **S2.** The `NoWarn NU1901–NU1904` decision. Do not carry the suppression into
  `src/` without an operator choice.
- **S3.** Any conflict discovered with `docs/operator-notes.md`.
- **S4.** Any statement in the five workspace documents that turns out to be an
  operator or business statement rather than an engineering one, and for which no
  canonical owner accepts it without a meaning change.
- **S5.** If the promoted root ADR would need to amend an existing root ADR body
  to be coherent. That requires explicit direct user instruction naming the ADR.

### Open questions for the operator

1. **Scriban advisories.** Upgrade Scriban past `5.12.1` if a release without
   NU1901–NU1904 exists, or scope the suppression to the one project that
   references it? (Root `NoWarn` is not offered.) Blocks the code move.
2. **Third-party licence facts.** Where do the surviving licence conclusions land
   — a table in `docs/architecture.md`, or an authorised root notice file? Blocks
   Commit 5.
3. **ADR shape.** One consolidated root ADR with clauses (recommended), or
   separate ADRs per promoted decision? Blocks Commit 2.
4. **`visual-regression.ps1` and `poppler-utils`.** Does the visual-regression
   script migrate to `scripts/`, or die with the workspace? Determines whether
   `docs/operations.md:52` is repointed or deleted.
5. **GUI assets.** If `CollisionRenderer.Gui` is retired, do the files under
   `design/assets/report-renderer/gui/**` get deleted, or retained as brand
   source? `docs/design.md:237` says "remove when that GUI is decommissioned
   during Pegasus integration", which reads as a decision already taken — confirm.
6. **Fonts.** Does any Pegasus image install `fonts-liberation` and
   `fonts-dejavu-core`? `docs/operations.md:53`'s current justification
   disappears with the workspace, and the row must not name an image that does
   not install them.
7. **Provenance home.** Confirm that freezing the renderer's import provenance in
   the promoted root ADR is the accepted mechanism, rather than a retired-imports
   subsection in `workspaces/README.md`.
8. **Template schema documentation.** `TEMPLATES.md` L86-399 is roughly 300 lines
   of hand-maintained payload schema. This plan retires it in favour of code plus
   validators as the authority. Confirm, or name the canonical owner that should
   carry it.
