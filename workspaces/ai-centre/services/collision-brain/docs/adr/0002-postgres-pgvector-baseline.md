# ADR 0002: PostgreSQL and pgvector baseline

Status: Accepted for v1 baseline; hosted vendor not selected

Use PostgreSQL for authoritative metadata, jobs, tombstones, full-text search, and vector chunks.
Use the open pgvector extension for vector distance. This keeps the baseline runnable locally and
available from multiple managed providers.

A dedicated vector database may replace or supplement this adapter only if a labelled benchmark
demonstrates materially better retrieval or operating characteristics and preserves export,
deletion, and provider-exit requirements.
