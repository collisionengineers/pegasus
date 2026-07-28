import { readFile, writeFile } from "node:fs/promises";
import { z } from "zod";
import { loadConfig } from "../config.js";
import { createApplicationContext } from "../context.js";
import type { Principal } from "../domain/types.js";
import { requiredArgument } from "./arguments.js";
import { isMain } from "./is-main.js";

const benchmarkSchema = z.object({
  name: z.string().min(1),
  documents: z.array(z.object({
    key: z.string().min(1),
    title: z.string().min(1),
    text: z.string().min(1),
    source: z.string().optional(),
    tags: z.array(z.string()).optional(),
  })).min(1),
  queries: z.array(z.object({
    query: z.string().min(1),
    relevant_document_keys: z.array(z.string()).min(1),
    limit: z.number().int().min(1).max(25).default(8),
  })).min(1),
});

const benchmarkPrincipal: Principal = {
  subject: "retrieval-benchmark",
  roles: ["reader", "contributor", "admin"],
};

export interface BenchmarkReport {
  benchmark: string;
  generated_at: string;
  embedding: {
    provider: string;
    model: string;
    dimensions: number;
    version: string;
  };
  document_count: number;
  query_count: number;
  recall_at_k: number;
  mean_reciprocal_rank: number;
  mean_lookup_ms: number;
  results: Array<{
    query: string;
    recall: number;
    reciprocal_rank: number;
    lookup_ms: number;
    retrieved_document_keys: string[];
  }>;
}

export async function runBenchmark(inputPath: string): Promise<BenchmarkReport> {
  const benchmark = benchmarkSchema.parse(JSON.parse(await readFile(inputPath, "utf8")));
  const context = await createApplicationContext(loadConfig());
  try {
    const keyByDocumentId = new Map<string, string>();
    for (const document of benchmark.documents) {
      const written = await context.rag.write(benchmarkPrincipal, {
        title: document.title,
        text: document.text,
        ...(document.source ? { source: document.source } : {}),
        ...(document.tags ? { tags: document.tags } : {}),
      });
      keyByDocumentId.set(written.document_id, document.key);
    }
    while (await context.rag.processOneJob()) {
      // Drain the benchmark corpus before evaluating queries.
    }

    const results: BenchmarkReport["results"] = [];
    for (const query of benchmark.queries) {
      const started = performance.now();
      const lookup = await context.rag.lookup(benchmarkPrincipal, {
        query: query.query,
        limit: query.limit,
      });
      const lookupMilliseconds = performance.now() - started;
      const retrieved = [...new Set(lookup.matches
        .map((match) => keyByDocumentId.get(match.documentId))
        .filter((key): key is string => Boolean(key)))];
      const relevant = new Set(query.relevant_document_keys);
      const retrievedRelevant = retrieved.filter((key) => relevant.has(key));
      const firstRelevant = retrieved.findIndex((key) => relevant.has(key));
      results.push({
        query: query.query,
        recall: retrievedRelevant.length / relevant.size,
        reciprocal_rank: firstRelevant < 0 ? 0 : 1 / (firstRelevant + 1),
        lookup_ms: Number(lookupMilliseconds.toFixed(3)),
        retrieved_document_keys: retrieved,
      });
    }

    const average = (values: number[]) =>
      values.reduce((sum, value) => sum + value, 0) / values.length;
    return {
      benchmark: benchmark.name,
      generated_at: new Date().toISOString(),
      embedding: context.embeddings.descriptor,
      document_count: benchmark.documents.length,
      query_count: benchmark.queries.length,
      recall_at_k: Number(average(results.map((result) => result.recall)).toFixed(4)),
      mean_reciprocal_rank: Number(
        average(results.map((result) => result.reciprocal_rank)).toFixed(4),
      ),
      mean_lookup_ms: Number(average(results.map((result) => result.lookup_ms)).toFixed(3)),
      results,
    };
  } finally {
    await context.close();
  }
}

if (isMain(import.meta.url)) {
  const inputPath = requiredArgument("--input");
  const outputIndex = process.argv.indexOf("--output");
  const outputPath = outputIndex >= 0 ? process.argv[outputIndex + 1] : undefined;
  const report = await runBenchmark(inputPath);
  const json = `${JSON.stringify(report, null, 2)}\n`;
  if (outputPath) {
    await writeFile(outputPath, json, { flag: "wx" });
    process.stderr.write(`Benchmark report written to ${outputPath}\n`);
  } else {
    process.stdout.write(json);
  }
}
