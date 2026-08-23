# Post-implementation report

**PR #523**, merged at `7d6a948a`, deployed as release 26.

## What shipped

`QdosMailClassificationPolicy` gains a second triage tell —
`subject.engineer-triage`, the generated subject line opening `Engineer
Triage`, anchored past any forward or reply prefix and matched case-sensitively.
Both tells feed **one** triage candidate; both are recorded as separate
predicates so the decision says which fired. Policy version 3 → 4.

`docs/principal-rules-and-mappings/qdos.md` follows: Version 4, the sixth
predicate row, and why the two tells share one candidate.

## Evidence

- Corpus measurement (read-only, unmodified): 7 files carry the body phrase,
  5 carry the subject line, **0 carry both**. The five come from three
  different QDOS staff, which is what makes it a template rather than one
  person's phrasing.
- Six new Core tests: the tell; through a `Fw:` prefix; through leading
  whitespace; both tells yielding one request; a reply on a triage thread; and
  two near-misses (mid-subject mention, wrong casing) staying Unclassified.
- `EveryPredicateIsAlwaysRecordedWithAUniqueKey` moved 5 → 6, which is the
  guard working as designed.
- CI green on `ce4d646c`; Core 937 local.

## The defect this ticket introduced, and how it was caught

The first version of the tell was
`^(?:\s*(?i:RE|FW|FWD)\s*:\s*)*Engineer Triage\b` — ambiguous whitespace on
both sides of the repeated group, so a non-matching subject had exponentially
many parses. Sixteen `"Re:  "` prefixes, an 85-character subject, ran past five
seconds. It runs on every received message's subject, in Web and Worker, on
third-party input from an approved mailbox.

The simplification pass cleared that exact line. The **independent review**
caught it and blocked the merge. Corrected to
`^\s*(?:(?i:RE|FW|FWD)\s*:\s*)*Engineer Triage\b`, pinned by
`ALongPrefixChainDoesNotStallClassification`, and re-verified by the reviewer
across inputs up to 200,000 characters — linear throughout.

Recorded here and in `plan` rather than quietly fixed: a by-hand pass run by
the author over their own diff is not the independent check the repository
asks for, and this is the evidence.

## Deviations from plan

Two, both from review: the anchored form, and one further widening it caused —
a subject led by whitespace with no prefix now matches where it did not.
Benign, one-directional (nothing matches less), and now covered by a test.
