# TICK-039 post-implementation report — 2026-08-20

INT-14 (automated legacy DOC extraction) is implemented by [[SIMPLI-013]]'s PR https://github.com/collisionengineers/pegasus/pull/449 (branch `task/simpli-013-collisiondocnet-integration`, commits c7457628 + d999277d). SIMPLI-013's post-implementation report is the full record; the `.doc` capability facts:

- `.doc` sources (direct uploads and email/`.msg` attachments) extract text through the existing `IIntakeSourceReader` dispatch — no second pipeline, no new Core surface. The extractor is the integrated CollisionDocNet FIB/CLX reader with its correctness defects fixed (reserved-field false rejection, unconditional CP1252 decode, cbMac bounds, lone-surrogate replacement, guard-CP placement).
- Fail closed: unreadable, encrypted, pre-97, or oversized `.doc` files keep the pre-existing honest `NeedsSorting` manual-sorting outcome (`FailureCode == null`); no macro/OLE content is ever opened.
- Evidence on the branch: DocumentExtraction unit suites 136/136; end-to-end `DirectLegacyDocTextIsExtractedThroughWebCaller` and `UnreadableLegacyContainersFallBackIntoNeedsSortingWithoutReference` green; PDF/DOCX/EML regression 202/202 unchanged; Release build zero warnings.
- `docs/capabilities.md` INT-14 note records "locally implemented and test-backed"; deployment and operator acceptance remain separate evidence.

This ticket's remaining step is the review/merge of PR #449; proof is written after merge per process.
