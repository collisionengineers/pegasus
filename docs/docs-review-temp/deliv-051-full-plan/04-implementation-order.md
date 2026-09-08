# Implementation order

Temporary working reference for DELIV-051. Amended from the operator answers on 2026-09-08. Canonical documents now carry the requirements; this folder is not an additional authority.

1. Amend the plan with the operator answers and skill veto.
2. Establish index ownership, scope-sensitive verification and concise AGENTS.
3. Reconcile functional contracts and technical decisions; migrate unique rules.
4. Retire obsolete owner documents and schedule columns, retain useful procedures.
5. Repair references and amend documentation consumers; include supplied worktree edits.
6. Validate documentation, scripts, capability/ADR inventories and release support.
7. Commit and push every worktree change, update PR 702 and the ticket report.
8. Obtain an independent subagent review against ticket criteria, disposition
   findings, and post its opinion on mergeability. Do not merge or deploy.

Validation is selected by changed inputs. This amendment changes documentation
and documentation tooling, not compiled application code or renderer assets.
Run documentation placement regressions, relative-link checks, UI catalogue,
Git whitespace checks and the CI change-classifier regressions. Preserve the
prior cancelled build as non-PASS; do not claim application execution or live
behavior from documentation-check success.
