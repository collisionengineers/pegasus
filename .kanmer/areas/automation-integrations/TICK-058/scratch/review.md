---
kind: review-attestation
pr: "646"
head_sha: "32a5a62ce4f13baba45a0bad06df5498f38dcd19"
verdict: needs-changes
reviewer: "codex-review-agent-tick-058"
independent: true
plan_hash: "2b229f222694c13a"
ticket_updated: "2026-09-02T19:40:26.951Z"
board_sha: "bd8fc62cf8ebfab581fda236b0968aa911d97c5b"
expected_reviewers:
  - "codex-review-agent-tick-058"
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "The plan, post-implementation report, and PR description falsely say the Provider API feature gate remains closed or disabled, although the reviewed head and canonical current-state documents record Features__ProviderApi=true and the surface live since release 37."
    disposition: open
---

# Independent delta review — TICK-058

## Verdict

**Needs changes.** The production implementation at
`32a5a62ce4f13baba45a0bad06df5498f38dcd19` correctly closes the original
existing-Case gap, but the current hand-off understates the deployed boundary.

## Review coverage

The immutable diff from
`cad00be9d42dbeaee9edf34c2d24de222d7ddb9d` changes the seven files named by
the remediation packet. Declared provider identity reuses
`IProviderCaseMatchPolicy.DeriveIndexKeys`, the existing candidate query, and
the existing Core eliminator. `ProcessIntake` rejects both `UniqueMatch` and
`Ambiguous` before declared assessment or allocation, and the durable worker
maps that exception to terminal
`provider_existing_case_match`. The integration test proves the unmatched
first submission creates one Case, the repeated matching submission fails with
a null Case reference, and Case/link counts do not increase.

The operator's create-only decision is represented in FRD-09 and existing-Case
updates remain concretely deferred to [[AUTO-017]]. No endpoint, schema,
migration, dependency, deployment, credential, or cloud mutation appears in
the PR diff.

The simplification dispositions are honest. The unused exception outcome was
removed, and the final test-support shape preserves the established
completion-only helper while sharing only dispatch/backoff with the new
terminal-status helper. The report retains the failed full-suite attempt caused
by the rejected over-broad helper and the later passing rerun; no assertion was
weakened. An independent focused matcher/policy run passed 54/54.

The historical Markdown boundary is also correct:
`23b0c564c81bf8a0665bc5a65f3f54d88010f835` is the exact first parent of
merge `0d985c9e0b3284f211f824d387e2f36460c0c826`, and the immutable
Markdown-placement command passes.

PR #646 had no review threads at the final gather. Its required hosted checks
were still running when this needs-changes verdict was recorded; they cannot
override the open major finding.

## Finding and required disposition

### F-001 — false closed-gate status (major, open)

The appended plan and post-implementation report say the Provider API feature
gate “remains closed,” and the PR description says it “remains disabled.”
Those statements are false at the reviewed head:
`infra/modules/platform.bicep` sets `Features__ProviderApi` to `true`,
while `docs/operations.md` and `docs/current-architecture.md` record the
surface live since release 37. The ticket itself records production deployment.

Correct the plan, post-implementation report, and PR description to the actual
boundary: the route is live and composed, no provider credential has been
issued, and this remediation changed no deployment, credential, or cloud
state. Retain the implementation evidence and the failed helper attempt.
The public disposition request is
https://github.com/collisionengineers/pegasus/pull/646#issuecomment-5515310839.

## Residual risk

No provider credential has been issued, so there is no real provider caller
yet. That is materially different from a closed composition gate and must be
stated accurately.
