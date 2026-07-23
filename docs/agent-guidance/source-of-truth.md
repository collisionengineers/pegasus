# Source-of-truth order

When two sources disagree, use this order and record the conflict rather than silently blending them:

1. Direct user instruction in the current task.
2. `docs/operator-notes/` — read-only operator truth.
3. Settled answers in `PROJECT_DISCOVERY_QUESTIONNAIRE.md`.
4. Accepted ADRs under `docs/architecture/decisions/`.
5. Executable contracts and tests that were explicitly accepted for this version.
6. Retrospectives as delivery constraints.
7. The local corpus and predecessor as evidence of real shapes and failure modes.

The corpus and predecessor are not specification authorities. They can demonstrate what happened, not what v2 must mean.

If a material ambiguity remains, document it in `docs/plans/open-decisions.md` and keep the code reversible. Do not invent a rule that changes references, workflow transitions, permissions, document retention, or external-system behavior.
