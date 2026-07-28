import { createHash, randomUUID } from "node:crypto";
import path from "node:path";
import { trace } from "@opentelemetry/api";
import { requireRole } from "../auth.js";
import {
  ConflictError,
  NotFoundError,
  ValidationError,
} from "../domain/errors.js";
import { chunkText, normaliseText } from "../domain/text.js";
import type {
  DocumentMetadata,
  DocumentStatus,
  LookupFilters,
  Principal,
} from "../domain/types.js";
import { extractText, isSupportedFilename } from "../ingestion/extract-text.js";
import type { UploadTokenService } from "../ingestion/upload-tokens.js";
import { logger } from "../observability/logger.js";
import type { DocumentRepository } from "../ports/document-repository.js";
import type { EmbeddingProvider } from "../ports/embedding-provider.js";
import type { ObjectStore, StoredObject } from "../ports/object-store.js";

const tracer = trace.getTracer("@collisionengineers/rag-pipeline");

export interface WriteInput {
  title: string;
  text?: string;
  uploadRef?: string;
  source?: string;
  tags?: string[];
}

export interface LookupInput {
  query: string;
  limit?: number;
  filters?: LookupFilters;
}

export interface ViewAllInput {
  cursor?: string;
  limit?: number;
  status?: DocumentStatus;
}

function safeFilename(value: string): string {
  const basename = path.basename(value).replace(/[^\p{L}\p{N}._ -]+/gu, "_").trim();
  return basename || "document.txt";
}

function contentHash(body: Buffer): string {
  return createHash("sha256").update(body).digest("hex");
}

export class RagService {
  constructor(
    private readonly repository: DocumentRepository,
    private readonly objectStore: ObjectStore,
    private readonly embeddings: EmbeddingProvider,
    private readonly uploads: UploadTokenService,
  ) {}

  async write(principal: Principal, input: WriteInput) {
    requireRole(principal, "contributor");
    const title = input.title.trim();
    if (!title) throw new ValidationError("title is required");
    if (Boolean(input.text) === Boolean(input.uploadRef)) {
      throw new ValidationError("Provide exactly one of text or upload_ref");
    }

    return tracer.startActiveSpan("rag.write", async (span) => {
      let source: StoredObject;
      let stagedKey: string | null = null;
      if (input.uploadRef) {
        const payload = this.uploads.verify(input.uploadRef);
        stagedKey = payload.key;
        source = await this.objectStore.get(payload.key);
        if (source.body.byteLength !== payload.sizeBytes ||
            contentHash(source.body) !== payload.contentHash) {
          throw new ValidationError("Staged upload no longer matches its signed reference");
        }
        if (!isSupportedFilename(source.filename)) {
          throw new ValidationError(`Unsupported file type for ${source.filename}`);
        }
      } else {
        const text = normaliseText(input.text as string);
        if (!text) throw new ValidationError("text must contain searchable content");
        source = {
          body: Buffer.from(text, "utf8"),
          contentType: "text/plain; charset=utf-8",
          filename: `${safeFilename(title)}.txt`,
        };
      }

      const hash = contentHash(source.body);
      const duplicate = await this.repository.findByContentHash(hash);
      if (duplicate) {
        if (stagedKey) await this.objectStore.delete(stagedKey);
        span.setAttribute("rag.deduplicated", true);
        span.end();
        return {
          document_id: duplicate.id,
          content_hash: duplicate.contentHash,
          status: duplicate.status,
          job_id: null,
          deduplicated: true,
        };
      }

      const documentId = randomUUID();
      const filename = safeFilename(source.filename);
      const sourceObjectKey = `documents/${documentId}/${filename}`;
      await this.objectStore.put({ key: sourceObjectKey, ...source });
      const metadata: DocumentMetadata = {
        title,
        source: input.source?.trim() || null,
        tags: [...new Set((input.tags ?? []).map((tag) => tag.trim()).filter(Boolean))],
        filename,
        mimeType: source.contentType,
        sizeBytes: source.body.byteLength,
      };
      let created: Awaited<ReturnType<DocumentRepository["createDocumentWithJob"]>>;
      try {
        created = await this.repository.createDocumentWithJob({
          id: documentId,
          contentHash: hash,
          sourceObjectKey,
          metadata,
          createdBy: principal.subject,
        });
      } catch (error) {
        await this.objectStore.delete(sourceObjectKey);
        if (stagedKey) await this.objectStore.delete(stagedKey);
        if (error instanceof ConflictError) {
          const existing = await this.repository.findByContentHash(hash);
          if (existing) {
            span.end();
            return {
              document_id: existing.id,
              content_hash: existing.contentHash,
              status: existing.status,
              job_id: null,
              deduplicated: true,
            };
          }
        }
        span.recordException(error as Error);
        span.end();
        throw error;
      }

      if (stagedKey) {
        try {
          await this.objectStore.delete(stagedKey);
        } catch (error) {
          logger.warn("Committed upload staging cleanup failed", {
            documentId,
            errorType: error instanceof Error ? error.name : "unknown",
          });
        }
      }
      const { document, job } = created;
      span.setAttribute("rag.document_id", documentId);
      span.end();
      return {
        document_id: document.id,
        content_hash: document.contentHash,
        status: document.status,
        job_id: job.id,
        deduplicated: false,
      };
    });
  }

