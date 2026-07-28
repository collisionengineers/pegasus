import { describe, expect, it } from "vitest";
import { MemoryObjectStore } from "../src/adapters/object-store/memory.js";
import { ValidationError } from "../src/domain/errors.js";
import { UploadTokenService } from "../src/ingestion/upload-tokens.js";
import { createTestService } from "./helpers.js";

describe("UploadTokenService", () => {
  it("signs staged object metadata and rejects tampering", async () => {
    const store = new MemoryObjectStore();
    const service = new UploadTokenService(store, "secret", 900);
    const staged = await service.stage({
      body: Buffer.from("knowledge"),
      filename: "knowledge.txt",
      contentType: "text/plain",
    });

    const payload = service.verify(staged.uploadRef);
    expect(payload.filename).toBe("knowledge.txt");
    expect(await store.exists(payload.key)).toBe(true);

    await expect(async () => service.verify(`${staged.uploadRef}x`))
      .rejects.toBeInstanceOf(ValidationError);
  });

  it("retains staged objects until their signed reference expires", async () => {
    const stagedAt = new Date();
    const { objectStore, uploads, rag } = createTestService(undefined, 60);
    const staged = await uploads.stage({
      body: Buffer.from("knowledge"),
      filename: "knowledge.txt",
      contentType: "text/plain",
    });
    const payload = uploads.verify(staged.uploadRef);

    expect(await rag.cleanupExpiredUploads(new Date(stagedAt.getTime() + 59_000))).toBe(0);
    expect(await objectStore.exists(payload.key)).toBe(true);
    expect(await rag.cleanupExpiredUploads(new Date(stagedAt.getTime() + 61_000))).toBe(1);
    expect(await objectStore.exists(payload.key)).toBe(false);
  });
});
