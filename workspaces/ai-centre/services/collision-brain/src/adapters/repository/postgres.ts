import { randomUUID } from "node:crypto";
import { Pool, type PoolClient, type QueryResultRow } from "pg";
import { ConflictError, NotFoundError } from "../../domain/errors.js";
import type {
  CreateDocumentInput,
  DocumentMetadata,
  DocumentRecord,
  DocumentStatus,
  DocumentSummary,
  IngestionJob,
  ListDocumentsInput,
  ListDocumentsResult,
  LookupMatch,
  Tombstone,
} from "../../domain/types.js";
import type {
  DocumentRepository,
  ReadyDocumentInput,
  SearchInput,
} from "../../ports/document-repository.js";

function vectorSql(vector: number[]): string {
  return `[${vector.join(",")}]`;
}

function parseMetadata(value: unknown): DocumentMetadata {
  return (typeof value === "string" ? JSON.parse(value) : value) as DocumentMetadata;
}

function parseEmbedding(value: unknown) {
  if (value === null || value === undefined) return null;
  return (typeof value === "string" ? JSON.parse(value) : value) as DocumentRecord["embedding"];
}

function documentFromRow(row: QueryResultRow): DocumentRecord {
  return {
    id: String(row.id),
    status: row.status as DocumentRecord["status"],
    contentHash: String(row.content_hash),
    sourceObjectKey: String(row.source_object_key),
    metadata: parseMetadata(row.metadata),
    embedding: parseEmbedding(row.embedding),
    chunkCount: Number(row.chunk_count),
    textLength: Number(row.text_length),
    error: row.error === null ? null : String(row.error),
    createdBy: String(row.created_by),
    createdAt: new Date(row.created_at as string | Date).toISOString(),
    updatedAt: new Date(row.updated_at as string | Date).toISOString(),
  };
}

function summaryFromRow(row: QueryResultRow): DocumentSummary {
  return {
    id: String(row.id),
    status: row.status as DocumentStatus,
    contentHash: String(row.content_hash),
    metadata: parseMetadata(row.metadata),
    embedding: parseEmbedding(row.embedding),
    chunkCount: Number(row.chunk_count),
    textLength: Number(row.text_length),
    error: row.error === null ? null : String(row.error),
    createdBy: String(row.created_by),
    createdAt: new Date(row.created_at as string | Date).toISOString(),
    updatedAt: new Date(row.updated_at as string | Date).toISOString(),
  };
}

function encodeCursor(createdAt: string, id: string): string {
  return Buffer.from(`${createdAt}|${id}`, "utf8").toString("base64url");
}

function decodeCursor(cursor: string): { createdAt: string; id: string } {
  const decoded = Buffer.from(cursor, "base64url").toString("utf8");
  const split = decoded.lastIndexOf("|");
  if (split < 1) throw new Error("Invalid cursor");
  return { createdAt: decoded.slice(0, split), id: decoded.slice(split + 1) };
}

export class PostgresDocumentRepository implements DocumentRepository {
  private readonly pool: Pool;

  constructor(connectionString: string) {
    this.pool = new Pool({ connectionString });
  }

  async initialise(): Promise<void> {
    await this.pool.query("SELECT 1");
  }

  async close(): Promise<void> {
    await this.pool.end();
  }

  async createDocumentWithJob(input: CreateDocumentInput) {
    return this.transaction(async (client) => {
      try {
        const documentResult = await client.query(
          `INSERT INTO documents (
             id, status, content_hash, source_object_key, metadata, created_by
           ) VALUES ($1, 'pending', $2, $3, $4::jsonb, $5)
           RETURNING *`,
          [input.id, input.contentHash, input.sourceObjectKey, JSON.stringify(input.metadata),
            input.createdBy],
        );
        const jobResult = await client.query(
          `INSERT INTO ingestion_jobs (id, document_id, state)
           VALUES ($1, $2, 'queued')
           RETURNING id, document_id, attempts, created_at`,
          [randomUUID(), input.id],
        );
        const documentRow = documentResult.rows[0];
        const jobRow = jobResult.rows[0];
        if (!documentRow || !jobRow) throw new Error("Insert did not return created records");
        return {
          document: documentFromRow(documentRow),
          job: {
            id: String(jobRow.id),
            documentId: String(jobRow.document_id),
            attempts: Number(jobRow.attempts),
            createdAt: new Date(jobRow.created_at as string | Date).toISOString(),
          },
        };
      } catch (error) {
        if ((error as { code?: string }).code === "23505") {
          throw new ConflictError("The supplied content already exists");
        }
        throw error;
      }
    });
  }

