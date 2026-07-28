import { readFile } from "node:fs/promises";
import { z } from "zod";
import { loadConfig } from "../config.js";
import { createApplicationContext } from "../context.js";
import type { Principal } from "../domain/types.js";
import { requiredArgument } from "./arguments.js";
import { isMain } from "./is-main.js";

const bundleSchema = z.object({
  schemaVersion: z.literal(1),
  exportedAt: z.string(),
  documents: z.array(z.object({
    document: z.object({
      metadata: z.object({
        title: z.string(),
        source: z.string().nullable(),
        tags: z.array(z.string()),
        filename: z.string().nullable(),
        mimeType: z.string(),
      }),
    }).passthrough(),
    sourceBase64: z.string(),
  })),
});

const importPrincipal: Principal = {
  subject: "data-import",
  roles: ["reader", "contributor", "admin"],
};

export async function importData(inputPath: string): Promise<number> {
  const bundle = bundleSchema.parse(JSON.parse(await readFile(inputPath, "utf8")));
  const context = await createApplicationContext(loadConfig());
  try {
    let imported = 0;
    for (const item of bundle.documents) {
      const body = Buffer.from(item.sourceBase64, "base64");
      const staged = await context.uploads.stage({
        body,
        filename: item.document.metadata.filename ?? "document.txt",
        contentType: item.document.metadata.mimeType,
      });
      const result = await context.rag.write(importPrincipal, {
        title: item.document.metadata.title,
        uploadRef: staged.uploadRef,
        ...(item.document.metadata.source ? { source: item.document.metadata.source } : {}),
        tags: item.document.metadata.tags,
      });
      if (!result.deduplicated) imported += 1;
    }
    while (await context.rag.processOneJob()) {
      // Drain the imported jobs before returning.
    }
    return imported;
  } finally {
    await context.close();
  }
}

if (isMain(import.meta.url)) {
  const inputPath = requiredArgument("--input");
  const count = await importData(inputPath);
  process.stderr.write(`Imported ${count} documents from ${inputPath}\n`);
}
