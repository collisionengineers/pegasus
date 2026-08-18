# Plan — DELIV-004: Prohibit shipping features behind disabled gates

## Approach

Add one explicit delivery rule to `AGENTS.md` beside the existing safety rails:
a closed composition or feature gate is a disabled flag, not a partially
shipped feature. A feature must be delivered with its real caller and
activation evidence, or deferred in the documented backlog/decision system.
This makes the existing `docs/engineering.md` anti-dormancy rule impossible
to misread without duplicating its detailed list of prohibited shapes.

## Governing docs

This is a repository-operating-rule clarification, not product behaviour or a
technical architecture decision; it has no linked PRD, FRD, or ADR. The plan
meets the repository documentation model by editing only `AGENTS.md`, the
canonical owner of repository rules, and preserving
`docs/engineering.md` as the detailed authority.

## Steps

1. Add an explicit `AGENTS.md` safety-rail bullet: a closed gate is a disabled
   flag; do not ship, release, merge as delivered, or claim a feature behind
   one. Defer work in the documented backlog/decision process instead.
2. Review the new wording against `docs/engineering.md` § “Abstractions and
   deferred capabilities” and retain that file unchanged unless a contradiction
   is found; it already provides the detailed no-dormancy rule.
3. Inspect the documentation-only diff and search both files for the new policy
   and its existing disabled-flag authority. Record the command output for
   later proof.

## Verification

Run a targeted text search showing the explicit `AGENTS.md` rule and the
existing `docs/engineering.md` prohibition. Review the diff to confirm that
only repository guidance changed; no application configuration, feature gate,
deployment, credential, or external service action occurs.

## Risks / open questions

- Risk: wording could imply that a closed gate is an acceptable release
  mechanism. Mitigation: use the exact “closed gate is a disabled flag” rule
  and direct deferral to the existing documented process.
- No open questions; the user confirmed the intended interpretation.

## Simplification pass — 2026-08-18

n/a — docs-only. The one-rule edit reuses the existing `docs/engineering.md` authority and adds no mechanism, abstraction, or duplicate taxonomy.