  async getDocument(id: string): Promise<DocumentRecord | null> {
    const result = await this.pool.query("SELECT * FROM documents WHERE id = $1", [id]);
    return result.rows[0] ? documentFromRow(result.rows[0]) : null;
  }

  async findByContentHash(contentHash: string): Promise<DocumentRecord | null> {
    const result = await this.pool.query(
      "SELECT * FROM documents WHERE content_hash = $1",
      [contentHash],
    );
    return result.rows[0] ? documentFromRow(result.rows[0]) : null;
  }

  async listDocuments(input: ListDocumentsInput): Promise<ListDocumentsResult> {
    const values: unknown[] = [];
    const predicates: string[] = [];
    if (input.status) {
      values.push(input.status);
      predicates.push(`status = $${values.length}`);
    }
    if (input.cursor) {
      const cursor = decodeCursor(input.cursor);
      values.push(cursor.createdAt, cursor.id);
      predicates.push(`(created_at, id) < ($${values.length - 1}::timestamptz, $${values.length}::uuid)`);
    }
    values.push(input.limit + 1);

    const where = predicates.length > 0 ? `WHERE ${predicates.join(" AND ")}` : "";
    const result = await this.pool.query(
      `SELECT * FROM (
         SELECT
           id, status::text, content_hash, metadata, embedding, chunk_count, text_length, error,
           created_by, created_at, updated_at
         FROM documents
         UNION ALL
         SELECT
           document_id AS id, 'deleted' AS status, content_hash,
           jsonb_build_object(
             'title', '[removed]', 'source', NULL, 'tags', '[]'::jsonb,
             'filename', NULL, 'mimeType', 'application/x-removed', 'sizeBytes', 0
           ) AS metadata,
           NULL AS embedding, chunk_count, 0 AS text_length, NULL AS error,
           removed_by AS created_by, removed_at AS created_at, removed_at AS updated_at
         FROM document_tombstones
       ) registry
       ${where}
       ORDER BY created_at DESC, id DESC
       LIMIT $${values.length}`,
      values,
    );
    const page = result.rows.slice(0, input.limit).map(summaryFromRow);
    const last = page.at(-1);
    return {
      documents: page,
      nextCursor: result.rows.length > input.limit && last
        ? encodeCursor(last.createdAt, last.id)
        : null,
    };
  }

  async claimNextJob(): Promise<IngestionJob | null> {
    return this.transaction(async (client) => {
      const result = await client.query(
        `SELECT id, document_id, attempts, created_at
         FROM ingestion_jobs
         WHERE state = 'queued'
         ORDER BY created_at
         FOR UPDATE SKIP LOCKED
         LIMIT 1`,
      );
      const row = result.rows[0];
      if (!row) return null;
      await client.query(
        `UPDATE ingestion_jobs
         SET state = 'processing', attempts = attempts + 1, started_at = now()
         WHERE id = $1`,
        [row.id],
      );
      return {
        id: String(row.id),
        documentId: String(row.document_id),
        attempts: Number(row.attempts) + 1,
        createdAt: new Date(row.created_at as string | Date).toISOString(),
      };
    });
  }

  async markProcessing(documentId: string): Promise<void> {
    await this.requireUpdated(
      `UPDATE documents
       SET status = 'processing', error = NULL, updated_at = now()
       WHERE id = $1`,
      [documentId],
      documentId,
    );
  }

  async markReady(input: ReadyDocumentInput): Promise<void> {
    await this.transaction(async (client) => {
      await client.query("DELETE FROM document_chunks WHERE document_id = $1", [input.documentId]);
      for (const chunk of input.chunks) {
        await client.query(
          `INSERT INTO document_chunks (
             id, document_id, chunk_index, content, citation, embedding
           ) VALUES ($1, $2, $3, $4, $5, $6::vector)`,
          [chunk.id, chunk.documentId, chunk.index, chunk.text, chunk.citation,
            vectorSql(chunk.embedding)],
        );
      }
      const result = await client.query(
        `UPDATE documents
         SET status = 'ready', embedding = $2::jsonb, chunk_count = $3, text_length = $4,
             error = NULL, updated_at = now()
         WHERE id = $1`,
        [input.documentId, JSON.stringify(input.embedding), input.chunks.length, input.textLength],
      );
      if (result.rowCount !== 1) throw new NotFoundError(`Document ${input.documentId} was not found`);
      await client.query(
        `UPDATE ingestion_jobs
         SET state = 'completed', completed_at = now()
         WHERE document_id = $1 AND state = 'processing'`,
        [input.documentId],
      );
    });
  }

