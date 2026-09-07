# Historical ticket record pointer

The complete pre-compaction content is retained byte-for-byte in the Kanmer
board Git history:

- Commit: `7b5707f20`
- Path: `.kanmer/areas/intake-processing/INTK-060/scratch/pr-646-disposition.md`

Recover with:

```powershell
git -C .worktrees/kanmer show "7b5707f20:.kanmer/areas/intake-processing/INTK-060/scratch/pr-646-disposition.md"
```

This live scratch file is intentionally compact so replacement controllers can
obtain a bounded execution packet. No historical information was deleted from
Git.
