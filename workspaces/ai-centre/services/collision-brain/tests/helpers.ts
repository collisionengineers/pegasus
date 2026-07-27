import { InMemoryDocumentRepository } from "../src/adapters/repository/in-memory.js";
import { LocalHashEmbeddingProvider } from "../src/adapters/embeddings/local-hash.js";
import { MemoryObjectStore } from "../src/adapters/object-store/memory.js";
import type { Principal } from "../src/domain/types.js";
import { UploadTokenService } from "../src/ingestion/upload-tokens.js";
import { RagService } from "../src/services/rag-service.js";

export const admin: Principal = {
  subject: "test-admin",
  roles: ["reader", "contributor", "admin"],
};

export function createTestService() {
  const repository = new InMemoryDocumentRepository();
  const objectStore = new MemoryObjectStore();
  const embeddings = new LocalHashEmbeddingProvider(384);
  const uploads = new UploadTokenService(objectStore, "test-upload-secret", 900);
  const rag = new RagService(repository, objectStore, embeddings, uploads);
  return { repository, objectStore, embeddings, uploads, rag };
}
