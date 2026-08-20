# Research — complete MCP-05 email actions

## Question

How should the Automation Actor expose the email-workspace capabilities that were deliberately omitted from TICK-062 without creating a second business implementation or a generic mutation surface?

## Verified findings

- `origin/dev` contains TICK-062's `MailMcpTools` and `AutomationMailIngressTests` (merge `47b12744`). Its delivered tools list retained mail, read exact-message detail, and correct classification through the same Core queries/use case as the Web pages.
- TICK-062's post-implementation report explicitly defers folder recommendation/move, Case association, read/flag/delete/restore, suggestions, and compose/send because their Core owners have not landed.
- EPIC-006 requires UI and Automation callers to reuse one canonical Core implementation. FRD-10 requires typed, scoped tools with actor attribution and denial before side effects.
- The linked MAIL/UI tickets are all still Preparing on the board. Therefore this ticket cannot correctly invent their contracts or begin implementation before those owners land.
- The existing extension points are concrete: `MailMcpTools`, the single `automation.mail` scope in `AutomationMcp`, the shared error/actor/operation-history conventions, and `AutomationMailIngressTests`.

## Implications

Keep this ticket behind the owning MAIL tickets. Extend the existing `MailMcpTools` class with thin typed tools only after each Core use case and contract lands. Do not add a generic action envelope, duplicate validation, accept arbitrary folder identities/recipients, or call Graph directly from MCP.

The ticket's future plan must re-read the landed Core contracts and reduce its scope to the actions that are actually available; UI-only assembly behavior does not need an MCP twin.

## Verified checks

- `git fetch origin`
- `git ls-tree -r --name-only origin/dev | rg 'MailMcp|AutomationMail'`
- `git log --all --oneline --grep='MCP-05'`
- Kanmer reads for TICK-062, EPIC-005 and EPIC-006 context, and linked ticket stages.
