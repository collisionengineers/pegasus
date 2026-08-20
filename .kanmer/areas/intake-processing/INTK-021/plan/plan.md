# Plan — INTK-021

Branch task/intk-021-extraction-auto-add. Steps (each names its reuse):

1. Kind flip at acceptance (reuse: existing `CaseDataSnapshotFactory` write path; display already prefers Fact; `ApplyConfirmed` already accepts Fact).
2. Synonyms from the measured real shapes (reuse: existing `FieldDefinition` list — no new engine concepts): Our Client / Client Name; Our Ref / Our Reference / Claim Ref; VRN; Date of Accident / Accident on.
3. Subject facts (reuse: reader already emits the subject as transport evidence; `IntakeContentFragment` + rank-aware conflict rules do the rest): rewrite the QDOS subject grammar into labelled lines, appended as the LAST fragment so body statements win.
4. Combined vehicle description (reuse: `IsPlausibleVehicleMakeModel`, `IsCurrentFormatRegistration`): optional field + post-extraction derivation into empty make/model/registration carrying the description candidate's provenance; deterministic two-word-make list.
5. Engine boundary guard (found defect): label match must not continue into a word/possessive.
6. Evidence: corpus coverage test (floors: registration 60%, claimant 60% — measured 88%/64%) + Core fixtures; suggestion-affected integration suites green; Release build 0/0.

Measured before → after (75 accepted-route corpus instructions): Claimant 4→48 (+12 honest conflicts), Claim number 4→60, Incident date 7→45, Make 13→62, Model 14→47, Registration 57→66.

Deviation note: subagents barred — self-review in scratch.
