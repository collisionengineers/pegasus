import { mkdir, readFile, readdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { NotFoundError, ValidationError } from "../../domain/errors.js";
import type { ObjectStore, PutObjectInput, StoredObject } from "../../ports/object-store.js";

interface ObjectMetadata {
  contentType: string;
  filename: string;
  createdAt: string;
}

export class FilesystemObjectStore implements ObjectStore {
  private readonly root: string;

  constructor(root: string) {
    this.root = path.resolve(root);
  }

  async put(input: PutObjectInput): Promise<void> {
    const target = this.resolveKey(input.key);
    await mkdir(path.dirname(target), { recursive: true });
    await Promise.all([
      writeFile(target, input.body),
      writeFile(`${target}.metadata.json`, JSON.stringify({
        contentType: input.contentType,
        filename: input.filename,
        createdAt: new Date().toISOString(),
      } satisfies ObjectMetadata)),
    ]);
  }

  async get(key: string): Promise<StoredObject> {
    const target = this.resolveKey(key);
    try {
      const [body, metadataRaw] = await Promise.all([
        readFile(target),
        readFile(`${target}.metadata.json`, "utf8"),
      ]);
      const metadata = JSON.parse(metadataRaw) as ObjectMetadata;
      return { body, contentType: metadata.contentType, filename: metadata.filename };
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === "ENOENT") {
        throw new NotFoundError(`Object ${key} was not found`);
      }
      throw error;
    }
  }

  async delete(key: string): Promise<void> {
    const target = this.resolveKey(key);
    await Promise.all([
      rm(target, { force: true }),
      rm(`${target}.metadata.json`, { force: true }),
    ]);
  }

  async exists(key: string): Promise<boolean> {
    try {
      await readFile(this.resolveKey(key));
      return true;
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === "ENOENT") return false;
      throw error;
    }
  }

  async deleteExpired(prefix: string, before: Date): Promise<number> {
    const directory = this.resolveKey(prefix);
    let entries;
    try {
      entries = await readdir(directory, { withFileTypes: true });
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === "ENOENT") return 0;
      throw error;
    }

    let removed = 0;
    for (const entry of entries) {
      if (!entry.isFile() || entry.name.endsWith(".metadata.json")) continue;
      const key = `${prefix.replace(/\/$/, "")}/${entry.name}`;
      try {
        const metadata = JSON.parse(
          await readFile(`${this.resolveKey(key)}.metadata.json`, "utf8"),
        ) as ObjectMetadata;
        if (new Date(metadata.createdAt) < before) {
          await this.delete(key);
          removed += 1;
        }
      } catch (error) {
        if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error;
      }
    }
    return removed;
  }

  private resolveKey(key: string): string {
    if (!key || key.includes("\0") || path.isAbsolute(key)) {
      throw new ValidationError("Object key is invalid");
    }
    const target = path.resolve(this.root, key);
    const relative = path.relative(this.root, target);
    if (relative.startsWith("..") || path.isAbsolute(relative)) {
      throw new ValidationError("Object key escapes the configured object store");
    }
    return target;
  }
}