  async markFailed(documentId: string, error: string): Promise<void> {
    await this.transaction(async (client) => {
      await client.query(
        `UPDATE documents
         SET status = 'failed', error = $2, updated_at = now()
         WHERE id = $1`,
        [documentId, error],
      );
      await client.query(
        `UPDATE ingestion_jobs
         SET state = 'failed', last_error = $2, completed_at = now()
         WHERE document_id = $1 AND state = 'processing'`,
        [documentId, error],
      );
    });
  }

  async markDeleting(documentId: string): Promise<void> {
    await this.requireUpdated(
      "UPDATE documents SET status = 'deleting', updated_at = now() WHERE id = $1",
      [documentId],
      documentId,
    );
  }

  async search(input: SearchInput): Promise<LookupMatch[]> {
    const values: unknown[] = [input.query, vectorSql(input.queryEmbedding)];
    const predicates = ["d.status = 'ready'"];

    if (input.filters.source) {
      values.push(input.filters.source);
      predicates.push(`d.metadata->>'source' = $${values.length}`);
    }
    if (input.filters.tags?.length) {
      values.push(JSON.stringify(input.filters.tags));
      predicates.push(`d.metadata->'tags' @> $${values.length}::jsonb`);
    }
    if (input.filters.documentIds?.length) {
      values.push(input.filters.documentIds);
      predicates.push(`d.id = ANY($${values.length}::uuid[])`);
    }
    values.push(input.limit);

    const result = await this.pool.query(
      `WITH query_values AS (
         SELECT plainto_tsquery('simple', $1) AS text_query, $2::vector AS query_embedding
       )
       SELECT
         d.id AS document_id,
         c.id AS chunk_id,
         d.metadata->>'title' AS title,
         d.metadata->>'source' AS source,
         d.metadata->'tags' AS tags,
         c.chunk_index,
         left(c.content, 800) AS excerpt,
         c.citation,
         (
           0.65 * ((1 - (c.embedding <=> query_values.query_embedding)) + 1) / 2 +
           0.35 * ts_rank_cd(c.search_vector, query_values.text_query)
         ) AS score
       FROM document_chunks c
       JOIN documents d ON d.id = c.document_id
       CROSS JOIN query_values
       WHERE ${predicates.join(" AND ")}
       ORDER BY score DESC
       LIMIT $${values.length}`,
      values,
    );

    return result.rows.map((row) => ({
      documentId: String(row.document_id),
      chunkId: String(row.chunk_id),
      title: String(row.title),
      source: row.source === null ? null : String(row.source),
      chunkIndex: Number(row.chunk_index),
      excerpt: String(row.excerpt),
      score: Number(row.score),
      citation: String(row.citation),
      tags: (typeof row.tags === "string" ? JSON.parse(row.tags) : row.tags) as string[],
    }));
  }

  async purgeDocument(documentId: string, removedBy: string): Promise<Tombstone> {
    return this.transaction(async (client) => {
      const result = await client.query(
        "SELECT id, content_hash, chunk_count FROM documents WHERE id = $1 FOR UPDATE",
        [documentId],
      );
      const row = result.rows[0];
      if (!row) throw new NotFoundError(`Document ${documentId} was not found`);
      const removedAt = new Date().toISOString();
      await client.query("DELETE FROM documents WHERE id = $1", [documentId]);
      await client.query(
        `INSERT INTO document_tombstones (
           document_id, content_hash, removed_by, removed_at, chunk_count
         ) VALUES ($1, $2, $3, $4, $5)
         ON CONFLICT (document_id) DO NOTHING`,
        [documentId, row.content_hash, removedBy, removedAt, row.chunk_count],
      );
      return {
        documentId,
        contentHash: String(row.content_hash),
        removedBy,
        removedAt,
        chunkCount: Number(row.chunk_count),
      };
    });
  }

  private async transaction<T>(operation: (client: PoolClient) => Promise<T>): Promise<T> {
    const client = await this.pool.connect();
    try {
      await client.query("BEGIN");
      const value = await operation(client);
      await client.query("COMMIT");
      return value;
    } catch (error) {
      await client.query("ROLLBACK");
      throw error;
    } finally {
      client.release();
    }
  }

  private async requireUpdated(sql: string, values: unknown[], documentId: string): Promise<void> {
    const result = await this.pool.query(sql, values);
    if (result.rowCount !== 1) throw new NotFoundError(`Document ${documentId} was not found`);
  }
}
