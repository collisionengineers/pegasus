---
name: kanmer-docs
description: Author and link the repo's governing documents (PRD/FRD/ADR) under /docs/. Use to create or update a product/functional/architecture doc, to satisfy the "every ticket links or creates a governing doc" rule, or to lay down the /docs/ structure. DO NOT USE for per-ticket pipeline docs (research/plan/proof — those live in the ticket folder).
---

# Kanmer docs — governing-document governance

The repo's own `/docs/` tree holds the durable product/architecture record;
tickets reference it by path via `refs` (`link_doc`). Per-ticket pipeline
documents (research, files, plan, checklist, proof) live *inside* the ticket
folder — not this skill's job.

## Workflow

1. **Decide which kind** you are writing — PRD, FRD or ADR — with the table and
   the granularity test below.
2. **Read the board's document model** (`get_doc_gates` with no `id`) for the
   path globs, and match the filenames already in the directory.
3. **Author** from the matching template in `assets/`.
4. **Link it** — `link_doc <id> <path>` — or set `docs_todo` if the doc is
   genuinely still owed.
5. **Return** to whatever sent you here.

This skill is stage-agnostic: it is called from Backlog to satisfy the
link-or-create rule, from Preparing when a plan turns up a design decision, and
in bulk from setup. It never moves a ticket.

## Which document am I writing?

| | Answers | Rule |
|---|---|---|
| **PRD** | why the product needs this | one per initiative |
| **FRD** | what ONE feature does | one crisp acceptance list, one "done" |
| **ADR** | why it is built this way | one decision; superseded, never edited |

**The granularity test:** one crisp acceptance list and one "done" — if a document needs two,
split it. (This test exists because it caught the FRD authoring in this very project; see
FRD-014 R2 and the R8b correction in the shaping record.)

FRDs are **durable end-state specs of the whole product**, absorbing shipped behaviour — not
change requests. Cross-cutting rules that span every feature (living documents, the
read-everything duty) are requirements *inside* FRDs plus the AGENTS-block layer, never FRDs of
their own.

(In a repo that has `docs/README.md`, that file is the canonical copy of this
table and this skill's copy must match it. The duplication is deliberate — the
plugin ships to repos with no such file — and is not checked automatically.)

## Where the documents live — ask, do not assume

**Paths are configured per board, so read them rather than hardcoding them.**
`get_doc_gates` with no `id` returns the board's document model, including the
governing-doc path globs. Use those.

The shipped defaults are `docs/prd/**`, `docs/frd/**`, `docs/adr/**` — but a
repo may set anything, and this one does:

```yaml
repoDocs:
  prd: docs/product/prd/**
  frd: docs/functional/frd/**
  adr: docs/architecture/adr/**
```

Writing to the default path on a board that overrides it produces a document the
globs classify as nothing, and `refs` pointing at a path that does not exist is
rejected outright — `assertRefs` requires the file to be there.

For the filename, **match what is already in the directory**. The conventional
shape is `<KIND>-<number>-<slug>.md` with the kind prefix included
(`PRD-001-…`, `FRD-014-…`, `ADR-0009-…`), zero-padded and monotonic per kind
— but the width varies by repo, so copy the neighbours rather than the example.

A repository may have a generated mirror of its document model. Use its own
canonical navigation and resolved `get_doc_gates` model; do not assume a
foreign repository path, npm command or raw board.yml profile is authoritative.

## Governing-document requirements

Call `get_doc_gates <id>` for the current profile before crossing a stage
boundary. Link existing governing documents through `link_doc`, or author and
link a required new document. Record genuine outstanding document work as
`docs_todo`. Do not invent a fixed Backlog gate or assume every profile needs
the same documents.
## Authoring rules
A **plan** must state how it meets each linked PRD/FRD/ADR — or, with explicit
user authorization, how it *modifies* one, or why a *new* ADR is created for a
design decision. `kanmer-plan` writes that "Governing docs" section; `kanmer-review`
checks it holds. Gates only check a doc's existence; this content rule is human-
and skill-enforced.

## Project guide outside the managed block

For a repository's user-owned `AGENTS.md` content, start from
`assets/agents-template.md` **only when the file is absent**. Its five sections
(Commands, Architecture map, Conventions, Gotchas, Verification) are a
deliberately incomplete skeleton: replace the TODOs with repository facts.

When `AGENTS.md` already exists, preserve its human-authored prose. Assess and
report any missing required sections instead of rewriting the guide. The
marker-delimited Kanmer operating block belongs to `kanmer-setup` and its
writer; this asset must never copy, replace, or redefine that managed block.

## Bulk (greenfield)
`kanmer-setup` calls this skill to split a product brief into PRDs → FRDs → ADRs
and materialise the `/docs/` tree + `doc-structure.md` before seeding the backlog.

---

**No successor — control returns to the caller.** Three call it:
`kanmer-tickets` or `kanmer-research` when a ticket needs a governing doc, `kanmer-plan` when the plan introduces a design decision
that deserves an ADR, and `kanmer-setup` in bulk on a greenfield board. Each
resumes where it left off once the document exists and is linked.
