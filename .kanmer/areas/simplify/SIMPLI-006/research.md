# Research — ADRs under the PRD/FRD/ADR taxonomy

Reshaped from the renumber-to-9 approach (`adr-consolidation.md`, superseded).
Root/approved plan: `docs/temp-plans/retire-now-rewrite-agents.md`.

## ADR classification (24 files; 0017 absent)
- **13 pure-technical** — keep as stable ADRs: 0001,0002,0003,0004,0007,0009,
  0011,0014,0015,0016,0019 (+ the technical cores of the mixed set).
- **8 mixed** (technical decision entangled with feature behaviour) — thin to the
  technical core, functional clauses → FRDs: 0005,0006,0008,0013,0018,0021,0022,0024.
- **2 feature-rules mis-filed as ADRs** → FRD: 0012 (MOT mileage) → FRD-06;
  0020 (QDOS case-association predicates) → FRD-09.
- **2 documentation-governance ADRs** → AGENTS.md/index: 0010, 0023.

## Why the ADR method needed modernizing (evidence: 0002, 0024, README)
- No machine-readable metadata — status/supersession were inconsistent prose bullets.
- Mega-ADRs (0002 decides ~7 things + a dated cost forecast) → pervasive partial
  supersession + stale bodies (0002 still read "App Service" after 0015).
- Inconsistent templates; numbering collisions/gaps (0016 was 0010; 0017 absent).

## Best-practice target (professional + AI-agent friendly)
Stable append-only IDs (never renumber); YAML frontmatter as the single source of
currency/relationships; one decision per ADR; a thin frontmatter-driven index
whose `status: accepted` filter is the current-architecture view; governance and
feature behaviour live OUTSIDE ADRs (AGENTS.md and the FRDs respectively).
