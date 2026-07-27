import {
  DeleteObjectCommand,
  GetObjectCommand,
  HeadObjectCommand,
  ListObjectsV2Command,
  PutObjectCommand,
  S3Client,
} from "@aws-sdk/client-s3";
import { NotFoundError } from "../../domain/errors.js";
import type { ObjectStore, PutObjectInput, StoredObject } from "../../ports/object-store.js";

export interface S3ObjectStoreConfig {
  endpoint: string | null;
  region: string;
  bucket: string;
  accessKeyId: string | null;
  secretAccessKey: string | null;
  forcePathStyle: boolean;
}

function isNotFound(error: unknown): boolean {
  const candidate = error as { name?: string; $metadata?: { httpStatusCode?: number } };
  return candidate.name === "NoSuchKey" ||
    candidate.name === "NotFound" ||
    candidate.$metadata?.httpStatusCode === 404;
}

export class S3ObjectStore implements ObjectStore {
  private readonly client: S3Client;

  constructor(private readonly config: S3ObjectStoreConfig) {
    this.client = new S3Client({
      region: config.region,
      ...(config.endpoint ? { endpoint: config.endpoint } : {}),
      forcePathStyle: config.forcePathStyle,
      ...(config.accessKeyId && config.secretAccessKey ? {
        credentials: {
          accessKeyId: config.accessKeyId,
          secretAccessKey: config.secretAccessKey,
        },
      } : {}),
    });
  }

  async put(input: PutObjectInput): Promise<void> {
    await this.client.send(new PutObjectCommand({
      Bucket: this.config.bucket,
      Key: input.key,
      Body: input.body,
      ContentType: input.contentType,
      Metadata: {
        filename: Buffer.from(input.filename, "utf8").toString("base64url"),
        createdat: new Date().toISOString(),
      },
    }));
  }

  async get(key: string): Promise<StoredObject> {
    try {
      const result = await this.client.send(new GetObjectCommand({
        Bucket: this.config.bucket,
        Key: key,
      }));
      if (!result.Body) throw new NotFoundError(`Object ${key} has no body`);
      return {
        body: Buffer.from(await result.Body.transformToByteArray()),
        contentType: result.ContentType ?? "application/octet-stream",
        filename: result.Metadata?.filename
          ? Buffer.from(result.Metadata.filename, "base64url").toString("utf8")
          : key.split("/").at(-1) ?? "document",
      };
    } catch (error) {
      if (isNotFound(error)) throw new NotFoundError(`Object ${key} was not found`);
      throw error;
    }
  }

  async delete(key: string): Promise<void> {
    await this.client.send(new DeleteObjectCommand({
      Bucket: this.config.bucket,
      Key: key,
    }));
  }

  async exists(key: string): Promise<boolean> {
    try {
      await this.client.send(new HeadObjectCommand({
        Bucket: this.config.bucket,
        Key: key,
      }));
      return true;
    } catch (error) {
      if (isNotFound(error)) return false;
      throw error;
    }
  }

  async deleteExpired(prefix: string, before: Date): Promise<number> {
    let token: string | undefined;
    let removed = 0;
    do {
      const page = await this.client.send(new ListObjectsV2Command({
        Bucket: this.config.bucket,
        Prefix: prefix,
        ...(token ? { ContinuationToken: token } : {}),
      }));
      for (const object of page.Contents ?? []) {
        if (object.Key && object.LastModified && object.LastModified < before) {
          await this.delete(object.Key);
          removed += 1;
        }
      }
      token = page.NextContinuationToken;
    } while (token);
    return removed;
  }
}
