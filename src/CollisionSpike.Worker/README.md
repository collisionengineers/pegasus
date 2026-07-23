# Worker host

This is the .NET 10 isolated Azure Functions composition root. It intentionally contains no timer, queue, or mailbox trigger yet. The current QDOS slice has a genuine-input Web caller; future mailbox automation must call that same Core use case instead of reproducing its classification, extraction, or reference rules in the Worker.

Copy `local.settings.example.json` to the ignored `local.settings.json` only when a trigger requires local Functions storage.
