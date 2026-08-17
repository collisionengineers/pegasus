# Research — SIMPLI-015 integration direction preserved from KANMER-002

## Operator decision

On 2026-08-14 the operator chose integration of the report renderer and document extractor into Pegasus, not extraction into standalone repositories/packages. This supersedes the contrary direction in SIMPLI-013 and SIMPLI-014.

## Preserved content from the retiring renderer plan set

The deleted `docs/temp-plans/report-renderer-integration*.md` set explored and resolved these implementation seams:

- keep Pegasus.Core as business-policy owner; workspace code cannot become an application caller merely by being present;
- integration requires an accepted thin ADR, explicit project/solution references, composition-root registration, and caller-backed tests;
- embed or explicitly copy governed templates, report CSS, logo and signatures from the canonical design tree; pin logical resource names and verify the complete resource set to prevent silent drift;
- retain the current renderer workspace ADRs as workspace history until the integration ADR deliberately supersedes the relevant mechanism; do not rewrite historical decisions mechanically;
- consolidate renderer MCP/tool surfaces rather than shipping duplicate hosts; production execution location, distribution boundary and authorization remain decisions to resolve in this ticket;
- migrate current architecture, operations, engineering/runbook and workspace documentation only when the real caller lands;
- preserve the 2026-08-03 resolution that the GUI host was removed, .NET 10/runtime uplift was completed, and unaccepted wording/assets remain fail-closed;
- renderer capability and decision coverage remains discoverable through TICK-203–TICK-216, all related to this ticket via the consolidation owner established by KANMER-001.

## Still-live work for SIMPLI-015

1. Write the accepted integration ADR and update the owning FRD for behavioural consequences.
2. Re-scope/archive SIMPLI-013 and SIMPLI-014 with explicit migration notes.
3. Select the application seam, project dependency direction, DI registration and production execution boundary.
4. Define MCP/tool consolidation and authorization.
5. Implement caller-backed build/test/runtime proof before adding either workspace to Pegasus.slnx or deployment.
6. Update current-state docs only after implementation/deployment evidence exists.

## Provenance

This summary was created by KANMER-002 immediately before retiring the temporary renderer plan files. Git history remains the complete verbatim record; this document is the actionable durable handoff.

## 2026-08-17 assessment — standalone repo/package vs integration (claude-code)

Operator reopened the question on 2026-08-17. Independent codebase survey (facts, then verdict). This confirms the 2026-08-14 direction — **integrate both** — with a sharper shape for each.

### Facts that decide it

| | document-extraction (CollisionDocNet) | report-renderer (CollisionRenderer) |
| --- | --- | --- |
| Pegasus already has this capability? | **Partly.** `IIntakeSourceReader` → `MimeKitPdfPigOpenXmlIntakeSourceReader` (PdfPig 0.1.15, MimeKit, DocumentFormat.OpenXml) governed by ADR-0001/ADR-0003. But it **does not extract `.doc` or `.msg`** — they are "retained for manual sorting" (`src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs:118`). CollisionDocNet extracts both. | **No.** No render port, no PDF/HTML engine anywhere in `src/`. Report production is a core product capability with no implementation. |
| Coupling to Pegasus repo | None — self-contained, zero PackageReferences, own lock files, already NuGet-shaped (`PackageId CollisionDocNet.Extraction`, release script). Trivially extractable. | **Hard-coupled.** `CollisionRenderer.Core.csproj:20-33` embeds `docs/design/assets/report-renderer/templates/**`, `docs/design/brand/logos/**`, `docs/design/brand/signatures/**` via `..\..\..\..\`; `Dockerfile` builds from the Pegasus repo root. Report templates *are* Pegasus product behaviour (expert report, fee note, valuation evidence). |
| Other consumers today | None. Pegasus is maintainer of record. | CLI, HTTP API project, MCP/MCPB win32 bundle for Claude Desktop — but no live consumer is evidenced; MCP disposition is SIMPLI-012's call. |
| Package-feed infrastructure | **None** — no `nuget.config`, no private feed, no central package management. A standalone package means standing up GitHub Packages/Azure Artifacts + CI/local auth + release-and-bump on every change. | Same. |
| ADR-0009 anticipated shape | "adapts the imported library behind `IIntakeSourceReader`" (project integration behind a Core port). | "consumes a Core-owned render contract". |
| ai-centre precedent (SIMPLI-001 → standalone) | Not comparable: ai-centre is *not* an application dependency (skills/experiments). Both of these are meant to be *called* by Pegasus. | Same. |

### Verdict

**Renderer → integrate.** It is Pegasus's own reporting capability, has no competitor in `src/`, is already welded to the canonical design tree, and its templates must co-version with the FRDs/Core policy that feed them. Shape: Core-owned render port; `CollisionRenderer.Core` (Scriban + Playwright + PDFsharp) becomes the Infrastructure adapter; templates/brand/signatures move to embedded resources under the design authority (pin logical names, verify the complete set); retire `CollisionRenderer.Api` (Pegasus.Web replaces it); consolidate MCP tools into Pegasus.Web's existing `ModelContextProtocol.AspNetCore` host or drop/defer per SIMPLI-012 — the win32 MCPB can remain a separately buildable *project in this repo* if a Claude Desktop channel survives; it does not need a separate repo. Open sub-decisions carried from TICK-215/214: where rendering executes in production (Worker container with the Playwright base image is the obvious candidate) and the MCPB distribution boundary.

**Extractor → integrate, but only through a real caller.** The honest reason to bring it in is the `.doc`/`.msg` gap; that is the caller-backed proof the repo invariant demands. Precondition: an ADR that resolves the overlap with ADR-0001 ("do not implement the PDF file format in Pegasus code") and ADR-0003 (PdfPig fixed) — either (a) scope CollisionDocNet to `.doc`/`.msg` (PdfPig stays the PDF path; one PDF implementation), or (b) replace the PdfPig/OpenXml path with parity evidence on the corpus. Two live PDF text extractors is a duplicate-implementation stop condition; pick one in the ADR. Not alpha-critical unless `.msg`/`.doc` intake volume for QDOS says otherwise — sequence accordingly under HZN-003.

**Why not standalone package for either:** single consumer, same owner, active co-development, no feed infrastructure, and a package can always be *produced from* this repo later (`dotnet pack` a project) if a second consumer appears — integration does not foreclose packaging; extraction now only adds release/bump friction and, for the renderer, would split design authority across repos or duplicate brand assets.

**Consequences for the tickets:** SIMPLI-013 and SIMPLI-014 are titled in the wrong direction. Re-scope (not archive) them as the two implementation tickets — "Integrate CollisionDocNet behind IIntakeSourceReader for .doc/.msg" and "Integrate CollisionRenderer behind a Core render contract" — each blocked by this ticket's ADR. Mechanics to plan under this ticket: home for the projects (`src/`, not `workspaces/` — once referenced they are no longer non-caller imports; a new top-level dir needs its own ADR), `TreatWarningsAsErrors=true` reconciliation, `Pegasus.slnx` + `DependencyDirectionTests.cs:139-172` updates, `workspaces.yml` retirement, ADR-0009 supersession (also stale on ai-centre), `packages.lock.json` for the renderer (TICK-212).
