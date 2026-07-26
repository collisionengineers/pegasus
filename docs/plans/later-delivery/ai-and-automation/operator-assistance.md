# Operator assistance and AI activation

Primary plan: `P-AI`

Features: `AI-01`; `AI-02`; `AI-03`; `AI-04`; `AI-06` — V3 release work.
Status: **Planned activation/evaluation contracts; no AI caller, model, vendor or policy is implemented.**

## Boundary and universal gates

This plan coordinates activation and evaluation only. Target Core policies remain authoritative: workspace/MCP for staff assistance, mail classification/action policy for email, intake/custody policy for documents, and deterministic reference-data policy for inspection addresses. There is no generic AI manager, autonomous agent, new data store, vendor adapter or shadow workflow.

Every slice requires a direct product contract, privacy/data-transfer/security assessment, model/vendor/licence/cost approval, representative evaluation with independent holdout where appropriate, explanation and correction design, human-review boundary, abuse/error testing, rollback, and a named intended caller. Model output is a candidate, never authority to allocate a case/reference, alter immutable identity, send a message, move mail, create a lifecycle transition or overwrite staff-confirmed data. Permanent action history and content-safe telemetry remain separate.

## Slices

### In-app staff assistant

Feature: `AI-01` — V3 release work.

An authenticated Web/MCP caller may expose only a directly approved staff-assistance contract. It needs an explicit decision on task scope, information access, answer attribution, tool permissions, correction/escalation, retention and evaluation. It is not conditional on rule-based insufficiency; it is V3 activation-gated by its own approvals.

### Assist email identification and actions

Features: `AI-02`; `AI-03` — V3 release work.

Mail Core remains the policy owner. These candidates activate only if accepted evidence shows the approved rule-based behavior is insufficient, and after the mailbox dossier plus AI gates are met. Ambiguous/low-confidence output must remain reviewable without automatic classification, folder move, association, action, send or case transition. Intended callers are the V2 workspace/MCP or approved Worker path, never a transport-specific classifier.

### Assist document extraction and review

Feature: `AI-04` — V3 release work.

Intake/source-custody Core owns source and review policy. This activates only if accepted evidence shows rule-based behavior is insufficient. It must preserve source provenance, present bounded candidates for staff review, and fail closed before case/reference allocation on missing, contradictory or low-confidence identity data. An intended intake-review caller and genuine local evaluation are required before any external processing.

### Assist inspection-address selection

Feature: `AI-06` — V3 release work.

The existing deterministic reviewed-reference-data path remains authoritative. This activates only if accepted evidence shows that rule-based/reference behavior is insufficient. A suggestion cannot overwrite confirmed inspection identity/address, invoke a geocoder, or silently select a repairer; ambiguity remains visible for review. The intended caller is the approved operator workflow, not a background auto-update.

## Evidence, rollout and deferred impact

Focused contract/negative tests, independent evaluation and a caller-backed operator review precede any exact approved shared-development use. Roll out to one approved slice/cohort with kill/revert control and retained candidate/decision evidence; recover by disabling the AI caller and retaining prior human decisions. No corpus upload, model prompt/content transfer, external account, feature flag, vendor, model deployment, autonomous action or cross-feature AI service is created now. The rule-insufficiency qualifier applies only to `AI-02`, `AI-03`, `AI-04` and `AI-06`—not `AI-01` (or separately owned V2 `AI-05`).
