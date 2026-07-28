import { mkdtemp, rm } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { FilesystemObjectStore } from "../src/adapters/object-store/filesystem.js";
import { MemoryObjectStore } from "../src/adapters/object-store/memory.js";
import type { ObjectStore } from "../src/ports/object-store.js";

async function exercise(store: ObjectStore) {
  await store.put({
    key: "uploads/example",
    body: Buffer.from("portable bytes"),
    filename: "example.txt",
    contentType: "text/plain",
  });
  expect(await store.exists("uploads/example")).toBe(true);
  expect((await store.get("uploads/example")).body.toString("utf8")).toBe("portable bytes");
  expect(await store.deleteExpired("uploads/", new Date(Date.now() + 60_000))).toBe(1);
  expect(await store.exists("uploads/example")).toBe(false);
}

describe("ObjectStore conformance", () => {
  const temporaryDirectories: string[] = [];

  afterEach(async () => {
    await Promise.all(temporaryDirectories.splice(0).map((directory) =>
      rm(directory, { recursive: true, force: true })));
  });

  it("conforms in memory", async () => {
    await exercise(new MemoryObjectStore());
  });

  it("conforms on the filesystem and blocks traversal", async () => {
    const directory = await mkdtemp(path.join(os.tmpdir(), "rag-object-store-"));
    temporaryDirectories.push(directory);
    const store = new FilesystemObjectStore(directory);
    await exercise(store);
    await expect(store.put({
      key: "../escape",
      body: Buffer.from("blocked"),
      filename: "blocked.txt",
      contentType: "text/plain",
    })).rejects.toThrow(/escapes/);
  });
});
