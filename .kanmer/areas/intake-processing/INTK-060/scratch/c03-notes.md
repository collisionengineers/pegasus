## C03 batch 1 (QDOS, PCH, selector, corpus scaffold) — assumptions and deviations

Worktree `C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c03`, branch
`c03-profiles`, head `0f1355108`. Build-only; the controller runs the waves.

### Assumptions (headless, recorded per M8)

- [ ] ASSUMPTION 1 (implementer, attempt 1): the clock default for an absent instruction date
  becomes opt-in per field definition (`DefaultsToProcessedDate`) and stays ON for QDOS,
  OFF for PCH — because the invariants say today's date is never an extracted fact, while
  the dispatch says existing QDOS corpus output must stay unchanged except the named
  corrections; alternatives: remove the default outright (changes QDOS production output
  beyond the named corrections), or keep it global (a new profile would emit today's date).
- [ ] ASSUMPTION 2 (implementer, attempt 1): PCH's two recorded fingerprints are TEMPLATE
  VARIANTS of one profile, not two profiles — because four of the five originals print both
  firm names in one footer sentence, so two profiles would make the PRINCIPAL ambiguous when
  only the template is; the profile signature is the two labels the fingerprints share and
  the brand signals are the variants, so no signal is invented. Alternatives: register two
  profiles (four of five originals extract nothing), or invent a broader single signature
  (evidence nobody accepted).
- [ ] ASSUMPTION 3 (implementer, attempt 1): PCH declares no repairer, storage or hire-rates
  APPENDIX rule — because no recorded original carries such a block; the labelled fields
  exist and stay unavailable. Alternatives: write label rules with no evidence behind them.
- [ ] ASSUMPTION 4 (implementer, attempt 1): the corpus test asserts zero WRONG identity and
  zero neighbouring-value leakage, and MEASURES recall/ambiguity/missing without a floor —
  because the plan forbids a claimed accuracy threshold without operator-labelled holdouts,
  and each method file's suggested gate is exactly "zero wrong accepted
  principal/VRM/reference/date". Alternatives: assert a coverage floor (a threshold nobody
  accepted) or assert presence per field (fails on the three genuinely row-shifted QDOS
  originals the labeller recorded as ambiguous).
- [ ] ASSUMPTION 5 (implementer, attempt 1): party/reference roles are declared ON the field
  definition and exposed through a new Core-owned `IInstructionFieldRoles` in the selector
  file — because `IntakeContracts.cs` is not in the C03 file map and `InstructionReviewField`
  cannot carry a role without editing it. Alternatives: edit `IntakeContracts.cs` (outside
  scope), or leave every recorded candidate's role null (the plan requires roles).

### Deviations from the C03 file map

- `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` EDITED (not in the C03 file
  map). `IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary` pinned Core to exactly
  one `IInstructionExtractionPolicy` implementation, which contradicts C03's fifteen. The
  rule now asserts what it protected: every implementation is Core's and in
  `Pegasus.Core.Intake`, none is duplicated in Infrastructure, and `ProcessIntake` still
  takes the interface and no concrete policy. Leaving it would have failed the wave for a
  non-defect.
- `tests/Pegasus.IntegrationTests/QdosMappingExtractionTests.cs` NOT edited, and its pinned
  Make/Model expectations for seven mapping-corpus emails are now stale (the split is gone;
  the combined text lives in `Vehicle description`). The suite is gated on the local
  `corpus/qdosmapping` tree, which is absent on this machine, so it SKIPS rather than fails.
  It needs an owner: convert its Make/Model rows to one `Vehicle description` row.

### Snapshot regeneration for stream A

- One tracked source changed: `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`.
  New `normalized-lf` SHA-256 `9ce94b6896bea8e4fc15196c51a618dc50d2ced65f0cdf4c01c3d18e3e4a5fbb`.
  The corpus JSON's `sourceSnapshots` was NOT edited here.
- No other entry in `principal-identification-corpus.v1.json` `sourceSnapshots` changed.

### DI hazard for stream A

`ProcessIntake` takes a single `IInstructionExtractionPolicy` and must keep receiving QDOS
(QDOS is the only profile with automatic allocation), while
`InstructionExtractionPolicySelector` consumes `IEnumerable<IInstructionExtractionPolicy>`.
Registering PCH under the same interface makes the single-instance resolve
registration-order dependent. A must make it explicit rather than incidental.
