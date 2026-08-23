---
id: MAIL-012
type: ticket
title: >-
  Classify QDOS's other triage template, the one with no "Triage Only Request"
  phrase
status: implementing
area: mail-communications
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-23T13:01:15.414Z'
taken_at: '2026-08-23T13:00:43.580Z'
branch: task/qdos26012-regressions
worktree: ../pegasus-worktrees/qdos26012-regressions
labels:
  - u34
  - production-defect
  - found-during-qa
  - classification
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T12:49:17.222Z'
updated: '2026-08-23T13:01:15.414Z'
---

## QDOS sends triage requests in two templates; we recognise one

`QdosMailClassificationPolicy` accepts exactly one triage tell: the literal
phrase **`Triage Only Request`** in an email body, matched `Ordinal`.

U34 is a triage request. Checked in production:

| Tell | U34 |
| --- | --- |
| `Triage Only Request` in the body | **ABSENT** |
| `Engineer Triage` in the subject | PRESENT |

The subject is never consulted for the triage predicate, and U34's only
attachment is a JPEG, so neither document title matches either. **No predicate
matches at all** — the message falls through to `Unclassified`.

So [[MAIL-011]] is necessary but not sufficient. With it, U34's route resolves
and it stops being Unidentified; it then classifies as Unclassified rather than
as a Triage.

## The corpus says these are two disjoint families

Measured over `corpus/` (read-only; never modified):

| Contains | Files |
| --- | ---: |
| `Triage Only Request` | 7 |
| `Engineer Triage` | 5 |
| **Both** | **0** |

Zero overlap. The `Engineer Triage` family — five messages from **three
different QDOS staff** (`lbeatie@`, `abruce@` ×3, and U34's `randerson@`) —
has never been classifiable, and never will be under the current tell.

The subjects, verbatim:

```
Engineer Triage - Our Claim Reference : 46246/1 - Vehicle …
Engineer Triage - Our Claim Reference 46384/1 , Vehicle Registration …
Engineer Triage - Our Claim Reference 47899/1, Vehicle Registration …
Engineer Triage - Our Claim Reference 47902/1, Vehicle Registration …
Engineer Triage - Our Claim Reference 47939/1, Vehicle registration …   (U34)
```

Separators and the casing of "Registration" drift between them; the prefix
`Engineer Triage` does not. Three senders writing the same opening is a
generated template, not one person's habit — the same standard of evidence the
existing tells were derived from.

## Why this is a policy change, and what stops it going wrong

- `QdosMailClassificationPolicy.Version` goes 3 → 4. The tell set is the
  policy.
- The new predicate must **not** be a second candidate. Two candidates for the
  same category resolve to `Ambiguous`, so a message carrying both tells would
  become unclassifiable — worse than today. The two tells feed one triage
  candidate; both stay visible as separate recorded predicates.
- Anchored, not `Contains`. All real QDOS mail arrives as a staff forward, so
  the subject reads `Fw: Engineer Triage - …`; the tell is the phrase at the
  start of the subject once forward and reply prefixes are stripped. A human
  typing "about your Engineer Triage query" mid-subject is not the tell — the
  same distinction the policy already draws for the body phrase.

## Operator check

This adds an accepted classification tell, which is operator truth. The
evidence above is measured rather than assumed, and the operator asked
directly (2026-08-23) whether U34 should have been a Triage. Recorded here so
the change is reviewable as a policy decision, not buried as a bug fix.

**Not in scope:** whether a classified Triage then opens Triage work.
`operator-notes.md` says a Triage stays Unidentified until a registration is
known — U34's subject carries `GD65TVY`. That is a downstream capability to
verify after deploy, not to build here.
