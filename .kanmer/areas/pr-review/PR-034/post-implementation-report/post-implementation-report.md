# Post-implementation report

Implemented in `fc6840361c1c19ece9a75d7ea68c713c75d01b75` on PR #469.

The canonical MIME reader now gives explicit attachment disposition precedence over Content-ID. A genuine inline image remains inline, while an explicitly attached Content-ID image remains in `AttachmentRecords` and cannot shift the ordinal of a later searchable PDF. This completes [[PR-018]]'s exact occurrence requirement without changing the single reader/projection/store path.

Files: `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` and `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`.

Evidence: exact occurrence/searchability proof passed; complete `MailWorkspaceWebTests + RetainedMailPersistenceTests` passed 39/39; Release solution build passed with 0 warnings/errors; `git diff --check` passed. No backfill or external write occurred.
