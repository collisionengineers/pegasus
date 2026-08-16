# Impact — KANMER-002: Repo plan-doc cleanup and organization into Kanmer

## Change surface (tracked repo, branch `KANMER-002-repo-doc-cleanup` → PR to `dev`)

**A. temp-plans retirement** — delete `docs/temp-plans/` (21 files + emptying `simplify/` subdir); retarget `docs/index.md:45`; rewrite `AGENTS.md:111` (new-Markdown homes) and `AGENTS.md:207-208` (the step-3 root-plan mandate — superseded in practice by the Kanmer ticket doc pipeline; the rewrite must say so, since deleting the folder otherwise leaves governance mandating a location that no longer exists); update `scripts/Test-DocumentationLinks.ps1:14` carve-out. One new board ticket filed first: the renderer integration-vs-SIMPLI-014 direction reconciliation, so the supersession of the "absorb into src/" plans is recorded before their files disappear.

**B. operator-notes extraction/retirement** — extract the six Class-C statements (research.md §2) into accepted homes; update ~20 inbound references, most sensitively `docs/frd/frd-08…md:6,12` (which structurally defers the mailbox inventory to operator-notes) and the five `workspaces/document-extraction/docs/*.md` cross-links; fix the stale :261 citation and the stale "no other canonical document records them" claim; then demote the file per the operator's retire decision.

**C. design/ move** — `git mv design/ docs/design/` + `docs/design.md` → `docs/design/README.md`, with same-commit updates to: `CollisionRenderer.Core.csproj` (3 EmbeddedResource paths), `workspaces/report-renderer/Dockerfile`, `.gitattributes`, `docs/index.md` (4 links), `current-architecture.md`, the moved README's own mockup links (path math, not find/replace), 13 FRD `UI behaviour:` header lines, `reference/README.md`, workspace NOTICE/docs, 6 `src/Pegasus.Web` comment citations, AGENTS.md governance table. Workspace ADRs that mention old paths: flagged for decision, not silently edited (append-only convention).

**D. reference/ cleanup** — delete one of the byte-identical `providers.xlsx` copies. The documented logo/signature duplication stays unless the operator reverses that decision.

**E. artifacts/ cleanup** — local-disk only (gitignored; not in the PR): remove the one-off scratch dirs and one redundant tool dir; retire `planning/remainder-delivery/` only after confirming wave 5–8 scope is board-covered; keep the active runtime/evaluation dirs.

**F. Empty directories** — nothing to do in the tracked tree.

## Risk assessment

- **Protected file.** `operator-notes.md` carries the stop-for-user-resolution rail, and open ticket TICK-201 independently mandates preserving its statements. The operator's instruction this session authorizes the extraction *direction*; the plan must still present a statement-by-statement disposition map (every material statement → its surviving home) for sign-off before edits, and the retire step needs an explicit operator choice: **delete vs demote to a provenance appendix** (research recommends demote — live anchors point into the file, and the provenance sections have no other home).
- **Build breakage** is the only hard technical risk: the renderer csproj EmbeddedResource paths and Dockerfile COPY break the workspace build/container if missed. Verification must include the workspace build, not just the main `Pegasus.slnx` build.
- **Link-checker blind spots.** `Test-DocumentationLinks.ps1` catches bracket links only; the bare-text surfaces (13 FRD headers, AGENTS.md, code comments) rely on the enumerated manual pass in research.md §3. A final repo-wide grep for `design/`, `docs/design.md`, `temp-plans`, `operator-notes.md` is the acceptance check.
- **Governance self-reference.** Retiring temp-plans removes the very location AGENTS.md's task workflow mandates for root plans. The AGENTS.md rewrite is therefore *part of* phase A, not a follow-up — otherwise the repo briefly mandates writing files into a deleted folder. (This ticket's own root plan lives in its board folder, consistent with the new pipeline.)
- **AGENTS.md edit collision.** The kanmer-managed block at the top of AGENTS.md/CLAUDE.md is owned by kanmer-setup tooling; phase A/B edits must stay outside the managed markers.
- **Sequencing dependency (cost, not correctness):** A before C — temp-plans holds ~90 design-path references; deleting first shrinks C's edit surface. B is independent of A/C except that A deletes several operator-notes inbound referrers and C moves `docs/design.md`, which operator-notes cites — do B's reference updates against the post-A/post-C tree.
- **No board fallout.** Verified: no ticket refs/body cites the moved paths, so this ticket creates no KANMER-001-style retarget debt. Exception to watch: any ticket taken *during* execution that cites `docs/design.md` fresh.

