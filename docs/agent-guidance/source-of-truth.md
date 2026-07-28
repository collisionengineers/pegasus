# Source-of-truth order

Resolve each claim through its owner; do not blend sources simply because they
are nearby. Later, explicit user instructions amend earlier instructions for
the affected scope only.

1. Direct user instruction for the current task.
2. `docs/operator-notes/` for operator and business truth. Repository
   maintainers may maintain their documentation and organization under the
   user's standing authorization, but material meaning changes require direct user resolution.
3. Canonical product behavior in `docs/product/`, with living requirements in
   `docs/product/areas/` and current allocation in
   `docs/product/capabilities.md`; the retained questionnaire and worksheet
   under `docs/history/product/` are reconciliation evidence, not active owners.
4. Accepted technical decisions routed through `docs/decisions/` and retained
   decisions under `docs/architecture/decisions/`.
5. Explicitly accepted executable contracts and tests for the exact release.
6. Retrospectives for delivery constraints and observed failures.
7. The local corpus, raw references, and predecessor for real shapes and failure
   modes only.

The corpus, predecessor, supplied references, and imported source workspaces are
not specification authorities: they can show shapes, behavior and failure modes,
not what Pegasus must do. Plans describe intended work; implementation evidence
describes what a caller currently does.
Neither replaces the other, and registration or documentation alone is not
caller evidence.

If sources conflict or a material ambiguity remains, obtain direct user
resolution, record it in the appropriate canonical owner, and keep affected
work reversible. Do not invent a rule affecting references, workflow
transitions, permissions, retention, or external-system behaviour. Track an
unresolved decision in `docs/product/open-decisions.md`.
