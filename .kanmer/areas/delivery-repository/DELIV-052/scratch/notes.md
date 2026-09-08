## Corrective release evidence boundary — 8 September 2026

The approved implementation plan D5 requires resolving the receipt mismatch, not creating duplicate CI. Fresh get_status still declares the Kanmer default `pr.yml` / `verify` / `push`; Pegasus `.github/workflows/ci.yml` declares `repository-check`, with pull_request and main-push triggers. The current installed kanmer-verify explicitly supports an absent matching receipt: classify each obligation, run only missing obligations in the exact detached merge-SHA worktree, and write a proof with typed attempts and no fabricated receipts. PR-head CI may inform review but is not an exact dev-merge receipt.

Use that existing missing-obligation fallback for this corrective release unless a separately approved contract change is implemented. This note makes no CI/board-policy change, waives no check, and does not claim this capture ticket is complete. Permanent receipt-contract selection remains scoped here.
