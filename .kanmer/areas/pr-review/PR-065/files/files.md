# Files

| Path | Change | Risk |
| --- | --- | --- |
| `.grok/skills/kanmer-setup/SKILL.md` | Remove the sentence linking to the missing greenfield manual. | Low: preserves the complete workflow below while removing an invalid reference. |

## Ripple effects

- `scripts/Test-DocumentationLinks.ps1` must pass.
- The repair must land in `dev`, after which PR #560 can incorporate the corrected base/head and rerun its documentation check.

## Context files

| Path | Why |
| --- | --- |
| `scripts/Test-DocumentationLinks.ps1` | Defines the failing documentation-link contract. |
| `AGENTS.md` | Requires the smallest scoped fix and no unrelated documentation tree. |

## Out of scope

No new manual, no edits to other skill copies, no product-code changes, and no changes to PR #560's feature implementation.
