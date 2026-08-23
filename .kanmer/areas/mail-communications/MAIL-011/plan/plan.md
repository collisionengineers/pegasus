# Plan

## 1. One pattern, in Core

`StaffForwardBodyCleaner` gains

```csharp
public static Regex ForwardedHeaderPattern => ForwardedHeaderRegex();
```

and the reader drops its `[GeneratedRegex]` in favour of it. One definition,
compiled once, referenced twice — instead of two literals and a comment asking
the next reader to keep them equal.

## 2. Allow the address lines Outlook actually writes

```
(?i)(?:\A|[\r\n])From:[\t ]*(?<from>[^\r\n]+)
[\r\n]+Sent:[^\r\n]*
[\r\n]+To:[^\r\n]*
(?:[\r\n]+(?:Cc|Bcc):[^\r\n]*)*        <- added
[\r\n]+Subject:[^\r\n]*
```

`Cc` and `Bcc` only — the two header lines Outlook writes between `To:` and
`Subject:`. Not a general "any header" wildcard: the block still has to be a
forwarded address header ending in `Subject:`, so the boundary stays as
specific as it was.

Verified against U34's own retained body before writing any code: the shipped
pattern matches **0** times, this one matches **1**, group `from` =
`Robin Anderson <randerson@qdosassist.co.uk>`.

## 3. What deliberately does not change

`QdosMailRoutePolicy` keeps `Version = 4` and every predicate as written. The
route bar — exactly one original sender, external to the staff domain — is
untouched. This ticket makes a well-formed header **readable**; it does not
make an ambiguous one acceptable. A body with two forwarded blocks still gives
the reader `Matches.Count == 2` and still fails closed.

## 4. Tests

Core (`StaffForwardBodyCleanerTests`):
- `ForwardedSenderAddress` reads the sender through a `Cc:` line, and through
  `Cc:` + `Bcc:`;
- `SplitForwardedHeader` returns the Cc line among the header lines and the
  message body after `Subject:`;
- a block with no `Subject:` still yields no header — the shape is not relaxed.

Infrastructure: a staff-forwarded `.eml` whose inline block carries a `Cc:`
produces exactly one `InlineForwardedOriginal` sender identity. This is the
assertion that maps directly onto U34.

## 5. U34 itself

The fix does not retro-resolve U34 — the receipt's decision is recorded and
intake is not re-run. After deploy the operator's own re-forward of that
message is identified normally; the existing U34 stays for them to resolve
through the Unidentified surface, which is what that surface is for.

**Not done here:** no migration, no reprocessing job. Re-running intake over
recorded receipts is a different capability with its own idempotency questions,
and inventing it inside a regex fix would be exactly the over-reach the repo
rails forbid.

## 6. Verification

Production, after deploy: the operator forwards one QDOS message whose original
carried a Cc. Expect a case rather than an Unidentified item, and the inbox row
showing the original sender with "Forwarded by Desk", like every other row.

## Simplification pass

To be recorded here, dated, before the PR.
