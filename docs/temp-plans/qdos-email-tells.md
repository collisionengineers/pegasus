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

## The Audit tell is in the attachment filename, not the subject

QDOS attaches a generated instruction letter, and its filename states which
kind of work is being instructed:

| Attachment filename pattern | Meaning | Emails |
| --- | --- | ---: |
| `…LtrtoAuditEngin….pdf` | Letter to Audit Engineer — **Audit instruction** | 27 |
| `…LtrtoEngineerInstructionCollisionEngineersLtd….doc` | Letter to Engineer Instruction — **Inspection instruction** | 17 |

Examples: `39980_1_LtrtoAuditEngin.pdf`, `48692_1_LtrtoAuditEngin.pdf`,
`1_LtrtoEngineerInstructionCollisionEngineersLtd-V1.doc`. The leading number is
the claim, so the claim reference appears in attachment names as well as
subjects.

**No email carries both letters.** The two sets are disjoint across all 329,
which makes this a clean separator rather than a weighted signal.

### This corrects an earlier reading

`EREF` does **not** separate Inspection from Audit. Audit-letter emails appear
under EREF 9, 10, 11, 13, 15, 17, 21, 22, 23, 24, 28 and 35, and
Inspection-letter emails appear under EREF 5, 6, 7, 8, 9, 10, 11, 13 and 15 —
the codes overlap heavily. `EREF` remains a useful type hint for other
categories, but the Inspection/Audit distinction is carried by the attachment.

### Supporting, not decisive

- 26 of the 27 audit-instruction emails come from `nduncombe@qdosassist.co.uk`,
  the highest-volume sender. Suggestive of a handler specialism, but a person
  can change role, so this must never decide a category.
- 56 emails attach a third-party or bodyshop engineer's report
  (`Bodyshopreport-V1.pdf` ×21, `Bodyshopsuppreport-V1.pdf` ×17,
  `TPIengineersreportforCLV-V1.pdf`, `EngineersReport-V1.pdf`). Consistent with
  Audit work, since an Audit reviews another firm's report — but the count far
  exceeds the 27 audit letters, so an attached third-party report alone does
  not mean Audit.

Nothing found so far distinguishes **Inspection + Audit** from either single
type. That may be correct rather than a gap: `requirements.md` says
Inspection + Audit begins with the normal Inspection reference and the Audit is
created inside the case later, after the Engineer records the assessment. If so
it is not an intake-time distinction at all, and only standalone Audit needs
detecting at intake. This needs operator confirmation.

## What the bodies show

All 329 bodies were extracted and searched. Keyword hits: `inspection` 51,
`repairable` 48, `assessment` 39, `triage` 38, `audit` 31, `total loss` 19,
`estimate` 10, `salvage` 7. Zero hits for "double check" and "previous
engineer".

The `audit` mentions are **not** instruction language. They are conversational,
and mostly chasers for work Collision Engineers owes:

- "Please can you forward your final audit report as soon as possible." —
  repeated near-verbatim from `accounts@`, the largest cluster
- "Can we do an audit report on this or is this the audit figures"
- "it was cancelled our end as we couldn't do an audit on it"
- "See attached initial and AUDIT Pav reports"

So body text mentioning "audit" indicates an existing audit case being chased
or queried, not a new audit instruction. Treating the word as an instruction
tell would misclassify chasers as new work.

One body shape is worth noting: EREF5 contains the instruction letter inline as
formatted text — `Our Ref:`, `Date:`, `Dear Sirs`, `Our Client:`, `Our Client's
Vehicle:`, `Registration:`, `Date of Accident:`. So the same instruction
content arrives sometimes inline and sometimes as an attached document, and
extraction must handle both.

## Proposed tells, in priority order

1. **Claim reference** — primary identity; drives new versus existing. Present
   in 326 of 329, in subjects and in attachment filenames.
2. **Instruction-letter attachment name** — decides Inspection versus Audit.
   Disjoint, generated by QDOS, and the only clean separator found.
3. **`EREF` code** — email type for the remaining categories, once the operator
   confirms the mapping. Not usable for Inspection versus Audit.
4. **Subject prefix** (`RE:`, `FW:`, `Automatic reply:`) — reply and
   auto-reply context; `Automatic reply:` maps to the settled `General` /
   `autoreply` subtype.
5. **Sender mailbox** — `accounts@` concentrates in chasing and billing;
   `nduncombe@` in audit instructions. Supporting only, never decisive.
6. **Attachment shape** — images and video versus PDF-only; supporting only.

Body keyword matching is explicitly **not** proposed as a tell. The evidence
above shows the word "audit" in a body signals an existing case being chased,
which is close to the opposite of a new audit instruction.

Incident kind (`RTA`, `PL`) and incident date are extraction fields, not
category tells.

## What still has to be answered

- The `EREF` code to category mapping, confirmed by the operator. This is the
  single highest-value confirmation available and it is a review, not a sort.
- Whether the instruction-letter filenames are stable and generated by QDOS's
  system rather than typed by a handler. The whole Inspection/Audit split rests
  on this, so it needs operator confirmation before it is built.
- Whether **Inspection + Audit** is an intake-time distinction at all, or
  whether it only appears later inside an existing Inspection case as
  `requirements.md` implies.
- Whether `EREF` numbering is stable over time or QDOS reuses and renumbers
  codes. If it can change, the classification policy version must record which
  mapping was in force.
- Coverage limits of this sample: 27 audit and 17 inspection instruction
  letters is thin for threshold setting, and `qdoslaw.co.uk` contributes only
  2 emails with a different reference format entirely.

## How this evidence was produced

Read-only scripts over the working copy, with outputs beside it under
`artifacts/evaluation/qdos-emails/`:

| File | Contents |
| --- | --- |
| `index.csv` | 329 rows: source path, sender, date, subject, attachment flag |
| `eref-profile.csv` | Per-email EREF code, senders, attachment names and extensions |
| `subject-parse.csv` | Parsed subject fields: EREF, claim ref, incident kind/date, prefix |
| `body-analysis.csv` | Decoded body text, attachment name list, keyword hit flags |

Nothing under `corpus/` was modified, and no email content is committed.
