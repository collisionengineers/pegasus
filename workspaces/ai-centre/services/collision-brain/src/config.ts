import { z } from "zod";

try {
  (process as { loadEnvFile?: (path?: string) => void }).loadEnvFile?.(".env");
} catch {
  // Deployments and tests normally inject environment variables directly.
}

const environmentSchema = z.object({
  NODE_ENV: z.enum(["development", "test", "production"]).default("development"),
  PORT: z.coerce.number().int().min(1).max(65535).default(3000),
  DATABASE_URL: z.string().min(1).default("postgres://rag:rag@localhost:5432/rag"),
  REPOSITORY_DRIVER: z.enum(["postgres", "memory"]).default("postgres"),
  OBJECT_STORE_DRIVER: z.enum(["filesystem", "memory", "s3"]).default("filesystem"),
  OBJECT_STORE_PATH: z.string().min(1).default(".data/objects"),
  S3_ENDPOINT: z.string().url().optional(),
  S3_REGION: z.string().min(1).default("us-east-1"),
  S3_BUCKET: z.string().min(1).optional(),
  S3_ACCESS_KEY_ID: z.string().min(1).optional(),
  S3_SECRET_ACCESS_KEY: z.string().min(1).optional(),
  S3_FORCE_PATH_STYLE: z.string().default("false")
    .transform((value) => value.toLowerCase() === "true"),
  EMBEDDING_DRIVER: z.literal("local-hash").default("local-hash"),
  EMBEDDING_DIMENSIONS: z.coerce.number().int().min(32).max(4096).default(384),
  AUTH_MODE: z.enum(["none", "shared-secret", "oidc"]).default("none"),
  MCP_SHARED_SECRET: z.string().optional(),
  OIDC_ISSUER: z.string().url().optional(),
  OIDC_AUDIENCE: z.string().min(1).optional(),
  OIDC_JWKS_URL: z.string().url().optional(),
  UPLOAD_TOKEN_SECRET: z.string().optional(),
  UPLOAD_TTL_SECONDS: z.coerce.number().int().min(60).max(86400).default(900),
  MAX_UPLOAD_BYTES: z.coerce.number().int().min(1024).default(25 * 1024 * 1024),
  WORKER_POLL_MS: z.coerce.number().int().min(100).max(60000).default(1000),
  RAG_HTTP_URL: z.string().url().default("http://localhost:3000/mcp"),
  RAG_HTTP_BEARER_TOKEN: z.string().optional(),
});

export type AppConfig = ReturnType<typeof loadConfig>;

export function loadConfig(source: NodeJS.ProcessEnv = process.env) {
  const value = environmentSchema.parse(source);

  if (value.NODE_ENV === "production" && value.AUTH_MODE === "none") {
    throw new Error("AUTH_MODE=none is not permitted in production");
  }
  if (value.AUTH_MODE === "shared-secret" && !value.MCP_SHARED_SECRET) {
    throw new Error("MCP_SHARED_SECRET is required for shared-secret authentication");
  }
  if (value.AUTH_MODE === "oidc" &&
      (!value.OIDC_ISSUER || !value.OIDC_AUDIENCE || !value.OIDC_JWKS_URL)) {
    throw new Error(
      "OIDC_ISSUER, OIDC_AUDIENCE, and OIDC_JWKS_URL are required for OIDC authentication",
    );
  }
  if (value.NODE_ENV === "production" && !value.UPLOAD_TOKEN_SECRET) {
    throw new Error("UPLOAD_TOKEN_SECRET is required in production");
  }
  if (value.OBJECT_STORE_DRIVER === "s3" && !value.S3_BUCKET) {
    throw new Error("S3_BUCKET is required when OBJECT_STORE_DRIVER=s3");
  }

  return {
    nodeEnv: value.NODE_ENV,
    port: value.PORT,
    databaseUrl: value.DATABASE_URL,
    repositoryDriver: value.REPOSITORY_DRIVER,
    objectStoreDriver: value.OBJECT_STORE_DRIVER,
    objectStorePath: value.OBJECT_STORE_PATH,
    s3Endpoint: value.S3_ENDPOINT ?? null,
    s3Region: value.S3_REGION,
    s3Bucket: value.S3_BUCKET ?? null,
    s3AccessKeyId: value.S3_ACCESS_KEY_ID ?? null,
    s3SecretAccessKey: value.S3_SECRET_ACCESS_KEY ?? null,
    s3ForcePathStyle: value.S3_FORCE_PATH_STYLE,
    embeddingDriver: value.EMBEDDING_DRIVER,
    embeddingDimensions: value.EMBEDDING_DIMENSIONS,
    authMode: value.AUTH_MODE,
    sharedSecret: value.MCP_SHARED_SECRET ?? null,
    oidcIssuer: value.OIDC_ISSUER ?? null,
    oidcAudience: value.OIDC_AUDIENCE ?? null,
    oidcJwksUrl: value.OIDC_JWKS_URL ?? null,
    uploadTokenSecret: value.UPLOAD_TOKEN_SECRET ?? "local-development-upload-secret",
    uploadTtlSeconds: value.UPLOAD_TTL_SECONDS,
    maxUploadBytes: value.MAX_UPLOAD_BYTES,
    workerPollMs: value.WORKER_POLL_MS,
    ragHttpUrl: value.RAG_HTTP_URL,
    ragHttpBearerToken: value.RAG_HTTP_BEARER_TOKEN ?? null,
  } as const;
}
