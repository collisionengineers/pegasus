# Independent review — PR #542 — 2026-08-25

Reviewer: independent review agent; not the implementer.

## Changes

- `CaseEvaMapping.cs` removes `EvaMappingAcceptance`, the activation message/check, nullable source and always-empty mapper blockers; it restores mapping metadata version 2 without making it a switch.
- `EvaBundleSchema.cs` retains format-version validation while removing acceptance-evidence validation.
- `DependencyInjection.cs`, `EvaHandoffStore.cs`, and `Program.cs` remove the obsolete acceptance dependency/configuration while preserving Review, authorization, image-byte, replay, history and first-send behavior.
- `platform.bicep` removes the three unused `Eva__AcceptedMapping__*` values.
- Core tests update the mapper/bundle contracts; integration/browser tests prove the export journeys run without activation configuration; the architecture test prevents the deleted type, settings and legacy message from returning.
- FRD-07, current architecture, operations and runbook align the documented behavior and deployed-state caveat with the change.

## Comments and disposition

- No blocking findings.
- Non-blocking: the post-implementation report groups changes by subsystem rather than enumerating all 16 paths. Disposition: won't-do-because every changed path is nevertheless accounted for by an accurate rationale, the PR contains one focused commit, and no behavior or scope is concealed.
- The runbook edit is not named in the files document. Disposition: won't-do-because it directly corrects the existing verification contract contradicted by this defect and is explicitly reported in both the PR and post-implementation report.

## Verdict

PASS. The plan did not miss behavior required by ENG-018 or FRD-07; the implementation matches the plan and removes the complete obsolete activation path without adding compatibility machinery. The simplification pass is present and honest: it reused the existing export pipeline, deleted rather than replaced the gate, and intentionally left non-operator internal handoff names unchanged.

Checked: ticket, gates, files/plan/checklist/report/open questions; FRD-07; every PR file and complete diff; clean one-commit branch against `dev`; `git diff --check`; absence searches for the removed type/config/message; focused Core export tests (26 passed), architecture regression (1 passed), and both end-to-end export journeys (2 passed). GitHub changes, documentation, infrastructure, local-development-scripts and reference-data checks are green; unit, browser and three SQL integration jobs were still running when this verdict was recorded. Merge intentionally not performed per review assignment.
