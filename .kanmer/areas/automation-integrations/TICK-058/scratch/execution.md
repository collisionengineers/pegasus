## Transitions

- 2026-09-02T17:57:36.313Z stage verifying → preparing by codex-mcp-client; reason: proof FAIL plan: the recorded Markdown-placement check uses mutable origin/dev as its base; at exact merged SHA 0d985c9e0b3284f211f824d387e2f36460c0c826 it now reverse-diffs later unrelated removals and fails. Bind the check to an immutable integration base before fresh verification.
