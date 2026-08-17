# Proof

Verified on merged `origin/dev` at `746401435892a76e4efb532ef2f3c41d26270590` (PR #382).

- Merge commit is present in `origin/dev`.
- `AGENTS.md` begins with the packaged managed marker; independent review confirmed the complete managed block exactly matches packaged Kanmer 0.3.3 and the Pegasus-specific tail is unchanged.
- All 33 packaged files in each tracked provider tree (`.agents/skills`, `.grok/skills`) have matching Git blob hashes (`bundle_mismatches=0`).
- Four retired Kanmer-owned paths are absent (`retired_present=0`).
- `git diff --check origin/dev^1 origin/dev` passed.
- GitHub checks passed: changes, documentation, reference-data; non-applicable code/infrastructure suites skipped.
- Migration dry-run was a no-op on the already-current format-3 board.

Final live `get_status` remains stale against the local checkout because local `dev` contains the user's separate commit and is behind merged `origin/dev`; this setup task did not rewrite or discard that work. The MCP server is packaged Kanmer 0.3.3 and the board remains format 3.
