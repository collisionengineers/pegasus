## Independent review — PR #456 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green. The badge's `NotReady` now includes unmerged image-initiated intakes awaiting instruction, mirroring exactly the filter the tab's row list applies (`MergedIntoCaseId == null && AwaitingInstruction`), with the state-code mapping reused from `EfImageIntakeStore.ToCode` (one owner) rather than a duplicated literal. Fixes the Dashboard tile through the same single count. Mixed-origin regression test asserts badge == rows.
