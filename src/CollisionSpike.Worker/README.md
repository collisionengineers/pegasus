# Worker host

This is the .NET 10 isolated Azure Functions composition root. It intentionally contains no timer, queue, or mailbox trigger yet. The current local slice has a genuine-input Web caller; future mailbox automation must call the provider-neutral `ProcessIntake` Core use case instead of reproducing receipt, extraction, or workflow rules in the Worker. QDOS remains the sole concrete extraction policy until another principal has approved rules and genuine evidence.

Copy `local.settings.example.json` to the ignored `local.settings.json` only when a trigger requires local Functions storage.
