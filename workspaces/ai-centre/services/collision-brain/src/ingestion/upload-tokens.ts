import { createHash, createHmac, randomUUID, timingSafeEqual } from "node:crypto";
import { ValidationError } from "../domain/errors.js";
import type { ObjectStore } from "../ports/object-store.js";

interface UploadTokenPayload {
  version: 1;
  key: string;
  filename: string;
  contentType: string;
  sizeBytes: number;
  contentHash: string;
  expiresAt: number;
}

export interface StageUploadInput {
  body: Buffer;
  filename: string;
  contentType: string;
}

export interface StagedUpload {
  uploadRef: string;
  contentHash: string;
  expiresAt: string;
}

export class UploadTokenService {
  constructor(
    private readonly objectStore: ObjectStore,
    private readonly secret: string,
    private readonly ttlSeconds: number,
  ) {}

  async stage(input: StageUploadInput): Promise<StagedUpload> {
    const key = `uploads/${randomUUID()}`;
    const contentHash = createHash("sha256").update(input.body).digest("hex");
    const expiresAt = Math.floor(Date.now() / 1000) + this.ttlSeconds;
    await this.objectStore.put({
      key,
      body: input.body,
      filename: input.filename,
      contentType: input.contentType,
    });
    const uploadRef = this.sign({
      version: 1,
      key,
      filename: input.filename,
      contentType: input.contentType,
      sizeBytes: input.body.byteLength,
      contentHash,
      expiresAt,
    });
    return {
      uploadRef,
      contentHash,
      expiresAt: new Date(expiresAt * 1000).toISOString(),
    };
  }

  expirationCutoff(now = new Date()): Date {
    return new Date(now.getTime() - this.ttlSeconds * 1000);
  }

  verify(uploadRef: string): UploadTokenPayload {
    const [encoded, suppliedSignature, extra] = uploadRef.split(".");
    if (!encoded || !suppliedSignature || extra) {
      throw new ValidationError("Upload reference is malformed");
    }
    const expectedSignature = this.signature(encoded);
    const supplied = Buffer.from(suppliedSignature, "base64url");
    const expected = Buffer.from(expectedSignature, "base64url");
    if (supplied.length !== expected.length || !timingSafeEqual(supplied, expected)) {
      throw new ValidationError("Upload reference signature is invalid");
    }

    let payload: UploadTokenPayload;
    try {
      payload = JSON.parse(Buffer.from(encoded, "base64url").toString("utf8")) as UploadTokenPayload;
    } catch {
      throw new ValidationError("Upload reference payload is invalid");
    }
    if (payload.version !== 1 || payload.expiresAt <= Math.floor(Date.now() / 1000)) {
      throw new ValidationError("Upload reference has expired");
    }
    return payload;
  }

  private sign(payload: UploadTokenPayload): string {
    const encoded = Buffer.from(JSON.stringify(payload), "utf8").toString("base64url");
    return `${encoded}.${this.signature(encoded)}`;
  }

  private signature(encoded: string): string {
    return createHmac("sha256", this.secret).update(encoded, "utf8").digest("base64url");
  }
}
