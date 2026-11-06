// A replay of one batch from a fixture file instead of Pegasus:
//   npm run build && npm run replay -- <fixture.json> [messageId ...]
// It calls the model exactly as a run does (same prompt, tools and turn
// cap), records every write the tools would have made, and prints one line
// per e-mail. Nothing reaches Pegasus. It needs ANTHROPIC_API_KEY and takes
// CLASSIFIER_MODEL and CLASSIFIER_MAX_BUDGET_USD when set. The fixture's
// shape is fixture-pegasus.ts's.

import { readFileSync } from "node:fs";
import type { ClassifierConfig } from "./config.js";
import { FixturePegasus, type Fixture } from "./fixture-pegasus.js";
import { runAgent } from "./run.js";
import { createClassifierServer, newOutcomes } from "./tools.js";

function required(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`${name} is not set.`);
  }
  return value;
}

const [fixturePath, ...only] = process.argv.slice(2);
if (!fixturePath) {
  console.error("Usage: npm run replay -- <fixture.json> [messageId ...]");
  process.exit(2);
}

const fixture = JSON.parse(readFileSync(fixturePath, "utf8")) as Fixture;
const candidates = only.length > 0
  ? fixture.candidates.filter((item) => only.includes(item.detail.summary.id))
  : fixture.candidates;
const batch = new Map(candidates.map((item) => [item.detail.summary.id, item]));

const config: ClassifierConfig = {
  anthropicApiKey: required("ANTHROPIC_API_KEY"),
  mcpUrl: "replay",
  tokenUrl: "replay",
  clientId: "replay",
  clientSecret: "replay",
  model: process.env.CLASSIFIER_MODEL?.trim() || "claude-sonnet-5-5",
  dryRun: false,
  batch: batch.size,
  scanPages: 1,
  maxBudgetUsd: Number(process.env.CLASSIFIER_MAX_BUDGET_USD?.trim() || "2"),
  storageAccount: undefined,
};

const pegasus = new FixturePegasus({ ...fixture, candidates });
const outcomes = newOutcomes();
const reads = new Set<string>();
const attachmentReads: string[] = [];
const lookups: string[] = [];
const log = (message: string, properties?: Record<string, unknown>) => {
  if (message === "read") {
    reads.add(String(properties?.messageId));
  } else if (message === "read attachment") {
    attachmentReads.push(`${String(properties?.messageId).slice(0, 8)} #${String(properties?.attachmentOrdinal)} ${String(properties?.fileName)} (${String(properties?.characters)} chars)`);
  } else if (message.startsWith("search ")) {
    lookups.push(`${message}: ${String(properties?.query)} -> ${String(properties?.hits)} (${String(properties?.state)})`);
  } else if (message === "result") {
    return;
  }
  console.error(JSON.stringify({ message, ...properties }));
};

const server = createClassifierServer({ pegasus, batch, outcomes, dryRun: false, log });
const result = await runAgent(config, server, batch.size, log);

console.log(`\nReplay of ${batch.size} e-mail(s): ${result.outcome}, ${result.turns} turns, USD ${result.costUsd.toFixed(3)}`);
console.log(`read ${reads.size}/${batch.size}; attachments read ${attachmentReads.length}; verdicts ${outcomes.verdicts.size}; deferred ${outcomes.deferred.size}; refused writes ${outcomes.failed.size}; lookups ${lookups.length}`);
for (const line of attachmentReads) {
  console.log(`  attachment ${line}`);
}
for (const line of lookups) {
  console.log(`  ${line}`);
}
console.log("");
for (const candidate of candidates) {
  const id = candidate.detail.summary.id;
  const writes = pegasus.writes.filter((write) => write.messageId === id);
  const verdict = writes.length === 0
    ? (outcomes.failed.has(id)
      ? `REFUSED ${outcomes.failed.get(id)}`
      : outcomes.deferred.has(id) ? "deferred" : outcomes.verdicts.has(id) ? "left" : "no verdict")
    : writes.map((write) => `${write.kind} ${write.detail}`).join(" | ");
  console.log(`${id}  ${(candidate.detail.summary.subject ?? "").slice(0, 70)}`);
  console.log(`    read=${reads.has(id) ? "yes" : "NO"}  ${verdict}`);
}
console.log(`\nModel summary:\n${result.summary ?? "(none)"}`);
