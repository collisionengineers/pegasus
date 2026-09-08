# Research — DELIV-056: definitive instruction fixture alignment

*The research. Not the files document — this records why the scoped test inputs need correction.*

## Question

Why did the D2 failures in PR #700 run 34196369756 stop before their existing assertions, and what is the smallest correction that makes each listed fixture establish a QDOS definitive instruction under the current policy?

## Findings

- The failed CI run's checkout was PR #700 merge SHA `98f4b701b0814900006a424719c17645b7288197`. Its D2 failures are fixture setup failures distinct from the unrelated migration and ENG fixture failures in the same run.
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` defines the current QDOS instruction document signature: `QDOS`, `Registration:`, and `Our Client’s Vehicle:`; the selector requires every signal and does not use sender identity.
- The 13 ticket-named integration-test files still create automatic-allocation inputs with the old `QDOS instruction` / `Vehicle Registration:` shorthand. Such material no longer selects the QDOS profile, so its expected `CaseCreated` result cannot be reached.
- `tests/Pegasus.IntegrationTests/RetainedInstructionAnalysisTests.cs` supplies the established in-repository QDOS-shaped evidence pattern: the signature signals plus ordinary claimant and claim fields. Its use confirms the replacement is document evidence, not a transport or test-only bypass.
- `docs/frd/frd-02-intake-and-source-identity.md` requires a definitive instruction to use normal automatic allocation; it does not permit a sender or filename to stand in for document evidence.
- `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` has a separate stale assertion for a gated disabled Send to Claude control. The current pre-handoff markup omits the control and dialog; its existing absence and unauthorized-POST checks remain the applicable access assertions.
- Ownership census: UIIMP-012 lists `QdosTriageIntegrationTests.cs`, but its lease expired on 2026-09-02 and its separate Notes assertion is already in current source. CASE-045 lists `TriageQueuesWebTests.cs` and `ImageIntakeWebTests.cs`, but is in review with an expired lease and owns unrelated image-principal coverage. No live current owner holds the fixture lines in this ticket.

## Implications

- Update only the listed test fixtures so each automatic-allocation scenario supplies all three current QDOS signature signals while preserving its scenario-specific fields, assets, and assertions.
- Do not alter QDOS policy, intake production code, schemas, shared test infrastructure, source inventory, or historical ticket worktrees/claims.
- Align only the stale Send to Claude display expectation; retain the security boundary checks and do not make the action visible early.

## Open questions

- None. The ticket body, governing FRD, current policy, supplied in-repository evidence, CI inventory, and parent execution authority fix the required outcome.
