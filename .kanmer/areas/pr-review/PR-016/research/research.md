# Research — PR-016

The current adapter scans one mailbox to exhaustion before the next and applies the global counter during that loop. A bounded fair result needs metadata-only candidates from every selected approved mailbox first, a global newest-first take of at most 100, then MIME reads only for that bounded set. Graph already returns received time and immutable id; no cursor persistence or backfill is needed. Source: current `GraphDeletedMailSearchSource` and `GraphMailClient`.
