# 0012: Adopt a tool-neutral repository workflow

- Date: 2026-07-27
- Status: accepted

## Context

Decision 0010 tied the repository's current work routes and documentation
ownership to a named workflow plugin. The useful controls are repository
controls: one accountable change record, explicit source authority,
caller-backed verification, independent exact-head review, exact-target
approval for external operations, and no agent merge. They must remain usable
when a particular plugin, skill, or tool is unavailable.

## Decision

Supersede Decision 0010 for current repository workflow. Keep the repository
workflow tool-neutral:

- material delivery uses one activated issue, one current change record, and one
  scoped pull request;
- planning resolves material decisions and is accepted before implementation;
- implementation updates the same record and proves the actual caller;
- review is independent and compares the exact base and head;
- external reads and writes remain separately approved for exact targets; and
- agents never merge.

Skills, MCP servers, command-line tools, and other automation may assist the
work, but none is repository authority or authorization. Active guidance and
checks must describe the required outcome rather than require or name a
particular workflow plugin. Decision 0010 and its onboarding record remain
unaltered historical evidence except for explicit supersession status and
links.

## Consequences

Current instructions, routing guidance, source-role wording, and validation are
portable across capable tools while preserving the same safety and evidence
gates. Historical plugin records remain discoverable and may quote their former
routes. Reintroducing a named plugin as an active requirement needs a new
accepted decision; documentation checks reject it in current guidance.
