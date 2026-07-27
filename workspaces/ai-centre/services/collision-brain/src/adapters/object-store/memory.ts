import { NotFoundError } from "../../domain/errors.js";
import type { ObjectStore, PutObjectInput, StoredObject } from "../../ports/object-store.js";

export class MemoryObjectStore implements ObjectStore {
  private readonly objects = new Map<string, StoredObject & { createdAt: Date }>();

  async put(input: PutObjectInput): Promise<void> {
    this.objects.set(input.key, {
      body: Buffer.from(input.body),
      contentType: input.contentType,
      filename: input.filename,
      createdAt: new Date(),
    });
  }

  async get(key: string): Promise<StoredObject> {
    const value = this.objects.get(key);
    if (!value) throw new NotFoundError(`Object ${key} was not found`);
    return {
      body: Buffer.from(value.body),
      contentType: value.contentType,
      filename: value.filename,
    };
  }

  async delete(key: string): Promise<void> {
    this.objects.delete(key);
  }

  async exists(key: string): Promise<boolean> {
    return this.objects.has(key);
  }

  async deleteExpired(prefix: string, before: Date): Promise<number> {
    let removed = 0;
    for (const [key, value] of this.objects) {
      if (key.startsWith(prefix) && value.createdAt < before) {
        this.objects.delete(key);
        removed += 1;
      }
    }
    return removed;
  }
}
