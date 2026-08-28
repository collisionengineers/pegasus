---
mergeSha: 68adedafb9159772515b1b4fb9758f0ab2261fe7
changeCommit: cb2ab070
pr: https://github.com/collisionengineers/pegasus/pull/578
disposition: PASS
---

# Proof — MAIL-022

Verified 2026-08-27 on merged `main` at
`68adedafb9159772515b1b4fb9758f0ab2261fe7`, read directly from the remote ref
without checking it out. The correction was carried by commit `cb2ab070` in
PR [#578](https://github.com/collisionengineers/pegasus/pull/578)
([[DELIV-029]], the release-35 documentation pass), which merged into `dev`
with green `repository-check` and reached `main` in the authorised
promotion-only fast-forward.

## Required outcome — the corrected row

```
git show origin/main:docs/open-decisions.md
```

Stale threshold row now reads:

```
| Ship the provisional 15 minutes (three missed `ApprovedInboxPollSchedule`
  recovery ticks at `0 */5 * * * *`), recorded in
  `GetRetainedMailFreshness.StaleAfter`. |
```

## No residual old wording

```
git grep -c "fifteen missed one-minute" origin/main -- docs/   → exit 1 (no match)
git grep -c "three missed .ApprovedInboxPollSchedule" origin/main -- docs/
  → docs/open-decisions.md:1   (exactly one occurrence, one list per concept)
```

## Consistent with the code remark it is cited by

```
git grep -n "ApprovedInboxPollSchedule" origin/main -- src/Pegasus.Core/Intake/RetainedMail.cs
  → 651: /// poll (<c>InboxRecoveryFunction</c>, <c>ApprovedInboxPollSchedule</c>) runs
```

The open-decisions row and the [[MAIL-021]] `StaleAfter` remark now state the
same model: Graph change notifications primary, `InboxRecoveryFunction` on
`0 */5 * * * *`, so 15 minutes is three missed recovery ticks.

## Scope

Docs-only, one table cell. No behaviour change, no code change, no other row
or file touched. Simplification pass: n/a — docs-only.

## Disposition

**PASS.** The required outcome is on merged `main`; nothing remains.
