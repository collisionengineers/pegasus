# Agent and delivery routing

Use one accountable lead. Delegated agents receive bounded, non-overlapping
scope and report facts, inference, limits, and next evidence; the lead integrates
the result and remains accountable. Read-only discovery/review may run in
parallel only when it cannot overlap a writer.

## Repository lifecycle

| Need | Route and boundary |
| --- | --- |
| Onboard/convert this repository | preserve source roles and material claims in one reviewed onboarding change and pull request; never merge |
| Plan one material change | inspect authorities and callers, resolve decisions, persist one plan in the activated issue/change record, obtain acceptance, then stop before implementation |
| Implement, fix, or remediate | continue the same scoped branch, issue, change record, and pull request; prove the real caller |
| Explain behavior or feedback | read-only plain-English explanation; distinguish intended, implemented, deployed, and accepted evidence |
| Review an actual pull request | independent read-only review of the exact base and head |
| Inspect or operate an external service | read current state or perform only the explicitly approved exact operation against named targets |

Repository business interpretation follows [the source registry](../index.md)
and [product authority](../product/index.md); a skill or tool is never itself a
product-rule source or authorization. Operator-facing UI work must also follow
[the design route](../../design/README.md) and the approved/current UI source it
links.

## Delivery state and records

Material work uses one GitHub issue when activated and one dated record under
`docs/changes/`. GitHub Project fields own live work state. Do not recreate the
removed repository-local plugin suite, `.repoplugin` task database, task-folder
handoffs, generated dashboard, or parallel status ledger.

The implementation lead maintains a current execution list before repository
edits or implementation delegation. Report planned, implemented, called,
locally verified, deployed, live verified, and accepted separately. A file,
registration, green structural check, or deployment cannot substitute for a
real caller or acceptance.

External/cloud reads and writes, deployment, credential rotation, account
changes, and destructive operations require the explicit authority named in
the root instructions. A branch, issue, change record, or PR does not broaden
that authority.
