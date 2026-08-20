- `scripts/email-eval-desktop/EmailEvaluationWorkflow.cs` — has the `Suggestion` display slot; `suggestion` is never populated (read-only finding, no change made)
- `scripts/email-eval-desktop/MainForm.cs` — renders the (always-empty) suggestion (read-only finding, no change made)
- `scripts/email-eval-desktop/tests/DesktopEvaluatorTests.cs:46` — confirms the always-"No category" behavior (read-only finding, no change made)

No files changed — this is a verification pass that found a genuine, pre-existing gap; it is not this ticket's job to close it (that is deferred, named honestly, and left in `preparing`).
