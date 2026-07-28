import type {
  ChunkRecord,
  CreateDocumentInput,
  DocumentRecord,
  EmbeddingDescriptor,
  IngestionJob,
  ListDocumentsInput,
  ListDocumentsResult,
  LookupFilters,
  LookupMatch,
  Tombstone,
} from "../domain/types.js";

export interface SearchInput {
  query: string;
  queryEmbedding: number[];
  filters: LookupFilters;
  limit: number;
}

export interface ReadyDocumentInput {
  documentId: string;
  chunks: ChunkRecord[];
  embedding: EmbeddingDescriptor;
  textLength: number;
}

export interface DocumentRepository {
  initialise(): Promise<void>;
  close(): Promise<void>;
  createDocumentWithJob(input: CreateDocumentInput): Promise<{
    document: DocumentRecord;
    job: IngestionJob;
  }>;
  getDocument(id: string): Promise<DocumentRecord | null>;
  findByContentHash(contentHash: string): Promise<DocumentRecord | null>;
  listDocuments(input: ListDocumentsInput): Promise<ListDocumentsResult>;
  claimNextJob(): Promise<IngestionJob | null>;
  markProcessing(documentId: string): Promise<void>;
  markReady(input: ReadyDocumentInput): Promise<void>;
  markFailed(documentId: string, error: string): Promise<void>;
  markDeleting(documentId: string): Promise<void>;
  search(input: SearchInput): Promise<LookupMatch[]>;
  purgeDocument(documentId: string, removedBy: string): Promise<Tombstone>;
}
