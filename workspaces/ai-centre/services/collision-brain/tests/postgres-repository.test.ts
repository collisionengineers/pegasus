import { randomUUID } from "node:crypto";
import { Pool } from "pg";
import { beforeAll, describe, expect, it } from "vitest";
import { PostgresDocumentRepository } from "../src/adapters/repository/postgres.js";

const enabled = process.env.TEST_POSTGRES === "1";
const connectionString = process.env.DATABASE_URL ??
  "postgres://rag:rag@localhost:5432/rag";

describe.skipIf(!enabled)("PostgresDocumentRepository", () => {
  beforeAll(async () => {
    const pool = new Pool({ connectionString });
    await pool.query(
      "TRUNCATE document_tombstones, ingestion_jobs, document_chunks, documents CASCADE",
    );
    await pool.end();
  });

  it("persists, searches, lists, and purges a ready document", async () => {
    const repository = new PostgresDocumentRepository(connectionString);
    const documentId = randomUUID();
    try {
      await repository.initialise();
      await repository.createDocumentWithJob({
        id: documentId,
        contentHash: "a".repeat(64),
        sourceObjectKey: `documents/${documentId}/source.txt`,
        metadata: {
          title: "Database test",
          source: "test",
          tags: ["integration"],
          filename: "source.txt",
          mimeType: "text/plain",
          sizeBytes: 20,
        },
        createdBy: "test",
      });
      expect((await repository.claimNextJob())?.documentId).toBe(documentId);
      await repository.markProcessing(documentId);
      const embedding = Array.from({ length: 384 }, (_, index) => index === 0 ? 1 : 0);
      await repository.markReady({
        documentId,
        embedding: {
          provider: "test",
          model: "test",
          dimensions: 384,
          version: "1",
        },
        textLength: 20,
        chunks: [{
          id: randomUUID(),
          documentId,
          index: 0,
          text: "database integration knowledge",
          citation: `rag://documents/${documentId}#chunk-1`,
          embedding,
        }],
      });

      const matches = await repository.search({
        query: "integration knowledge",
        queryEmbedding: embedding,
        filters: { tags: ["integration"] },
        limit: 5,
      });
      expect(matches[0]?.documentId).toBe(documentId);
      expect((await repository.listDocuments({ limit: 10 })).documents[0]?.status).toBe("ready");

      await repository.markDeleting(documentId);
      await repository.purgeDocument(documentId, "test-admin");
      expect(await repository.getDocument(documentId)).toBeNull();
      expect((await repository.listDocuments({ limit: 10, status: "deleted" })).documents)
        .toHaveLength(1);
    } finally {
      await repository.close();
    }
  });

  it("reclaims a processing job after its lease expires", async () => {
    const repository = new PostgresDocumentRepository(connectionString);
    const pool = new Pool({ connectionString });
    const documentId = randomUUID();
    try {
      await repository.initialise();
      await repository.createDocumentWithJob({
        id: documentId,
        contentHash: "b".repeat(64),
        sourceObjectKey: `documents/${documentId}/source.txt`,
        metadata: {
          title: "Lease recovery",
          source: "test",
          tags: ["integration"],
          filename: "source.txt",
          mimeType: "text/plain",
          sizeBytes: 20,
        },
        createdBy: "test",
      });
      expect((await repository.claimNextJob())?.attempts).toBe(1);
      await pool.query(
        "UPDATE ingestion_jobs SET started_at = now() - interval '16 minutes' WHERE document_id = $1",
        [documentId],
      );

      const reclaimed = await repository.claimNextJob();
      expect(reclaimed?.documentId).toBe(documentId);
      expect(reclaimed?.attempts).toBe(2);
    } finally {
      await pool.end();
      await repository.close();
    }
  });
});
