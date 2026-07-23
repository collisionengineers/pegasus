# Project hooks

`.codex/hooks.json` defines one `SessionStart` context hook. It reads repository state and returns a short plain-text additional-context message covering the source hierarchy, corpus boundary, real-caller requirement, and Azure mutation boundary.

The hook:

- makes no filesystem or cloud changes;
- consumes the event JSON from standard input and writes supported model-visible SessionStart context to standard output;
- resolves the repository root at runtime;
- reports only branch and dirty-file count, not file contents or secrets;
- can be reviewed at `.codex/hooks/Write-SessionContext.ps1`.

Codex requires the user to trust project hooks. Open `/hooks`, review the command, and enable it for this trusted repository. If the hook fails, work can continue; `AGENTS.md` remains authoritative.
