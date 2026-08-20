# Research

## Verified

`MailWorkspaceWebTests` exercises the authenticated `/Inbox` list/detail route but only checks the Deleted no-search prompt. `ProductionGraphSourceTests` proves the adapter and `ProductionCompositionTests` proves DI; neither proves the Razor caller. The existing test convention is `WithWebHostBuilder` plus `RemoveAll<T>` and a singleton external-boundary fake. `SearchDeletedMail` already owns authorization, 100-message cap, ordering and paging, while `IndexModel` renders mailbox tabs, match labels, unavailable/truncated states and pager links.

## Implication

Inject one controlled `IDeletedMailSearchSource` into an integration-authenticated Web factory and exercise the real `/Inbox?folder=deleted...` route. The fake should expose two approved mailbox choices with zero retained rows, 26 results for paging/truncation/matches, and a switchable unavailable result. No production behavior changes.
