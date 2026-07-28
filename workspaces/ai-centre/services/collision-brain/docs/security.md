# Security notes

- V1 is restricted to non-sensitive knowledge. Case files, personal data, credentials, and raw
  operational mail are prohibited until a separate data-protection review changes this boundary.
- `AUTH_MODE=none` is refused in production.
- `lookup` and `view_all` require reader access; `write` requires contributor access; `remove`
  requires administrator access.
- Upload references are HMAC signed, expire, bind filename/type/size/hash, and are deleted after
  consumption. Unconsumed expired uploads are removed by the worker.
- Filenames are reduced to safe basenames and filesystem object keys are containment checked.
- File type allowlisting does not make a parser safe. Keep parsers patched, cap sizes, and run the
  worker with minimal privileges and constrained resources.
- Logs record IDs, counts, timings, and error types rather than document bodies or raw queries.
- Retrieved document instructions are untrusted evidence, not system or tool instructions.
- Removal deletes active source and index content. The retained tombstone contains document ID,
  hash, actor, time, and chunk count only.
