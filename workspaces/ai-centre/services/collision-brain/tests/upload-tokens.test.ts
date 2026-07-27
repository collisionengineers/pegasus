import { describe, expect, it } from "vitest";
import { MemoryObjectStore } from "../src/adapters/object-store/memory.js";
import { ValidationError } from "../src/domain/errors.js";
import { UploadTokenService } from "../src/ingestion/upload-tokens.js";

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
});
