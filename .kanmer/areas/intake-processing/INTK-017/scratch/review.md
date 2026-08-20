## Independent review — PR #443 (orchestrator, 2026-08-20)

Verdict: **pass** (merge armed on green CI; branch already carries ENG-004's merged commit).

- Rank-aware conflict resolution is deterministic and stays fail-closed where it matters: identical values are no longer "conflicts", typed-valid candidates (registration/mileage/date validators reused from the engine's own parsers) beat unparsable ones, earliest-fragment (instruction before appended report) wins only when unambiguous, and distinct values in the SAME fragment remain a genuine operator-review conflict. This directly removes the mechanism that blanked most of QDOS26002's fields.
- Label matching keeps ENG-004's positional rule and adds the right exception: an explicit `:`/`-` after the label is a label anywhere on the line. Values now also truncate at the next known field label — the other flattened-layout failure.
- Vehicle registration: widened synonyms (longest-first) plus a sole-unlabelled-registration fallback that yields a *suggestion* with an honest evidence line, withheld when more than one distinct registration-shaped value exists. That fixes "no VRM captured at all" without inventing certainty.
- Field-set inventory and the excluded fields (contact_*/vat_status) recorded with reasons in the plan. Red-first 8 fixtures; Core 706/706; focused InstructionDraftWebTests 5/5.
- Production re-extraction of QDOS26002 is correctly deferred to the verify stage post-deploy.
