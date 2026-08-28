# Checklist — PLAT-055

- [x] Retrieve Infisical `eva_api_client_secret` without emitting it.
- [x] Create a new enabled Key Vault version and silently prove exact equality.
- [x] Persist the new versioned URI in the production azd environment.
- [x] Repoint Web and Worker to the same new version without changing unrelated settings.
- [x] Verify both hosts are healthy and Key Vault synchronization succeeds.
- [x] Verify EVA token-only authentication returns a valid token envelope.
- [x] Record sanitized command evidence and remediation outcome; roll back references on failed health.

## Progress

- 2026-08-28: The first Worker CLI invocation was rejected before applying its
  setting. The guarded operation restored Web and azd to the prior URI, and
  read-only checks confirmed the rollback before retrying.
- 2026-08-28: Retried the Worker update through the CLI's JSON-file input,
  repointed Web and azd, restarted the active Web revision, and removed the
  temporary JSON file. The worktree is clean.
