# CASE-049 checklist

- [ ] Replace native export prerequisite/duplicate gate and prove state/read-only projection behavior.
- [ ] Wire one atomic guarded Engineer handoff and actual native action; remove mandatory second UI step.
- [ ] Align known consumers/docs, run focused checks and scoped snapshot verification, preserve attempts and publish for independent review.
- [ ] Verify exact integrated result after review/merge.

## Progress

Author implementation frozen in .worktrees/case-049 at base19e6f523; 26 declared files changed. PLAT-072 is integrated and the current ENG-041 correction scope is disjoint. Native handoff, no-export access, retained read-only projection, focused tests and governing docs are implemented. Root has the exact filters/capture owners in scratch/execution.md and owns runtime verification. Test-bearing checkboxes remain unticked until those checks pass; snapshots and PR are still owed. No author build/test or external write.
