# Shared packages

This imported source workspace does not own Pegasus application contracts. New packages may be
created here only for two or more AI-workspace consumers and must not duplicate
`Pegasus.Core`, the application audit model, the design system, or
`workspaces/report-renderer`.

The only prospective shared package is `agent-contracts/` for AI-specific tool
schemas, proposals, approvals, events, and failure envelopes. It requires a
separately accepted caller and contract before implementation.
