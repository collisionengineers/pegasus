Implemented the two-line replay split plus durable Pending→Uncertain save on provider exception. Added deterministic LocalDB overlap proof: matching Pending replay is refused, different key remains blocked, row stays pending, no replay probe, one move total, and completed replay succeeds.

Pushed head 83293162c3059d52b05d5139e2d1b8ee56b8d5a9 to existing PR #477. Leaving Review for independent kanmer-review; no self-review or merge.
