import { randomUUID } from "node:crypto";
import { ConflictError, NotFoundError } from "../../domain/errors.js";
import { lexicalScore } from "../../domain/text.js";
import type {
  ChunkRecord,
  CreateDocumentInput,
  DocumentRecord,
  DocumentSummary,
  IngestionJob,
  ListDocumentsInput,
  ListDocumentsResult,
  LookupMatch,
  Tombstone,
} from "../../domain/types.js";
import { cosineSimilarity } from "../../domain/vector.js";
import type {
  DocumentRepository,
  ReadyDocumentInput,
  SearchInput,
} from "../../ports/document-repository.js";

interface InternalJob extends IngestionJob {
  state: "queued" | "processing" | "completed" | "failed";
}

function activeSummary(document: DocumentRecord): DocumentSummary {
  return {
    id: document.id,
    status: document.status,
    contentHash: document.contentHash,
    metadata: structuredClone(document.metadata),
    embedding: document.embedding ? structuredClone(document.embedding) : null,
    chunkCount: document.chunkCount,
    textLength: document.textLength,
    error: document.error,
    createdBy: document.createdBy,
    createdAt: document.createdAt,
    updatedAt: document.updatedAt,
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

export class InMemoryDocumentRepository implements DocumentRepository {
  private readonly documents = new Map<string, DocumentRecord>();
  private readonly chunks = new Map<string, ChunkRecord[]>();
  private readonly jobs = new Map<string, InternalJob>();
  private readonly tombstones = new Map<string, Tombstone>();

  async initialise(): Promise<void> {}
  async close(): Promise<void> {}

  async createDocumentWithJob(input: CreateDocumentInput) {
    const duplicate = await this.findByContentHash(input.contentHash);
    if (duplicate) throw new ConflictError(`Content already exists as document ${duplicate.id}`);
    const now = new Date().toISOString();
    const document: DocumentRecord = {
      id: input.id,
      status: "pending",
      contentHash: input.contentHash,
      sourceObjectKey: input.sourceObjectKey,
      metadata: structuredClone(input.metadata),
      embedding: null,
      chunkCount: 0,
      textLength: 0,
      error: null,
      createdBy: input.createdBy,
      createdAt: now,
      updatedAt: now,
    };
    const job: InternalJob = {
      id: randomUUID(),
      documentId: document.id,
      attempts: 0,
      createdAt: now,
      state: "queued",
    };
    this.documents.set(document.id, document);
    this.jobs.set(job.id, job);
    return { document: structuredClone(document), job: structuredClone(job) };
  }

  async getDocument(id: string): Promise<DocumentRecord | null> {
    const document = this.documents.get(id);
    return document ? structuredClone(document) : null;
  }

  async findByContentHash(contentHash: string): Promise<DocumentRecord | null> {
    const document = [...this.documents.values()].find(
      (candidate) => candidate.contentHash === contentHash,
    );
    return document ? structuredClone(document) : null;
  }

  async listDocuments(input: ListDocumentsInput): Promise<ListDocumentsResult> {
    const active = [...this.documents.values()].map(activeSummary);
    const deleted: DocumentSummary[] = [...this.tombstones.values()].map((tombstone) => ({
      id: tombstone.documentId,
      status: "deleted",
      contentHash: tombstone.contentHash,
      metadata: {
        title: "[removed]",
        source: null,
        tags: [],
        filename: null,
        mimeType: "application/x-removed",
        sizeBytes: 0,
      },
      embedding: null,
      chunkCount: tombstone.chunkCount,
      textLength: 0,
      error: null,
      createdBy: tombstone.removedBy,
      createdAt: tombstone.removedAt,
      updatedAt: tombstone.removedAt,
    }));
    let values = [...active, ...deleted]
      .filter((document) => !input.status || document.status === input.status)
      .sort((left, right) =>
        right.createdAt.localeCompare(left.createdAt) || right.id.localeCompare(left.id));

    if (input.cursor) {
      const cursor = decodeCursor(input.cursor);
      values = values.filter((document) =>
        document.createdAt < cursor.createdAt ||
        (document.createdAt === cursor.createdAt && document.id < cursor.id));
    }

    const page = values.slice(0, input.limit);
    const last = page.at(-1);
    return {
      documents: structuredClone(page),
      nextCursor: values.length > page.length && last
        ? encodeCursor(last.createdAt, last.id)
        : null,
    };
  }

  async claimNextJob(): Promise<IngestionJob | null> {
    const job = [...this.jobs.values()]
      .filter((candidate) => candidate.state === "queued")
      .sort((left, right) => left.createdAt.localeCompare(right.createdAt))[0];
    if (!job) return null;
    job.state = "processing";
    job.attempts += 1;
    return structuredClone(job);
  }

  async markProcessing(documentId: string): Promise<void> {
    const document = this.requiredDocument(documentId);
    document.status = "processing";
    document.updatedAt = new Date().toISOString();
    document.error = null;
  }

  async markReady(input: ReadyDocumentInput): Promise<void> {
    const document = this.requiredDocument(input.documentId);
    document.status = "ready";
    document.embedding = structuredClone(input.embedding);
    document.chunkCount = input.chunks.length;
    document.textLength = input.textLength;
    document.updatedAt = new Date().toISOString();
    document.error = null;
    this.chunks.set(document.id, structuredClone(input.chunks));
    this.finishJob(document.id, "completed");
  }

  async markFailed(documentId: string, error: string): Promise<void> {
    const document = this.requiredDocument(documentId);
    document.status = "failed";
    document.error = error;
    document.updatedAt = new Date().toISOString();
    this.finishJob(document.id, "failed");
  }

  async markDeleting(documentId: string): Promise<void> {
    const document = this.requiredDocument(documentId);
    document.status = "deleting";
    document.updatedAt = new Date().toISOString();
  }

  async search(input: SearchInput): Promise<LookupMatch[]> {
    const matches: LookupMatch[] = [];
    for (const document of this.documents.values()) {
      if (document.status !== "ready") continue;
      if (input.filters.source && document.metadata.source !== input.filters.source) continue;
      if (input.filters.documentIds &&
        !input.filters.documentIds.includes(document.id)) continue;
      if (input.filters.tags &&
        !input.filters.tags.every((tag) => document.metadata.tags.includes(tag))) continue;

      for (const chunk of this.chunks.get(document.id) ?? []) {
        const semantic = (cosineSimilarity(input.queryEmbedding, chunk.embedding) + 1) / 2;
        const lexical = lexicalScore(input.query, chunk.text);
        const score = semantic * 0.65 + lexical * 0.35;
        matches.push({
          documentId: document.id,
          chunkId: chunk.id,
          title: document.metadata.title,
          source: document.metadata.source,
          chunkIndex: chunk.index,
          excerpt: chunk.text.slice(0, 800),
          score,
          citation: chunk.citation,
          tags: [...document.metadata.tags],
        });
      }
    }
    return matches
      .sort((left, right) => right.score - left.score)
      .slice(0, input.limit)
      .map((match) => structuredClone(match));
  }

  async purgeDocument(documentId: string, removedBy: string): Promise<Tombstone> {
    const document = this.requiredDocument(documentId);
    const tombstone: Tombstone = {
      documentId,
      contentHash: document.contentHash,
      removedBy,
      removedAt: new Date().toISOString(),
      chunkCount: document.chunkCount,
    };
    this.documents.delete(documentId);
    this.chunks.delete(documentId);
    for (const [jobId, job] of this.jobs) {
      if (job.documentId === documentId) this.jobs.delete(jobId);
    }
    this.tombstones.set(documentId, tombstone);
    return structuredClone(tombstone);
  }

  private requiredDocument(id: string): DocumentRecord {
    const document = this.documents.get(id);
    if (!document) throw new NotFoundError(`Document ${id} was not found`);
    return document;
  }

  private finishJob(documentId: string, state: "completed" | "failed"): void {
    const job = [...this.jobs.values()].find(
      (candidate) => candidate.documentId === documentId && candidate.state === "processing",
    );
    if (job) job.state = state;
  }
}
