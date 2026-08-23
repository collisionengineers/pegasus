# Files

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/StaffForwardBodyCleaner.cs` | The forwarded-header pattern allows `Cc:`/`Bcc:` lines between `To:` and `Subject:`, and is exposed so the reader can use it. |
| `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` | Deletes its own copy of the pattern and uses Core's. |
| `tests/Pegasus.Core.Tests/Intake/StaffForwardBodyCleanerTests.cs` | Cc and Bcc header blocks. |
| `tests/Pegasus.IntegrationTests/…IntakeSourceReaderTests` | A forward carrying a Cc yields one `InlineForwardedOriginal` identity. |

## The duplication being removed

Two `[GeneratedRegex]` declarations, byte-identical, in two projects:

- `MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex`
- `StaffForwardBodyCleaner.ForwardedHeaderRegex`, carrying
  `// Mirrors MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex.`

and a class remark stating "the two patterns must be changed together". The
comment is the only thing holding them together, and it does not compile.
Infrastructure already depends on Core, so Core owning the pattern is the
existing direction, not a new seam.

## What stays duplicated deliberately

The two callers keep their own strictness, because they answer different
questions:

- the **reader** requires exactly one forwarded block in the body — route
  identity is fail-closed evidence;
- the **cleaner** takes the first — the outermost forward is the one to display.

Sharing the *shape of a forwarded header* is one concept. Sharing "what counts
as proof of a route" is a different one, and it is not being merged.
