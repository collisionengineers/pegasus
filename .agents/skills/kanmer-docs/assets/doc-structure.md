# Documentation structure

A descriptive mirror of this repo's document model. **Not authoritative** — the
board's `board.docs` (in `.kanmer/data/board.yml`) is the source of truth; this
file just tells a human where things live.

## The /docs/ tree
```
docs/
  product/        vision.md, open-questions.md
  prd/            NNNN-<slug>.md   product requirements
  frd/            NNNN-<slug>.md   functional requirements (behaviour + acceptance criteria)
  adr/            NNNN-<slug>.md   architecture decisions (one per file)
  api/            interface/contract docs
  operations/     runbooks, deploy notes
  contributing/   doc-structure.md (this file)
```

## Conventions
- Numbering is zero-padded and monotonic per kind (`0001`, `0002`, …).
- A ticket references the docs it implements via `refs` (`link_doc`).
- `board.docs.repoDocs` classifies a path's governing-doc kind by glob
  (default `docs/prd/**`, `docs/frd/**`, `docs/adr/**`).

## Per-ticket pipeline docs (not here)
research / files / open-questions / plan / checklist / post-implementation-report
/ proof live *inside* each ticket's folder under `.kanmer/`, not in `/docs/`.
Which of them a ticket owes depends on its profile — ask `get_doc_gates`.