## Task classification

**Not docs-only** — it changes `scripts/Test-DocumentationLinks.ps1`, a csproj, a Dockerfile, and `.gitattributes`. Full canonical verification applies: `dotnet restore`, `dotnet build --configuration Release` (solution **and** report-renderer workspace), link checker run, plus the renderer container build if feasible locally.

## Decisions needed before plan.md is final

1. **Operator-notes retire shape:** delete outright vs demote to provenance appendix (recommended), and the landing homes for the six Class-C statements — AGENTS.md/runbook for the two repo-rule items; `docs/operations.md` vs a PRD "operating envelope" subsection for operating hours / support / commercial policy / residency.
2. **Alert-recipient discrepancy:** reconcile "Alex receives alerts" against the configured `digital@collisionengineers.co.uk` recipient during extraction — which is authoritative going forward?
3. **Renderer direction ticket:** confirm filing the SIMPLI-014 reconciliation ticket (area: reports-renderer or simplify) before temp-plans deletion.
4. **Workspace ADR mentions of old design paths:** annotate, edit, or leave (append-only convention suggests leave-with-note).
5. **`rendererref1/` duplicated brand assets:** keep the documented duplication (default) or reverse it.
6. **artifacts/ deletions** are irreversible local deletes (no git safety net): confirm the §5 delete list, especially `releases/0.1.0-alpha.1/` (265 MB shipped-build archive) if space-pruning is wanted at all.

## Acceptance shape

- `docs/temp-plans/` gone; index/AGENTS/link-checker updated; reconciliation ticket filed; link checker passes.
- Six Class-C statements live in approved homes; all inbound operator-notes references resolve; the file is retired per the approved shape with no material statement lost (disposition map as evidence).
- `docs/design/` in place with README authority; solution + workspace builds green; repo-wide grep finds no stale path references outside CHANGELOG/history.
- Duplicate xlsx removed; artifacts cleanup executed per approved list (recorded in post-implementation report as local ops, not PR content).

---

## Decisions recorded (operator, 2026-08-14)

- **Decision 2 resolved — alert recipient:** `digital@collisionengineers.co.uk` **is Alex** — the operations.md configuration and the operator-notes "Alex receives alerts" statement describe the same recipient. The extraction records this equivalence rather than treating it as a discrepancy.
- **Decision 3 resolved — renderer/extractor direction:** both the report renderer and the document extractor are being **integrated into the repo**, not made standalone. [[SIMPLI-015]] filed to record the direction (governing ADR owed), re-scope SIMPLI-013/SIMPLI-014, and hold the planning content. Consequences here: research §1's gap flag reverses (the temp-plans integration plans were directionally right; the *standalone* tickets are what get re-scoped), and phase A must carry forward still-needed renderer planning content into [[SIMPLI-015]] before deleting the temp-plans set.
- **Still open before plan.md is final:** decision 1 (operator-notes retire shape — delete vs demote — and homes for the six Class-C statements), decision 4 (workspace ADR mentions of old design paths — note: with the renderer integrating into the repo, its workspace docs/ADRs will eventually fold into canonical docs anyway, which weakens the case for editing them now), decision 5 (`rendererref1/` duplicated brand assets), decision 6 (artifacts/ delete list confirmation).
