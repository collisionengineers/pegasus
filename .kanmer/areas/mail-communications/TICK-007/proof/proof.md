# Proof — TICK-007 (EVAL-05)

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #463); promoted to `main` (`39bb118a`). `deployment: n/a` — the display surface is the ADR-0016 standalone desktop evaluator, deliberately outside the deployed application.

- Verification lane at the cut: the evaluator renders the rule-generated suggestion beside the human review — category, subtype, matched-predicate evidence lines, and `policy {key} v{version}` — sourced from `QdosMailClassificationPolicy` (the sole `IMailClassificationPolicy` owner, the same class the app composes); classification failure degrades to "Suggested: No category" and never blocks review or filing; the suggestion is recorded read-only beside the reviewer's decision. Test `RuleClassifiedEmailPopulatesCategorySubtypeEvidenceAndPolicyVersion` present.
- Boundary honestly kept: nothing renders in Pegasus.Web (in-app rule display belongs to MAIL-21/22); the tool stays out of `Pegasus.slnx` and every deployment unit.
