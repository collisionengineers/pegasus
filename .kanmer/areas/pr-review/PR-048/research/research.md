# Research

Core `EfIntakeMutationStore.ExecuteAsync` resolves operation history and request fingerprint before receipt/Case/lease freshness. The Mail page's old fresh checks and new lease claim ran before that owner, making exact successful POST replay unreachable. The accepted correction is lease-first preparation, then a final POST that server-resolves only message→receipt and calls the existing Core command directly.
