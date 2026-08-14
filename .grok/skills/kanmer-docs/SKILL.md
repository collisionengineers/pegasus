---
name: kanmer-docs
description: Author and link the repo's governing documents (PRD/FRD/ADR) under /docs/. Use to create or update a product/functional/architecture doc, to satisfy the "every ticket links or creates a governing doc" rule, or to lay down the /docs/ structure. DO NOT USE for per-ticket pipeline docs (research/plan/proof — those live in the ticket folder).
---

# Kanmer docs — governing-document governance

The repo's own `/docs/` tree holds the durable product/architecture record;
tickets reference it by path via `refs` (`link_doc`). Per-ticket pipeline docs
(research/impact/plan/checklist/proof) live *inside* the ticket folder — not
this skill's job.

## The /docs/ tree
- `docs/product/` — vision (`vision.md`), open questions, PRDs at `docs/prd/NNNN-<slug>.md`.
- `docs/functional/` — FRDs at `docs/frd/NNNN-<slug>.md`: behaviour + acceptance criteria.
- `docs/architecture/` — ADRs at `docs/adr/NNNN-<slug>.md`: one decision each (context / decision / consequences).
- `docs/contributing/doc-structure.md` — the descriptive mirror of `board.docs` (never authoritative; `board.yml` is the source of truth).

Numbering is zero-padded and monotonic per kind. `board.docs.repoDocs`
classifies a path's kind by glob (default `docs/prd/**`, `docs/frd/**`,
`docs/adr/**`).

## The link-or-create rule (gate: leaving Backlog)
Before a ticket leaves Backlog it must either:
- **link** an existing governing doc — `link_doc <id> docs/prd/<slug>.md` (or frd/adr); or
- **create** the doc first (author it here via the `prd`/`frd`/`adr` templates), then link it; or
- set **`docs_todo`** when the doc is genuinely still to be written (imports, spikes) — a tracked debt that `kanmer-groom` surfaces.

## Authoring rules
A **plan** must state how it meets each linked PRD/FRD/ADR — or, with explicit
user authorization, how it *modifies* one, or why a *new* ADR is created for a
design decision. `kanmer-plan` writes that "Governing docs" section; `kanmer-review`
checks it holds. Gates only check a doc's existence; this content rule is human-
and skill-enforced.

## Bulk (greenfield)
`kanmer-setup` calls this skill to split a product brief into PRDs → FRDs → ADRs
and materialise the `/docs/` tree + `doc-structure.md` before seeding the backlog.
