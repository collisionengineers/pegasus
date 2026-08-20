# Files

## Modify

- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` — give explicit attachment disposition precedence over Content-ID when classifying inline images.
- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` — extend the canonical/display occurrence proof with an explicitly attached Content-ID image before a later attachment.

## Overlap and dependencies

- Both files are already owned by [[TICK-053]] and overlap retained blocker [[PR-018]] by design.
- This completes PR-018's attachment identity dependency; it does not add a parser, projection, store, or backfill.
