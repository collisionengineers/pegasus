// One sweep: find the eligible mail, and only if there is any, let the agent
// work through it with its tools. runAgent is the one way the model is
// called; the replay harness uses it over a fixture instead of Pegasus.

import { tmpdir } from "node:os";
import { join } from "node:path";
import { query } from "@anthropic-ai/claude-agent-sdk";
import { readConfig, type ClassifierConfig } from "./config.js";
import { fingerprint, isCandidate, isEligible } from "./eligibility.js";
import { PegasusMail, PegasusUnavailableError } from "./pegasus.js";
import { SystemPrompt } from "./prompt.js";
import {
  attemptsAt,
  isClosedAt,
  MemoryReviewedStore,
  reviewedAt,
  TableReviewedStore,
  type ReviewedStore,
} from "./reviewed.js";
import { createClassifierServer, ServerName, ToolNames, type Candidate, type Logger } from "./tools.js";

const RunTimeLimitMs = 12 * 60 * 1000;

// A message whose write failed, or that a completed run gave no verdict, is
// looked at again on the next run. After this many such runs, with the
// message unchanged, it is closed as failed and not looked at again unless it
// changes.
const MaxFailedAttempts = 3;

const NoVerdict = "no verdict in a completed run";

// Host variables the Claude subprocess needs; the Windows ones serve local runs.
const PassedThrough = ["PATH", "LANG", "PATHEXT", "SystemRoot", "ComSpec", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "TEMP", "TMP"];

export interface RunSummary {
  eligible: number;
  outcome: string;
  costUsd: number;
}

/** What one call of the model over a batch came to. */
export interface AgentResult {
  outcome: string;
  completed: boolean;
  costUsd: number;
  turns: number;
  summary: string | undefined;
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
      if (!isCandidate(summary) || isClosedAt(await reviewed.get(summary.id), fingerprint(summary))) {
        continue;
      }
      const detail = await pegasus.get(summary.id);
      if (!isEligible(detail)) {
        await reviewed.set(summary.id, reviewedAt(fingerprint(summary)));
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

/**
 * Calls the model once over the server's batch and reports how the call
 * ended. The caller owns the batch, the failed map and the verdict set.
 */
export async function runAgent(
  config: ClassifierConfig,
  server: ReturnType<typeof createClassifierServer>,
  batchSize: number,
  log: Logger,
): Promise<AgentResult> {
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
  const result: AgentResult = { outcome: "no result", completed: false, costUsd: 0, turns: 0, summary: undefined };
  try {
    for await (const message of query({
      prompt: "Deal with this run's eligible e-mails.",
      options: {
        model: config.model,
        systemPrompt: SystemPrompt,
        tools: [],
        mcpServers: { [ServerName]: server },
        allowedTools: ToolNames.map((name) => `mcp__${ServerName}__${name}`),
        permissionMode: "dontAsk",
        settingSources: [],
        persistSession: false,
        // Per e-mail: a read, an attachment read, up to two lookups (R561),
        // a classification and a link, beside the list.
        maxTurns: batchSize * 7 + 5,
        maxBudgetUsd: config.maxBudgetUsd,
        abortController,
        cwd: tmpdir(),
        env: environment,
      },
    })) {
      if (message.type === "result") {
        result.outcome = message.is_error && message.subtype === "success" ? "error" : message.subtype;
        result.completed = message.subtype === "success" && !message.is_error;
        result.costUsd = message.total_cost_usd;
        result.turns = message.num_turns;
        result.summary = message.subtype === "success" ? message.result : undefined;
        log("result", {
          subtype: message.subtype,
          isError: message.is_error,
          turns: message.num_turns,
          costUsd: result.costUsd,
          summary: result.summary,
          errors: message.subtype === "success" ? undefined : message.errors,
        });
      }
    }
  } catch (error) {
    // A single-shot query throws after an error result; that result is
    // already logged. With no result at all, the run itself failed.
    if (result.outcome === "no result") {
      throw error;
    }
  } finally {
    clearTimeout(timer);
  }
  return result;
}

export async function runClassifier(log: Logger): Promise<RunSummary> {
  const config = readConfig();
  const reviewed: ReviewedStore = config.storageAccount && !config.dryRun
    ? new TableReviewedStore(config.storageAccount)
    : new MemoryReviewedStore();
  const pegasus = new PegasusMail(config);
  try {
    await pegasus.connect();
  } catch (error) {
    // A Web the release stopped, or one restarting, skips this tick.
    if (error instanceof PegasusUnavailableError) {
      log("pegasus unavailable", { status: error.status });
      return { eligible: 0, outcome: "pegasus unavailable", costUsd: 0 };
    }
    throw error;
  }
  try {
    const batch = await collectBatch(config, pegasus, reviewed);
    if (batch.size === 0) {
      log("nothing eligible");
      return { eligible: 0, outcome: "nothing eligible", costUsd: 0 };
    }
    log("eligible", { count: batch.size, dryRun: config.dryRun });

    const failed = new Map<string, string>();
    const verdicts = new Set<string>();
    const server = createClassifierServer({ pegasus, batch, failed, verdicts, dryRun: config.dryRun, log });
    const result = await runAgent(config, server, batch.size, log);
    let { outcome } = result;

    // A completed run that decided nothing, with eligible mail and no refused
    // write, is a failed run: the model did not read the batch (#1829).
    if (result.completed && verdicts.size === 0 && failed.size === 0) {
      outcome = "failed: no verdicts";
      log("failed", { reason: "the run completed without a verdict on any eligible e-mail", eligible: batch.size, summary: result.summary });
    }

    // A message is marked reviewed only on a verdict. A message whose write
    // failed, or that a completed run left without a verdict, counts one
    // failed attempt and is looked at again next run until it has failed
    // MaxFailedAttempts runs while unchanged; then it is closed as failed. A
    // run cut short by its turn, budget or time cap writes nothing for the
    // messages it did not reach, so they are tried again next time.
    for (const { detail } of batch.values()) {
      const messageId = detail.summary.id;
      const seen = fingerprint(detail.summary);
      const lastError = failed.get(messageId)
        ?? (result.completed && !verdicts.has(messageId) ? NoVerdict : undefined);
      if (lastError !== undefined) {
        const attempts = attemptsAt(await reviewed.get(messageId), seen) + 1;
        const giveUp = attempts >= MaxFailedAttempts;
        await reviewed.set(messageId, { fingerprint: seen, outcome: giveUp ? "failed" : "retrying", attempts });
        if (giveUp) {
          log("closed as failed: no accepted verdict in any attempt", { messageId, attempts, lastError });
        }
      } else if (verdicts.has(messageId)) {
        await reviewed.set(messageId, reviewedAt(seen));
      }
    }
    return { eligible: batch.size, outcome, costUsd: result.costUsd };
  } finally {
    await pegasus.close();
  }
}
