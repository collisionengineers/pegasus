---
status: accepted
---

# ADR-0010: Adopt single-context domain documentation

Pegasus uses a root `CONTEXT.md` exclusively as its domain glossary and `docs/adr/` as the canonical durable-decision store. Existing product, operator, architecture, operations, design, evidence, and change-record owners retain their roles; existing published decision clauses, rationale, status, and provenance move unchanged, with only `DOC-CON-012`-authorized relative-link edits; workspace-local decision stores remain local; and no parallel `docs/decisions/` compatibility path remains. This atomic cutover aligns engineering-skill consumers with one discoverable domain layout without turning the glossary or ADRs into a second requirements database.
