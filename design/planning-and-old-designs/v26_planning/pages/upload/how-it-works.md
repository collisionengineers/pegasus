# Upload — how it works (confirmation outcomes so far)

Read from the live source on 13 September 2026 (`src/Pegasus.Web/Pages/Upload.cshtml`, `UploadGroupStatus.cshtml`, `src/Pegasus.Web/Presentation/UploadOutcome.cs`).

After an upload each file gets a confirmation outcome built from what intake recorded: attached to a Case, a new-Case proposal (Create case seeded), vehicle images, Unidentified, or cannot become a case. Its actions route to existing pages: `/Cases/Create` for a proposal, the image record and the Unidentified record for theirs, and `/Intake/Details` for attach-with-override and reversal. A manual upload keeps even a unique viable Case match for explicit confirmation; grouped image uploads decide each member independently.
