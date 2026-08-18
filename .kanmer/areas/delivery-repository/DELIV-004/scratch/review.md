# Review — DELIV-004 — 2026-08-18

## Changes

| File | What changed | Why |
|---|---|---|
| `AGENTS.md` | Added a four-line safety-rail clarification: a closed composition or feature gate is a disabled flag, not a partially shipped feature. | Prevents a gated-off capability from being shipped, released, merged as delivered, claimed, or documented as delivered; routes it to the existing backlog/decision process until real-caller activation evidence exists. |

## Comments

No blocking or non-blocking comments were raised.

## Disposition

No comments required a disposition.

## Verdict

**pass**

An independent agent who did not implement the change checked the ticket's
research, files, plan, checklist, post-implementation report, PR #398 diff and
description. The plan misses no ticket implication; implementation misses no
plan step; the governing-docs determination is correct; and the dated
`n/a — docs-only` simplification disposition is honest. The PR changes only
`AGENTS.md`; `docs/engineering.md` remains unchanged and continues to state
the detailed anti-dormancy rule. No open-questions document exists. All
applicable repository checks succeeded (changes, documentation,
reference-data); the remaining code/infrastructure checks are appropriately
skipped for this docs-only diff.
