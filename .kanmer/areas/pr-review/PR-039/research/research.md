# Research — PR-039

## Question

Why is uncertain recovery unreachable from the authenticated page?

## Verified findings

- The durable row retains the original operation key, reason and freshness fingerprint, but `RetainedMailFolderMoveResult` does not expose the key.
- After redirect Razor generates a new key and asks for a new reason; the store correctly rejects that while uncertainty is active.
- Same-key uncertain replay already probes destination/source/other and never calls `MoveAsync`.

## Implication

Return the original operation key and confirmation reason in the safe result model. Render a distinct authenticated status-check POST with those values and current freshness tokens; it contains no transport identity.
