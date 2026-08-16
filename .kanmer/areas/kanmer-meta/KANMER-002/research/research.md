# Research — KANMER-002: Repo plan-doc cleanup and organization into Kanmer

Date: 2026-08-14, against `origin/dev` @ c99a7c1a in worktree `.worktrees/KANMER-002`. Three parallel explore agents covered: (1) `docs/temp-plans/` coverage audit, (2) `docs/operator-notes.md` canonical-coverage map (scope added by the operator this session: extract durable rules then retire the file if no longer needed), (3) structural survey — `design/` move, `reference/`, `artifacts/`, empty directories.

---

## 1. `docs/temp-plans/` — retirement audit

**Verdict: every one of the 21 files is retirable.** Each is either fully implemented/merged or fully absorbed into board tickets; git history preserves the record.

| File | Status | Evidence / covering tickets |
|---|---|---|
| README.md | meta | Retire after `docs/index.md:45` retarget + AGENTS.md workflow rewrite |
| case-custody-eva-export.md | Implemented | `864f46fc` merged; residuals TICK-022, TICK-119 |
| case-edit-lease-continuity.md | Implemented | `659abfa7` merged; residual TICK-183 |
| qdos-audit-intake-inbox.md | Implemented | `73a3380d` merged; residuals TICK-112, BUG-001 |
| qdos-forward-intake-failure.md | Implemented | `1bbce75b` merged; residuals TICK-112, TICK-113 |
| upload-case-creation-and-inbox.md | Implemented | `dd6e35da`, `71b3d747` etc. merged; absorbed into capability tickets |
| retire-now-rewrite-agents.md | Implemented | SIMPLI-002/004/005/006 all done |
| report-renderer-workspace-uplift.md | Implemented | PR #340 (`f9e4313b`) |
| report-renderer-integration.md + 12 satellite files | Not implemented **by design** — superseded planning | Capability rows individually ticketed: TICK-081, 096–100, 203–216; runtime-uplift + desktop-removal satellites delivered via the workspace-uplift task |
| simplify/simplify.md | Triaged onto board | SIMPLI-001–014 map 1:1 to its phases |

**Inbound references (exhaustive):** exactly one literal link breaks — `docs/index.md:45` → `temp-plans/README.md`. Plus three non-link textual dependencies: `AGENTS.md:111` (temp-plans as a valid new-Markdown home) and `AGENTS.md:207-208` (step-3 root-plan mandate at `docs/temp-plans/<slug>.md`), and `scripts/Test-DocumentationLinks.ps1:14` (regex carve-out `^docs/temp-plans/(?!README\.md$)`). Nothing in capabilities/ADR/PRD/FRD/operations/src/tests/workflows/CLAUDE.md cites any individual plan file.

**Gap flag (only one):** `report-renderer-integration-seam.md` / `-docs-migration.md` / `-skills-surface.md` all assume the renderer is absorbed into `Pegasus.Core`/`Pegasus.Infrastructure`, while in-progress **SIMPLI-014 goes the opposite direction (extract renderer standalone, like SIMPLI-001 did for ai-centre)**. Nothing on the board records that the integration-direction plans are superseded. Proposed ticket before deletion: *"Reconcile report-renderer integration-into-src/ vs SIMPLI-014 standalone-extraction direction."*

