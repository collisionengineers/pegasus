import { writeFile } from "node:fs/promises";
import { loadConfig } from "../config.js";
import { createApplicationContext } from "../context.js";
import type { ExportBundle } from "../domain/types.js";
import { requiredArgument } from "./arguments.js";
import { isMain } from "./is-main.js";

export async function exportData(outputPath: string): Promise<number> {
  const context = await createApplicationContext(loadConfig());
  try {
    const documents: ExportBundle["documents"] = [];
    let cursor: string | undefined;
    do {
      const page = await context.repository.listDocuments({
        limit: 100,
        status: "ready",
        ...(cursor ? { cursor } : {}),
      });
      for (const summary of page.documents) {
        const document = await context.repository.getDocument(summary.id);
        if (!document) continue;
        const source = await context.objectStore.get(document.sourceObjectKey);
        documents.push({
          document: summary,
          sourceBase64: source.body.toString("base64"),
        });
      }
      cursor = page.nextCursor ?? undefined;
    } while (cursor);

    const bundle: ExportBundle = {
      schemaVersion: 1,
      exportedAt: new Date().toISOString(),
      documents,
    };
    await writeFile(outputPath, JSON.stringify(bundle, null, 2), { flag: "wx" });
    return documents.length;
  } finally {
    await context.close();
  }
}

if (isMain(import.meta.url)) {
  const outputPath = requiredArgument("--output");
  const count = await exportData(outputPath);
  process.stderr.write(`Exported ${count} documents to ${outputPath}\n`);
}
