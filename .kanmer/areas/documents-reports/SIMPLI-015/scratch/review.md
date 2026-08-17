# Review — PR #389 (SIMPLI-015, docs-only) — 2026-08-17

Reviewer: independent subagent (no session context) commissioned by claude-code; claude-code authored and merges. Docs-only review: diff + description checked for missing/unauthorised scope and ADR conventions; both doc scripts run by the reviewer (links 220 files resolve; placement passed).

## Changes (reviewer's words)
New ADR-0025 (accepted; one decision — integrate when a caller exists, never extract; activation still gated by ADR-0009; mechanics deferred), its index row, two honest `workspaces/README.md` cells. Nothing unauthorised; nothing missed (`docs/capabilities.md` RPT owner cells stay FRD-11; `docs/index.md` routes via the ADR index; `current-architecture.md` as-built section unchanged).

## Comments
- N1 [fix-in-PR] status line should name the acceptor in ADR-0024's form → **fixed** `01f300f9`.
- N2 [fix-in-PR] "follow the ADR-0009 rule for new production projects" was imprecise (0009 fixes four projects, has no add rule) → **fixed** `01f300f9`: cites the AGENTS.md invariant; activating change folds into an existing project or reconciles the four-project boundary in its own ADR.
- N3 [note] "a project in this repository, referenced from `Pegasus.slnx`" is the definition of integrated, not a mechanics decision — accepted.
- N4 [note] TICK-209/210 omitted from the sub-decision list because they were proof tickets consolidated into SIMPLI-014 — correct; recorded in proof.
- N5 [note] no FRD-grade behaviour in Consequences; the one behavioural item is routed to FRD-05.
- N6 [note] ADR-0009 correctly untouched (immutable body; no `refined_by` field; refined not superseded).

## Facts checked by the reviewer
Renderer csproj embeds `docs/design/**` via `..\..\..\..\`; Dockerfile builds from repo root; reader parks `.doc`/`.msg`; no `nuget.config`/`Directory.Packages.props`; ADR-0001:40 quote exact; `related_capabilities`/`related_frd` all exist.

## Verdict
**PASS.** Merge on green CI; then `verifying`.
