# Checklist — CASE-031

- [ ] Add evidence-backed QDOS claimant-address extraction using the existing field engine, with positive, absent, conflicting, third-party and whitespace tests.
- [ ] Add nullable InstructionDraft persistence, correction/replay wiring, EF migration and unambiguous draft-to-Case provenance promotion without changing completeness.
- [ ] Extend the canonical Case claimant projection, Save Case normalization, persistence/replay and provenance tests with claimant address.
- [ ] Display and edit claimant address through the existing intake, Case and Automation MCP callers under current lease/version rules.
- [ ] Map the canonical Case claimant address into EvaInstructionPayload and exact JSON ClmAdd while leaving every ZIP/export type and mapping unchanged.
- [ ] Block missing, unusable and over-40-character claimant addresses before images, persistence or EVA transport, with zero-call tests for manual and automatic paths.
- [ ] Run focused tests, unchanged EVA bundle contract tests, locked restore, Release build and the full non-Corpus suite; complete the simplification pass and record exit-code evidence for the post-implementation report.

## Progress notes
