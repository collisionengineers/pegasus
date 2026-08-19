# Open questions — MAIL-02

- [x] **Should successfully classified messages be aggregated into a generic Other destination?** — No. Every recognised example remains its own named category/subtype. Acknowledgement remains `General / acknowledgement`; autoreply, undeliverable, billing, remittance, case update, cancellation, images-before-instruction and every other known example retain their own classifications.
- [x] **What is `Other`?** — Only the explicit taxonomy escape hatch for a genuinely new classification not covered by the canonical registry. It requires a new category name and reasoning and must not hide a known category.
- [x] **What is `Needs sorting`?** — The fail-closed destination for mail that cannot be safely matched/classified/routed because evidence is missing, unsupported, contradictory or ambiguous. It is not a category and is not interchangeable with Other.
- [x] **Where is the exhaustive definition owned?** — FRD-08 will contain one canonical in-repo classification and folder catalogue covering every category/subtype, criteria, evidence/method, ambiguity/failure behaviour, operational destination and designated Outlook folder type.

## Parked (explicitly deferred)

- [x] **Does MAIL-02 need to activate exact automatic predicates, confidence rules, or holdout thresholds beyond delivered routes?** — No. Resolved with the operator on 2026-08-19: MAIL-02 maps an already-existing classification to an operational destination; it does not keep that mapping dark and does not own automatic classification predicates. The delivered QDOS route and manual classifications may consume the policy through their callers. Additional provider-route activation remains owned by route-specific tickets such as [[TICK-036]], [[TICK-037]], and [[TICK-038]], under the evidence gates in `docs/open-decisions.md`; do not create a duplicate ticket here.
- [ ] Real Outlook/Graph/cloud activation and live verification requires exact-target approval.
