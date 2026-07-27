CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS documents (
  id uuid PRIMARY KEY,
  status text NOT NULL CHECK (status IN ('pending', 'processing', 'ready', 'failed', 'deleting')),
  content_hash text NOT NULL UNIQUE,
  source_object_key text NOT NULL,
  metadata jsonb NOT NULL,
  embedding jsonb,
  chunk_count integer NOT NULL DEFAULT 0 CHECK (chunk_count >= 0),
  text_length integer NOT NULL DEFAULT 0 CHECK (text_length >= 0),
  error text,
  created_by text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS document_chunks (
  id uuid PRIMARY KEY,
  document_id uuid NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
  chunk_index integer NOT NULL CHECK (chunk_index >= 0),
  content text NOT NULL,
  citation text NOT NULL,
  embedding vector(384) NOT NULL,
  search_vector tsvector GENERATED ALWAYS AS (to_tsvector('simple', content)) STORED,
  UNIQUE (document_id, chunk_index)
);

CREATE INDEX IF NOT EXISTS document_chunks_search_vector_idx
  ON document_chunks USING gin (search_vector);

CREATE INDEX IF NOT EXISTS document_chunks_embedding_idx
  ON document_chunks USING hnsw (embedding vector_cosine_ops);

CREATE TABLE IF NOT EXISTS ingestion_jobs (
  id uuid PRIMARY KEY,
  document_id uuid NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
  state text NOT NULL CHECK (state IN ('queued', 'processing', 'completed', 'failed')),
  attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
  last_error text,
  created_at timestamptz NOT NULL DEFAULT now(),
  started_at timestamptz,
  completed_at timestamptz
);

CREATE INDEX IF NOT EXISTS ingestion_jobs_queue_idx
  ON ingestion_jobs (created_at)
  WHERE state = 'queued';

CREATE TABLE IF NOT EXISTS document_tombstones (
  document_id uuid PRIMARY KEY,
  content_hash text NOT NULL,
  removed_by text NOT NULL,
  removed_at timestamptz NOT NULL,
  chunk_count integer NOT NULL DEFAULT 0 CHECK (chunk_count >= 0)
);
