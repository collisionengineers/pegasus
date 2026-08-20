# Proof — INTK-017

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #443), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: rank-aware conflict resolution (`ResolveConflictingCandidates` — typed-valid beats unparsable, earliest fragment wins, same-fragment conflicts stay conflicts), typed validation on registration/mileage/dates, sole-VRM fallback (fail-closed on multiple), label-boundary truncation, registration synonyms; delivered field set = Claimant name, Claim number, Vehicle registration/make/model/mileage, Accident circumstances, Date of incident, Instruction date, Inspection address, Inspection date. 17 fixture tests at the cut.
- Disclosed scope trim (operator to confirm): contacts (name/email/phone) and VAT status deferred — no persisted pathway and no verified evidence the QDOS form carries them.
- QDOS26002's stored wrong suggestions are data from the old rules: correct/decline them on the case or re-send the instruction; new intakes extract with the fixed rules (pinned by the literal production-value fixture in ENG-004's test).
- Full transcript: DELIV-013 scratch.
