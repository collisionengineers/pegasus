# Open questions — INTK-042

- [x] Must dispatched-message-loss recovery land before immediate publication becomes the ordinary path? Yes. [[INTK-003]] blocks this ticket and owns that recovery rule.
- [x] Can implementation proceed concurrently with the claimed INTK-040 worktree? No. Its current uncommitted diff overlaps the Core intake owner, Worker composition, FRD-02, and tests; INTK-042 waits for it to merge and starts from refreshed `origin/dev`.
- [x] Does Web become an intake processor? No. It may publish a committed stable work id through the shared adapter, while Worker remains the sole queue-trigger processor.
- [x] Should a queue-send failure make an already committed receipt/case appear uncommitted? No. It remains durable and visible, publication failure is observed, and the slow recovery path retries it.

## Parked (explicitly deferred)

- Exact live latency and cost proof is deferred to the release/proof ticket because this research makes no cloud writes or deployment.
