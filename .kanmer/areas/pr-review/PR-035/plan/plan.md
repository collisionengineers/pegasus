# Plan

Estimated diff: about 7 production lines and 25–35 test lines.

1. Catch the existing Core `ArgumentException` around the reload lookup and return the same `NotFound` response used by GET.
2. Extend authenticated correction POST coverage for whitespace-only and over-200-character search context, also proving no correction-history write.
3. Run focused `MailWorkspaceWebTests`, Release build, and diff check.
4. Record four-lens dispositions and PIR.

Reuse: existing Core validation, Razor response convention, anti-forgery helper, and seeded correction fixture.