  async lookup(principal: Principal, input: LookupInput) {
    requireRole(principal, "reader");
    const query = normaliseText(input.query);
    if (!query) throw new ValidationError("query is required");
    const limit = Math.min(Math.max(input.limit ?? 8, 1), 25);
    const [queryEmbedding] = await this.embeddings.embed([query]);
    if (!queryEmbedding) throw new Error("Embedding provider returned no query vector");
    const matches = await this.repository.search({
      query,
      queryEmbedding,
      filters: input.filters ?? {},
      limit,
    });
    return {
      query,
      matches,
      count: matches.length,
      embedding: this.embeddings.descriptor,
    };
  }

  async viewAll(principal: Principal, input: ViewAllInput) {
    requireRole(principal, "reader");
    const limit = Math.min(Math.max(input.limit ?? 50, 1), 100);
    const result = await this.repository.listDocuments({
      limit,
      ...(input.cursor ? { cursor: input.cursor } : {}),
      ...(input.status ? { status: input.status } : {}),
    });
    return {
      documents: result.documents,
      next_cursor: result.nextCursor,
    };
  }

  async remove(principal: Principal, documentId: string, confirm: boolean) {
    requireRole(principal, "admin");
    if (!confirm) throw new ValidationError("confirm must be true for permanent removal");
    const document = await this.repository.getDocument(documentId);
    if (!document) throw new NotFoundError(`Document ${documentId} was not found`);
    await this.repository.markDeleting(documentId);
    await this.objectStore.delete(document.sourceObjectKey);
    const tombstone = await this.repository.purgeDocument(documentId, principal.subject);
    return {
      document_id: tombstone.documentId,
      status: "deleted" as const,
      removed_at: tombstone.removedAt,
      removed_chunks: tombstone.chunkCount,
      tombstone_retained: true,
    };
  }

  async processOneJob(): Promise<boolean> {
    const job = await this.repository.claimNextJob();
    if (!job) return false;
    const document = await this.repository.getDocument(job.documentId);
    if (!document) return true;

    try {
      await this.repository.markProcessing(document.id);
      const source = await this.objectStore.get(document.sourceObjectKey);
      const text = await extractText(source);
      const chunks = chunkText(text);
      if (chunks.length === 0) throw new ValidationError("Document produced no searchable chunks");
      const vectors = await this.embeddings.embed(chunks.map((chunk) => chunk.text));
      if (vectors.length !== chunks.length) {
        throw new Error("Embedding provider returned an unexpected number of vectors");
      }
      await this.repository.markReady({
        documentId: document.id,
        textLength: text.length,
        embedding: this.embeddings.descriptor,
        chunks: chunks.map((chunk, index) => ({
          id: randomUUID(),
          documentId: document.id,
          index: chunk.index,
          text: chunk.text,
          embedding: vectors[index] as number[],
          citation: `rag://documents/${document.id}#chunk-${chunk.index + 1}`,
        })),
      });
      logger.info("Ingestion completed", {
        documentId: document.id,
        chunkCount: chunks.length,
        attempt: job.attempts,
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : "Unknown ingestion failure";
      const current = await this.repository.getDocument(document.id);
      if (current) {
        await this.repository.markFailed(document.id, message.slice(0, 1000));
      }
      logger.error("Ingestion failed", {
        documentId: document.id,
        attempt: job.attempts,
        errorType: error instanceof Error ? error.name : "unknown",
        documentRemoved: current === null,
      });
    }
    return true;
  }

  async cleanupExpiredUploads(now = new Date()): Promise<number> {
    return this.objectStore.deleteExpired("uploads/", this.uploads.expirationCutoff(now));
  }
}
