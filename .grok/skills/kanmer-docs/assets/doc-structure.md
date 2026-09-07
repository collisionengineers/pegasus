# Document structure

**Descriptive mirror — never authoritative.** The board's `board.yml` is the
source of truth. This file is the canonical `kanmer-docs` asset. `kanmer-setup`
materializes a target repository's `docs/contributing/doc-structure.md` from
this asset after resolving that board's document globs; the freshness check
validates both the target-neutral asset and the generated mirror.

## The `/docs/` tree

The repository's governing documents live under the configured paths:

```
docs/
  <configured product path>   vision and product context
  <configured FRD path>       functional requirements
  <configured ADR path>       architecture decisions
  contributing/doc-structure.md  this generated mirror
```

The target board classifies governing documents with its `repoDocs` globs. The
asset leaves those repository-specific values unresolved:

| Kind | Resolved by the target board |
|---|---|
| `prd` | `<board repoDocs.prd>` |
| `frd` | `<board repoDocs.frd>` |
| `adr` | `<board repoDocs.adr>` |

A ticket links a governing document through `refs`. Whether that link is
required at a stage boundary is profile-resolved; call `get_doc_gates` for the
ticket instead of relying on a fixed table.

## Ticket documents

Ticket pipeline documents live inside the ticket folder under
`.kanmer/areas/<area>/<ID>/`. Format 3 uses one folder per document type, and
the folder may contain more than one Markdown document:

| Type | Current path | Purpose |
|---|---|---|
| `research` | `research/*.md` | findings and sources |
| `files` | `files/*.md` | current v3 location for paths changed and implementation context |
| `open-questions` | `open-questions/*.md` | questions only the user can answer |
| `plan` | `plan/*.md` | approach and governing-document mapping |
| `checklist` | `checklist/*.md` | executable progress (`- [ ]` / `- [x]`) |
| `post-implementation-report` | `post-implementation-report/*.md` | reviewers' brief |
| `proof` | `proof/*.md` | evidence at the exact configured integration-branch SHA after review and merge |

Running notes are `scratch/<slug>.md`. Human-supplied inputs belong in
`reference/`; binary evidence belongs in `assets/`. Neither is a pipeline
document or a gate by itself.

## Workflow model

The board has six fixed stages:

`backlog → preparing → implementing → review → verifying → done`

Document requirements are resolved from the ticket's profile and board/area
configuration. A move crosses one gated boundary at a time; use
`get_doc_gates <id>` immediately before every move to see the effective
requirements and what is already satisfied. Creation is ungated, so historical
backfill can create a ticket directly in any stage.

The board's `profiles` and `defaultProfile` fields, plus any area overrides,
define the effective requirements. Format 3 currently uses the seven fixed
document types listed above; profile configuration selects which of those are
required at each boundary. Ask `get_doc_gates` for the effective requirements.
This asset describes the live format-3 model, not an independent policy.

## Generation and freshness

Resolve the consuming repository's document globs and profile gates through
`get_doc_gates`. Follow that repository's canonical documentation navigation
and validation commands. No Kanmer-source path or npm command is implied in a
consumer. Installed copies must agree on this target-neutral contract.
