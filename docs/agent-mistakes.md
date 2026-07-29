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

### AM-20260727-001: Repeated full review after metadata-only correction
- Occurred: 2026-07-27T04:31:21Z
- Detected: 2026-07-27T04:31:21Z
- Workflow/package version: azure-workflow 0.1.0-alpha.1+codex.20260727012309
- Change/PR: [PR #4](https://github.com/collisionengineers/collisionspike_v2/pull/4)
- Classification: workflow-gap
- What happened: After the required review of PR #4 against its new `main`
  base found only stale wording in the PR and issue descriptions, the agent
  corrected those two external descriptions and started another complete PR
  review solely because the evidence fingerprint had changed. The base, head,
  tracked diff, and successful CI result had not changed, and targeted readback
  had already proved the wording correction. The user stopped the extra review.
- Impact: The agent caused unnecessary delay, tool use, and confusion without
  improving confidence in the repository change. The interrupted read-only
  review made no repository, Azure, or other external modification.
- Recovery: The extra reviewer was stopped. The agent read back that PR #4
  targets `main`, remains mergeable, and has successful exact-head CI before
  proceeding under the user's explicit merge instruction.
- Why the gate failed: The agent applied exact-snapshot invalidation
  mechanically to a metadata-only correction instead of applying proportional
  verification and distinguishing repository-content changes from already-read
  back descriptive text.
- Reusable prevention signal: Batch mutable PR/issue descriptions before the
  decisive review. When a later correction changes only descriptive metadata
  and leaves base, head, diff, checks, and review findings unchanged, use a
  targeted readback rather than automatically repeating the complete review.
- Follow-up: none

### AM-20260729-001: Review search traversed excluded reference subtree
- Occurred: 2026-07-29T09:23:00Z
- Detected: 2026-07-29T09:23:00Z
- Workflow/package version: unknown
- Change/PR: [PR #18](https://github.com/collisionengineers/pegasus/pull/18)
- Classification: authority
- What happened: During direct remediation review, an agent searched the broad
  `docs/` root for stale terminology. The search returned matches from the
  explicitly opaque `docs/reference/imp-docs/` subtree.
- Impact: The excluded source was read into tool output. No file in that subtree
  was modified, and no product or remediation decision used its contents.
- Recovery: The search was stopped at the returned result; subsequent review is
  limited to explicit allowed paths, and the change record disclaims reliance on
  the excluded subtree.
- Why the gate failed: The search scope named `docs/` without pruning the
  excluded prefix before traversal.
- Reusable prevention signal: Scope every documentation search to explicit
  allowed owners or first-party subdirectories; never use `docs/` as the root
  when an opaque descendant exists.
- Follow-up: none
