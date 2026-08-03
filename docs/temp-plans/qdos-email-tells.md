# QDOS email tells: evidence from 329 genuine emails

Evidence for [`qdos-email-classification`](qdos-email-classification.md).
Read-only analysis of the local corpus. Source emails stay in `corpus/`; the
working copy and generated indexes are under
`artifacts/evaluation/qdos-emails/`, which is git-ignored.

## What was analysed

329 emails whose sender is a QDOS domain, drawn from 1040 local `.eml` files.
327 from `qdosassist.co.uk`, 2 from `qdoslaw.co.uk`, across 27 distinct
senders. This is a partial sample of real traffic, not a designed cohort, so
counts show what exists rather than true frequency.

## The subject line is templated

QDOS generates its subjects to a fixed shape:

```
(EREF8) RTA on 18/06/2026 : Mr Nick Jones (Our Ref: MFI/AKH/46553/1, Vehicle: ...)
 ^^^^^  ^^^ ^^^^^^^^^^^^   ^^^^^^^^^^^^^   ^^^^^^^^^^^^^^^^^^^^^^^
 type   kind  incident date  claimant        claim reference
```

| Part | Meaning | Present in |
| --- | --- | ---: |
| `(EREF<n>)` | QDOS's own email-template code | 316 / 329 |
| `RTA` / `PL` | Incident kind (285 RTA, 2 PL) | 287 / 329 |
| `on DD/MM/YYYY` | Date of the incident | 287 / 329 |
| `: <name>` | Claimant name | most |
| `Our Ref: …/NNNNN/N` | QDOS claim reference (operator-confirmed) | 305 / 329 |
| Claim number `NNNNN/N` anywhere in subject | The durable claim identity | **326 / 329** |
| `RE:` / `FW:` / `Automatic reply:` prefix | Reply or forward context | 49 / 329 |

Optional trailing fields also seen: `Vehicle:`, `Registration:`, `Your Ref:`.

## New case versus existing case

**The claim reference is the answer, and it is present in 326 of 329 emails.**

The rule is a lookup, not a guess: extract the claim number, look it up. Known
means existing work; unknown means potentially new. This needs no keyword
matching and no confidence score.

In this sample:

| | Count |
| --- | ---: |
| Distinct claim references | 226 |
| References appearing once | 159 |
| References appearing more than once | 67 |

The prefix reinforces it: `RE:` on 42 emails means an existing thread, never a
new instruction.

Caveat: a known claim reference proves QDOS has an existing claim. It does not
prove Pegasus has a Case — early emails on a claim arrive before any Case
exists. So the lookup answers "have we seen this claim", and Case association
remains a separate step under the existing matching rules.

## EREF is the email-type code, and it moves through a case

The same claim carries different EREF codes as work progresses. Claim
`46913/1`, in order:

```
EREF5   akhoyer        initial
EREF6   jmoseley
EREF9   lbirchenough
EREF12  nduncombe
EREF12  engineers      RE: ...
```

And a claim can repeat one code across a thread — claim `46528/1` has seven
emails, all `EREF8`, six of them `RE:`.

So `EREF` identifies **what kind of message this is**, not where the case has
got to. That makes it a strong candidate for the category predicate, which is
exactly what MAIL-22 needs.

### What the codes mean is not yet established

Codes seen: 1–26, 28, 29, 31, 33–39, 42, 87. Volume is concentrated in 5–13
(203 of 329).

Two patterns are visible but **unconfirmed**, and must not be built on until
the operator confirms them:

- Low codes (5–13) carry images and video (`.jpg`, `.png`, `.mp4`, `.mov`) and
  come from many different senders — consistent with instruction and image
  traffic.
- Higher codes (21–42) carry only `.pdf` and `.png`, and come largely from
  `accounts@qdosassist.co.uk` — consistent with billing and post-report
  correspondence.

A code-to-category mapping is the single most valuable thing to confirm, and
confirming it is an operator review, not an inference.

## The 13 without an EREF

Not QDOS's automated template, and they matter:

- 2 from `qdoslaw.co.uk`: `Our ref: ELM/NAK0011 - Mutual Client: … - Vehicle
  Reg; LT17 UCU` — a different reference format entirely.
- `Engineer Triage- Our claim Reference : 46684/1 , vehicle registration : …`
  — these are Triage requests, matching the separate Triage workflow.
- Bare `46670/1 - Mohammed Jameel` — claim number with no template at all.

These prove the classifier cannot depend on `EREF` alone. The claim-number
extraction still works on all of them, which is another reason to treat the
claim reference as the primary key and `EREF` as the type hint.

## Proposed tells, in priority order

1. **Claim reference** — primary identity; drives new versus existing.
2. **`EREF` code** — email type, once the operator confirms the mapping.
3. **Subject prefix** (`RE:`, `FW:`, `Automatic reply:`) — reply and
   auto-reply context; `Automatic reply:` maps to the settled `General` /
   `autoreply` subtype.
4. **Sender mailbox** — `accounts@` concentrates in the higher codes; a
   supporting signal, never decisive alone.
5. **Attachment shape** — images and video versus PDF-only; supporting only.

Incident kind (`RTA`, `PL`) and incident date are extraction fields, not
category tells.

## What still has to be answered

- The `EREF` code to category mapping, confirmed by the operator.
- Which codes, if any, mean standalone Audit. Nothing in the subject template
  distinguishes Inspection from Audit, so this may live in the body or the
  attachments rather than the subject.
- Whether `EREF` numbering is stable over time or QDOS reuses and renumbers
  codes. If it can change, the classification policy version must record which
  mapping was in force.