**Structural note:** `docs/temp-plans/simplify/` contains only `simplify.md` (sibling `adr-consolidation.md` already deleted in PR #374), so the subdirectory empties with it.

---

## 2. `docs/operator-notes.md` — canonical-coverage map

**Coverage is very high but not complete.** Nearly all ~30 sections are Class A (fully restated in a canonical owner — every cited FRD anchor verified to exist and carry matching rules; no dangling citations) or Class B (provenance/confirmation records whose value is attribution, not an uncovered rule). Highlights: Triage → frd-03; chasing → frd-01; findings/correction → frd-06; routing → ADR-0008 + frd-09; mailbox taxonomy → frd-08; staff roles → frd-04; interface language → design.md; PdfPig rule → ADR-0001/0003; Box custody → frd-05 + open-decisions + operations.

**Class C — six statements with no canonical home (the extraction backlog):**

1. **Development data boundary** (:395-399) — supplied data permissible for dev; PII/DPIA/retention out of scope; never create synthetic test data. (Present in AGENTS.md, which is the routing table's correct home for a repo rule — needs only an explicit acceptance of that home.)
2. **Naming convention** (:401-403) — logical purpose-at-a-glance names; reserved terms never reused. (Half-covered in engineering.md, non-canonically.)
3. **Operating hours** (:475-478) — ingestion/processing continuous; staff use business-hours but app available outside them barring planned maintenance.
4. **Support and incident response** (:479-486) — Alex first-line; alert recipients extendable via monitoring config; acknowledgement expectations; emergency-access roster. **Discrepancy to resolve during extraction:** operations.md shows budget/cost alerts route to `digital@collisionengineers.co.uk`, not "Alex" by name.
5. **Commercial and licensing constraints** (:487-493) — no fixed budget; lowest practical tiers; reuse existing licences; confirm entitlement per vendor integration. (The £75/month alert figure already lives in open-decisions/operations; the *policy* doesn't.)
6. **Data residency and region** (:494-497) — region/UK-residency not a requirement; UK South a chosen default (ADR-0015), not an operator constraint. ADR-0015 doesn't state this framing.

**Secondary findings:** the CAP-001..022 ID namespace is dead — capabilities.md replaced it wholesale with the 231-ID scheme (content survives; tokens don't). Stale self-claim at "Additional recorded operator statements": the Box audit-subfolder-nesting bullet *is* now canonically recorded (frd-01). Dangling citation at :261 → `reference/reports/repairer-identity-and-case-party-roles.md` (deleted 2026-08-02, CHANGELOG #216).

**Inbound references (~20):** governing definition in AGENTS.md/CLAUDE.md (three mentions + routing table + safety rail); docs/index.md authority chain; PRD/FRD README banners; **frd-08 lines 6/12 structurally defer the mailbox inventory to operator-notes and link its `#confirmed-mailbox-categorisation` anchor**; design.md, current-architecture.md, open-decisions.md, runbook.md, reference/README.md; **five `workspaces/document-extraction/docs/*.md` files with live cross-links**; temp-plans files (moot on retirement); board: TICK-201 (todo — mandates preserving operator-notes statements), SIMPLI-004/006 impact notes. **No code, test, or script depends on the file.**

**Verdict:** before retirement — (a) land the six Class-C statements in accepted homes (two belong in AGENTS.md/runbook per the routing table; the four business-authority ones fit operations.md or a short PRD "operating envelope" subsection — operator's placement call); (b) update the inbound references, especially frd-08's deferral line and the five document-extraction workspace docs; (c) **retire = demote to a provenance appendix rather than delete** — the provenance sections are self-declared attribution records, and live anchors point into the file.

---

## 3. `design/` folder move (→ `docs/design/`, with `docs/design.md` becoming `docs/design/README.md`)

Inventory: 12 tracked files, 3.9 MB — Scriban report templates + report.css, brand logo (checksum-pinned), 3 engineer signature PNGs, 3 UI mockup rasters. No README today; the binding authority is `docs/design.md` (1,140 lines) — "README.md as authority" is the post-move state.

**Build-breaking dependents (must change in the same commit):**
- `workspaces/report-renderer/src/CollisionRenderer.Core/CollisionRenderer.Core.csproj:20,24,28` — relative `EmbeddedResource` includes `..\..\..\..\design\...` (templates, logo, signatures).
- `workspaces/report-renderer/Dockerfile:10` — `COPY design/ design/` (repo-root build context).
- `.gitattributes:4-5` — eol normalization globs for `design/assets/report-renderer/**` (silent drift, not breakage).

**Docs/link surface:** `docs/index.md` (4 links); `docs/current-architecture.md:594`; `docs/design.md`'s own mockup links (`../design/references/...` — path math changes when it becomes README inside the folder) plus 5 bare-text path mentions; **13 FRD files** with the plain-text header line `UI behaviour: docs/design.md`; `reference/README.md:9`; `workspaces/report-renderer` NOTICE + 3 docs + 4 workspace ADRs (immutable — flag for decision, don't silently edit); `workspaces/document-extraction` 2 docs; **6 `src/Pegasus.Web` files citing `docs/design.md#anchors` in CSS/cshtml comments**; AGENTS.md/CLAUDE.md governance table. CHANGELOG mentions are historical — leave.

**Safety net:** `scripts/Test-DocumentationLinks.ps1` catches all bracket-syntax link breaks but **not** bare-text mentions (FRD headers, AGENTS.md, code comments) — those need the enumerated manual pass above. **Sequencing:** retire temp-plans first — it holds ~90 of the design-path references and deleting it first shrinks the move's edit surface massively.

---

## 4. `reference/` folder

63 files, 19 MB, tracked. Authority: evidence-only (its README defers all behavioural authority); created by superseded ADR-0023; operator-notes' EVA row links it as "canonical reference authority" *for what counts as raw evidence*.

- **Byte-identical duplicate** (SHA-256 `25f7e2c6…`, both 1,311,863 bytes): `workproviders-and-repairers/contacts/providers.xlsx` ≡ `workproviders-and-repairers/providers.xlsx`. Undocumented — delete one after checking inbound path references.
- **Deliberate duplication, keep by default:** `rendererref1/` logo + 3 signatures ≡ `design/brand/*` — explicitly documented as intentional at `reference/README.md:22-26` ("reference preserves supplied evidence grouping; design owns runtime use"). Removing them means reversing a documented decision. Note: that same README passage needs a path update when `design/` moves.
- **Dangling citation:** operator-notes.md:261 → deleted `reference/reports/repairer-identity-and-case-party-roles.md` (cosmetic fix; fold into the operator-notes work).
- Everything else (EVA schema, eva_information, 2 retained reports, provider spreadsheets) is still cited and retained-by-design — no further cleanup found.

---

## 5. `artifacts/` folder

**Entirely gitignored** (`.gitignore:21`) — it does not exist in the task worktree; cleanup is **local-disk housekeeping in the main checkout, invisible to git and to any PR**. ~2.3 GB surveyed there.

- **The "likely already implemented" planning folder:** `artifacts/planning/remainder-delivery/…/REMAINDER_DELIVERY_PLAN.md` (8-wave CollisionSpike v2 plan). Waves 0–4 (spine, Identity, Box custody, Outlook/Graph worker) verifiably match shipped `src/` code and migrations. **Wave 6 (Provider API + Staff MCP) has no code yet** — before deleting, confirm waves 5–8 scope survives on the board (integration-provider-api area TICK-058..061 and the mail-worker area tickets appear to cover it).
- **Safe local deletions:** `plan-audit/`, `inventory-validation-2872b192…/`, `provider-directory-review-019f93f0/`, empty `retirement-fixture-codex/`, and one of the two redundant `dotnet-ef` tool dirs (`dotnet-tools/` vs `tools/` — neither referenced by any script; the tracked manifest is `.config/dotnet-tools.json`).
- **Prunable if space matters:** `test-results/` (76 MB, regenerated), `releases/0.1.0-alpha.1/` (265 MB, shipped-build archive).
- **Keep (active):** `reference-data-staging/` (script target of `Build-ProviderReferenceData.ps1:46` — empty ≠ dead), `intake/` (1.9 GB local content store), `local-development/`, `evaluation/` (recent QDOS cohort results).

---

## 6. Empty directories

**Zero** in the worktree (verified two ways, dotfolders included). The only empty dirs on disk are under gitignored `artifacts/` in the main checkout and are covered in §5. If the ticket meant a broader sweep than the tracked tree, that needs the operator to name the location.

---

## Board cross-check

The board tree has no ticket `refs[]` or body citing `design/`, `docs/design.md`, `reference/`, or `artifacts/` as a path dependency — this ticket's moves create no board-retargeting work (unlike [[KANMER-001]]). Coverage-relevant tickets found: TICK-201 (operator-notes preservation constraint), SIMPLI-014 (renderer direction conflict, §1), SIMPLI-001 research/impact (generic mentions only).
