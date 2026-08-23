# Proof — production, 2026-08-23

Tier: **production**. Release 26 (`7d6a948a`), against the database wiped clean
by [[PLAT-040]].

The operator forwarded a QDOS `Engineer Triage - …` message — the family this
ticket exists for, and the one that carries **no** `Triage Only Request` body
phrase. Read from `IntakeMailClassificationDecisions`:

| Receipt | Outcome | Family | Subtype | Case type | Policy version |
| --- | --- | --- | --- | --- | ---: |
| `2029243b…` | classified | new-instruction-received | audit | audit | 4 |
| `ba45fbbd…` | classified | new-instruction-received | inspection | inspection_and_audit | 4 |
| **`d42a5515…`** | **classified** | **pre-instruction-emails** | **triage-request** | *(none)* | **4** |

The third row is the fix. Under version 3 that message matched no predicate at
all and fell through to `Unclassified`; under version 4 it classifies as a
triage request on the strength of its generated subject line.

The operator confirms the same from the surface: *"Identified in the inbox as
Triage."*

Two further checks in the same batch:

- The two instruction emails still classify correctly as `audit` and
  `inspection`, with `CaseType` resolved — the new tell did not create a second
  candidate or an `Ambiguous` outcome anywhere.
- `PolicyVersion` reads **4** on all three, confirming the deployed policy is
  the one this ticket shipped.

## What this proof does not cover

A triage-request classification is recorded and rendered, but **nothing
downstream acts on it**: no Triage was created and the message does not appear
in the Triage queue. That is a separate and larger defect — the triage-creation
gate is composed with `NoAcceptedIntakeTriageMatcher` in production — filed as
[[INTK-031]]. Classifying the message was this ticket's scope, and it is
proved.
