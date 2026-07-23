# Worker host

This is the .NET 10 isolated Azure Functions composition root. It intentionally contains no timer, queue, or mailbox trigger yet: the first QDOS vertical slice must introduce the real caller and its genuine-input proof together.

Copy `local.settings.example.json` to the ignored `local.settings.json` only when a trigger requires local Functions storage.
