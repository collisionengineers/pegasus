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
import {
  createClassifierServer,
  newOutcomes,
  ServerName,
  ToolNames,
  type Candidate,
  type Logger,
  type RunOutcomes,
} from "./tools.js";

const RunTimeLimitMs = 12 * 60 * 1000;

// A message whose write failed, or that a completed run gave no verdict, is
// looked at again on the next run. After this many such runs, with the
// message unchanged, it is closed as failed and not looked at again unless it
// changes. A message left while the lookups were unavailable counts no run.
export const MaxFailedAttempts = 3;

const NoVerdict = "no verdict in a completed run";

// Runs in a row that list no candidate while Pegasus counts mail awaiting a
// decision before the run's outcome reads failed: an hour at the 15-minute
// schedule (#1892).
export const HiddenTicksToFail = 4;

/**
 * The model's calls per e-mail: a read, an attachment read, a photo read, up
 * to two lookups (R561), a Case search, a classification or a leave, and one
 * of a link, a Case (R714), a Triage or a query.
 */
export const TurnsPerEmail = 8;

/** The run's turn cap: the e-mails' calls beside the list and the closing summary. */
export function turnBudget(batchSize: number): number {
  return batchSize * TurnsPerEmail + 5;
}

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

/** The scan's batch, with what it saw of the list so a hiding list shows. */
export interface CollectedBatch {
  batch: Map<string, Candidate>;
  /** List rows that read as candidates, closed ones included. */
  candidatesListed: number;
  /** Pegasus's own count of mail awaiting a decision, from the first page. */
  awaitingDecisionCount: number;
}

export async function collectBatch(
  config: Pick<ClassifierConfig, "scanPages" | "batch">,
  pegasus: Pick<PegasusMail, "list" | "get" | "linkDestinations">,
  reviewed: ReviewedStore,
): Promise<CollectedBatch> {
  const batch = new Map<string, Candidate>();
  let candidatesListed = 0;
  let awaitingDecisionCount = 0;
  let continuation: string | null = null;
  for (let page = 0; page < config.scanPages; page++) {
    const listed = await pegasus.list(continuation);
    if (page === 0) {
      awaitingDecisionCount = listed.awaitingDecisionCount ?? 0;
    }
    for (const summary of listed.items) {
      if (!isCandidate(summary)) {
        continue;
      }
      candidatesListed++;
      if (isClosedAt(await reviewed.get(summary.id), fingerprint(summary))) {
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
        return { batch, candidatesListed, awaitingDecisionCount };
      }
    }
    continuation = listed.continuation;
    if (!continuation) {
      break;
    }
  }
  return { batch, candidatesListed, awaitingDecisionCount };
}

/**
 * Whether this run's list hid mail Pegasus counts as awaiting a decision, and
 * how many runs in a row it has. A run that lists a candidate resets it.
 */
export async function checkHidden(
  collected: CollectedBatch,
  reviewed: ReviewedStore,
  log: Logger,
): Promise<{ hidden: boolean; ticks: number }> {
  const previous = await reviewed.hiddenTicks();
  if (collected.candidatesListed > 0 || collected.awaitingDecisionCount === 0) {
    if (previous !== 0) {
      await reviewed.setHiddenTicks(0);
    }
    return { hidden: false, ticks: 0 };
  }
  const ticks = previous + 1;
  await reviewed.setHiddenTicks(ticks);
  log("warning: candidates hidden", {
    awaitingDecisionCount: collected.awaitingDecisionCount,
    consecutiveRuns: ticks,
    reason: "the mail list showed no candidate while Pegasus counts mail the pipeline decided that is Unclassified or has no Case",
  });
  return { hidden: true, ticks };
}

/**
 * Marks each message of the batch from what the run came to. A verdict marks
 * it reviewed at what its list row reads now, after the run's own writes; a
 * message left while the lookups were unavailable is left as it was; a failed
 * write, or no verdict from a completed run, counts one failed attempt, and
 * MaxFailedAttempts of them while unchanged close it as failed. A run cut
 * short by its turn, budget or time cap writes nothing for the messages it did
 * not reach, so they are tried again next time.
 */
export async function recordOutcomes(
  batch: Map<string, Candidate>,
  outcomes: RunOutcomes,
  completed: boolean,
  reviewed: ReviewedStore,
  pegasus: Pick<PegasusMail, "get">,
  log: Logger,
): Promise<void> {
  for (const { detail } of batch.values()) {
    const messageId = detail.summary.id;
    const seen = fingerprint(detail.summary);
    if (outcomes.verdicts.has(messageId)) {
      let now = seen;
      if (outcomes.written.has(messageId)) {
        try {
          now = fingerprint((await pegasus.get(messageId)).summary);
        } catch (error) {
          log("reviewed at the listed fingerprint", { messageId, error: (error as Error).message });
        }
      }
      await reviewed.set(messageId, reviewedAt(now));
      continue;
    }
    if (outcomes.deferred.has(messageId)) {
      continue;
    }
    const lastError = outcomes.failed.get(messageId) ?? (completed ? NoVerdict : undefined);
    if (lastError === undefined) {
      continue;
    }
    const attempts = attemptsAt(await reviewed.get(messageId), seen) + 1;
    const giveUp = attempts >= MaxFailedAttempts;
    await reviewed.set(messageId, { fingerprint: seen, outcome: giveUp ? "failed" : "retrying", attempts });
    if (giveUp) {
      log("closed as failed: no accepted verdict in any attempt", { messageId, attempts, lastError });
    }
  }
}

/**
 * Calls the model once over the server's batch and reports how the call
 * ended. The caller owns the batch and the run's outcomes.
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
        maxTurns: turnBudget(batchSize),
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
    const collected = await collectBatch(config, pegasus, reviewed);
    const { batch } = collected;
    const hidden = await checkHidden(collected, reviewed, log);
    if (batch.size === 0) {
      if (hidden.ticks >= HiddenTicksToFail) {
        return { eligible: 0, outcome: "failed: candidates hidden", costUsd: 0 };
      }
      log("nothing eligible");
      return { eligible: 0, outcome: hidden.hidden ? "nothing eligible: candidates hidden" : "nothing eligible", costUsd: 0 };
    }
    log("eligible", { count: batch.size, dryRun: config.dryRun });

    const outcomes = newOutcomes();
    const server = createClassifierServer({ pegasus, batch, outcomes, dryRun: config.dryRun, log });
    const result = await runAgent(config, server, batch.size, log);
    let { outcome } = result;

    // A completed run that decided nothing, with eligible mail and no refused
    // or deferred write, is a failed run: the model did not read the batch (#1829).
    if (result.completed && outcomes.verdicts.size === 0 && outcomes.failed.size === 0 && outcomes.deferred.size === 0) {
      outcome = "failed: no verdicts";
      log("failed", { reason: "the run completed without a verdict on any eligible e-mail", eligible: batch.size, summary: result.summary });
    }

    await recordOutcomes(batch, outcomes, result.completed, reviewed, pegasus, log);
    return { eligible: batch.size, outcome, costUsd: result.costUsd };
  } finally {
    await pegasus.close();
  }
}
