# Domain docs

How engineering skills consume Pegasus domain documentation while exploring the codebase.

## Before exploring, read these

- `docs/index.md` for repository authority and the smallest canonical owner for the question.
- `CONTEXT.md` at the repository root for the domain glossary.
- Relevant accepted ADRs routed through `docs/adr/README.md`.

If `CONTEXT.md` or `docs/adr/` does not exist, proceed silently. Domain modeling creates them lazily when terms or decisions are resolved.

## File structure

Pegasus uses a single-context layout:

```text
/
├── CONTEXT.md
├── docs/
│   └── adr/
└── src/
```

## Use the glossary's vocabulary

Use terms as defined in `CONTEXT.md` without overriding the canonical product, operator, or decision owner selected through `docs/index.md`. If a needed concept is absent, reconsider the language or note the gap for domain modeling; do not invent a synonym.

## Flag ADR conflicts

Surface contradictions with an accepted ADR explicitly rather than silently overriding it. Published ADR bodies are immutable; changed meaning uses the repository's accepted addendum or superseding-decision process.
