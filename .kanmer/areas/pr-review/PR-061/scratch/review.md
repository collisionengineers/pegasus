## Fresh independent re-review — cc6b0ee7 — PASS

The fix reuses the existing serializable transaction and UPDLOCK/HOLDLOCK query, reads the locked workflow State, and throws the existing CaseNotInReviewException before any replay/proxy/history work when the state is not Review. The held-lock regression proves a committed demotion wins and leaves zero export records. Release/focused evidence is recorded and final GitHub CI is fully green. Simplification is honest; no new abstraction, schema, retry or compatibility path was added. **PASS; no findings.**
