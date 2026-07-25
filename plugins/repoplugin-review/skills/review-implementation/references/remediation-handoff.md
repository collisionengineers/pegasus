# Remediation handoff shape

```markdown
# Review remediation — <task-id>

Plan handoff: <immutable handoff or hash>
Review scope: <paths and requirements checked>
Evidence limits: <unrun/unavailable checks or none>

## Blocker | Required | Advisory

- Evidence: <concrete file, command result, caller trace, or diff>
- Required outcome: <observable correction>
- Owner: <original implementation owner or named boundary>
- Re-check: <exact review or validation evidence>
```

Use one finding per item. Do not use a severity label without evidence and an observable required outcome.
