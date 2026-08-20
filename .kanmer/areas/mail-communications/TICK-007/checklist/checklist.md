# Validation checklist

## Migrated validation — [[TICK-008]]

- [ ] Run provider-specific instruction-email categorisation against real `.eml` files through the local folder-based evaluator. (Manual GUI verification against real reviewer files — outside this automated implementation pass; recommended as the reviewer's own first-run smoke check. `corpus/` may not be used for this: it is local, ignored, and immutable per repository rules.)
- [x] Record the rule-generated category and evidence beside the human review. — `EmailEvaluationWorkflow.LoadCurrentAsync` now calls the Core `IMailClassificationPolicy` and renders category, subtype, matched-predicate evidence and policy version into the existing `Suggestion` slot; proven by `RuleClassifiedEmailPopulatesCategorySubtypeEvidenceAndPolicyVersion`.
- [x] Preserve the local-only evaluator boundary; do not treat this as production or live-service acceptance. — No Outlook/Box/Azure/deployment touch; the desktop project stays outside `Pegasus.slnx`; classification runs against local `.eml` bytes only.
