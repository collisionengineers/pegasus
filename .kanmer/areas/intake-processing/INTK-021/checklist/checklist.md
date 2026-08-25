# Checklist — INTK-021

- [x] Extracted values land as Fact (auto-added) at acceptance
- [x] Real-shape synonyms added
- [x] Subject facts feed the engine (body wins conflicts)
- [x] Combined vehicle description splits into make/model/registration
- [x] Labelled registrations accept prefix/suffix UK formats
- [x] Engine label-boundary defect fixed
- [x] Corpus coverage test with floors + CSV artifact
- [x] Core fixtures for the new rules (5 added; 847/847 green)
- [x] Suggestion-affected integration suites green
- [x] Release build 0/0; simplification pass recorded
- [ ] PR merged on green CI

## Progress notes
2026-08-20: coverage on 75 real accepted instructions — claimant 4→48, claim number 4→60, registration 57→68, make 13→54 (junk splits eliminated), model 14→47, incident date 7→45.

<!-- kanmer-groom:release-take:INTK-021:2026-08-25 -->
### Board-hygiene claim release — 2026-08-25

Audit record written before releasing this completed ticket's stale take. Previous assignee: `claude-code`; branch: `task/intk-021-extraction-auto-add`; worktree: `../pegasus-worktrees/intk-021`; taken at: `2026-08-20T15:28:32.203Z`. The branch and worktree coordinates are preserved here; this groom does not delete either.
