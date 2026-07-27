import { describe, expect, it } from "vitest";
import { ForbiddenError } from "../src/domain/errors.js";
import type { Principal } from "../src/domain/types.js";
import { admin, createTestService } from "./helpers.js";

describe("RagService", () => {
  it("runs the complete text lifecycle and retains a content-free tombstone", async () => {
    const { rag } = createTestService();

    const written = await rag.write(admin, {
      title: "Repair policy",
      text: "Structural repair evidence must identify the source method and the relevant section.",
      source: "engineering-manual",
      tags: ["repair", "evidence"],
    });
    expect(written.status).toBe("pending");
    expect(written.deduplicated).toBe(false);

    expect((await rag.lookup(admin, { query: "structural repair" })).matches).toEqual([]);
    expect(await rag.processOneJob()).toBe(true);

    const registry = await rag.viewAll(admin, {});
    expect(registry.documents).toHaveLength(1);
    expect(registry.documents[0]?.status).toBe("ready");
    expect(registry.documents[0]).not.toHaveProperty("sourceObjectKey");

    const lookup = await rag.lookup(admin, {
      query: "What evidence is required for structural repairs?",
      filters: { tags: ["repair"] },
    });
    expect(lookup.matches).toHaveLength(1);
    expect(lookup.matches[0]?.documentId).toBe(written.document_id);
    expect(lookup.matches[0]?.citation).toBe(
      `rag://documents/${written.document_id}#chunk-1`,
    );

    const duplicate = await rag.write(admin, {
      title: "Duplicate",
      text: "Structural repair evidence must identify the source method and the relevant section.",
    });
    expect(duplicate.deduplicated).toBe(true);
    expect(duplicate.document_id).toBe(written.document_id);

    const removed = await rag.remove(admin, written.document_id, true);
    expect(removed.status).toBe("deleted");
    expect((await rag.lookup(admin, { query: "structural repair" })).matches).toEqual([]);

    const deleted = await rag.viewAll(admin, { status: "deleted" });
    expect(deleted.documents).toHaveLength(1);
    expect(deleted.documents[0]?.metadata.title).toBe("[removed]");
    expect(deleted.documents[0]?.metadata.sizeBytes).toBe(0);
  });

  it("ingests a securely staged HTML file", async () => {
    const { rag, uploads } = createTestService();
    const staged = await uploads.stage({
      filename: "guide.html",
      contentType: "text/html",
      body: Buffer.from("<html><body><h1>Tyre guide</h1><p>Replace unsafe tyres.</p></body></html>"),
    });
    const written = await rag.write(admin, {
      title: "Tyre guide",
      uploadRef: staged.uploadRef,
    });
    await rag.processOneJob();

    const lookup = await rag.lookup(admin, { query: "unsafe tyres" });
    expect(lookup.matches[0]?.documentId).toBe(written.document_id);
    expect(lookup.matches[0]?.excerpt).not.toContain("<html>");
  });

  it("enforces contributor and administrator roles", async () => {
    const reader: Principal = { subject: "reader", roles: ["reader"] };
    const contributor: Principal = {
      subject: "contributor",
      roles: ["reader", "contributor"],
    };
    const { rag } = createTestService();

    await expect(rag.write(reader, { title: "Denied", text: "Content" }))
      .rejects.toBeInstanceOf(ForbiddenError);

    const written = await rag.write(contributor, { title: "Allowed", text: "Content" });
    await expect(rag.remove(contributor, written.document_id, true))
      .rejects.toBeInstanceOf(ForbiddenError);
  });
});
