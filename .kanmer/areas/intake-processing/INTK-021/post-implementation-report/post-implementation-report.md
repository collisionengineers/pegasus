# Post-implementation report — INTK-021

Branch task/intk-021-extraction-auto-add (91962d74). Delivered:

1. **Auto-add**: `CaseDataSnapshotFactory` writes unambiguous extracted instruction fields as **Fact** with their extraction provenance (method now `AddExtractedValue`); conflicted candidates still park as Suggestions; the case-detail rows (which already prefer Fact) therefore show the value as recorded, and staff correction remains available. Suggestion-asserting tests updated to the new contract.
2. **Real-shape coverage**: synonyms measured from the corpus (Our Client / Client Name; Our Ref / Our Reference / Claim Ref; VRN; Date of Accident / Accident on); message-subject facts (QDOS subject grammar → labelled lines, appended last so the document body outranks them); combined "Our Client's Vehicle" descriptions split into make/model/registration (two-word-make list; provenance carried from the description candidate); labelled registrations accepted in current, prefix, and suffix UK formats (`IsUkRegistration`) — the unlabelled sole-VRM fallback deliberately stays current-format-only.
3. **Engine defect fixed**: a label could match into a longer word/possessive ("Our Client" inside "Our Client's Vehicle") producing junk conflict candidates — labels now require a non-word boundary.

**Measured** (75 real accepted-route corpus instructions): claimant 4→48 (+12 honest conflicts), claim number 4→60, registration 57→68, make 13→54, model 14→47, incident date 7→45. The corpus-conditional `QdosExtractionCoverageTests` pins floors (registration/claimant ≥60%) and writes the per-field CSV to artifacts/evaluation — it runs only where the corpus exists (never committed), which is how the "real shapes, not synthetic geometry" bar is honoured without violating corpus immutability; committed fixtures use analogous shapes.

Tests: Core 847/847; suggestion-affected integration suites (CaseDataCompleteness, ProviderInspectionMode, CaseCreate, CaseVehicle, QdosIntake, QdosTriage) all green; Release build 0/0.

Deviation: subagents barred — self-reviewed.

## Verification hand-off
Post-deploy: a real instruction email produces a case with populated (not suggested) details; the coverage CSV from the release evidences the rates.
