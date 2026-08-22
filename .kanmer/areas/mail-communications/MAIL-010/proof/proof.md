# Proof

**Shipped:** PR #505, commits `7198c1c2`, `4c0ae068` · **Deployed:** Release 17, still live on Release 18 (`1f3be493`), smoke-asserted source SHA.

## The three strings are gone

At the deployed revision, `Mail/Index.cshtml` no longer contains:

- `No Deleted Items in the **bounded** approved scope matched "…"` — trimmed to
  `No Deleted Items matched "…"`, removing a word on the closed banned list;
- `Enter a search term to read accepted Deleted Items within the selected approved mailbox
  scope.` — a hint sentence under a field;
- `Search includes retained messages in their current Outlook folders.` — copy describing
  the page's own mechanics.

A scan of `src/Pegasus.Web/Pages/**/*.cshtml` for every banned word now returns only Razor
comments and C# identifiers.

## Authenticated callers exercise it

CI's full suite passed on this revision, including the two `MailWorkspaceWebTests` this
ticket rewrote. They no longer assert the deleted copy; they assert the behaviour it
described — that Deleted Items lists nothing until a search term is given, and that a
message moved out of the Inbox is still found by search. Both drive the real page through
an authenticated Web caller.

That rewrite matters to this proof: the first attempt deleted the copy and left the tests
pinned to it, and **CI caught it**. The assertions now check behaviour, so they cannot pass
for the wrong reason.

## Evidence tier

**Deployed-code plus authenticated web tests.** The live Deleted Items view has not been
opened, because that needs a sign-in I must not perform.
