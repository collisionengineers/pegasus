# Research — PR-013

Review scratch proves `ReplaceFolderBindings` clears an already tracked collection and constructs replacement entities with identical composite keys. EF can retain deleted entries in the tracker, so a retained logical type conflicts before save. Current relational coverage never refreshes an existing type. Verified against current PR branch; no external premise.
