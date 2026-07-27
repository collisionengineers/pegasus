# Agent and delivery routing

Use one accountable lead. Delegated agents receive bounded, non-overlapping
scope and report facts, inference, limits, and next evidence; the lead integrates
the result and remains accountable. Read-only discovery/review may run in
parallel only when it cannot overlap a writer.

## Azure Workflow lifecycle

| Need | Route and boundary |
| --- | --- |
| Onboard/convert this repository | `$azure-workflow:onboard-azure-repository`; zero-loss conversion through an independently reviewed PR, never merge |
| Plan one material change | `$azure-workflow:plan-azure-repository-change`; inspect authorities/callers, resolve decisions, persist and independently review the plan, then stop before implementation |
| Implement, fix, or remediate | `$azure-workflow:deliver-azure-repository-change`; use one scoped branch/change record/PR and caller-backed proof |
| Explain behavior or feedback | `$azure-workflow:explain-repository`; read-only plain-English explanation, not a correctness verdict or fix |
| Review an actual PR | `$azure-workflow:review-repository-pull-request`; independent read-only exact-base/head review |
| Inspect or operate Azure | `$azure-workflow:operate-azure-repository`; read current state and perform only an explicitly approved exact mutation |

Repository business interpretation still follows [the source registry](../index.md)
and [product authority](../product/index.md); a workflow skill is never itself a
product-rule source. Operator-facing UI work must also follow
[the design route](../../design/README.md) and the approved/current UI source it
links.

## Delivery state and records

Material work uses one GitHub issue when activated and one dated record under
`docs/changes/`. GitHub Project fields own live work state. Do not recreate the
removed repository-local plugin suite, `.repoplugin` task database, task-folder
handoffs, generated dashboard, or parallel status ledger.

The implementation lead calls the real harness `update_plan` before repository
edits or implementation delegation. Report planned, implemented, called,
locally verified, deployed, live verified, and accepted separately. A file,
registration, green structural check, or deployment cannot substitute for a
real caller or acceptance.

External/cloud reads and writes, deployment, credential rotation, account
changes, and destructive operations require the explicit authority named in
the root instructions. A branch, issue, change record, or PR does not broaden
that authority.
