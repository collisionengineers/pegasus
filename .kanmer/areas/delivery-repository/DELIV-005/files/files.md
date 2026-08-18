# Files — DELIV-005

## Changed files

| File | Change | Risk |
|---|---|---|
| `.github/workflows/ci.yml` | Remove the Markdown-placement workflow step only. | A broad edit could accidentally remove regression or documentation-link validation. |

## Context files

| File | Why read it |
|---|---|
| `scripts/Test-MarkdownPlacement.ps1` | Confirms the exact gate being removed and why it rejects the asset README. |
| `scripts/Test-TestMarkdownPlacement.ps1` | Confirms the retained regression test is independent of the workflow gate. |
| `AGENTS.md` | Requires a ticket/worktree/PR and preserves unrelated work. |

## Deliberately out of scope

- Altering the allowed Markdown-path policy or moving
  `src/Pegasus.Web/wwwroot/images/marks/README.md`.
- Removing `Markdown placement regression tests` or `Documentation links`.
- Any application, deployment, or external-service change.
