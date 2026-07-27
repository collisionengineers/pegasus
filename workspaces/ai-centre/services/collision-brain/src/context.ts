import { createAuthProvider } from "./auth.js";
import { LocalHashEmbeddingProvider } from "./adapters/embeddings/local-hash.js";
import { FilesystemObjectStore } from "./adapters/object-store/filesystem.js";
import { MemoryObjectStore } from "./adapters/object-store/memory.js";
import { S3ObjectStore } from "./adapters/object-store/s3.js";
import { InMemoryDocumentRepository } from "./adapters/repository/in-memory.js";
import { PostgresDocumentRepository } from "./adapters/repository/postgres.js";
import type { AppConfig } from "./config.js";
import { UploadTokenService } from "./ingestion/upload-tokens.js";
import type { AuthProvider } from "./ports/auth-provider.js";
import type { DocumentRepository } from "./ports/document-repository.js";
import type { EmbeddingProvider } from "./ports/embedding-provider.js";
import type { ObjectStore } from "./ports/object-store.js";
import { RagService } from "./services/rag-service.js";

export interface ApplicationContext {
  config: AppConfig;
  repository: DocumentRepository;
  objectStore: ObjectStore;
  embeddings: EmbeddingProvider;
  auth: AuthProvider;
  uploads: UploadTokenService;
  rag: RagService;
  close(): Promise<void>;
}

export async function createApplicationContext(config: AppConfig): Promise<ApplicationContext> {
  const repository: DocumentRepository = config.repositoryDriver === "memory"
    ? new InMemoryDocumentRepository()
    : new PostgresDocumentRepository(config.databaseUrl);
  const objectStore: ObjectStore = config.objectStoreDriver === "memory"
    ? new MemoryObjectStore()
    : config.objectStoreDriver === "s3"
      ? new S3ObjectStore({
        endpoint: config.s3Endpoint,
        region: config.s3Region,
        bucket: config.s3Bucket as string,
        accessKeyId: config.s3AccessKeyId,
        secretAccessKey: config.s3SecretAccessKey,
        forcePathStyle: config.s3ForcePathStyle,
      })
      : new FilesystemObjectStore(config.objectStorePath);
  if (config.repositoryDriver === "postgres" && config.embeddingDimensions !== 384) {
    throw new Error("The v1 PostgreSQL migration requires EMBEDDING_DIMENSIONS=384");
  }
  const embeddings = new LocalHashEmbeddingProvider(config.embeddingDimensions);
  const auth = createAuthProvider(config);
  const uploads = new UploadTokenService(
    objectStore,
    config.uploadTokenSecret,
    config.uploadTtlSeconds,
  );
  await repository.initialise();
  const rag = new RagService(repository, objectStore, embeddings, uploads);

  return {
    config,
    repository,
    objectStore,
    embeddings,
    auth,
    uploads,
    rag,
    close: () => repository.close(),
  };
}
