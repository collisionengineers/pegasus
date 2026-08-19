# Research — MAIL-04

## Question

What existing owner, caller, persistence boundary, and acceptance surface should carry explainable classification evidence, policy version, and correction history without duplicating business policy?

## Verified findings

- `docs/frd/frd-08-email-mailbox-and-background-processing.md` is the canonical behavioural owner. It requires source identity, policy key/version, outcome, material evidence, ambiguity/confidence facts, actor and time; corrections append structured before/after history and never erase the original decision.
- `Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` is the single Core taxonomy/result owner. `QdosMailClassificationPolicy` supplies policy identity and predicate evidence, and `ProcessIntake` is the real classification caller.
- `EfIntakeReceiptStore` persists the current classification decision and predicate evidence; `EfRetainedMailboxMessageStore` projects retained messages for the Mail UI. The current decision record is replaced during re-evaluation, so append-only correction history is the material missing behaviour.
- `Pages/Mail/Message.cshtml(.cs)` already displays the current retained-message classification. The workspace should extend this caller rather than create a parallel classification implementation.
- Existing classification tests cover policy decisions and persistence, but not an authorised correction/reversal history and deterministic downstream recomputation.
- The operator's 2026-08-18 instruction to drive the email-workspace epic through functional completion designates this post-alpha capability for implementation. It does not approve any real Outlook or cloud mutation; tests must use local/in-memory/SQL fixtures and mailbox adapters/fakes.

## Implications

Reuse the Core taxonomy and existing receipt/retained-mail stores. Add one Core-owned correction command/port and append-only persistence/audit shape, then expose it only from exact-message detail with reason and explicit confirmation. Preserve the prior decision and fail closed on stale identity/version, invalid category, absent actor/reason, or unsupported mailbox state. No production mailbox write is needed for this ticket.

## Additional research from a previous implementation

### Provenance and scope

The findings below come from a **previous implementation** and are reference evidence for explainability/audit design. Project-specific naming has intentionally been omitted. They do not replace Pegasus's canonical FRD or prove live activation.

### Versioned classification is operationally useful

The previous implementation's deterministic classifier carried an explicit taxonomy/policy version. That creates a practical precedent for persisting the version that produced each decision so later corrections, re-evaluations and regression results can be interpreted against the rules that actually ran at the time.

For Pegasus, policy key/version should remain part of every automated classification decision and should also be captured when a human correction causes downstream recomputation.

### Machine suggestion and human decision were kept distinct

Inbound persistence in the previous implementation protected human-classified rows from later automated category/subtype/confidence overwrites. It also retained the automated suggestion separately so the machine proposal could be compared with the eventual staff decision.

This is directly relevant to MAIL-04: the audit model should preserve at least three concepts rather than replace one field in place:

- the original automated classification and evidence;
- the current accepted/human-corrected classification;
- the append-only correction/re-evaluation history connecting them.

A later classifier run should not silently erase a deliberate human correction unless the product policy explicitly defines and records that transition.

### Ambiguity should be persisted as evidence, not hidden

The previous implementation represented provider ambiguity, multiple case candidates, conflicting reference/VRM evidence and unmatched identities explicitly. This is useful explainability data because it records **why the system abstained**.

Evidence worth retaining with a classification/correction decision includes, where available:

- exact message identity key (`source mailbox` + RFC `Internet-Message-ID`);
- policy key/version;
- matched predicates/reasons;
- sender/provider identity evidence and whether it was exact-address, exact-domain, ambiguous or unmatched;
- attachment/document provider evidence separately from sender evidence;
- candidate Case/PO/job references and VRMs extracted from the message;
- live case-correlation result, including zero/one/multiple candidates and any conflict veto;
- reply/forward signals such as `In-Reply-To`, `References` and subject-prefix fallback;
- final category/subtype plus ambiguity/abstention state;
- actor, timestamp and reason for any human correction.

This does not mean every evidence field belongs in one table; it means the correction/history surface should be able to reconstruct the material decision evidence without relying on mutable mailbox state.

### Exact duplicate delivery and duplicate business work need separate audit semantics

The previous implementation distinguished duplicate `Internet-Message-ID` delivery from multiple messages on the same case and from a duplicated business instruction. MAIL-04 should preserve that distinction in evidence/history so a dedupe event is not misreported as a classification correction or a duplicate instruction.

### Useful correction-history shape

A robust append-only correction record should be capable of recording:

```text
message identity
previous decision snapshot / decision id
new decision
policy key + version involved
material evidence snapshot or stable evidence references
actor
reason
timestamp
resulting downstream recomputation/version
```

This shape is a research recommendation combining the previous-implementation behaviour with the existing Pegasus requirement that corrections never erase the original decision.

### Regression/evaluation evidence

The previous implementation contained a sizeable human-labelled `.eml` evaluation corpus, but its labels belonged to an older taxonomy. This is still useful for MAIL-04 as historical regression material: examples can be remapped to the current Pegasus taxonomy and used to verify that policy-version changes are explainable and that correction outcomes remain reproducible.

The key rule is not to import those historical labels as current product vocabulary. Store the Pegasus policy version and expected current outcome alongside any reused regression fixture.

### Additional acceptance implications

MAIL-04 tests should cover:

- automated decision -> human correction while preserving the original decision/evidence;
- later automated re-evaluation does not silently overwrite the accepted human correction;
- ambiguous/unmatched decisions retain the evidence explaining abstention;
- stale message identity/version or stale correction precondition fails closed;
- correction history identifies actor, reason, timestamp and before/after values;
- duplicate delivery does not create a fake second classification/correction history entry;
- policy-version change remains distinguishable from a human correction;
- exact-message detail can explain the current result from durable evidence without requiring the message to remain in the same Outlook folder.

No Outlook mutation is required to prove these behaviours.
