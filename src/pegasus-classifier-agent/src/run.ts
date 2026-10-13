// One sweep: find the eligible mail, and only if there is any, let the agent
// work through it with its three tools.

import { tmpdir } from "node:os";
import { join } from "node:path";
import { query } from "@anthropic-ai/claude-agent-sdk";
import { readConfig, type ClassifierConfig } from "./config.js";
import { fingerprint, isCandidate, isEligible } from "./eligibility.js";
import { PegasusMail } from "./pegasus.js";
import { SystemPrompt } from "./prompt.js";
import { MemoryReviewedStore, TableReviewedStore, type ReviewedStore } from "./reviewed.js";
import { createClassifierServer, ServerName, ToolNames, type Candidate, type Logger } from "./tools.js";

const RunTimeLimitMs = 12 * 60 * 1000;

// Host variables the Claude subprocess needs; the Windows ones serve local runs.
const PassedThrough = ["PATH", "LANG", "PATHEXT", "SystemRoot", "ComSpec", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "TEMP", "TMP"];

export interface RunSummary {
  eligible: number;
  outcome: string;
  costUsd: number;
}

async function collectBatch(
  config: ClassifierConfig,
  pegasus: PegasusMail,
  reviewed: ReviewedStore,
): Promise<Map<string, Candidate>> {
  const batch = new Map<string, Candidate>();
  let continuation: string | null = null;
  for (let page = 0; page < config.scanPages; page++) {
    const listed = await pegasus.list(continuation);
    for (const summary of listed.items) {
      if (!isCandidate(summary) || (await reviewed.get(summary.id)) === fingerprint(summary)) {
        continue;
      }
      const detail = await pegasus.get(summary.id);
      if (!isEligible(detail)) {
        await reviewed.set(summary.id, fingerprint(summary));
        continue;
      }
      const destinations = detail.canLinkCase ? await pegasus.linkDestinations(summary.id) : [];
      batch.set(summary.id, { detail, destinations });
      if (batch.size >= config.batch) {
        return batch;
      }
    }
    continuation = listed.continuation;
    if (!continuation) {
      break;
    }
  }
  return batch;
}

export async function runClassifier(log: Logger): Promise<RunSummary> {
  const config = readConfig();
  const reviewed: ReviewedStore = config.storageAccount && !config.dryRun
    ? new TableReviewedStore(config.storageAccount)
    : new MemoryReviewedStore();
  const pegasus = new PegasusMail(config);
  await pegasus.connect();
  try {
    const batch = await collectBatch(config, pegasus, reviewed);
    if (batch.size === 0) {
      log("nothing eligible");
      return { eligible: 0, outcome: "nothing eligible", costUsd: 0 };
    }
    log("eligible", { count: batch.size, dryRun: config.dryRun });

    // The subprocess gets only what it needs to run: no Azure identity
    // endpoint, storage settings or Pegasus secret.
    const environment: Record<string, string | undefined> = {
      ...Object.fromEntries(PassedThrough.map((name) => [name, process.env[name]])),
      HOME: process.env.HOME ?? tmpdir(),
      TMPDIR: tmpdir(),
      ANTHROPIC_API_KEY: config.anthropicApiKey,
      CLAUDE_CONFIG_DIR: join(tmpdir(), "pegasus-classifier-agent"),
      CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC: "1",
    };

    // End the run cleanly before the Functions host's 14-minute limit.
    const abortController = new AbortController();
    const timer = setTimeout(() => abortController.abort(), RunTimeLimitMs);
    const failed = new Set<string>();
    let outcome = "no result";
    let completed = false;
    let costUsd = 0;
    try {
      for await (const message of query({
        prompt: "Deal with this run's eligible e-mails.",
        options: {
          model: config.model,
          systemPrompt: SystemPrompt,
          tools: [],
          mcpServers: {
            [ServerName]: createClassifierServer({ pegasus, batch, failed, dryRun: config.dryRun, log }),
          },
          allowedTools: ToolNames.map((name) => `mcp__${ServerName}__${name}`),
          permissionMode: "dontAsk",
          settingSources: [],
          persistSession: false,
          maxTurns: config.batch * 3 + 5,
          maxBudgetUsd: config.maxBudgetUsd,
          abortController,
          cwd: tmpdir(),
          env: environment,
        },
      })) {
        if (message.type === "result") {
          outcome = message.is_error && message.subtype === "success" ? "error" : message.subtype;
          completed = message.subtype === "success" && !message.is_error;
          costUsd = message.total_cost_usd;
          log("result", {
            subtype: message.subtype,
            isError: message.is_error,
            turns: message.num_turns,
            costUsd,
            summary: message.subtype === "success" ? message.result : undefined,
            errors: message.subtype === "success" ? undefined : message.errors,
          });
        }
      }
    } catch (error) {
      // A single-shot query throws after an error result; that result is
      // already logged. With no result at all, the run itself failed.
      if (outcome === "no result") {
        throw error;
      }
    } finally {
      clearTimeout(timer);
    }

    // A completed run has looked at every listed message, except those whose
    // write failed. A run cut short by its turn, budget or time cap tries them
    // all again next time.
    if (completed) {
      for (const { detail } of batch.values()) {
        if (!failed.has(detail.summary.id)) {
          await reviewed.set(detail.summary.id, fingerprint(detail.summary));
        }
      }
    }
    return { eligible: batch.size, outcome, costUsd };
  } finally {
    await pegasus.close();
  }
}
