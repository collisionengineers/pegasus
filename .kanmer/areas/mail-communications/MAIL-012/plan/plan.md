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
