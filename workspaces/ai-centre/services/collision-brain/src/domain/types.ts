export const documentStatuses = [
  "pending",
  "processing",
  "ready",
  "failed",
  "deleting",
  "deleted",
] as const;

export type DocumentStatus = (typeof documentStatuses)[number];
export type ActiveDocumentStatus = Exclude<DocumentStatus, "deleted">;
export type Role = "reader" | "contributor" | "admin";

export interface Principal {
  subject: string;
  roles: Role[];
}

export interface DocumentMetadata {
  title: string;
  source: string | null;
  tags: string[];
  filename: string | null;
  mimeType: string;
  sizeBytes: number;
}

export interface EmbeddingDescriptor {
  provider: string;
  model: string;
  dimensions: number;
  version: string;
}

export interface DocumentRecord {
  id: string;
  status: ActiveDocumentStatus;
  contentHash: string;
  sourceObjectKey: string;
  metadata: DocumentMetadata;
  embedding: EmbeddingDescriptor | null;
  chunkCount: number;
  textLength: number;
  error: string | null;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
}

export interface DocumentSummary {
  id: string;
  status: DocumentStatus;
  contentHash: string;
  metadata: DocumentMetadata;
  embedding: EmbeddingDescriptor | null;
  chunkCount: number;
  textLength: number;
  error: string | null;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
}

export interface ChunkRecord {
  id: string;
  documentId: string;
  index: number;
  text: string;
  citation: string;
  embedding: number[];
}

export interface IngestionJob {
  id: string;
  documentId: string;
  attempts: number;
  createdAt: string;
}

export interface LookupFilters {
  source?: string;
  tags?: string[];
  documentIds?: string[];
}

export interface LookupMatch {
  documentId: string;
  chunkId: string;
  title: string;
  source: string | null;
  chunkIndex: number;
  excerpt: string;
  score: number;
  citation: string;
  tags: string[];
}

export interface ListDocumentsInput {
  cursor?: string;
  limit: number;
  status?: DocumentStatus;
}

export interface ListDocumentsResult {
  documents: DocumentSummary[];
  nextCursor: string | null;
}

export interface CreateDocumentInput {
  id: string;
  contentHash: string;
  sourceObjectKey: string;
  metadata: DocumentMetadata;
  createdBy: string;
}

export interface Tombstone {
  documentId: string;
  contentHash: string;
  removedBy: string;
  removedAt: string;
  chunkCount: number;
}

export interface ExportBundle {
  schemaVersion: 1;
  exportedAt: string;
  documents: Array<{
    document: DocumentSummary;
    sourceBase64: string;
  }>;
}
