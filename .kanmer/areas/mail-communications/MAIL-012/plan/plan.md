# Plan

## 1. The tell

```csharp
private const string TriageSubjectPrefix = "Engineer Triage";

[GeneratedRegex(@"^(?:\s*(?i:RE|FW|FWD)\s*:\s*)*Engineer Triage\b",
    RegexOptions.CultureInvariant)]
private static partial Regex TriageSubjectRegex();
```

Prefixes case-insensitive (clients disagree about `Fw`/`FW`/`Fwd`); the phrase
itself case-sensitive, because the casing of generated text is part of what
makes it discriminating — the rule the body phrase already follows.

## 2. Two tells, one candidate

```csharp
var isTriageRequest = hasTriagePhrase || hasTriageSubject;
```

Both predicates are still recorded separately, so the evidence trail says which
tell fired. Only the candidate is merged. Adding a second candidate for the
same category would produce `Ambiguous` — a message carrying *both* tells would
classify worse than one carrying either, which is the opposite of the point.

## 3. Version 3 → 4

The accepted tell set is the policy. Recorded decisions keep their stamped
version, so old receipts stay readable as decisions made under v3.

## 4. Tests

- the generated subject classifies as `triage-request`;
- it is read through a `Fw:` prefix, which is how every real QDOS message arrives;
- both tells together yield one triage request, not `Ambiguous`;
- two near-misses stay `Unclassified` — the phrase mid-subject
  ("Chasing your Engineer Triage response…") and the wrong casing.

The near-misses are the ones that matter. Without them this is a `Contains`
with extra steps.

## 5. Verification

Production, after deploy: forward a QDOS `Engineer Triage - …` message and
expect a Triage rather than an Unidentified item. The existing U34 is not
retro-classified — its decision is recorded, and re-running intake over
recorded receipts is a separate capability (same boundary [[MAIL-011]] draws).

## Simplification pass — 2026-08-23

Run by hand over the branch diff (the operator's standing instruction this
session forbids delegating to the `code-simplifier` agent).

| Lens | Finding | Disposition |
| --- | --- | --- |
| **Reuse** | Reuses the existing category, subtype, reply-context flag and destination routing. Nothing new added to the taxonomy. | — |
| **Simplification** | `hasTriageSubject` could have been folded straight into `isTriageRequest` without its own local. | **Kept separate.** The predicate record needs it by itself, so folding it would mean evaluating the regex twice or losing the recorded evidence. |
| **Altitude** | The forward-prefix handling could have been a shared "strip mail prefixes" helper. | **Not extracted.** One caller. `ReplyPrefixRegex` answers a different question — whether this *is* a reply, which feeds `isReplyContext` — so a shared stripper would have to serve two purposes and would not simplify either. |
| **Efficiency** | Source-generated regex, evaluated once per classification against a subject line. | — |

Nothing was left unapplied.

## Independent review — 2026-08-23, PR #523: one blocker, and the pass above was wrong

A reviewer that did not implement the work found a **remotely triggerable hang**
in the tell added by this ticket, and the simplification pass above had
explicitly cleared the same line.

### The defect

```
^(?:\s*(?i:RE|FW|FWD)\s*:\s*)*Engineer Triage\b
```

The trailing `\s*` of one iteration and the leading `\s*` of the next match the
same whitespace, so every gap has two valid parses and a subject that fails to
match has exponentially many to enumerate. Measured on this machine, `"Re:  "`
repeated, against the shipped and the corrected pattern:

| Subject | Shipped | Corrected |
| --- | ---: | ---: |
| 12 prefixes (65 chars) | 330 ms | 0 ms |
| 16 prefixes (85 chars) | **>5 s (killed)** | 0 ms |
| 20 prefixes (105 chars) | **>5 s (killed)** | 0 ms |

`[GeneratedRegex]` carries no `matchTimeout` and the repository sets no global
one. `Classify` runs on the subject of **every received message**, in both the
Web and Worker paths, and the subject is third-party input from an approved
mailbox. Anyone able to email an approved mailbox — or a long enough genuine
reply chain — pins a core with no exception and no telemetry. Intake stops.

### The fix

```
^\s*(?:(?i:RE|FW|FWD)\s*:\s*)*Engineer Triage\b
```

Leading whitespace consumed once, outside the group; every iteration must then
consume a literal prefix, so the match is linear. Pinned by
`ALongPrefixChainDoesNotStallClassification`, which fails the build if a
24-prefix subject takes more than two seconds.

### What this says about the pass

The Efficiency lens above recorded *"Source-generated regex, evaluated once per
classification against a subject line"* and the section closed *"Nothing was
left unapplied."* Both were true and both missed the point: the cost is not how
often it runs, it is what one run can cost on hostile input. A pass run by hand
by the agent that wrote the line cleared the line it had just written.

The repository requires the pass to use "`/simplify` plus the `code-simplifier`
agent, or equivalent independent lenses". By-hand-by-the-author is not
equivalent, and this is the evidence. The independent review was the control
that worked.

### Two further review items, both applied

| Finding | Disposition |
| --- | --- |
| **The tell list existed in two places and they disagreed.** `docs/principal-rules-and-mappings/qdos.md` still said Version 3 with five predicates, and its own header claims it describes the deployed criteria. A "one list per concept" breach that `files.md` had not listed at all. | **Fixed.** The doc now carries Version 4, the sixth predicate, and why the two tells share one candidate. |
| **A reply on a triage thread now classifies as a triage request** with reply context, where under v3 it was Unclassified. The corpus contains exactly one: `RE: Engineer Triage - Our Claim Reference : 46246/1`. Downstream this is a destination view, not an allocation, so there is no case-creation risk. | **Intended, and now pinned** by `AReplyOnATriageThreadIsATriageRequestInReplyContext`. The near-miss tests covered mid-subject and wrong casing but skipped the reply case — the one the corpus actually holds. |
