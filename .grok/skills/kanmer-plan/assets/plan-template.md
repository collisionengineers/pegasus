# Plan — <ticket id>: <title>

Written FROM research.md and impact.md — if either is missing or stale, fix
that first.

## Approach

The chosen approach and why it beat the alternatives (one paragraph).

## Governing docs

**Required.** How this plan meets each linked PRD/FRD/ADR (`refs`). For each:
- **Meets** — which requirement/acceptance-criterion each step satisfies; or
- **Modifies** (only with explicit user authorization) — what changes in the doc and why; or
- **New ADR** — the design decision this introduces, written via `kanmer-docs` and linked.

`kanmer-review` checks this section holds against the diff.

## Steps

1. Concrete, ordered steps. Each should be checkable — checklist.md is
   derived from this list.
2. …

## Verification

How proof.md will be produced: the tests to run, the behaviours to observe,
the commands whose output becomes the evidence.

## Risks / open questions

- Risk and its mitigation, or the question and who answers it.
