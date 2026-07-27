# `0.0.0-development` classification foundation and local EML evaluator

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Planned `0.0.0-development` foundation — mailbox-policy gated**

## Purpose

Define the smallest local, reviewable evaluator that can call one versioned
Core provider-specific instruction-classification policy against `.eml` files.
Its intended real caller is a development-only local folder workspace, not
Outlook, Graph, a Worker trigger, or a mailbox API.

## Feature coverage

Primary feature ownership is: `EVAL-01`, `EVAL-02`, `EVAL-03`, `EVAL-04`,
`EVAL-05`, `MAIL-20`, `MAIL-21`, and `MAIL-22`. `0.1.0-alpha.1` Outlook receipt and `Next`/`unallocated`
four-mailbox/email-workspace features are secondary dependencies only; this
plan owns neither a transport adapter nor an application mailbox UI.

## Authority and current boundary

- **Authority:** the [mailbox decision dossier](README.md), the [feature
  maturity map](../feature-maturity-map.md), and the current Core/Worker caller
  inventory in the active planning task.
- **Policy owner:** one planned versioned Core classification policy. The local
  evaluator adapts folder files and records comparison evidence; it must never
  become a second classifier or a runtime rule store.
- **Current evidence:** no classifier, evaluator, Outlook/Graph adapter,
  Worker trigger, or mailbox caller exists. The Development upload route reaches
  `ProcessIntake`, but is not this caller.
- **Decision gate:** the dossier remains the only owner of predicate,
  precedence, ambiguity, correction, acceptance cohort, rollout, rollback, and
  automatic-matching decisions. The settled taxonomy is input to the reviewer
  experience, not authority to invent those missing rules.

## Review local EML workspace

**Evidence state:** Planned — mailbox-policy gated

`EVAL-01` through `EVAL-04` require a local development-only workspace with
ignored `unchecked` and `checked` folders. A reviewer opens a copied local
`.eml`, selects the detailed Received/Sent/Reply taxonomy, records required
reasoning, and may select `Other` only with a new category name and reasoning.
Moving the reviewed source to `checked` records the human result without
altering its original content or claiming a mailbox move.

The future evaluator caller must preserve immutable local source identity,
reviewer/time, taxonomy/reasoning, and workspace outcome. Invalid folders,
unsupported/corrupt files, duplicate identity, incomplete review, ambiguous
taxonomy selection, or failed move leave the source visibly unresolved and
create no case, reference, Outlook action, Graph request, or background work.
The workspace is a local evaluation tool: no corpus upload, live mailbox read,
credential, Graph scope, mail send/move/category action, or production queue is
in scope.

## Compare versioned policy with human results

**Evidence state:** Planned — mailbox-policy gated

`EVAL-05` compares a human review with the category, ambiguity outcome,
decision evidence, and immutable policy version returned by the one Core
policy. It does not overwrite the human result, turn a comparison into a
runtime correction mechanism, or accept a rule solely because it agrees with a
small sample. A missing policy, unknown version, incomplete evidence, or
ambiguous output is a visible non-pass and blocks any claim of acceptance.

## Prove the Core classification policy

**Evidence state:** Planned — mailbox-policy gated

`MAIL-20`, `MAIL-21`, and `MAIL-22` use the local evaluator to exercise live
provider-specific instruction identification against local `.eml` files once
the dossier accepts the policy. The shared Core owner must return versioned
rules, decision evidence, an explicit ambiguity outcome, and an acceptance
cohort. It consumes the settled detailed Received/Sent/Reply taxonomy but does
not infer queue routing, folder destinations, automatic association, or an
Outlook transport contract.

Implementation evidence must include focused policy and evaluator tests,
negative/fail-closed cases, a representative local cohort and holdout, reviewer
comparison, and a recorded decision on the cohort's acceptance threshold. It
must distinguish **Planned**, **Implemented**, **Called**, and **Locally
verified**; neither a local evaluation result nor registration proves
deployment, live mailbox behavior, or operator acceptance.

## Rollout, recovery and deferred impact

The local caller is activated only after the policy decision is accepted and
the evaluator's workspace/retention procedure is approved. Rollback disables
the evaluator/policy caller and retains local review/comparison evidence; it
does not delete originals, rewrite reviews, or change mailbox state.

`0.1.0-alpha.1` may reuse the same accepted Core policy for the bounded `instructions@`
receipt path, while `Next`/`unallocated` may reuse it for four-mailbox classification and email
actions. Those callers need their own Graph scope, identity, storage,
correction, rollout, and acceptance evidence. Deliberately absent are Outlook
or Graph adapters, mailbox credentials, a Worker trigger, generic rule engine,
authoring UI, runtime configuration table, feature flag, second classifier,
and automatic matching implementation.
