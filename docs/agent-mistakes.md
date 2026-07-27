# Agent mistake log

## Purpose

Retain factual evidence about material agent mistakes that can improve this repository workflow or the Azure Workflow plugin. This is an append-only evidence log, not a blame record, task board, or automatic source of policy.

## What to record

Record when an agent violated available authority, made a false completion/evidence claim, crossed scope or authorization, allowed a defect to escape a required gate, or exposed a reusable workflow gap. Recover or contain first.

## What not to record

Do not record expected red tests, ordinary findings caught by their intended gate, user decisions, external tool failures the agent handled correctly, style preferences, or inferred historical incidents.

## Incident template

```markdown
### AM-YYYYMMDD-NNN: <short factual title>
- Occurred: <UTC timestamp>
- Detected: <UTC timestamp>
- Workflow/package version: <version or unknown>
- Change/PR: <relative link or none>
- Classification: authority | false-evidence | scope | escaped-defect | workflow-gap
- What happened: <observable facts>
- Impact: <actual impact>
- Recovery: <containment/correction and evidence>
- Why the gate failed: <specific mechanism>
- Reusable prevention signal: <candidate improvement, not automatic policy>
- Follow-up: <issue/change/incident ID or none>
```

## Entries

Append incidents below; do not edit earlier entries.
